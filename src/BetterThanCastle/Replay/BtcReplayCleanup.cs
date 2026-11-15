using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Automatic cleanup of the replay library: keyframe cache (cash/*.btcs) and previews not used for N days, replays not opened for N days,
    /// and an optional hard age limit that ignores whether a replay was opened. Replays go to Replays/.trash (restorable, purged after 14 days).
    /// Runs once a short while after the game starts (background thread) and on the "Clean now" button. Everything is off by default.
    /// </summary>
    internal static class BtcReplayCleanup
    {
        internal static readonly int[] Steps = { 1, 2, 3, 5, 7, 10, 14, 21, 30, 45, 60, 90, 120, 180, 365 };

        private static readonly Dictionary<string, long> Opened = new Dictionary<string, long>();   // replay file name -> UTC ticks of the last open
        private static bool _loaded;
        private static volatile bool _running;
        internal static volatile string LastResult;

        // ---- settings (BtcMenuState, persisted) ----
        internal static bool CacheOn { get { return BtcMenuState.Bool("replay:clean:cacheOn", false); } set { BtcMenuState.SetBool("replay:clean:cacheOn", value); } }
        internal static int CacheDays { get { return BtcMenuState.Int("replay:clean:cacheDays", 14); } set { BtcMenuState.SetInt("replay:clean:cacheDays", value); } }
        internal static bool UnusedOn { get { return BtcMenuState.Bool("replay:clean:unusedOn", false); } set { BtcMenuState.SetBool("replay:clean:unusedOn", value); } }
        internal static int UnusedDays { get { return BtcMenuState.Int("replay:clean:unusedDays", 30); } set { BtcMenuState.SetInt("replay:clean:unusedDays", value); } }
        internal static bool FullOn { get { return BtcMenuState.Bool("replay:clean:fullOn", false); } set { BtcMenuState.SetBool("replay:clean:fullOn", value); } }
        internal static int FullDays { get { return BtcMenuState.Int("replay:clean:fullDays", 90); } set { BtcMenuState.SetInt("replay:clean:fullDays", value); } }

        private static string OpenedFile { get { return Path.Combine(BtcReplay.ReplaysDir, ".opened.txt"); } }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                if (!File.Exists(OpenedFile)) return;
                foreach (string line in File.ReadAllLines(OpenedFile))
                {
                    int t = line.LastIndexOf('\t');
                    long v;
                    if (t > 0 && long.TryParse(line.Substring(t + 1), out v)) Opened[line.Substring(0, t)] = v;
                }
            }
            catch (Exception ex) { BtcLog.Warn("ReplayClean", "read opened: " + ex.Message); }
        }

        private static void Save()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                foreach (KeyValuePair<string, long> kv in Opened) sb.Append(kv.Key).Append('\t').Append(kv.Value).Append('\n');
                Directory.CreateDirectory(BtcReplay.ReplaysDir);
                File.WriteAllText(OpenedFile, sb.ToString());
            }
            catch (Exception ex) { BtcLog.Warn("ReplayClean", "write opened: " + ex.Message); }
        }

        /// <summary>Called when a replay is opened: remembers the time and refreshes the age of its cache and preview files.</summary>
        internal static void Opened_(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
                lock (Opened) { Load(); Opened[Path.GetFileName(path)] = DateTime.UtcNow.Ticks; Save(); }
                string a, b;
                CacheFiles(path, out a, out b);
                Touch(a); Touch(b);
            }
            catch (Exception ex) { BtcLog.Warn("ReplayClean", "opened: " + ex.Message); }
        }

        /// <summary>Keyframe cache and preview file of a replay (same names as BtcReplaySeek / BtcReplayPreview use).</summary>
        private static void CacheFiles(string path, out string cash, out string preview)
        {
            FileInfo fi = new FileInfo(path);
            string name = SafeName(path);
            cash = Path.Combine(Path.Combine(BtcPaths.Root, "cash"), name + "_" + fi.Length.ToString("x") + "_" + fi.LastWriteTimeUtc.Ticks.ToString("x") + ".btcs");
            preview = Path.Combine(Path.Combine(BtcPaths.Root, "previews"), name + "_" + fi.Length.ToString("x") + "_p256.png");
        }

        private static void Touch(string f) { if (File.Exists(f)) File.SetLastWriteTimeUtc(f, DateTime.UtcNow); }

        private static string SafeName(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            char[] bad = Path.GetInvalidFileNameChars();
            for (int i = 0; i < bad.Length; i++) name = name.Replace(bad[i], '_');
            return name.Length > 60 ? name.Substring(0, 60) : name;
        }

        /// <summary>Starts the cleanup in the background (once per call; ignored while one is running).</summary>
        internal static void RunAsync(bool manual)
        {
            if (_running) return;
            if (!manual && !CacheOn && !UnusedOn && !FullOn) return;
            _running = true;
            int cd = CacheDays, ud = UnusedDays, fd = FullDays;
            bool co = CacheOn, uo = UnusedOn, fo = FullOn;
            HashSet<string> favs = BtcReplayLibrary.FavoriteNames();
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    if (!manual) System.Threading.Thread.Sleep(20000);
                    LastResult = Run(co, cd, uo, ud, fo, fd, favs);
                }
                catch (Exception ex) { LastResult = "error: " + ex.Message; BtcLog.Error("ReplayClean", ex.ToString()); }
                finally { _running = false; }
            });
        }

        internal static bool Running { get { return _running; } }

        private static string Run(bool cacheOn, int cacheDays, bool unusedOn, int unusedDays, bool fullOn, int fullDays, HashSet<string> favs)
        {
            DateTime now = DateTime.UtcNow;
            int cacheDel = 0, repDel = 0;
            long freed = 0;
            string rdir = BtcReplay.ReplaysDir;
            HashSet<string> keep = new HashSet<string>();   // cache + previews of favourites are never removed
            foreach (string fn in favs)
            {
                string fp = Path.Combine(rdir, fn);
                if (!File.Exists(fp)) continue;
                string a, b;
                CacheFiles(fp, out a, out b);
                keep.Add(a); keep.Add(b);
            }
            if (cacheOn)
            {
                string[] dirs = { Path.Combine(BtcPaths.Root, "cash"), Path.Combine(BtcPaths.Root, "previews") };
                for (int i = 0; i < dirs.Length; i++)
                {
                    if (!Directory.Exists(dirs[i])) continue;
                    foreach (string f in Directory.GetFiles(dirs[i]))
                    {
                        try
                        {
                            if (keep.Contains(f)) continue;
                            FileInfo fi = new FileInfo(f);
                            if ((now - fi.LastWriteTimeUtc).TotalDays <= cacheDays) continue;
                            freed += fi.Length; File.Delete(f); cacheDel++;
                        }
                        catch (Exception ex) { BtcLog.Warn("ReplayClean", "cache " + f + ": " + ex.Message); }
                    }
                }
            }
            if (unusedOn || fullOn)
            {
                string dir = BtcReplay.ReplaysDir;
                string trash = Path.Combine(dir, ".trash");
                string rec = BtcReplay.RecFile, feed = BtcReplay.FeedFile;
                if (Directory.Exists(dir))
                {
                    foreach (string f in Directory.GetFiles(dir, "*.btcr"))
                    {
                        try
                        {
                            if (f == rec || f == feed) continue;
                            string n = Path.GetFileName(f);
                            if (favs.Contains(n)) continue;   // favourites are never deleted
                            FileInfo fi = new FileInfo(f);
                            DateTime created = fi.LastWriteTimeUtc;
                            DateTime last = created;
                            lock (Opened) { Load(); long t; if (Opened.TryGetValue(n, out t)) last = new DateTime(t, DateTimeKind.Utc); }
                            bool kill = (fullOn && (now - created).TotalDays > fullDays) || (unusedOn && (now - last).TotalDays > unusedDays);
                            if (!kill) continue;
                            Directory.CreateDirectory(trash);
                            string dst = Path.Combine(trash, n);
                            if (File.Exists(dst)) dst = Path.Combine(trash, DateTime.Now.ToString("yyyyMMdd-HHmmss-") + n);
                            freed += fi.Length;
                            File.Move(f, dst);
                            File.SetLastWriteTimeUtc(dst, now);   // the trash purge counts 14 days from now, not from the recording
                            repDel++;
                        }
                        catch (Exception ex) { BtcLog.Warn("ReplayClean", "replay " + f + ": " + ex.Message); }
                    }
                }
            }
            string res = repDel + " replays, " + cacheDel + " cache files, " + (freed / 1048576L) + " MB";
            BtcLog.Info("ReplayClean", res);
            return res;
        }

        // ---- UI (the gear panel of the Replay tab) ----
        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }

        private static int Step(int cur, int dir)
        {
            int i = 0;
            for (int k = 0; k < Steps.Length; k++) if (Steps[k] <= cur) i = k;
            i = Mathf.Clamp(i + dir, 0, Steps.Length - 1);
            return Steps[i];
        }

        private static string DaysText(int d) { return d + Tt(" d", " дн"); }

        private static void Row(ref float y, float w, int fs, string label, bool on, Action<bool> setOn, int days, Action<int> setDays)
        {
            BtcUi.ToggleUndo(ref y, w, fs, "replay", label, on, null, setOn);
            if (!on) return;
            int dir = BtcUi.Stepper(ref y, w, fs, Tt("   after how many days", "   через сколько дней"), DaysText(days));
            if (dir != 0)
            {
                int old = days, nv = Step(days, dir);
                if (nv != old) { setDays(nv); BtcUndo.Push("replay", label, delegate { setDays(old); }); }
            }
        }

        internal static void Draw(ref float y, float w, int fs)
        {
            BtcUi.Note(ref y, w, fs, Tt("Old replays go to the trash folder (Replays/.trash) and are removed for good after 14 days, so a mistake can be undone. Cache = keyframes (instant rewind, rebuilt on the next open) and map previews. The check runs a few seconds after the game starts and on the button below.", "Старые реплеи уходят в корзину (Replays/.trash) и удаляются навсегда через 14 дней, так что ошибку можно исправить. Кэш = ключевые кадры (мгновенная перемотка, пересоздаются при следующем открытии) и превью карт. Проверка идёт через несколько секунд после запуска игры и по кнопке ниже."));
            Row(ref y, w, fs, Tt("Delete the cache of replays not opened for a while", "Удалять кэш реплеев, в которые давно не заходили"), CacheOn, v => CacheOn = v, CacheDays, v => CacheDays = v);
            Row(ref y, w, fs, Tt("Delete replays not opened for a while", "Удалять реплеи, в которые давно не заходили"), UnusedOn, v => UnusedOn = v, UnusedDays, v => UnusedDays = v);
            Row(ref y, w, fs, Tt("Delete all replays older than this, even opened ones", "Удалять все реплеи старше срока, даже просмотренные"), FullOn, v => FullOn = v, FullDays, v => FullDays = v);
            if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Trash, Running ? Tt("Cleaning...", "Очистка...") : Tt("Clean now", "Очистить сейчас"), Tt("Applies the rules above right now (nothing is deleted if all switches are off)", "Применяет правила выше прямо сейчас (если все переключатели выключены, ничего не удаляется)"))) { if (!CacheOn && !UnusedOn && !FullOn) LastResult = Tt("Nothing to do: all switches are off.", "Нечего делать: все переключатели выключены."); else RunAsync(true); }
            string r = LastResult;
            if (!string.IsNullOrEmpty(r)) BtcUi.Status(ref y, w, fs, Tt("Last cleanup: ", "Последняя очистка: ") + r, new Color(1f, 1f, 1f, 0.6f));
        }
    }
}
