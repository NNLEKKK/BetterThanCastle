using System;
using System.Collections.Generic;
using Brix.Game;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Match statistics UI (IMGUI): thin bar (top or bottom) + hotkey panel with a table and a time-series graph.
    /// Who sees what: a player only the own faction; spectators and replay playback every faction.
    /// With 2+ shown factions every table cell shows the value and, in brackets, the difference vs the mean of the other factions (green better, red worse).
    /// All strings/graph points are rebuilt at 4 Hz in Tick(); OnGUI only draws cached data.
    /// Settings: Overlays tab, section "Match statistics" (cfg [Stats]); hotkey "match_stats_panel" (Advanced Hotkeys).
    /// </summary>
    internal static class BtcMatchStatsUI
    {
        internal const string HotkeyId = "match_stats_panel";
        private static readonly KeyCode[] DefKeys = { KeyCode.F7 };

        internal static bool PanelOpen;

        private sealed class DStat
        {
            internal string En, Ru, Tag;
            internal int Kind; // 0 raw, 1 survival %, 2 average (A / B)
            internal int A, B;
            internal int Fmt;  // 0 int, 1 percent, 2 seconds, 3 milliseconds
            internal bool Auth, LowerBetter;
            internal int Det = -1; // BtcMatchStats.D_* when the row expands into a per-item breakdown
            internal string Name { get { return BtcLang.Russian ? Ru : En; } }
        }

        private static readonly List<DStat> Stats = new List<DStat>();
        private static int NS { get { return Stats.Count; } }
        private const int FirstProf = 15;
        private static int FirstRes;
        private const int StatCrystals = 4;

        private static DStat S(string en, string ru, string tag, int kind, int a, int b, int fmt, bool auth, bool lower)
        {
            DStat s = new DStat { En = en, Ru = ru, Tag = tag, Kind = kind, A = a, B = b, Fmt = fmt, Auth = auth, LowerBetter = lower };
            Stats.Add(s);
            return s;
        }

        static BtcMatchStatsUI()
        {
            S("Bricktrons", "Бриктроны", "U", 0, BtcMatchStats.V_ALIVE, 0, 0, false, false);
            S("Losses", "Потери", "L", 0, BtcMatchStats.V_LOSS, 0, 0, false, true);
            S("Survival % (accuracy)", "Сохранность % (точность)", "S", 1, BtcMatchStats.V_LOSS, BtcMatchStats.V_OWNED, 1, false, false);
            S("Energy", "Энергия", "E", 0, BtcMatchStats.V_ENERGY, 0, 0, false, false);
            S("Crystals", "Кристаллы", "C", 0, BtcMatchStats.V_CRYS, 0, 0, false, false);
            S("Crystal-time", "Кристалло-время", "Ct", 0, BtcMatchStats.V_CRYSEC, 0, 2, false, false);
            S("Avg capture hold", "Ср. удержание захвата", "Hold", 2, BtcMatchStats.V_HOLDSUM, BtcMatchStats.V_HOLDN, 2, false, false);
            S("Work score (bricktron-time)", "Очки работы (бриктрон-время)", "W", 0, BtcMatchStats.V_SCORE, 0, 3, false, false);
            S("Idle workers (bricktron-s)", "Простой рабочих (бриктрон-с)", "Idle", 0, BtcMatchStats.V_IDLE, 0, 2, true, true);
            S("Trades", "Размены", "Tr", 0, BtcMatchStats.V_TRADE, 0, 0, false, false);
            S("Free deaths", "Бесплатные смерти", "Fr", 0, BtcMatchStats.V_FREE, 0, 0, false, true);
            S("Crafted items", "Скрафчено", "Cr", 0, BtcMatchStats.V_CRAFT, 0, 0, true, false).Det = BtcMatchStats.D_CRAFT;
            S("Built structures", "Построено", "Bu", 0, BtcMatchStats.V_BUILT, 0, 0, true, false).Det = BtcMatchStats.D_BUILT;
            S("Buildings lost", "Потеряно построек", "BL", 0, BtcMatchStats.V_BLOST, 0, 0, true, true).Det = BtcMatchStats.D_BLOST;
            string[] pe = { "worker", "halberdier", "archer", "arbalist", "knight", "alchemist", "artificer" };
            string[] pr = { "рабочие", "алебардщики", "лучники", "арбалетчики", "рыцари", "алхимики", "артефакторы" };
            string[] pt = { "dWk", "dHb", "dAr", "dAb", "dKn", "dAl", "dAf" };
            for (int i = 0; i < pe.Length; i++) S("Deaths: " + pe[i], "Смерти: " + pr[i], pt[i], 0, BtcMatchStats.V_PROF0 + i, 0, 0, false, true);
            FirstRes = Stats.Count;
            string[] re = { "Stone", "Log", "Iron ore", "Brimstone", "Plant", "Plank", "Brick", "Iron ingot", "Glass", "Cog", "Rope", "Fabric" };
            string[] rr = { "Камень", "Бревно", "Железная руда", "Сера", "Растение", "Доска", "Кирпич", "Слиток", "Стекло", "Шестерня", "Верёвка", "Ткань" };
            string[] rt = { "St", "Lg", "Fe", "Br", "Pl", "Pk", "Bk", "In", "Gl", "Cg", "Rp", "Fb" };
            for (int i = 0; i < re.Length; i++) S("Gathered: " + re[i], "Добыто: " + rr[i], rt[i], 0, BtcMatchStats.V_RES0 + i, 0, 0, false, false);
            S("Barrels exploded", "Взорвано бочек", "Bx", 0, BtcMatchStats.V_BARREL, 0, 0, true, false);
            S("Actions per minute (avg)", "Действий в минуту (ср.)", "Apm", 2, BtcMatchStats.V_ACT60, BtcMatchStats.V_ACTT, 0, true, false);
            S("Effective actions per minute (EPM, no spam)", "Эффективных действий в минуту (EPM, без спама)", "Epm", 2, BtcMatchStats.V_EFF60, BtcMatchStats.V_ACTT, 0, true, false);
            S("Peak APM (10 s window)", "Пик APM (окно 10 с)", "ApmPk", 0, BtcMatchStats.V_APMPEAK, 0, 0, true, false);
            Expand = new bool[Stats.Count];
            InBar = new bool[Stats.Count];
            InPanel = new bool[Stats.Count];
        }

        private static bool[] InBar, InPanel, Expand;
        private static string _barCfg, _panelCfg;

        // ------------------------------------------------------------------ config
        private static bool Enabled { get { return BtcConfig.StatsEnabled == null || BtcConfig.StatsEnabled.Value; } }

        private static void SyncMasks()
        {
            string b = BtcConfig.StatsBarList != null ? BtcConfig.StatsBarList.Value : "0,1,2,3,4,7,9";
            if (!ReferenceEquals(b, _barCfg)) { _barCfg = b; Parse(b, InBar, false); }
            string p = BtcConfig.StatsPanelHidden != null ? BtcConfig.StatsPanelHidden.Value : string.Empty;
            if (!ReferenceEquals(p, _panelCfg)) { _panelCfg = p; Parse(p, InPanel, true); }
        }

        private static void Parse(string s, bool[] dst, bool inverted)
        {
            for (int i = 0; i < dst.Length; i++) dst[i] = inverted;
            if (string.IsNullOrEmpty(s)) return;
            string[] a = s.Split(',');
            for (int i = 0; i < a.Length; i++)
            {
                int k;
                if (int.TryParse(a[i].Trim(), out k) && k >= 0 && k < dst.Length) dst[k] = !inverted;
            }
        }

        private static string Join(bool[] src, bool want)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < src.Length; i++) if (src[i] == want) { if (sb.Length > 0) sb.Append(','); sb.Append(i); }
            return sb.ToString();
        }

        private static float F(BepInEx.Configuration.ConfigEntry<float> e, float def) { return e != null ? e.Value : def; }
        private static bool B(BepInEx.Configuration.ConfigEntry<bool> e, bool def) { return e != null ? e.Value : def; }

        // ------------------------------------------------------------------ values
        private static double Value(DStat s, int[] v)
        {
            if (v == null) return 0;
            switch (s.Kind)
            {
                case 1:
                    {
                        int owned = v[s.B];
                        return owned <= 0 ? 100.0 : 100.0 * (1.0 - (double)v[s.A] / owned);
                    }
                case 2:
                    return v[s.B] <= 0 ? 0.0 : (double)v[s.A] / v[s.B];
            }
            return v[s.A];
        }

        private static bool Avail(DStat s, int[] v)
        {
            return !s.Auth || (v != null && v[BtcMatchStats.V_AUTH] != 0);
        }

        private static string Fmt(DStat s, double x, bool signed)
        {
            string sign = signed && x > 0.0005 ? "+" : string.Empty;
            switch (s.Fmt)
            {
                case 1: return sign + x.ToString("0.0") + "%";
                case 2: return sign + FmtTime(x);
                case 3: return sign + FmtTime(x / 1000.0);
            }
            return sign + Math.Round(x).ToString("0");
        }

        private static string FmtTime(double sec)
        {
            bool neg = sec < 0;
            double a = Math.Abs(sec);
            string r;
            if (a >= 3600) r = ((int)(a / 3600)) + "h" + ((int)(a % 3600 / 60)).ToString("00") + "m";
            else if (a >= 60) r = ((int)(a / 60)) + "m" + ((int)(a % 60)).ToString("00") + "s";
            else r = ((int)a) + "s";
            return neg ? "-" + r : r;
        }

        // ------------------------------------------------------------------ cache (4 Hz)
        private const int MaxFac = 8;
        private static readonly List<BtcMsFaction> Vis = new List<BtcMsFaction>();
        private static readonly string[] FName = new string[MaxFac];
        private static readonly Color[] FCol = new Color[MaxFac];
        private static readonly Sprite[] FFlag = new Sprite[MaxFac];
        private static readonly Sprite[] FCrys = new Sprite[MaxFac];
        private static readonly int[] BarIdx = new int[64];
        private static int _barN;
        private static readonly string[,] BarTxt = new string[MaxFac, 64];
        private static readonly Color[,] BarCol = new Color[MaxFac, 64];
        private static readonly float[,] BarW = new float[MaxFac, 64];
        private static readonly float[] BlockW = new float[MaxFac];
        private static readonly float[] NameW = new float[MaxFac];
        private const int MaxRows = 256;
        private static readonly int[] RowIdx = new int[MaxRows];
        private static readonly bool[] RowSub = new bool[MaxRows];
        private static readonly List<string> DetKeys = new List<string>();
        private static int _rowN;
        private static readonly string[,] CellTxt = new string[MaxFac, MaxRows];
        private static readonly Color[,] CellCol = new Color[MaxFac, MaxRows];
        private static readonly string[] RowName = new string[MaxRows];
        private static readonly float[][] GraphY = new float[MaxFac][];
        private static readonly int[] GraphN = new int[MaxFac];
        private static float _gMin, _gMax, _gT;
        private static string _gMinTxt = "", _gMaxTxt = "", _gTTxt = "", _header = "", _hint = "";
        private static int _graphStat;
        private static float _nextCache;
        private static int _cacheFs = -1;
        private static readonly GUIContent Gc = new GUIContent();
        private static GUIStyle _lbl, _lblBold, _lblR;
        private static int _styleFs = -1;
        private static Vector2 _scroll;

        private static bool SpectatorView()
        {
            if (BtcReplay.Mode == BtcReplay.State.Playing) return true;
            User u = User.LocalUser;
            return u == null || u.faction == null || u.faction.IsSpectator;
        }

        private static void Collect()
        {
            Vis.Clear();
            BtcMsStore st = BtcMatchStats.Current;
            bool all = SpectatorView();
            User u = User.LocalUser;
            int own = u != null && u.faction != null ? u.faction.ID : int.MinValue;
            for (int i = 0; i < st.Factions.Count && Vis.Count < MaxFac; i++)
            {
                BtcMsFaction f = st.Factions[i];
                if (all)
                {
                    if (f.V[BtcMatchStats.V_OWNED] > 0 || f.V[BtcMatchStats.V_ALIVE] > 0 || f.V[BtcMatchStats.V_CRYSEC] > 0) Vis.Add(f);
                }
                else if (f.Id == own) Vis.Add(f);
            }
        }

        private static double DiffOf(DStat s, int fi)
        {
            BtcMsFaction f = Vis[fi];
            double v = Value(s, f.V);
            if (Vis.Count > 1)
            {
                double sum = 0; int n = 0;
                for (int j = 0; j < Vis.Count; j++) { if (j == fi) continue; sum += Value(s, Vis[j].V); n++; }
                return v - sum / n;
            }
            BtcMsSample old = f.At(BtcMatchStats.Current.T - 60f);
            return old != null ? v - Value(s, old.V) : 0.0;
        }

        private static Color Good(DStat s, double d)
        {
            if (Math.Abs(d) < 0.0005) return BtcUiStyle.Text;
            bool good = s.LowerBetter ? d < 0 : d > 0;
            return good ? new Color(0.45f, 1f, 0.45f, 1f) : new Color(1f, 0.45f, 0.4f, 1f);
        }

        private static void Rebuild(int fs)
        {
            SyncMasks();
            Collect();
            EnsureStyles(fs);
            BtcMsStore st = BtcMatchStats.Current;
            for (int i = 0; i < Vis.Count; i++)
            {
                Faction fac = BtcMatchStats.FactionOf(Vis[i].Id);
                string nm = null;
                nm = BtcReplayNames.NickOf(Vis[i].Id);
                if (string.IsNullOrEmpty(nm)) { try { if (fac != null && fac.Owner != null) nm = fac.Owner.ScreenName; } catch { } }
                if (string.IsNullOrEmpty(nm)) nm = fac != null ? fac.name : ("#" + Vis[i].Id);
                if (nm != null) nm = System.Text.RegularExpressions.Regex.Replace(nm, "<[^>]*>", string.Empty).Trim();
                if (string.IsNullOrEmpty(nm)) nm = "#" + Vis[i].Id;
                if (nm.Length > 16) nm = nm.Substring(0, 16);
                FName[i] = nm;
                FCol[i] = fac != null ? fac.FactionColor : Color.gray;
                FCol[i].a = 1f;
                FFlag[i] = fac != null ? fac.FactionIcon : null;
                FCrys[i] = fac != null ? fac.CrystalIcon : null;
                Gc.text = nm; NameW[i] = _lblBold.CalcSize(Gc).x;
            }
            // bar
            _barN = 0;
            for (int k = 0; k < NS && _barN < BarIdx.Length; k++) if (InBar[k]) BarIdx[_barN++] = k;
            float pad = fs * 0.6f;
            for (int i = 0; i < Vis.Count; i++)
            {
                float w = fs * 1.2f + NameW[i] + pad;
                for (int c = 0; c < _barN; c++)
                {
                    DStat s = Stats[BarIdx[c]];
                    string t;
                    Color col = BtcUiStyle.Text;
                    if (!Avail(s, Vis[i].V)) t = "-";
                    else t = Fmt(s, Value(s, Vis[i].V), false);
                    bool icon = BarIdx[c] == StatCrystals && FCrys[i] != null;
                    string full = icon ? t : s.Tag + " " + t;
                    BarTxt[i, c] = full;
                    BarCol[i, c] = col;
                    Gc.text = full;
                    float cw = _lbl.CalcSize(Gc).x + (icon ? fs * 1.3f : 0f) + pad;
                    BarW[i, c] = cw;
                    w += cw;
                }
                BlockW[i] = w;
            }
            // panel table
            _rowN = 0;
            for (int k = 0; k < NS && _rowN < RowIdx.Length; k++)
            {
                if (!InPanel[k]) continue;
                DStat s = Stats[k];
                RowIdx[_rowN] = k;
                RowSub[_rowN] = false;
                RowName[_rowN] = s.Det >= 0 ? (Expand[k] ? "[-] " : "[+] ") + s.Name : s.Name;
                for (int i = 0; i < Vis.Count; i++)
                {
                    if (!Avail(s, Vis[i].V)) { CellTxt[i, _rowN] = "n/a"; CellCol[i, _rowN] = BtcUiStyle.Dim; continue; }
                    Cell(s, i, Value(s, Vis[i].V), Vis.Count > 1 ? DiffOf(s, i) : 0.0);
                }
                _rowN++;
                if (s.Det >= 0 && Expand[k]) AddDetailRows(s, k);
            }
            bool ru = BtcLang.Russian;
            string src = BtcMatchStats.RemoteFresh ? (BtcReplay.Mode == BtcReplay.State.Playing ? (ru ? "записано в реплее" : "recorded in the replay") : (ru ? "данные хоста" : "host data")) : (BtcMatchStats.Authoritative ? (ru ? "локально" : "local") : (ru ? "ждём данные хоста (нужен BC ON у хоста)" : "waiting for host data (host needs BC ON)"));
            _header = (ru ? "Статистика матча  " : "Match statistics  ") + FmtTime(st.T) + "  |  " + src + (SpectatorView() ? (ru ? "  |  зритель: все игроки" : "  |  spectator: all players") : (ru ? "  |  только ваша статистика" : "  |  your stats only"));
            _hint = ru ? "Клик по строке: график, [+] раскрывает по предметам. В скобках разница с противником (зелёный лучше, красный хуже)." : "Click a row: graph, [+] expands per item. In brackets: difference vs the opponent (green better, red worse).";
            BuildGraph();
        }

        private static void Cell(DStat s, int i, double v, double d)
        {
            string val = Fmt(s, v, false);
            if (Vis.Count > 1) { CellTxt[i, _rowN] = val + " (" + Fmt(s, d, true) + ")"; CellCol[i, _rowN] = Good(s, d); }
            else { CellTxt[i, _rowN] = val; CellCol[i, _rowN] = BtcUiStyle.Text; }
            // Perfect survival (accuracy 100%: no losses) is highlighted bright blue in every view.
            if (s.Kind == 1 && v >= 99.9995) CellCol[i, _rowN] = PerfectBlue;
        }

        private static readonly Color PerfectBlue = new Color(0.25f, 0.6f, 1f, 1f);

        /// <summary>Sub-rows of an expanded stat: every item key any shown faction has, sorted by name; diff vs the mean of the others.</summary>
        private static void AddDetailRows(DStat s, int k)
        {
            DetKeys.Clear();
            for (int i = 0; i < Vis.Count; i++)
                foreach (KeyValuePair<string, int> kv in Vis[i].Det[s.Det])
                    if (!DetKeys.Contains(kv.Key)) DetKeys.Add(kv.Key);
            DetKeys.Sort(StringComparer.OrdinalIgnoreCase);
            for (int r = 0; r < DetKeys.Count && _rowN < MaxRows; r++)
            {
                string key = DetKeys[r];
                RowIdx[_rowN] = k;
                RowSub[_rowN] = true;
                RowName[_rowN] = "      " + key;
                for (int i = 0; i < Vis.Count; i++)
                {
                    int v;
                    Vis[i].Det[s.Det].TryGetValue(key, out v);
                    double sum = 0;
                    for (int j = 0; j < Vis.Count; j++)
                    {
                        if (j == i) continue;
                        int o;
                        Vis[j].Det[s.Det].TryGetValue(key, out o);
                        sum += o;
                    }
                    Cell(s, i, v, Vis.Count > 1 ? v - sum / (Vis.Count - 1) : 0.0);
                }
                _rowN++;
            }
        }

        private static float SampleValue(DStat s, BtcMsFaction f, int k, bool diff)
        {
            BtcMsSample smp = f.Series[k];
            double v = Value(s, smp.V);
            if (!diff) return (float)v;
            if (Vis.Count > 1)
            {
                double sum = 0; int n = 0;
                for (int j = 0; j < Vis.Count; j++)
                {
                    if (Vis[j] == f) continue;
                    int o = Vis[j].Find(smp.Idx);
                    if (o < 0) continue;
                    sum += Value(s, Vis[j].Series[o].V); n++;
                }
                return n > 0 ? (float)(v - sum / n) : 0f;
            }
            int back = f.Find(smp.Idx - 12);
            return back >= 0 ? (float)(v - Value(s, f.Series[back].V)) : 0f;
        }

        private static void BuildGraph()
        {
            if (_graphStat < 0 || _graphStat >= NS) _graphStat = 0;
            DStat s = Stats[_graphStat];
            bool diff = false;
            _gMin = float.MaxValue; _gMax = float.MinValue; _gT = Mathf.Max(1f, BtcMatchStats.Current.T);
            for (int i = 0; i < Vis.Count; i++)
            {
                BtcMsFaction f = Vis[i];
                int n = f.Series.Count;
                // replay: the file holds the whole series; show it only up to the playback time (rewinds keep it)
                if (BtcReplay.Mode == BtcReplay.State.Playing) while (n > 0 && f.Series[n - 1].T > BtcMatchStats.Current.T + 0.5f) n--;
                if (GraphY[i] == null || GraphY[i].Length < n + 1) GraphY[i] = new float[Mathf.NextPowerOfTwo(n + 1)];
                float[] y = GraphY[i];
                for (int k = 0; k < n; k++) y[k] = SampleValue(s, f, k, diff);
                y[n] = diff ? (float)DiffOf(s, i) : (float)Value(s, f.V);
                GraphN[i] = n + 1;
                for (int k = 0; k <= n; k++) { if (y[k] < _gMin) _gMin = y[k]; if (y[k] > _gMax) _gMax = y[k]; }
            }
            if (_gMin > _gMax) { _gMin = 0f; _gMax = 1f; }
            if (diff) { float m = Mathf.Max(Mathf.Abs(_gMin), Mathf.Abs(_gMax)); _gMin = -m; _gMax = m; }
            if (_gMax - _gMin < 1e-3f) _gMax = _gMin + 1f;
            _gMinTxt = Fmt(s, _gMin, diff);
            _gMaxTxt = Fmt(s, _gMax, diff);
            _gTTxt = FmtTime(_gT);
        }

        private static int FontSize(float scale)
        {
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(11f, Screen.height / 80f) * scale), 8, 48);
        }

        private static void EnsureStyles(int fs)
        {
            if (_lbl == null)
            {
                // GUI.skin is only valid inside OnGUI (Event.current is null in Update)
                if (Event.current == null) return;
                _lbl = new GUIStyle(GUI.skin.label) { fontSize = fs, alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Clip };
                _lbl.normal.textColor = Color.white;
                _lbl.padding = new RectOffset(0, 0, 0, 0);
                _lblBold = new GUIStyle(_lbl) { fontStyle = FontStyle.Bold };
                _lblR = new GUIStyle(_lbl) { alignment = TextAnchor.MiddleRight };
                _styleFs = fs;
                return;
            }
            if (_styleFs == fs) return;
            _styleFs = fs;
            _lbl.fontSize = fs;
            _lblBold.fontSize = fs;
            _lblR.fontSize = fs;
        }

        // ------------------------------------------------------------------ per frame
        /// <summary>Update(): hotkey + 4 Hz cache.</summary>
        internal static void Tick()
        {
            try
            {
                bool view = Enabled && BtcMatchStats.Active;
                if (!view) { PanelOpen = false; return; }
                KeyCode[] keys = BtcHotkeys.GetKeys(HotkeyId, DefKeys);
                if (PanelOpen)
                {
                    if (Input.GetKeyDown(KeyCode.Escape) || (keys != null && keys.Length > 0 && Input.GetKeyDown(keys[keys.Length - 1]))) PanelOpen = false;
                }
                else if (BtcHotkeys.ComboPressed(keys)) PanelOpen = true;
                float now = Time.unscaledTime;
                bool needBar = B(BtcConfig.StatsBar, true);
                if ((needBar || PanelOpen) && now >= _nextCache)
                {
                    _nextCache = now + 0.25f;
                    _cacheFs = FontSize(F(BtcConfig.StatsBarScale, 1f));
                    _panelFs = FontSize(F(BtcConfig.StatsPanelScale, 1f));
                    if (_lbl != null) Rebuild(_cacheFs);
                    else _nextCache = 0f;
                }
            }
            catch (Exception e) { BtcLog.Error("MatchStats", "ui tick: " + e); _nextCache = Time.unscaledTime + 5f; }
        }

        private static int _panelFs = 12;

        internal static void OnGUI()
        {
            if (!Enabled || !BtcMatchStats.Active) return;
            try
            {
                if (_lbl == null) { EnsureStyles(FontSize(1f)); _nextCache = 0f; return; }
                Event e = Event.current;
                if (B(BtcConfig.StatsBar, true) && Vis.Count > 0) DrawBar(e);
                if (PanelOpen) DrawPanel(e);
            }
            catch (Exception ex) { BtcLog.Error("MatchStats", "OnGUI: " + ex); PanelOpen = false; }
        }

        private static void Box(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, BtcUiStyle.White);
            GUI.color = old;
        }

        private static void DrawSprite(Rect r, Sprite s)
        {
            if (s == null || s.texture == null) return;
            Texture2D t = s.texture;
            Rect tr = s.textureRect;
            GUI.DrawTextureWithTexCoords(r, t, new Rect(tr.x / t.width, tr.y / t.height, tr.width / t.width, tr.height / t.height));
        }

        private static void DrawBar(Event e)
        {
            int fs = _cacheFs > 0 ? _cacheFs : 12;
            EnsureStyles(fs);
            float h = fs * 1.75f;
            float gap = fs * 0.8f;
            float total = 0f;
            for (int i = 0; i < Vis.Count; i++) total += BlockW[i] + (i > 0 ? gap : 0f);
            float x = Mathf.Max(0f, (Screen.width - total) * 0.5f);
            float off = F(BtcConfig.StatsBarOffset, 0f);
            float y = B(BtcConfig.StatsBarBottom, false) ? Screen.height - h - off : off;
            float op = Mathf.Clamp01(F(BtcConfig.StatsBarOpacity, 0.85f));
            Color old = GUI.color;
            Box(new Rect(x - fs * 0.4f, y, total + fs * 0.8f, h), new Color(0.06f, 0.06f, 0.08f, 0.8f * op));
            float pad = fs * 0.6f;
            for (int i = 0; i < Vis.Count; i++)
            {
                if (i > 0) x += gap;
                Box(new Rect(x, y + h * 0.15f, fs * 0.35f, h * 0.7f), new Color(FCol[i].r, FCol[i].g, FCol[i].b, op));
                float cx = x + fs * 0.6f;
                if (FFlag[i] != null) { GUI.color = new Color(1f, 1f, 1f, op); DrawSprite(new Rect(cx, y + h * 0.1f, h * 0.8f, h * 0.8f), FFlag[i]); cx += h * 0.85f; }
                GUI.color = new Color(FCol[i].r * 0.5f + 0.5f, FCol[i].g * 0.5f + 0.5f, FCol[i].b * 0.5f + 0.5f, op);
                GUI.Label(new Rect(cx, y, NameW[i] + 2f, h), FName[i], _lblBold);
                cx += NameW[i] + pad;
                for (int c = 0; c < _barN; c++)
                {
                    float tx = cx;
                    if (BarIdx[c] == StatCrystals && FCrys[i] != null)
                    {
                        GUI.color = new Color(1f, 1f, 1f, op);
                        DrawSprite(new Rect(cx, y + h * 0.15f, h * 0.7f, h * 0.7f), FCrys[i]);
                        tx += fs * 1.3f;
                    }
                    Color cc = BarCol[i, c];
                    GUI.color = new Color(cc.r, cc.g, cc.b, op);
                    GUI.Label(new Rect(tx, y, BarW[i, c], h), BarTxt[i, c], _lbl);
                    GUI.color = old;
                    cx += BarW[i, c];
                }
                x += BlockW[i];
            }
            GUI.color = old;
        }

        private static void DrawPanel(Event e)
        {
            int fs = _panelFs;
            if (_styleFs != fs) EnsureStyles(fs);
            float ps = F(BtcConfig.StatsPanelScale, 1f);
            float op = Mathf.Clamp(F(BtcConfig.StatsPanelOpacity, 0.95f), 0.2f, 1f);
            float W = Mathf.Min(Screen.width * 0.94f, 1150f * ps), H = Mathf.Min(Screen.height * 0.86f, 700f * ps);
            Rect pr = new Rect((Screen.width - W) * 0.5f, (Screen.height - H) * 0.5f, W, H);
            Color old = GUI.color;
            Box(pr, new Color(BtcUiStyle.Panel.r, BtcUiStyle.Panel.g, BtcUiStyle.Panel.b, op));
            float rowH = fs * 1.6f;
            float pad = fs * 0.6f;
            GUI.color = BtcUiStyle.Accent;
            GUI.Label(new Rect(pr.x + pad, pr.y + pad * 0.5f, W - rowH * 2f, rowH), _header, _lblBold);
            GUI.color = old;
            if (GUI.Button(new Rect(pr.xMax - rowH * 1.4f, pr.y + pad * 0.5f, rowH * 1.2f, rowH), "X")) { PanelOpen = false; return; }
            GUI.color = BtcUiStyle.Dim;
            GUI.Label(new Rect(pr.x + pad, pr.y + pad * 0.5f + rowH, W - pad * 2f, rowH), _hint, _lbl);
            GUI.color = old;
            float top = pr.y + pad * 0.5f + rowH * 2.2f;
            float nameW = fs * 15f;
            float colW = fs * (Vis.Count > 1 ? 10f : 7f);
            float tableW = Mathf.Min(W * 0.55f, nameW + colW * Mathf.Max(1, Vis.Count) + fs * 1.5f);
            Rect tr = new Rect(pr.x + pad, top, tableW, pr.yMax - top - pad);
            // column headers
            for (int i = 0; i < Vis.Count; i++)
            {
                GUI.color = new Color(FCol[i].r * 0.5f + 0.5f, FCol[i].g * 0.5f + 0.5f, FCol[i].b * 0.5f + 0.5f, 1f);
                GUI.Label(new Rect(tr.x + nameW + colW * i, tr.y, colW - 4f, rowH), FName[i], _lblBold);
            }
            GUI.color = old;
            Rect view = new Rect(tr.x, tr.y + rowH, tr.width, tr.height - rowH);
            Rect content = new Rect(0f, 0f, tr.width - fs * 1.2f, rowH * _rowN);
            _scroll = GUI.BeginScrollView(view, _scroll, content);
            for (int r = 0; r < _rowN; r++)
            {
                float ry = r * rowH;
                int k = RowIdx[r];
                if (k == _graphStat && !RowSub[r]) Box(new Rect(0f, ry, content.width, rowH), new Color(0.55f, 0.85f, 0.55f, 0.14f));
                else if ((r & 1) == 1) Box(new Rect(0f, ry, content.width, rowH), BtcUiStyle.Hover);
                GUI.color = RowSub[r] ? BtcUiStyle.Dim : BtcUiStyle.Text;
                if (GUI.Button(new Rect(0f, ry, nameW - 4f, rowH), RowName[r], _lbl)) { if (Stats[k].Det >= 0 && !RowSub[r]) Expand[k] = !Expand[k]; _graphStat = k; _nextCache = 0f; }
                for (int i = 0; i < Vis.Count; i++)
                {
                    GUI.color = CellCol[i, r];
                    if (GUI.Button(new Rect(nameW + colW * i, ry, colW - 4f, rowH), CellTxt[i, r], _lbl)) { _graphStat = k; _nextCache = 0f; }
                }
                GUI.color = old;
            }
            GUI.EndScrollView();
            // graph
            Rect g = new Rect(tr.xMax + pad * 2f, top + rowH, pr.xMax - tr.xMax - pad * 3f, pr.yMax - top - pad * 2f - rowH * 2f);
            if (g.width > fs * 8f && g.height > fs * 4f) DrawGraph(g, fs, rowH);
            GUI.color = old;
        }

        private static void DrawGraph(Rect g, int fs, float rowH)
        {
            Color old = GUI.color;
            DStat s = Stats[_graphStat];
            GUI.color = BtcUiStyle.Text;
            GUI.Label(new Rect(g.x, g.y - rowH, g.width, rowH), s.Name, _lblBold);
            Box(g, new Color(0f, 0f, 0f, 0.35f));
            Box(new Rect(g.x, g.y, g.width, 1f), BtcUiStyle.Line);
            Box(new Rect(g.x, g.yMax - 1f, g.width, 1f), BtcUiStyle.Line);
            float span = _gMax - _gMin;
            for (int i = 0; i < Vis.Count; i++)
            {
                int n = GraphN[i];
                float[] y = GraphY[i];
                if (y == null || n <= 0) continue;
                Color c = FCol[i];
                c.a = 1f;
                GUI.color = c;
                float prev = float.NaN;
                int cols = Mathf.Max(2, (int)(g.width / 2f));
                for (int x = 0; x < cols; x++)
                {
                    int k = n == 1 ? 0 : Mathf.RoundToInt((float)x / (cols - 1) * (n - 1));
                    float py = g.yMax - (y[k] - _gMin) / span * g.height;
                    float a = float.IsNaN(prev) ? py : prev;
                    float lo = Mathf.Min(a, py), hi = Mathf.Max(a, py);
                    GUI.DrawTexture(new Rect(g.x + x * 2f, lo - 1f, 2f, hi - lo + 2f), BtcUiStyle.White);
                    prev = py;
                }
            }
            GUI.color = BtcUiStyle.Dim;
            GUI.Label(new Rect(g.x + 2f, g.y, g.width * 0.5f, rowH), _gMaxTxt, _lbl);
            GUI.Label(new Rect(g.x + 2f, g.yMax - rowH, g.width * 0.5f, rowH), _gMinTxt, _lbl);
            GUI.Label(new Rect(g.x, g.yMax, g.width * 0.5f, rowH), "0s", _lbl);
            GUI.Label(new Rect(g.x + g.width * 0.5f, g.yMax, g.width * 0.5f, rowH), _gTTxt, _lblR);
            GUI.color = old;
        }

        // ------------------------------------------------------------------ settings (Overlays tab, section "Match statistics")
        internal static void DrawSettings(Func<string, bool, bool> check, Func<string, string, bool> button, BtcInterface.SliderFn slider)
        {
            SyncMasks();
            bool ru = BtcLang.Russian;
            if (BtcConfig.StatsEnabled == null) return;
            if (check(ru ? "Включено (сбор, полоса и панель статистики)" : "Enabled (collecting, stats bar and panel)", BtcConfig.StatsEnabled.Value)) BtcConfig.StatsEnabled.Value = !BtcConfig.StatsEnabled.Value;
            if (check(ru ? "Показывать полосу" : "Show the bar", BtcConfig.StatsBar.Value)) BtcConfig.StatsBar.Value = !BtcConfig.StatsBar.Value;
            if (button(ru ? "Положение полосы" : "Bar position", BtcConfig.StatsBarBottom.Value ? (ru ? "снизу" : "bottom") : (ru ? "сверху" : "top"))) BtcConfig.StatsBarBottom.Value = !BtcConfig.StatsBarBottom.Value;
            Set(BtcConfig.StatsBarOffset, slider(ru ? "Отступ полосы от края (px)" : "Bar offset from the edge (px)", 9700, BtcConfig.StatsBarOffset.Value, 0f, 300f, 2f, "0", string.Empty));
            Set(BtcConfig.StatsBarScale, slider(ru ? "Масштаб полосы" : "Bar scale", 9701, BtcConfig.StatsBarScale.Value, 0.5f, 2f, 0.05f, "0.00", string.Empty));
            Set(BtcConfig.StatsBarOpacity, slider(ru ? "Непрозрачность полосы" : "Bar opacity", 9702, BtcConfig.StatsBarOpacity.Value, 0.1f, 1f, 0.05f, "0.00", string.Empty));
            Set(BtcConfig.StatsPanelScale, slider(ru ? "Масштаб панели" : "Panel scale", 9703, BtcConfig.StatsPanelScale.Value, 0.5f, 2f, 0.05f, "0.00", string.Empty));
            Set(BtcConfig.StatsPanelOpacity, slider(ru ? "Непрозрачность панели" : "Panel opacity", 9704, BtcConfig.StatsPanelOpacity.Value, 0.2f, 1f, 0.05f, "0.00", string.Empty));
            string keys = KeysText();
            button(ru ? "Горячая клавиша панели" : "Panel hotkey", keys + (ru ? "  (меняется в Advanced Hotkeys > Match statistics)" : "  (change in Advanced Hotkeys > Match statistics)"));
            bool barChg = false, panelChg = false;
            for (int k = 0; k < NS; k++)
            {
                if (check((ru ? "Полоса: " : "Bar: ") + Stats[k].Name, InBar[k])) { InBar[k] = !InBar[k]; barChg = true; }
            }
            for (int k = 0; k < NS; k++)
            {
                if (check((ru ? "Панель: " : "Panel: ") + Stats[k].Name, InPanel[k])) { InPanel[k] = !InPanel[k]; panelChg = true; }
            }
            if (barChg) { BtcConfig.StatsBarList.Value = Join(InBar, true); _barCfg = BtcConfig.StatsBarList.Value; _nextCache = 0f; }
            if (panelChg) { BtcConfig.StatsPanelHidden.Value = Join(InPanel, false); _panelCfg = BtcConfig.StatsPanelHidden.Value; _nextCache = 0f; }
        }

        private static void Set(BepInEx.Configuration.ConfigEntry<float> e, float v)
        {
            if (e != null && Mathf.Abs(e.Value - v) > 0.0005f) { e.Value = v; _nextCache = 0f; }
        }

        private static string KeysText()
        {
            KeyCode[] k = BtcHotkeys.GetKeys(HotkeyId, DefKeys);
            if (k == null || k.Length == 0) return "-";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < k.Length; i++) { if (i > 0) sb.Append('+'); sb.Append(k[i]); }
            return sb.ToString();
        }
    }
}
