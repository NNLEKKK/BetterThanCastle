using System;
using System.Collections;
using System.IO;
using Brix.Assets;
using Brix.Network;
using HarmonyLib;
using UnityEngine.Networking;

namespace BetterThanCastle.Patches
{
    // Host -> client map delivery (BetterConnection ON, client said BTC hello). Vanilla zips the whole map folder with lzip and the client
    // unzips it into Info/Downloads/Maps/<checksum>/. Here the payload is a .btcs (smaller, carries the btc-*.txt sidecars) and/or has no
    // thumbnail.png. Vanilla clients and BC OFF keep the untouched vanilla path.
    [HarmonyPatch(typeof(UNetManager), "SendFiles")]
    internal static class ShareSendFilesPatch
    {
        private static bool Prefix(UNetManager __instance, NetworkConnection connection)
        {
            try
            {
                BtcSaveFormat.LoadSettings();
                if (!BtcNetworkGate.Enabled || !BtcSaveFormat.ShareOn || connection == null || NetworkServer.connections.Count <= 1) return true;
                // .btcs only for a BTC client; a zip without thumbnail.png is plain vanilla format, so any client gets it
                // (the hello may also arrive after the download request)
                bool btcs = BtcSaveFormat.SendBtcs && BtcSpectate.SaidHello(connection);
                if (!btcs && BtcSaveFormat.SendThumb) return true;   // nothing to change
                string checksum = GameParam.instance.mapChecksum;
                string root = Path.GetTempPath() + "CastleStory/";
                string src = GameParam.Map.path;
                string tag = (btcs ? "b" : "z") + (BtcSaveFormat.SendThumb ? "t" : "n");
                string dir = root + "btc_" + tag + "/";
                Directory.CreateDirectory(dir);
                string file = dir + checksum;   // the file name is the folder name on the client
                if (!File.Exists(file))
                {
                    // the payload is cached by checksum and never rebuilt: build it under .part and rename only when complete,
                    // so a failed or cut-off pack never leaves a truncated file that every later join would receive
                    string part = file + ".part";
                    if (File.Exists(part)) File.Delete(part);
                    string err;
                    if (btcs)
                    {
                        if (!BtcSaveFormat.PackForSend(src, part, BtcSaveFormat.SendThumb, out err))
                        {
                            if (File.Exists(part)) File.Delete(part);
                            BtcLog.Warn("ShareSend", "pack failed, vanilla path: " + err);
                            return true;
                        }
                    }
                    else
                    {
                        string tmp = root + "btc_compressdir";
                        if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
                        DirectoryCopy.Copy(src, tmp, false);
                        string th = Path.Combine(tmp, "thumbnail.png");
                        if (File.Exists(th)) File.Delete(th);
                        lzip.compressDir(tmp, 10, part, false, null, false);
                    }
                    if (File.Exists(part)) File.Move(part, file);
                }
                if (!File.Exists(file)) return true;
                BtcLog.Info("ShareSend", (btcs ? ".btcs " : "zip ") + (SendThumbText()) + new FileInfo(file).Length + " bytes to conn " + connection.connectionId);
                __instance.FileTransfer.SendFileToClient(connection, file);
                return false;
            }
            catch (Exception e)
            {
                BtcLog.Error("Hook", "UNetManager.SendFiles: " + e);
                return true;
            }
        }

        private static string SendThumbText() { return BtcSaveFormat.SendThumb ? "with thumbnail, " : "no thumbnail, "; }
    }

    // Host upload speed (BetterConnection ON): vanilla sends ONE 1400-byte chunk per 0.04 s tick (~35 KB/s, a 3 MB map = 90 s).
    // Same msg 49 chunks, same order, so any client reads them; we just send many per tick while the reliable channel's
    // pending queue stays short (a full HLAPI queue drops the message, which would corrupt the file). Completion stays vanilla.
    [HarmonyPatch(typeof(UNetFileTransferMessageHandler), "SendFileData")]
    internal static class ShareFastUploadPatch
    {
        // vanilla's 1400-byte chunk does not fit an HLAPI packet (PacketSize 1440 - 100 reserve) and goes as 2 fragments + an end
        // message; 1300 + 6 header bytes fits one packet. The client writes whatever chunk size it gets.
        private const int Chunk = 1300;
        private const int MaxChunksPerTick = 160;   // ~5.6 MB/s cap
        private const int MaxPending = 24;
        private static System.Reflection.FieldInfo _channels, _pending;
        private static bool _init, _ok;
        private static FileStream _cur;
        private static float _t0;

