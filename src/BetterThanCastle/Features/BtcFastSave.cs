using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using Brix.Assets;
using Brix.Engine;
using Brix.External.Signals;
using Brix.IO.Serialization;
using Brix.NewUI.CornerMenus;
using Brix.UI.Builder.Menu;
using Brix.Utils;
using HarmonyLib;
using MoonSharp.Interpreter;
using Newtonsoft.Json;
using UnityEngine;

namespace BetterThanCastle
{
    // Cfg Performance/FastSave (local, applies at once). Ported from Castle Story Plus 0.10.0 (Saving/FastSave.cs, SaveSlicer.cs;
    // GPLv3, Tricky12321). A save of a running game no longer freezes it:
    //  - the quicksave and the save menu write the game objects a slice per frame (simulation paused meanwhile, single player);
    //  - the vanilla save copied the whole old save to temp, wrote everything there, deleted the old save and copied it all back.
    //    Now the big data files (game objects, trees, plants, boulders, ores, terrain damage, placed blocks, factions) are collected
    //    on the main thread and written by a background thread into a NEW folder, the old save's other files are copied over and
    //    the folder takes the old one's place with two renames. A failed write keeps the old save. Game objects are written compact.
    // Anything that reads saves (lists, loading, quitting) first waits for a save still being written.
    // SIDECARS: every BTC sidecar (inf-heal, wards, phantoms, terraform, saved teams...) is written from an Asset_Map.Disk_Save
    // postfix into __instance.path. While Save() runs map.path IS the new staging folder (same trick as vanilla MoveToStaging) and
    // Disk_Save runs synchronously inside Save(), so the sidecars land in the folder that becomes the save. The background thread
    // then copies only the old files that are still MISSING (CopyMissing), so it never overwrites a fresh sidecar, and an old
    // sidecar of a feature that wrote nothing this time is carried over exactly as in vanilla. Reading: the wait prefix runs
    // first (Priority.First) so the Disk_ReadGameObjects sidecar loaders never see a half-swapped folder.
    internal static class BtcFastSave
    {
        private struct Job { public string Path; public object Data; public string Text; }

        private static readonly string StagingRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "BtcSaving").Replace("\\", "/") + "/";
        private static List<Job> _jobs;
        private static Thread _thread;
        private static volatile string _failure;
        private static string _failedSave;
        private static bool _disabled, _cleaned;
        private static JsonSerializer _serializer;
        private static MethodInfo _diskSave;
        private static FieldInfo _stagePath;
        private static MethodInfo _pathSetter;
        private static GameObject _host;
        private static string _prepared;
        private static int _lastLen;

        internal static bool Busy;
        internal static bool Deferring { get { return _jobs != null; } }
        internal static bool Writing { get { return _thread != null && _thread.IsAlive; } }
        internal static bool On { get { return BtcConfig.FastSave != null && BtcConfig.FastSave.Value && !_disabled; } }

        // ---- host component (dialog after a failure, wait on quit, slicer coroutines)
        private sealed class Runner : MonoBehaviour
        {
            private void Update()
            {
                string failure = _failure;
                if (failure == null || Writing) return;
                _failure = null;
                try
                {
                    GameSignals.Invoke(GameSignals.OnDisplayGenericDialog, new OkDialogContainerPopulator.GenericDialog(I2Helper.TryGet("##gamemenu_savegame_failure").ToUpper(), I2Helper.TryGet("##gamemenu_savegame_failure_message") + "\n\n\"" + _failedSave + ": " + failure + "\"", new OkDialogContainerPopulator.GenericDialog.Btn[1]
                    {
                        new OkDialogContainerPopulator.GenericDialog.Btn(I2Helper.TryGet("##gamemenu_savegame_failure_ok").ToUpper(), delegate { GameSignals.Invoke(GameSignals.OnCloseDialog); }, string.Empty)
                    }));
                }
                catch (Exception e) { BtcLog.Error("FastSave", "dialog: " + e.Message); }
            }

