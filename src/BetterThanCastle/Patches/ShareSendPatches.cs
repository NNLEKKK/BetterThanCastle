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
                if (!BtcNetworkGate.Enabled || !BtcSaveFormat.ShareOn || connection == null || NetworkServer.connections.Count <= 1 || !BtcSpectate.SaidHello(connection)) return true;
                BtcSaveFormat.LoadSettings();
                if (!BtcSaveFormat.SendBtcs && BtcSaveFormat.SendThumb) return true;   // nothing to change
                string checksum = GameParam.instance.mapChecksum;
                string root = Path.GetTempPath() + "CastleStory/";
                string src = GameParam.Map.path;
                string tag = (BtcSaveFormat.SendBtcs ? "b" : "z") + (BtcSaveFormat.SendThumb ? "t" : "n");
                string dir = root + "btc_" + tag + "/";
                Directory.CreateDirectory(dir);
                string file = dir + checksum;   // the file name is the folder name on the client
                if (!File.Exists(file))
                {
                    string err;
                    if (BtcSaveFormat.SendBtcs)
                    {
                        if (!BtcSaveFormat.PackForSend(src, file, BtcSaveFormat.SendThumb, out err))
                        {
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
                        lzip.compressDir(tmp, 10, file, false, null, false);
                    }
                }
                if (!File.Exists(file)) return true;
                BtcLog.Info("ShareSend", (BtcSaveFormat.SendBtcs ? ".btcs " : "zip ") + (SendThumbText()) + new FileInfo(file).Length + " bytes to conn " + connection.connectionId);
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
                if (n < 4 || !BtcSaveFormat.IsBtcsMagic(head)) return true;
                // our payload: finish the way vanilla does, but unpack the .btcs
                Traverse.Create(q).Method("Dequeue").GetValue();
                fs.Close();
                try { File.SetLastWriteTime(name, DateTime.FromBinary((long)AccessTools.Field(info.GetType(), "fileModifiedtime").GetValue(info))); } catch (Exception) { }
                string dest = name.Split('.')[0];
                string err;
                if (!BtcSaveFormat.Unpack(name, dest, null, out err)) BtcLog.Error("ShareSend", "unpack of the received .btcs failed: " + err);
                else BtcLog.Info("ShareSend", "received .btcs unpacked to " + dest);
                File.Delete(name);
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