        private static bool Prefix(UNetFileTransferMessageHandler.FileData file, NetworkConnection connection)
        {
            try
            {
                if (!BtcNetworkGate.Enabled || file == null || connection == null || !file.UploadStarted || !Init()) return true;
                FileStream fs = file.FileStream;
                if (!fs.CanRead) return true;
                if (!ReferenceEquals(fs, _cur)) { _cur = fs; _t0 = UnityEngine.Time.realtimeSinceStartup; }
                int budget = BtcOwnTransport.ActiveFor(connection) ? 40 : MaxChunksPerTick;   // own transport: its bulk lane paces itself
                // every chunk is ours; vanilla only runs at the end (Position == Length: no chunk, just the completion message)
                for (int i = 0; i < budget && fs.Position < fs.Length; i++)
                {
                    if (Pending(connection) > MaxPending) return false;   // link is busy: try again next tick
                    long at = fs.Position;
                    byte[] buf = new byte[(int)Math.Min(Chunk, fs.Length - at)];
                    int n = fs.Read(buf, 0, buf.Length);
                    if (n != buf.Length) { fs.Position = at; return false; }
                    if (!connection.Send(49, new UNetFileTransferMessageHandler.FileDataMessage(buf))) { fs.Position = at; return false; }
                }
                if (fs.Position < fs.Length) return false;
                BtcLog.Info("ShareSend", "upload of " + fs.Length + " bytes to conn " + connection.connectionId + " done in " + (UnityEngine.Time.realtimeSinceStartup - _t0).ToString("0.00") + " s");
                return true;
            }
            catch (Exception e)
            {
                BtcLog.Error("Hook", "FileTransfer.SendFileData: " + e);
                return true;
            }
        }

        private static bool Init()
        {
            if (_init) return _ok;
            _init = true;
            _channels = AccessTools.Field(typeof(NetworkConnection), "m_Channels");
            Type cb = AccessTools.TypeByName("UnityEngine.Networking.ChannelBuffer");
            _pending = cb != null ? AccessTools.Field(cb, "m_PendingPackets") : null;
            _ok = _channels != null && _pending != null;
            if (!_ok) BtcLog.Error("ShareSend", "fast upload off: HLAPI channel fields not found");
            return _ok;
        }

        private static int Pending(NetworkConnection c)
        {
            Array ch = _channels.GetValue(c) as Array;
            if (ch == null || ch.Length == 0) return 0;
            ICollection q = _pending.GetValue(ch.GetValue(0)) as ICollection;   // Channels.DefaultReliable
            return q != null ? q.Count : 0;
        }
    }

    // Client: a finished download that is a .btcs is unpacked by us instead of lzip. Everything else goes to vanilla.
    [HarmonyPatch(typeof(UNetFileTransferMessageHandler), "FileDataCompletedMessageReceived")]
    internal static class ShareReceiveBtcsPatch
    {
        private static readonly AccessTools.FieldRef<UNetFileTransferMessageHandler, IEnumerable> Incoming =
            AccessTools.FieldRefAccess<UNetFileTransferMessageHandler, IEnumerable>("IncomingFiles");

        private static bool Prefix(UNetFileTransferMessageHandler __instance)
        {
            try
            {
                IEnumerable q = Incoming(__instance);
                if (q == null) return true;
                object info = null;
                foreach (object o in q) { info = o; break; }   // queue head
                if (info == null) return true;
                FileStream fs = (FileStream)AccessTools.Field(info.GetType(), "fileStream").GetValue(info);
                string name = fs.Name;
                fs.Flush();
                byte[] head = new byte[4];
                int n;
                using (FileStream rd = new FileStream(name, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) n = rd.Read(head, 0, 4);
                if (n < 4 || !BtcSaveFormat.IsBtcsMagic(head)) { BtcLog.Info("ShareSend", "received zip " + name + " " + fs.Length + " bytes, vanilla unzip"); return true; }
                // our payload: finish the way vanilla does, but unpack the .btcs
                Traverse.Create(q).Method("Dequeue").GetValue();
                // the entry is ours from here on: never fall back to vanilla, it would dequeue (and unpack) the next file
                try
                {
                    fs.Close();
                    try { File.SetLastWriteTime(name, DateTime.FromBinary((long)AccessTools.Field(info.GetType(), "fileModifiedtime").GetValue(info))); } catch (Exception) { }
                    // only the file's own extension goes: a dot in a folder (~/.local/share/Steam) must not cut the path
                    string dest = Path.Combine(Path.GetDirectoryName(name), Path.GetFileNameWithoutExtension(name));
                    string err;
                    if (!BtcSaveFormat.Unpack(name, dest, null, out err)) BtcLog.Error("ShareSend", "unpack of the received .btcs failed: " + err);
                    else BtcLog.Info("ShareSend", "received .btcs unpacked to " + dest);
                    File.Delete(name);
                }
                catch (Exception e) { BtcLog.Error("ShareSend", "received .btcs: " + e); }
                return false;
            }
            catch (Exception e)
            {
                BtcLog.Error("Hook", "FileDataCompleted: " + e);
                return true;
            }
        }
    }
}