            private void OnApplicationQuit() { WaitForWriting(); }
        }

        private static Runner Host()
        {
            if (_host == null)
            {
                _host = new GameObject("BtcFastSave");
                UnityEngine.Object.DontDestroyOnLoad(_host);
                _host.AddComponent<Runner>();
            }
            return _host.GetComponent<Runner>();
        }

        internal static void WaitForWriting()
        {
            Thread t = _thread;
            if (t != null && t != Thread.CurrentThread && t.IsAlive)
            {
                Stopwatch w = Stopwatch.StartNew();
                t.Join();
                BtcLog.Info("FastSave", "waited " + w.ElapsedMilliseconds + " ms for the save being written");
            }
        }

        // ---- Disk_Overwrite / Disk_WriteAsNew
        internal static bool Save(Asset_Map map, bool asNew, AssetAuthorType author)
        {
            if (!On || map.mapType != Asset_Map.MapType.Save || map.IsStaging()) return false;
            if (_diskSave == null) _diskSave = AccessTools.Method(typeof(Asset_Map), "Disk_Save");
            if (_stagePath == null) _stagePath = AccessTools.Field(typeof(Asset_Map), "_stagePath");
            if (_pathSetter == null) _pathSetter = AccessTools.PropertySetter(typeof(Asset_Map), "path");
            if (_diskSave == null || _stagePath == null || _pathSetter == null) { _disabled = true; BtcLog.Error("FastSave", "Asset_Map members not found, using the vanilla save"); return false; }
            WaitForWriting();
            Host();
            if (!_cleaned)
            {
                _cleaned = true;
                try { if (Directory.Exists(StagingRoot)) Directory.Delete(StagingRoot, true); } catch (Exception e) { BtcLog.Warn("FastSave", "clear " + StagingRoot + ": " + e.Message); }
            }
            Stopwatch main = Stopwatch.StartNew();
            if (asNew) map.authorType = author;
            string oldFolder = map.IsShell() ? null : Folder(map.path);
            string stage = StagingRoot + System.IO.Path.GetRandomFileName() + "/";
            Directory.CreateDirectory(stage);
            _stagePath.SetValue(map, stage);
            List<Job> jobs = new List<Job>();
            _jobs = jobs;
            try
            {
                _diskSave.Invoke(map, null);   // the Disk_Save postfixes (sidecars) write into map.path = stage
            }
            catch (Exception e)
            {
                _stagePath.SetValue(map, string.Empty);
                TryDelete(stage);
                BtcLog.Error("FastSave", "Disk_Save failed: " + e);
                throw;
            }
            finally { _jobs = null; }
            if (asNew)
                _pathSetter.Invoke(map, new object[] { AssetDispensary.saves.GetProperPath(map) + (!string.IsNullOrEmpty(map.customName) ? map.customName : System.IO.Path.GetRandomFileName()) + "/" });
            _stagePath.SetValue(map, string.Empty);
            string finalFolder = Folder(map.path);
            string name = map.customName;
            if (_serializer == null) _serializer = JsonSerializer.Create(Io.GenerateUnitySerializerSettings("save"));   // made on the main thread
            long mainMs = main.ElapsedMilliseconds;
            _thread = new Thread(delegate () { Write(jobs, stage, oldFolder, finalFolder, name, mainMs); });
            _thread.IsBackground = false;
            _thread.Start();
            GameSignals.Invoke(GameSignals.GameSaved);
            GameSignals.Invoke<string, float>(GameSignals.OnAutoFadeDialog, I2Helper.TryGet("##saved_game"), asNew ? 2f : 1f);
            return true;
        }

        internal static bool Defer(string path, object data, string text)
        {
            if (_jobs == null) return false;
            _jobs.Add(new Job { Path = path, Data = data, Text = text });
            return true;
        }

