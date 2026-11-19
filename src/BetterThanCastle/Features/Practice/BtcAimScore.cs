using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Aim trainer scoring. Every hit is a Fitts pointing task: ID = log2(D/W + 1) (D = screen distance from the cursor to the target when it
    /// dropped, W = target width on screen) + a search term 0.6*log2(N) (Hick: more targets = longer to find the wounded one) + a precision
    /// term log2(1 + 24px/W) (tiny targets are harder than Fitts alone says), times a motion factor for moving targets.
    /// Throughput TP = sum(ID) / sum(reaction time) in bits per second; it barely depends on the settings, so it measures the hand, not the setup.
    /// Rating = 150 * TP * accuracy^2 (misclicks cost a lot, as in a real fight).
    /// </summary>
    internal static class BtcAimScore
    {
        internal struct Hit { internal float Mt, D, W, Off, Id; }

        internal sealed class Result
        {
            internal readonly List<Hit> Hits = new List<Hit>();
            internal int MissWrong, MissEmpty;
            internal float Duration;
            internal BtcAimConfig.Snap Cfg;
            internal float Tp, Acc, AvgMt, MedMt, BestMt, SdMt, AvgId, AvgOff, PerMin;
            internal int Rating;
            internal int PrevBest = -1, PrevBestAll = -1;
            internal bool NewBest, NewCatBest;
            internal int MissPick;
            internal int Clicks { get { return Hits.Count + MissWrong + MissEmpty + MissPick; } }
        }

        /// <summary>Moving targets are harder: a base per movement kind (pack = everyone shifts at once, wander = each on its own) times the
        /// class speed (shield-up knight walks, halberdier runs fastest). First runs showed the old wander factor too generous.</summary>
        internal static float MotionFactor(int move, int unit)
        {
            float b = move == (int)BtcAimMove.Strafe ? 0.35f : move == (int)BtcAimMove.Wander ? 0.45f : move == (int)BtcAimMove.Pack ? 0.4f : 0f;
            float k = unit == (int)BtcAimUnit.KnightShield ? 0.45f : unit == (int)BtcAimUnit.Knight ? 0.8f : unit == (int)BtcAimUnit.Worker ? 0.9f : 1f;
            return 1f + b * k;
        }

        /// <summary>Bits of one hit. k = wounded targets to compare (1 in the Focus game), step = HP step between them in percent.</summary>
        internal static float HitId(float d, float w, int n, float motion, int k, int step)
        {
            w = Mathf.Max(2f, w);
            float fitts = Mathf.Log(d / w + 1f, 2f);
            float search = 0.6f * Mathf.Log(Mathf.Max(1, n), 2f);
            float prec = Mathf.Log(1f + 24f / w, 2f);
            // Most wounded: choose among k bars (Hick) and tell apart bars that differ by step % (finer step = harder)
            float choice = k > 1 ? Mathf.Log(k, 2f) + 0.5f * Mathf.Log(1f + 25f / Mathf.Max(1, step), 2f) : 0f;
            return (fitts + search + prec + choice) * motion;
        }

        /// <summary>Expected bits per target for the current settings (shown before the run). Mean distance between two random points of a square = 0.52 side.</summary>
        internal static float Predict(float unitWidth, float fovDeg)
        {
            int n = BtcAimConfig.Targets;
            int cols = Mathf.CeilToInt(Mathf.Sqrt(n));
            float side = Mathf.Max(1f, (cols - 1) * BtcAimConfig.Spacing);
            float px = Screen.height / (2f * Mathf.Tan(fovDeg * 0.5f * Mathf.Deg2Rad) * BtcAimConfig.Height);
            float w = unitWidth * px, d = 0.52f * side * px;
            int k = BtcAimConfig.Game == BtcAimGame.Triage ? BtcAimConfig.WoundedCount(n, BtcAimConfig.WoundPct) : 1;
            return HitId(d, w, n, MotionFactor((int)BtcAimConfig.Move, (int)BtcAimConfig.Unit), k, BtcAimConfig.StepPct);
        }

        internal static string DifficultyName(float bits)
        {
            if (bits < 4f) return BtcGameplay.T("Easy", "Легко");
            if (bits < 5.5f) return BtcGameplay.T("Medium", "Средне");
            if (bits < 7f) return BtcGameplay.T("Hard", "Сложно");
            if (bits < 8.5f) return BtcGameplay.T("Very hard", "Очень сложно");
            return BtcGameplay.T("Insane", "Безумно");
        }

        private static readonly int[] RankAt = { 0, 250, 400, 550, 700, 850, 1000, 1200, 1450, 1700, 1950 };
        /// <summary>XX and XXX are hidden: X looks like the top rank (no "next rank") until XX is reached.</summary>
        private const int TopVisible = 8;
        private static readonly string[] RankNames = { "F", "D", "C", "B", "A", "S", "SS", "SSS", "X", "XX", "XXX" };
        private static readonly Color[] RankCol =
        {
            new Color(0.62f, 0.62f, 0.62f), new Color(0.80f, 0.52f, 0.30f), new Color(0.55f, 0.82f, 0.45f), new Color(0.45f, 0.70f, 1f),
            new Color(0.35f, 0.85f, 0.80f), new Color(0.98f, 0.80f, 0.25f), new Color(1f, 0.55f, 0.25f), new Color(1f, 0.35f, 0.40f),
            new Color(0.72f, 0.38f, 1f), new Color(1f, 0.32f, 0.85f), new Color(1f, 0.92f, 0.55f)
        };

        internal static int RankOf(int rating) { int r = 0; for (int i = 0; i < RankAt.Length; i++) if (rating >= RankAt[i]) r = i; return r; }
        internal static string RankName(int r) { return RankNames[r]; }

        // ---------------- categories: separate bests and ranks for every game x standing/moving ----------------
        internal const int Cats = 4;
        internal static int CatOf(BtcAimConfig.Snap s) { return s.Game * 2 + (s.Move != (int)BtcAimMove.Stand ? 1 : 0); }
        private static int CatOfKey(string key) { return (key.Contains("_g1") ? 2 : 0) + (key.Contains("_m0") ? 0 : 1); }
        internal static string CatName(int c)
        {
            string g = c >= 2 ? BtcGameplay.T("Most wounded", "Самый раненый") : BtcGameplay.T("Focus", "Фокус");
            return g + " · " + ((c & 1) != 0 ? BtcGameplay.T("moving", "движутся") : BtcGameplay.T("standing", "стоят"));
        }

        /// <summary>Best rating in a category (-1 = no runs) and how many runs it has.</summary>
        internal static int BestCat(int cat, out int runs)
        {
            Load();
            int b = -1; runs = 0;
            for (int i = 0; i < _hist.Count; i++) if (_hist[i].Cat == cat) { runs++; if (_hist[i].Rating > b) b = _hist[i].Rating; }
            return b;
        }
        internal static Color RankColor(int r) { return RankCol[r]; }
        /// <summary>0..1 progress from this rank to the next one.</summary>
        internal static float RankProgress(int rating)
        {
            int r = RankOf(rating);
            if (r >= TopVisible) return 1f;
            return Mathf.Clamp01((rating - RankAt[r]) / (float)(RankAt[r + 1] - RankAt[r]));
        }
        internal static int NextRankAt(int rating) { int r = RankOf(rating); return r >= TopVisible ? -1 : RankAt[r + 1]; }

        internal static void Compute(Result r)
        {
            int n = r.Hits.Count;
            float sumId = 0f, sumMt = 0f, sumOff = 0f;
            List<float> mts = new List<float>(n);
            for (int i = 0; i < n; i++) { Hit h = r.Hits[i]; sumId += h.Id; sumMt += h.Mt; sumOff += h.Off; mts.Add(h.Mt); }
            r.Acc = r.Clicks > 0 ? n / (float)r.Clicks : 0f;
            r.Tp = sumMt > 0.01f ? sumId / sumMt : 0f;
            r.AvgMt = n > 0 ? sumMt / n : 0f;
            r.AvgId = n > 0 ? sumId / n : 0f;
            r.AvgOff = n > 0 ? sumOff / n : 0f;
            r.PerMin = r.Duration > 0.1f ? n * 60f / r.Duration : 0f;
            mts.Sort();
            r.MedMt = n > 0 ? (n % 2 == 1 ? mts[n / 2] : (mts[n / 2 - 1] + mts[n / 2]) * 0.5f) : 0f;
            r.BestMt = n > 0 ? mts[0] : 0f;
            float v = 0f;
            for (int i = 0; i < n; i++) { float dd = r.Hits[i].Mt - r.AvgMt; v += dd * dd; }
            r.SdMt = n > 1 ? Mathf.Sqrt(v / (n - 1)) : 0f;
            r.Rating = Mathf.RoundToInt(150f * r.Tp * r.Acc * r.Acc);
        }

        // ---------------- history (<game>/BetterThanCastle/practice-logs/aim-history.txt) ----------------
        private static string HistPath { get { return Path.Combine(Path.Combine(Path.Combine(Paths.GameRootPath, "BetterThanCastle"), "practice-logs"), "aim-history.txt"); } }

        internal static string LogDir { get { return Path.GetDirectoryName(HistPath); } }

        internal struct Entry { internal string Key; internal int Rating, Cat; }

        private static List<Entry> _hist;

        private static void Load()
        {
            if (_hist != null) return;
            _hist = new List<Entry>();
            try
            {
                if (!File.Exists(HistPath)) return;
                foreach (string line in File.ReadAllLines(HistPath))
                {
                    string[] p = line.Split('|');
                    int rt;
                    if (p.Length >= 3 && int.TryParse(p[2], out rt)) _hist.Add(new Entry { Key = p[1], Rating = rt, Cat = CatOfKey(p[1]) });
                }
            }
            catch (Exception e) { BtcLog.Warn("Aim", "history read: " + e.Message); }
        }

        internal static int Best(string key)
        {
            Load();
            int b = -1;
            for (int i = 0; i < _hist.Count; i++) if ((key == null || _hist[i].Key == key) && _hist[i].Rating > b) b = _hist[i].Rating;
            return b;
        }

        internal static int Runs() { Load(); return _hist.Count; }

        /// <summary>Last ratings (oldest first), at most max, for the history sparkline.</summary>
        internal static List<int> Recent(int max)
        {
            Load();
            List<int> l = new List<int>();
            for (int i = Mathf.Max(0, _hist.Count - max); i < _hist.Count; i++) l.Add(_hist[i].Rating);
            return l;
        }

        internal static void Record(Result r)
        {
            Load();
            string key = KeyOf(r.Cfg);
            int runs, cat = CatOf(r.Cfg);
            r.PrevBest = Best(key); r.PrevBestAll = BestCat(cat, out runs);
            r.NewBest = r.Rating > r.PrevBest && r.Hits.Count > 0;
            r.NewCatBest = r.Rating > r.PrevBestAll && r.Hits.Count > 0;
            _hist.Add(new Entry { Key = key, Rating = r.Rating, Cat = cat });
            try
            {
                Directory.CreateDirectory(LogDir);
                CultureInfo ci = CultureInfo.InvariantCulture;
                StringBuilder sb = new StringBuilder();
                sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", ci)).Append('|').Append(key).Append('|').Append(r.Rating)
                  .Append("|hits=").Append(r.Hits.Count).Append(" pick=").Append(r.MissPick).Append(" wrong=").Append(r.MissWrong).Append(" empty=").Append(r.MissEmpty)
                  .Append(" acc=").Append(r.Acc.ToString("0.000", ci)).Append(" tp=").Append(r.Tp.ToString("0.00", ci))
                  .Append(" mt=").Append(r.AvgMt.ToString("0.000", ci)).Append(" med=").Append(r.MedMt.ToString("0.000", ci))
                  .Append(" id=").Append(r.AvgId.ToString("0.00", ci)).Append(" off=").Append(r.AvgOff.ToString("0.00", ci));
                File.AppendAllText(HistPath, sb.Append("\r\n").ToString());
            }
            catch (Exception e) { BtcLog.Warn("Aim", "history write: " + e.Message); }
        }

        internal static string KeyOf(BtcAimConfig.Snap s) { return BtcAimConfig.Key(s); }
    }
}