        private static void Write(List<Job> jobs, string stage, string oldFolder, string finalFolder, string name, long mainMs)
        {
            Stopwatch watch = Stopwatch.StartNew();
            try
            {
                for (int i = 0; i < jobs.Count; i++)
                {
                    Job job = jobs[i];
                    if (job.Text != null) File.WriteAllText(job.Path, job.Text);
                    else WriteJson(job.Data, job.Path);
                }
                if (oldFolder != null && Directory.Exists(oldFolder)) CopyMissing(oldFolder, stage);
                Swap(stage, finalFolder);
                BtcLog.Info("FastSave", name + " saved, " + mainMs + " ms in the game, " + watch.ElapsedMilliseconds + " ms writing in the background");
            }
            catch (Exception e)
            {
                BtcLog.Error("FastSave", "writing " + name + " failed, the old save is kept: " + e);
                TryDelete(stage);
                _disabled = true;
                _failedSave = name;
                _failure = e.Message;
            }
            finally { jobs.Clear(); jobs.TrimExcess(); }
        }

        private static void WriteJson(object data, string destination)
        {
            using (StreamWriter text = File.CreateText(destination))
            using (JsonTextWriter json = new JsonTextWriter(text))
                _serializer.Serialize(json, data);
        }

        private static void CopyMissing(string from, string to)
        {
            foreach (string file in Directory.GetFiles(from))
            {
                string target = System.IO.Path.Combine(to, System.IO.Path.GetFileName(file));
                if (!File.Exists(target)) File.Copy(file, target);
            }
            foreach (string folder in Directory.GetDirectories(from))
            {
                string target = System.IO.Path.Combine(to, System.IO.Path.GetFileName(folder));
                Directory.CreateDirectory(target);
                CopyMissing(folder, target);
            }
        }

        private static void Swap(string stage, string finalFolder)
        {
            string from = stage.TrimEnd('/');
            string to = finalFolder.TrimEnd('/');
            string old = null;
            if (Directory.Exists(to))
            {
                old = StagingRoot + System.IO.Path.GetRandomFileName();
                if (!TryMove(to, old)) { Directory.Delete(to, true); old = null; }   // another drive: delete then copy in
            }
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(to));
                if (!TryMove(from, to))
                {
                    Directory.CreateDirectory(to);
                    CopyMissing(from, to);
                    Directory.Delete(from, true);
                }
            }
            catch
            {
                if (old != null && !Directory.Exists(to)) { TryMove(old, to); old = null; }
                throw;
            }
            if (old != null) TryDelete(old);
        }

        private static bool TryMove(string from, string to)
        {
            try { Directory.Move(from, to); return true; }
            catch (IOException) { return false; }
        }

        private static void TryDelete(string folder)
        {
            try { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
            catch (Exception e) { BtcLog.Warn("FastSave", "could not delete " + folder + ": " + e.Message); }
        }

        private static string Folder(string path) { return path.Replace("\\", "/").TrimEnd('/') + "/"; }

        internal static string FileOf(string method)
        {
            switch (method)
            {
                case "Disk_WriteTrees": return "trees.json";
                case "Disk_WritePlants": return "plants.json";
                case "Disk_WriteBoulders": return "boulders.json";
                case "Disk_WriteIrons": return "iron.json";
                case "Disk_WritePyrites": return "pyrite.json";
                case "Disk_WriteTerrainDamage": return "TerrainDamage.json";
                case "Disk_WritePlacedBlocks": return "PlacedBlocks.json";
                case "Disk_WriteFactionMeta": return "Factions.json";
                case "Disk_WriteGameObjects": return "gameobjects.json";
                default: return null;
            }
        }

        // ---- slicer: game objects a slice per frame, then the given save runs in one frame with them prepared
        internal static string Prepared { get { return _prepared; } set { _prepared = value; } }

        internal static bool StartSliced(string label, Action save)
        {
            if (Busy || !On || Neo.IsMultiplayer) return false;
            Runner r = Host();
            Busy = true;
            r.StartCoroutine(Run(label, save));
            return true;
        }

        private static IEnumerator Run(string label, Action save)
        {
            float timeScale = Time.timeScale;
            Time.timeScale = 0f;
            Stopwatch total = Stopwatch.StartNew();
            int frames = 0;
            string objects = null;
            IEnumerator writing = WriteObjects(25, delegate (string json) { objects = json; });
            while (true)
            {
                bool more;
                try { more = writing.MoveNext(); }
                catch (Exception e) { BtcLog.Error("FastSave", label + ": slicing game objects failed, the game's own save runs: " + e); objects = null; break; }
                if (!more) break;
                frames++;
                yield return null;
            }
            long sliced = total.ElapsedMilliseconds;
            Stopwatch rest = Stopwatch.StartNew();
            _prepared = objects;
            try { save(); }
            catch (Exception e) { BtcLog.Error("FastSave", label + " failed: " + e); }
            finally
            {
                _prepared = null;
                if (Time.timeScale == 0f) Time.timeScale = timeScale;
                Busy = false;
            }
            BtcLog.Info("FastSave", label + " timing: game objects " + sliced + " ms over " + (frames + 1) + " frames, rest of the save " + rest.ElapsedMilliseconds + " ms in one frame");
        }

        private static FieldInfo _queueField;

        // same output as GameObjectSerializer.GetJsonGameState("save") (compact), one serializer and writer for all objects
        private static IEnumerator WriteObjects(long sliceMs, Action<string> done)
        {
            GameObjectSerializer gos = UnityEngine.Object.FindObjectOfType<GameObjectSerializer>();
            if (gos == null) yield break;
            if (_queueField == null) _queueField = typeof(GameObjectSerializer).GetField("_saveQueue", BindingFlags.Instance | BindingFlags.NonPublic);
            LinkedList<SerializationComponent> q = _queueField == null ? null : _queueField.GetValue(gos) as LinkedList<SerializationComponent>;
            if (q == null) yield break;
            List<SerializationComponent> queue = new List<SerializationComponent>(q);
            System.Text.StringBuilder text = new System.Text.StringBuilder(Mathf.Max(1024, _lastLen + _lastLen / 8));
            text.Append("[");
            JsonSerializer serializer = JsonSerializer.Create(Io.GetSaveSerializerSettings("save"));
            serializer.Formatting = Formatting.None;
            StringWriter writer = new StringWriter(text);
            Stopwatch slice = Stopwatch.StartNew();
            for (int i = 0; i < queue.Count; i++)
            {
                SerializationComponent item = queue[i];
                if (item != null) item.Save(writer, serializer);
                if (slice.ElapsedMilliseconds >= sliceMs)
                {
                    yield return null;
                    slice.Reset(); slice.Start();
                }
            }
            if (text.Length > 1) text.Remove(text.Length - 1, 1);
            text.Append("]");
            _lastLen = text.Length;
            done(text.ToString());
        }
    }

    [HarmonyPatch(typeof(Asset_Map), "Disk_Overwrite")]
    internal static class FastSaveOverwritePatch
    {
        private static bool Prefix(Asset_Map __instance)
        {
            try { return !BtcFastSave.Save(__instance, false, AssetAuthorType.Official); }
            catch (Exception e) { BtcLog.Error("Hook", "FastSave overwrite: " + e.Message); throw; }
        }
    }

    [HarmonyPatch(typeof(Asset_Map), "Disk_WriteAsNew")]
    internal static class FastSaveWriteAsNewPatch
    {
        private static bool Prefix(Asset_Map __instance, AssetAuthorType author)
        {
            try { return !BtcFastSave.Save(__instance, true, author); }
            catch (Exception e) { BtcLog.Error("Hook", "FastSave write-as-new: " + e.Message); throw; }
        }
    }

    // The big data files go to the background thread (the data is the game's own fresh arrays, except the live terrain damage dictionary).
    [HarmonyPatch]
    internal static class FastSaveDeferPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (MethodInfo m in typeof(Asset_Map).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                if (BtcFastSave.FileOf(m.Name) != null && m.GetParameters().Length == 1) yield return m;
        }

        private static bool Prefix(Asset_Map __instance, MethodBase __originalMethod, object[] __args)
        {
            if (!BtcFastSave.Deferring) return true;
            try
            {
                object data = __args[0];
                Dictionary<XYZ, float> damage = data as Dictionary<XYZ, float>;
                if (damage != null) data = new Dictionary<XYZ, float>(damage);
                string text = data as string;
                string path = __instance.path + BtcFastSave.FileOf(__originalMethod.Name);
                return !BtcFastSave.Defer(path, text != null ? null : data, text);
            }
            catch (Exception e) { BtcLog.Error("Hook", "FastSave defer: " + e.Message); return true; }
        }
    }

    // Reading saves waits for a save still being written. First, so the sidecar loaders (Disk_ReadGameObjects prefixes) see the final folder.
    [HarmonyPatch]
    internal static class FastSaveWaitPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            MethodBase rc = AccessTools.Method(typeof(AssetHandler<Asset_Map>), "RefreshCache");
            if (rc != null) yield return rc;
            MethodBase vl = AccessTools.Method(typeof(VoxelEngine), "Load");
            if (vl != null) yield return vl;
            foreach (MethodInfo m in typeof(Asset_Map).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                if (m.Name.StartsWith("Disk_Read", StringComparison.Ordinal) || m.Name == "get_Disk_ReadPlacedBlocks" || m.Name == "Disk_EstimateTrees") yield return m;
        }

        [HarmonyPriority(Priority.First)]
        private static void Prefix() { BtcFastSave.WaitForWriting(); }
    }

    // The sliced game objects are handed to the game's own Disk_Save through GetJsonGameState.
    [HarmonyPatch(typeof(GameObjectSerializer), "GetJsonGameState")]
    internal static class FastSavePreparedPatch
    {
        private static bool Prefix(string target, ref string __result)
        {
            if (target != "save" || BtcFastSave.Prepared == null) return true;
            __result = BtcFastSave.Prepared;
            BtcFastSave.Prepared = null;
            return false;
        }
    }

    [HarmonyPatch(typeof(MenuCalls), "QuickSave")]
    internal static class FastSaveQuickSavePatch
    {
        private static bool _running;

        private static bool Prefix(ScriptExecutionContext context, CallbackArguments args, ref DynValue __result)
        {
            if (_running || BtcFastSave.Busy || !BtcFastSave.On) return true;
            bool started = BtcFastSave.StartSliced("Quicksave", delegate
            {
                _running = true;
                try { MenuCalls.QuickSave(context, args); }
                finally { _running = false; }
            });
            if (!started) return true;
            __result = DynValue.Void;
            return false;
        }
    }

    [HarmonyPatch(typeof(BaseMapSaverMenu), "OnSave")]
    internal static class FastSaveMenuPatch
    {
        private static bool _running, _pending;

        private static bool Prefix(BaseMapSaverMenu __instance)
        {
            if (_running) return true;
            if (_pending) return false;
            if (BtcFastSave.Busy || !BtcFastSave.On || BaseMapSaverMenu.SelectedAsset == null) return true;
            _pending = true;
            bool started = BtcFastSave.StartSliced("Save", delegate
            {
                _pending = false;
                if (__instance == null) return;
                _running = true;
                try { MethodInfo m = AccessTools.Method(typeof(BaseMapSaverMenu), "OnSave"); m.Invoke(__instance, null); }
                finally { _running = false; }
            });
            if (!started) { _pending = false; return true; }
            return false;
        }
    }
}
