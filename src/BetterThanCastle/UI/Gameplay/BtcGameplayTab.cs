using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Gameplay Settings tab (menu tab 4), new style. Everything the HOST configures: rules (vanilla is the base, minor changes yellow,
    /// critical ones orange), presets, the list of differences, player requests. Header master switch = "rules on" (off = vanilla game,
    /// values stay saved). Clients see the host's rules compared with THEIR OWN rules (switchable to vanilla). Recipes, handicap, map and timer
    /// still live on the old pages (reachable from "Old pages") until they are reworked. Undo scope "gameplay". UI only: no wire or cfg change.
    /// </summary>
    internal sealed partial class BtcGameplayTab : BtcMenuTab
    {
        internal static readonly BtcGameplayTab Instance = new BtcGameplayTab();

        internal override int Id { get { return 4; } }
        internal override bool IsActive { get { return BtcMenu.CurrentId == 4 && !UseLegacy; } }
        internal override string UndoScope { get { return Scope; } }

        internal const string Scope = "gameplay";
        /// <summary>True while one of the old pages (handicap, recipes, map, timer) is shown instead of this tab.</summary>
        internal static bool UseLegacy;

        internal static readonly Color Minor = new Color(0.95f, 0.78f, 0.1f, 1f);
        internal static readonly Color Critical = new Color(1f, 0.45f, 0.25f, 1f);

        private const int PRules = 0, PChanges = 1, PPresets = 2, PRequests = 3, PRecipes = 4, PMap = 5, PHandicap = 6, PTimer = 7, PShield = 8;

        internal static int Page { get { return BtcMenuState.Int("gameplay:page", 0); } set { BtcMenuState.SetInt("gameplay:page", value); } }
        internal static string Query { get { return BtcMenuState.Get("gameplay:q", ""); } set { BtcMenuState.Set("gameplay:q", value ?? ""); } }
        private static bool OnlyChanged { get { return BtcMenuState.Bool("gameplay:changed", false); } set { BtcMenuState.SetBool("gameplay:changed", value); } }
        /// <summary>Client: compare the host's rules with my own rules (true) or with vanilla (false).</summary>
        internal static bool BaseMine { get { return BtcMenuState.Bool("gameplay:basemine", true); } set { BtcMenuState.SetBool("gameplay:basemine", value); } }

        private Vector2 _scroll;
        private float _h;
        private Vector2 _off;
        private GUIStyle _ttl, _chip, _tf, _val;
        private int _fs;
        private readonly List<string> _keys = new List<string>();
        private string _presetName = "";
        private int _cmp = -1;
        private int _delConfirm = -1;
        private float _delUntil;
        private BtcGameplay.PendingReq _open;
        private static int _iDmgAdv = -2;

        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }

        private void Styles(int fs)
        {
            BtcVisualRows.Styles(fs);
            if (_ttl != null && _fs == fs) return;
            _fs = fs;
            GUIStyle b = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
            _ttl = new GUIStyle(b); _ttl.fontSize = Mathf.RoundToInt(fs * 1.15f); _ttl.fontStyle = FontStyle.Bold; _ttl.alignment = TextAnchor.MiddleLeft; _ttl.normal.textColor = Color.white;
            _chip = new GUIStyle(b); _chip.fontSize = Mathf.RoundToInt(fs * 0.85f); _chip.alignment = TextAnchor.MiddleCenter; _chip.clipping = TextClipping.Clip;
            _val = new GUIStyle(b); _val.fontSize = Mathf.RoundToInt(fs * 0.85f); _val.alignment = TextAnchor.MiddleRight; _val.clipping = TextClipping.Clip;
            _tf = new GUIStyle(GUI.skin.textField); _tf.font = b.font; _tf.fontSize = Mathf.RoundToInt(fs * 0.9f); _tf.alignment = TextAnchor.MiddleLeft; _tf.padding = new RectOffset(4, 4, 0, 0);
            _tf.normal.background = null; _tf.focused.background = null; _tf.hover.background = null; _tf.active.background = null;
            _tf.normal.textColor = Color.white; _tf.focused.textColor = Color.white; _tf.hover.textColor = Color.white;
        }

        // ------------------------------------------------------------------ criticality
        /// <summary>0 = not a rule / no effect, 1 = minor, 2 = critical (how much a CHANGED row matters for a game).</summary>
        internal static int Crit(int i, float v)
        {
            if (!BtcGameplay.IsRule(i)) return 0;
            BtcGameplay.Def d = BtcGameplay.Defs[i];
            string id = d.Id;
            if (id == "dmg.adv" || id == "mov.adv" || id == "timer.ms" || id.StartsWith("gcol.")) return 0;
            if (id.StartsWith("path.")) return v < d.Val ? 2 : 1;
            if (id == "gen.maxp" || id.StartsWith("timer.")) return 1;
            string[] crit = { "dmg.", "hp.", "stam.", "spd.", "rng.", "start.", "ore.", "vis.", "eco.income", "eco.pen", "eco.base", "eco.factor", "eco.begin", "eco.cap", "mov.noskip", "mov.allies", "mov.shootdown", "shield." };
            for (int k = 0; k < crit.Length; k++) if (id.StartsWith(crit[k])) return 2;
            return 1;
        }

        private static bool AbsOn(BtcGameplay.Def d, float[] a) { return d.Abs && d.ModeIdx >= 0 && a[d.ModeIdx] > 0.5f; }

        internal static bool RowChanged(int i, float[] a, float[] b)
        {
            BtcGameplay.Def d = BtcGameplay.Defs[i];
            if (d.Hidden || i >= a.Length || i >= b.Length) return false;
            if (Mathf.Abs(a[i] - b[i]) > 0.0001f) return true;
            if (d.Abs && d.ModeIdx >= 0)
            {
                if (AbsOn(d, a) != AbsOn(d, b)) return true;
                if (AbsOn(d, a) && Mathf.Abs(a[d.NumIdx] - b[d.NumIdx]) > 0.0001f) return true;
            }
            return false;
        }

        internal static void Count(float[] a, float[] b, out int crit, out int minor)
        {
            crit = 0; minor = 0;
            if (a == null || b == null) return;
            for (int i = 0; i < BtcGameplay.Defs.Count; i++)
            {
                if (!RowChanged(i, a, b)) continue;
                int c = Crit(i, a[i]);
                if (c == 2) crit++; else if (c == 1) minor++;
            }
        }

        private static Color CritColor(int c) { return c == 2 ? Critical : (c == 1 ? Minor : new Color(1f, 1f, 1f, 0.5f)); }

        private static string ValText(int i, float[] a)
        {
            BtcGameplay.Def d = BtcGameplay.Defs[i];
            if (AbsOn(d, a)) return a[d.NumIdx].ToString("0.##", CultureInfo.InvariantCulture) + " " + Tt(d.UnitEn, d.UnitRu);
            return BtcGameplay.Fmt(i, a[i]);
        }

        // ------------------------------------------------------------------ groups
        private static readonly string[] GroupOrder = { "dmg", "hp", "spd", "rng", "eco", "ore", "vis", "gen", "build", "mov", "path", "timer", "shield", "spec" };

        private static string GroupName(string g)
        {
            switch (g)
            {
                case "dmg": return Tt("Damage", "Урон");
                case "hp": return Tt("HP and stamina", "ХП и стамина");
                case "spd": return Tt("Speed", "Скорость");
                case "rng": return Tt("Shooting range", "Дальность стрельбы");
                case "eco": return Tt("Economy and start", "Экономика и старт");
                case "ore": return Tt("Deep layer (stone and ore)", "Глубокий слой (камень и руда)");
                case "vis": return Tt("Fog of war: vision", "Туман войны: обзор");
                case "gen": return Tt("Players and editor", "Игроки и редактор");
                case "gcol": return Tt("New colours (slots 5-8)", "Новые цвета (слоты 5-8)");
                case "build": return Tt("Building", "Постройка");
                case "mov": return Tt("Movement and worker AI", "Движение и ИИ рабочих");
                case "path": return Tt("Pathfinding budgets", "Бюджеты поиска пути");
                case "timer": return Tt("Timer and game length", "Таймер и длина игры");
                case "shield": return Tt("Base shields", "Защита базы");
                case "spec": return Tt("Spectators and returning players", "Зрители и возврат игроков");
                case "local": return Tt("Local options (never a rule)", "Локальные опции (не правила)");
            }
            return g;
        }

        private static int GroupIcon(string g)
        {
            switch (g)
            {
                case "dmg": return BtcReplayIcons.Swords;
                case "hp": return BtcReplayIcons.Group;
                case "spd": return BtcReplayIcons.Play;
                case "rng": return BtcReplayIcons.Eye;
                case "eco": return BtcReplayIcons.Chart;
                case "ore": return BtcReplayIcons.Gear;
                case "gen": return BtcReplayIcons.Group;
                case "mov": return BtcReplayIcons.Wave;
                case "build": return BtcReplayIcons.Blueprint;
                case "timer": return BtcReplayIcons.Pause;
                case "shield": return BtcReplayIcons.Shield;
                case "local": return BtcReplayIcons.Lock;
            }
            return BtcReplayIcons.Gear;
        }

        private static string GroupOf(int i)
        {
            BtcGameplay.Def d = BtcGameplay.Defs[i];
            return BtcGameplay.IsRule(i) ? d.Group : "local";
        }

        private static List<string> GroupList()
        {
            List<string> l = new List<string>(GroupOrder);
            for (int i = 0; i < BtcGameplay.Defs.Count; i++)
            {
                string g = GroupOf(i);
                if (g != "local" && !l.Contains(g)) l.Add(g);
            }
            l.Add("local");
            return l;
        }

        private static bool Visible(BtcGameplay.Def d, float[] shown)
        {
            if (d.Hidden || d.Id == "timer.ms" || d.Group == "gcol") return false;   // lobby colour customisation (gcol) is removed from the UI for now
            if (d.Adv != 0)
            {
                if (_iDmgAdv == -2) _iDmgAdv = BtcGameplay.Idx("dmg.adv");
                string g = d.Group;
                bool balance = g == "dmg" || g == "hp" || g == "spd" || g == "eco" || g == "vis" || g == "rng" || g == "ore";
                if (balance)
                {
                    bool adv = _iDmgAdv >= 0 && shown[_iDmgAdv] > 0.5f;
                    if (!((d.Adv == 1 && !adv) || (d.Adv == 2 && adv))) return false;
                }
                else if (!BtcQol.AdvShown(shown)) return false;
            }
            if (d.Group == "shield" && !BtcShieldPage.Vis(d, shown)) return false;
            return BtcSimplePhys.Vis(d, shown);
        }

        private static bool Match(BtcGameplay.Def d, string q)
        {
            if (q.Length == 0) return true;
            if (d.En.ToLowerInvariant().Contains(q) || d.Ru.ToLowerInvariant().Contains(q) || d.Id.Contains(q)) return true;
            return GroupName(d.Group).ToLowerInvariant().Contains(q);
        }

        // ------------------------------------------------------------------ draw
        internal override void Draw(Rect area, int fs)
        {
            Styles(fs);
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == BtcMenu.MenuKeyMain)
            {
                GUIUtility.keyboardControl = 0;
                BtcMenu.PgDownFrame = Time.frameCount;
                ResourcePackManager.CloseMenu();
                e.Use();
                return;
            }
            BtcUi.ClickAway(e);
            BtcGameplay.Tick();
            bool host = BtcGameplay.IsHost, session = BtcGameplay.InSession;
            bool draft = !host && session && BtcGameplayUI._draft;
            float[] shown = host ? BtcGameplay.Raw : (draft ? BtcGameplay.Proposal : BtcGameplay.Raw);
            bool baseMine = !host && session && BaseMine;
            float[] bas = baseMine ? BtcGameplay.Mine : BtcGameplay.Vanilla;
            bool interact = (host && BtcGameplay.CanEdit) || draft;

            float railW = BtcMenu.SubRailWidth(fs);
            DrawRail(new Rect(area.x, area.y, railW, area.height), fs, host ? BtcGameplay.Pending.Count : 0);
            area = new Rect(area.x + railW, area.y, area.width - railW, area.height);
            BtcReplayUI.Fill(new Rect(area.x - 1f, area.y, 1f, area.height), BtcMenu.Line);

            float pad = fs, ib = fs * 1.9f;
            float y0 = area.y + fs * 0.4f;
            GUI.Label(new Rect(area.x + pad, y0, area.width * 0.5f, ib), PageName(Page), _ttl);

            // master switch (host only; a client just sees the host's rules)
            bool on = host ? BtcGameplay.RulesOn : true;
            Rect sw = new Rect(area.xMax - pad - fs * 2.4f, y0 + (ib - fs * 1.2f) * 0.5f, fs * 2.4f, fs * 1.2f);
            string tip = !host ? Tt("The host decides the rules; below you see the host's settings", "Правила задаёт хост; ниже настройки хоста")
                : (on ? Tt("Rules are ON (click: play by vanilla rules, your values stay saved)", "Правила включены (клик - играть по ванильным правилам, ваши значения сохранятся)")
                      : Tt("Rules are OFF: the game is vanilla (click to apply your values again)", "Правила выключены: игра ванильная (клик - снова применить ваши значения)"));
            if (BtcUi.HeaderSwitch(sw, on, tip))
            {
                if (!host) { BtcSfx.Warn(); BtcMenu.Toast(Tt("Only the host can switch the rules", "Правила переключает только хост"), BtcReplayIcons.Lock); }
                else if (!BtcGameplay.CanEdit) { BtcSfx.Warn(); BtcMenu.Toast(Tt("Rules are locked after the game started", "Правила заблокированы после старта игры"), BtcReplayIcons.Lock); }
                else
                {
                    bool was = on;
                    BtcGameplay.RulesOn = !on;
                    BtcSfx.Switch(!on);
                    BtcUndo.Push(Scope, Tt("Rules on/off", "Правила вкл/выкл"), delegate { BtcGameplay.RulesOn = was; });
                }
            }

            // controls row: search + icon buttons (rules and changes pages)
            float y = y0 + ib + fs * 0.3f;
            int page = Page;
            int reqN = host ? BtcGameplay.Pending.Count : 0;
            bool searching = Query.Trim().Length > 0;
            {
                bool tools = page == PRules || page == PChanges || page == PRecipes || searching;
                float ibs = fs * 1.8f;
                float rx = area.xMax - pad;
                if (page == PRules && !searching)
                {
                    rx -= ibs;
                    if (BtcReplayIcons.Button(new Rect(rx, y, ibs, ibs), BtcReplayIcons.ChevR, false, Tt("Collapse all sections", "Свернуть все разделы"))) BtcUi.SetOpen(_keys, false);
                    rx -= ibs + 3f;
                    if (BtcReplayIcons.Button(new Rect(rx, y, ibs, ibs), BtcReplayIcons.ChevD, false, Tt("Expand all sections", "Развернуть все разделы"))) BtcUi.SetOpen(_keys, true);
                    rx -= 3f;
                }
                if (tools)
                {
                    rx -= ibs;
                    if (BtcReplayIcons.Button(new Rect(rx, y, ibs, ibs), BtcReplayIcons.Bolt, OnlyChanged, Tt("Show only the rules that differ from the base", "Показывать только то, что отличается от базы"))) OnlyChanged = !OnlyChanged;
                }
                if (tools && !host && session)
                {
                    rx -= ibs + 3f;
                    if (BtcReplayIcons.Button(new Rect(rx, y, ibs, ibs), BtcReplayIcons.Ring, !BaseMine, Tt("Compare the host with vanilla", "Сравнивать хоста с ванилой"))) BaseMine = false;
                    rx -= ibs + 3f;
                    if (BtcReplayIcons.Button(new Rect(rx, y, ibs, ibs), BtcReplayIcons.Monitor, BaseMine, Tt("Compare the host with MY rules", "Сравнивать хоста с МОИМИ правилами"))) BaseMine = true;
                }
                Rect sr = new Rect(area.x + pad, y, rx - area.x - pad - 6f, ibs);
                BtcReplayUI.Fill(sr, new Color(1f, 1f, 1f, 0.06f));
                BtcReplayIcons.Glyph(BtcReplayIcons.Search, new Rect(sr.x + fs * 0.4f, sr.y + fs * 0.3f, fs * 1.2f, fs * 1.2f), new Color(1f, 1f, 1f, 0.45f));
                BtcUi.Field(sr);
                string q = GUI.TextField(new Rect(sr.x + fs * 2.0f, sr.y, sr.width - fs * 2.4f - fs * 1.6f, sr.height), Query, 48, _tf);
                if (q != Query) Query = q;
                if (string.IsNullOrEmpty(Query) && GUIUtility.keyboardControl == 0) GUI.Label(new Rect(sr.x + fs * 2.1f, sr.y, sr.width, sr.height), Tt("Search everywhere: rules, recipes, pages...", "Поиск везде: правила, рецепты, страницы..."), BtcVisualRows.Dim);
                if (!string.IsNullOrEmpty(Query) && BtcReplayIcons.Plain(new Rect(sr.xMax - fs * 1.7f, sr.y + fs * 0.1f, fs * 1.6f, fs * 1.6f), BtcReplayIcons.Cross, Color.white, Tt("Clear search", "Очистить поиск"))) { Query = ""; GUIUtility.keyboardControl = 0; }
                y += ibs + fs * 0.4f;
            }
            BtcReplayUI.Fill(new Rect(area.x + pad, y - fs * 0.2f, area.width - pad * 2f, 1f), BtcMenu.Line);

            Rect view = new Rect(area.x, y, area.width, area.yMax - y);
            float vw = view.width - (_h > view.height ? 14f : 0f);
            _scroll = GUI.BeginScrollView(view, _scroll, new Rect(0f, 0f, vw, _h));
            _off = new Vector2(view.x - _scroll.x, view.y - _scroll.y);
            float cy = fs * 0.2f;
            _keys.Clear();
            try
            {
                Status(ref cy, vw, fs, host, session, draft, on);
                if (searching) PageSearch(ref cy, vw, fs, shown, bas, interact);
                else switch (page)
                {
                    case PRules: PageRules(ref cy, vw, fs, shown, bas, interact, baseMine); break;
                    case PChanges: PageChanges(ref cy, vw, fs, shown, bas, baseMine); break;
                    case PPresets: PagePresets(ref cy, vw, fs, shown, interact); break;
                    case PRequests: PageRequests(ref cy, vw, fs, host, session, draft); break;
                    case PRecipes: PageRecipes(ref cy, vw, fs, host, session, draft); break;
                    case PMap: PageMap(ref cy, vw, fs, host, session); break;
                    case PHandicap: PageHandicap(ref cy, vw, fs); break;
                    case PTimer: PageRules(ref cy, vw, fs, shown, bas, interact, baseMine, "timer"); break;
                    case PShield: PageShield(ref cy, vw, fs, shown, bas, interact, baseMine); break;
                }
            }
            catch (Exception ex) { if (e.type == EventType.Repaint) BtcLog.Error("GameplayTab", ex.ToString()); }
            GUI.EndScrollView();
            _h = cy + fs * 3f;
            BtcReplayIcons.DrawTip(fs, area.xMax, false);
        }

        // ------------------------------------------------------------------ page rail (icons on the left, like Advanced Hotkeys)

        private static string PageName(int p)
        {
            switch (p)
            {
                case PRules: return Tt("Rules", "Правила");
                case PChanges: return Tt("Differences", "Отличия");
                case PPresets: return Tt("Presets", "Пресеты");
                case PRequests: return Tt("Requests", "Запросы");
                case PRecipes: return Tt("Recipes", "Рецепты");
                case PHandicap: return Tt("Handicap", "Фора");
                case PTimer: return Tt("Timer and game length", "Таймер и длина игры");
                case PShield: return Tt("Base shields", "Защита базы");
                case PMap: return Tt("Map: symmetry, scale, map presets", "Карта: симметрия, скейл, пресеты карт");
            }
            return "";
        }

        private static int PageIcon(int p)
        {
            switch (p)
            {
                case PRules: return BtcReplayIcons.Gear;
                case PChanges: return BtcReplayIcons.Diamond;
                case PPresets: return BtcReplayIcons.Folder;
                case PRequests: return BtcReplayIcons.Chat;
                case PRecipes: return BtcReplayIcons.Blueprint;
                case PHandicap: return BtcReplayIcons.Group;
                case PTimer: return BtcReplayIcons.Pause;
                case PShield: return BtcReplayIcons.Shield;
                case PMap: return BtcReplayIcons.Map;
            }
            return BtcReplayIcons.Gear;
        }

        private static readonly int[] RailPages = { PRules, PChanges, PPresets, PRequests, PRecipes, PMap, PTimer, PShield, PHandicap };

        private void DrawRail(Rect r, int fs, int reqN)
        {
            Event e = Event.current;
            float item = r.width * 0.82f, x = r.x + (r.width - item) * 0.5f, y = r.y + fs * 0.6f, itemW = item;
            BtcMenu.RailGeometry(r, fs, ref item, ref itemW, ref x);
            for (int i = 0; i < RailPages.Length; i++)
            {
                int p = RailPages[i];
                if (p == PRecipes) { BtcReplayUI.Fill(new Rect(x + itemW * 0.15f, y, itemW * 0.7f, 1f), BtcMenu.Line); y += fs * 0.5f; }
                Rect ir = new Rect(x, y, itemW, item);
                bool on = p == Page, hover = ir.Contains(e.mousePosition);
                if (on) { BtcReplayUI.Fill(ir, new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.14f)); BtcReplayUI.Fill(new Rect(r.x, y + item * 0.15f, 3f, item * 0.7f), BtcMenu.Accent); }
                else if (hover) BtcReplayUI.Fill(ir, new Color(1f, 1f, 1f, 0.07f));
                float g = item * 0.55f;
                BtcMenu.RailGlyph(ir, fs, PageIcon(p), PageName(p), on, hover);
                if (p == PRequests && reqN > 0) BtcReplayUI.Fill(new Rect(ir.xMax - fs * 0.55f, ir.y + fs * 0.1f, fs * 0.5f, fs * 0.5f), Minor);
                BtcReplayIcons.Hint(ir, PageName(p));
                if (BtcSfx.Btn(ir, "", BtcReplayUI.sBtn))
                {
                    Page = p; _scroll = Vector2.zero;
                }
                y += item + fs * 0.3f;
            }
        }

        /// <summary>Square icon button (accent when on) with a tooltip: used instead of text buttons.</summary>
        private bool IcoBtn(Rect r, int icon, bool on, Color c, string tip)
        {
            bool hv = r.Contains(Event.current.mousePosition);
            BtcReplayUI.Fill(r, on ? new Color(c.r, c.g, c.b, 0.22f) : new Color(1f, 1f, 1f, hv ? 0.14f : 0.07f));
            float g = Mathf.Min(r.width, r.height) * 0.62f;
            BtcReplayIcons.Glyph(icon, new Rect(r.center.x - g * 0.5f, r.center.y - g * 0.5f, g, g), on || hv ? c : new Color(1f, 1f, 1f, 0.7f));
            BtcReplayIcons.Hint(r, new Rect(_off.x + r.x, _off.y + r.y, r.width, r.height), tip);
            return BtcSfx.Btn(r, "", BtcReplayUI.sBtn);
        }

        private bool Chip(Rect r, string text, bool on, Color c)
        {
            bool hv = r.Contains(Event.current.mousePosition);
            BtcReplayUI.Fill(r, on ? new Color(c.r, c.g, c.b, 0.22f) : new Color(1f, 1f, 1f, hv ? 0.14f : 0.07f));
            BtcVisualRows.Txt(r, text, _chip, on ? c : new Color(1f, 1f, 1f, hv ? 0.95f : 0.7f));
            return BtcSfx.Btn(r, "", BtcReplayUI.sBtn);
        }

        private void Status(ref float y, float w, int fs, bool host, bool session, bool draft, bool on)
        {
            string text;
            Color c = BtcMenu.Accent;
            if (host)
            {
                if (!on) { text = Tt("Rules are OFF: the game plays by vanilla rules. Your values below stay saved.", "Правила ВЫКЛЮЧЕНЫ: игра идёт по ванильным правилам. Ваши значения ниже сохраняются."); c = Minor; }
                else if (!session) text = Tt("Offline: these rules apply to your next game (singleplayer or a lobby you host).", "Офлайн: правила применятся в вашей следующей игре (одиночной или в вашем лобби).");
                else if (BtcGameplay.CanEdit) text = Tt("You are the host: changes apply to everybody at once.", "Вы хост: изменения сразу применяются у всех.");
                else { text = Tt("The game has started: rules are locked.", "Игра началась: правила заблокированы."); c = Minor; }
            }
            else if (session) text = draft ? Tt("Request mode: set what you want and send it to the host (Requests page).", "Режим запроса: выберите, что хотите, и отправьте хосту (страница Запросы).") : Tt("These are the host's rules (read-only). Rows show how they differ from the base above.", "Это правила хоста (только просмотр). Строки показывают отличие от выбранной базы.");
            else text = Tt("Host a lobby (or join one) to change settings. Presets can be edited anywhere.", "Создайте лобби (или зайдите в чужое), чтобы менять настройки. Пресеты можно править где угодно.");
            float pad = fs, h = fs * 2.3f;
            Rect card = new Rect(pad, y, w - pad * 2f, h);
            BtcReplayUI.Fill(card, new Color(1f, 1f, 1f, 0.04f));
            BtcReplayUI.Fill(new Rect(card.x, card.y + h * 0.12f, 3f, h * 0.76f), c);
            int ic = c == Minor ? BtcReplayIcons.Lock : (host ? (session ? BtcReplayIcons.Check : BtcReplayIcons.Gear) : BtcReplayIcons.Eye);
            float ig = fs * 1.3f;
            BtcReplayIcons.Glyph(ic, new Rect(card.x + fs * 0.7f, card.center.y - ig * 0.5f, ig, ig), c);
            BtcVisualRows.Txt(new Rect(card.x + fs * 2.4f, card.y, card.width - fs * 2.7f, h), text, BtcVisualRows.Lbl, new Color(1f, 1f, 1f, 0.85f));
            y += h + fs * 0.4f;
        }

        private void Summary(ref float y, float w, int fs, float[] shown, float[] bas, bool baseMine)
        {
            int crit, minor;
            Count(shown, bas, out crit, out minor);
            float pad = fs, h = fs * 2.1f;
            Rect row = new Rect(pad, y, w - pad * 2f, h);
            BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, 0.03f));
            string basis = baseMine ? Tt("my rules", "моих правил") : Tt("vanilla", "ванили");
            BtcVisualRows.Txt(new Rect(row.x + fs * 0.6f, row.y, fs * 14f, h), Tt("Differences from " + basis + ":", "Отличия от " + basis + ":"), BtcVisualRows.Lbl, new Color(1f, 1f, 1f, 0.8f));
            float x = row.x + fs * 15f;
            string t1 = crit + " " + Tt("critical", "критично"), t2 = minor + " " + Tt("minor", "незначительно");
            BtcReplayUI.Fill(new Rect(x, row.center.y - fs * 0.35f, fs * 0.7f, fs * 0.7f), Critical);
            BtcVisualRows.Txt(new Rect(x + fs * 1.0f, row.y, fs * 9f, h), t1, BtcVisualRows.Lbl, crit > 0 ? Critical : new Color(1f, 1f, 1f, 0.5f));
            x += fs * 9.5f;
            BtcReplayUI.Fill(new Rect(x, row.center.y - fs * 0.35f, fs * 0.7f, fs * 0.7f), Minor);
            BtcVisualRows.Txt(new Rect(x + fs * 1.0f, row.y, fs * 11f, h), t2, BtcVisualRows.Lbl, minor > 0 ? Minor : new Color(1f, 1f, 1f, 0.5f));
            BtcReplayIcons.Hint(row, new Rect(_off.x + row.x, _off.y + row.y, row.width, row.height), Tt("Critical = changes fights, economy or the winner. Minor = small tweaks and convenience. Click the Changes page for the full list.", "Критично = меняет бои, экономику или победителя. Незначительно = мелкие правки и удобство. Полный список на странице Отличия."));
            y += h + fs * 0.4f;
        }

        // ------------------------------------------------------------------ rules page
        private void PageRules(ref float y, float w, int fs, float[] shown, float[] bas, bool interact, bool baseMine, string only = null)
        {
            if (only == null) Summary(ref y, w, fs, shown, bas, baseMine);
            string q = Query.Trim().ToLowerInvariant();
            bool onlyCh = OnlyChanged;
            bool filtering = q.Length > 0 || onlyCh;
            List<string> groups = GroupList();
            List<int> rows = new List<int>();
            bool any = false;
            for (int g = 0; g < groups.Count; g++)
            {
                if (only != null ? groups[g] != only : (groups[g] == "timer" || groups[g] == "shield")) continue;   // the timer and shield rules have their own pages
                rows.Clear();
                int crit = 0, minor = 0;
                for (int i = 0; i < BtcGameplay.Defs.Count; i++)
                {
                    BtcGameplay.Def d = BtcGameplay.Defs[i];
                    if (GroupOf(i) != groups[g] || !Visible(d, shown) || !Match(d, q)) continue;
                    bool ch = RowChanged(i, shown, bas);
                    if (onlyCh && !ch) continue;
                    rows.Add(i);
                    if (ch) { int c = Crit(i, shown[i]); if (c == 2) crit++; else if (c == 1) minor++; }
                }
                if (rows.Count == 0) continue;
                any = true;
                string key = "gp:fold:" + groups[g];
                _keys.Add(key);
                string right = (crit > 0 ? crit + " " + Tt("critical", "критично") : "") + (crit > 0 && minor > 0 ? ", " : "") + (minor > 0 ? minor + " " + Tt("minor", "незн.") : "");
                bool open = BtcVisualRows.Fold(ref y, w, fs, key, GroupIcon(groups[g]), GroupName(groups[g]), right);
                if (open || filtering)
                {
                    if (groups[g] == "ore" && q.Length == 0) OreBlock(ref y, w, fs, shown, interact);
                    else for (int r = 0; r < rows.Count; r++) Row(ref y, w, fs, rows[r], shown, bas, interact);
                    y += fs * 0.3f;
                }
            }
            if (!any) BtcUi.Note(ref y, w, fs, onlyCh && q.Length == 0 ? Tt("Nothing is changed: these are the base rules.", "Ничего не изменено: это базовые правила.") : Tt("No rule matches your search.", "Ничего не найдено."));
        }

        // ------------------------------------------------------------------ deep layer (ore): one linked editor instead of three loose rows
        private void OreBlock(ref float y, float w, int fs, float[] shown, bool interact)
        {
            int[] ids = { BtcGameplay.iOreStone, BtcGameplay.iOreIron, BtcGameplay.iOreBrim };
            string[] names = { Tt("Hard stone", "Твёрдый камень"), Tt("Iron ore", "Железная руда"), Tt("Brimstone (orange crystal)", "Бримстоун (оранжевый кристалл)") };
            Color[] cols = { new Color(0.62f, 0.64f, 0.7f, 1f), new Color(0.72f, 0.5f, 0.38f, 1f), new Color(1f, 0.6f, 0.2f, 1f) };
            bool custom = shown[ids[0]] >= 0f || shown[ids[1]] >= 0f || shown[ids[2]] >= 0f;
            float[] v = new float[3];
            for (int i = 0; i < 3; i++) v[i] = shown[ids[i]] >= 0f ? shown[ids[i]] : BtcGameplay.Defs[ids[i]].Val;
            float[] old = (float[])v.Clone();
            float[] oldRaw = { shown[ids[0]], shown[ids[1]], shown[ids[2]] };
            if (BtcVisualRows.Switch(ref y, w, fs, 0, Tt("Custom ore mix (off = vanilla of the map)", "Свой состав руды (выкл = ваниль карты)"), custom, null) && interact)
            {
                if (custom) BtcGameplay.SetOre(-1f, -1f, -1f);
                else BtcGameplay.SetOre(BtcGameplay.Defs[ids[0]].Val, BtcGameplay.Defs[ids[1]].Val, BtcGameplay.Defs[ids[2]].Val);
                BtcUndo.Push(Scope, Tt("Ore mix", "Состав руды"), delegate { BtcGameplay.SetOre(oldRaw[0], oldRaw[1], oldRaw[2]); });
            }
            if (!custom)
            {
                BtcUi.Note(ref y, w, fs, Tt("The ore share of the deep layer comes from the map (vanilla). New maps only: it is applied when the terrain is generated.", "Доли руды в глубоком слое берутся с карты (ваниль). Только для новых карт: применяется при генерации земли."));
                return;
            }
            // stacked bar: stone, iron, brimstone, soft earth (the rest)
            float soft = Mathf.Max(0f, 100f - v[0] - v[1] - v[2]);
            float pad = fs, bh = fs * 2.0f;
            Rect bar = new Rect(pad, y + fs * 0.2f, w - pad * 2f, bh);
            BtcReplayUI.Fill(bar, new Color(1f, 1f, 1f, 0.05f));
            float x = bar.x;
            float[] seg = { v[0], v[1], v[2], soft };
            Color[] sc = { cols[0], cols[1], cols[2], new Color(0.45f, 0.7f, 0.35f, 1f) };
            string[] sn = { names[0], names[1], names[2], Tt("Soft earth", "Мягкая земля") };
            for (int i = 0; i < 4; i++)
            {
                float sw = bar.width * Mathf.Clamp01(seg[i] / 100f);
                if (sw < 0.5f) continue;
                Rect sr = new Rect(x, bar.y, sw, bh);
                BtcReplayUI.Fill(sr, sc[i]);
                if (sw > fs * 3.2f) BtcVisualRows.Txt(sr, seg[i].ToString("0.#", CultureInfo.InvariantCulture) + "%", _chip, new Color(0.05f, 0.06f, 0.07f, 1f));
                BtcReplayIcons.Hint(sr, new Rect(_off.x + sr.x, _off.y + sr.y, sr.width, sr.height), sn[i] + ": " + seg[i].ToString("0.#", CultureInfo.InvariantCulture) + "%");
                x += sw;
            }
            y += bh + fs * 0.6f;
            for (int i = 0; i < 3; i++)
            {
                float nv = BtcVisualRows.Slider(ref y, w, fs, 0, "gp:ore" + i, names[i], v[i], 0f, 100f, 0.1f, "0.#", "%");
                BtcReplayUI.Fill(new Rect(fs - 5f, y - fs * 2.1f - 3f, 3f, fs * 2.1f), cols[i]);
                if (interact && Mathf.Abs(nv - v[i]) > 0.0001f)
                {
                    BtcGameplay.Set(ids[i], nv);   // linked: the other two shrink so that the total never exceeds 100 (100 = only this ore)
                    BtcUndo.Push(Scope, Tt("Ore mix", "Состав руды"), delegate { BtcGameplay.SetOre(oldRaw[0], oldRaw[1], oldRaw[2]); }, "gp:ore");
                }
            }
            BtcVisualRows.Txt(new Rect(pad + fs * 0.6f, y, w - pad * 2f, fs * 1.6f), Tt("Soft earth is the rest: ", "Мягкая земля - остаток: ") + soft.ToString("0.#", CultureInfo.InvariantCulture) + "%", BtcVisualRows.Dim, new Color(1f, 1f, 1f, 0.6f));
            y += fs * 1.8f;
        }

        private void Row(ref float y, float w, int fs, int i, float[] shown, float[] bas, bool interact)
        {
            BtcGameplay.Def d = BtcGameplay.Defs[i];
            bool abs = AbsOn(d, shown);
            int ni = abs ? d.NumIdx : i;
            BtcGameplay.Def dn = BtcGameplay.Defs[ni];
            string label = Tt(d.En, d.Ru);
            bool changed = RowChanged(i, shown, bas);
            int crit = changed ? Crit(i, shown[i]) : 0;
            float rowH = fs * 2.1f;
            float rowY = y;
            string id = "gp:" + d.Id;

            // right gutter: [abs chip][reset][star]
            float gw = BtcVisualRows.GutterW(fs);
            float resetW = fs * 2.0f, chipW = d.Abs ? fs * 3.4f : 0f;
            BtcVisualRows.Star(BtcVisualRows.StarRect(y, w, fs), id, label);
            float rx = w - gw - resetW;
            if (changed)
            {
                Rect rr = new Rect(rx, y + rowH * 0.1f, resetW - 4f, rowH * 0.8f);
                if (BtcReplayIcons.Plain(rr, BtcReplayIcons.Undo, Color.white, Tt("Reset to ", "Сбросить к ") + (bas == BtcGameplay.Vanilla ? Tt("vanilla", "ваниле") : Tt("my value", "моему значению")) + ": " + ValText(i, bas)) && interact)
                    ResetRow(i, shown, bas, label);
            }
            if (d.Abs)
            {
                Rect cr = new Rect(rx - chipW, y + rowH * 0.1f, chipW - 4f, rowH * 0.8f);
                string ct = abs ? Tt(d.UnitEn, d.UnitRu) : "x";
                if (Chip(cr, ct, abs, BtcMenu.Accent))
                {
                    if (interact)
                    {
                        bool wasAbs = abs;
                        float oldNum = shown[d.NumIdx];
                        BtcGameplay.Set(d.ModeIdx, wasAbs ? 0f : 1f);
                        if (!wasAbs) BtcGameplay.Set(d.NumIdx, BtcGameplay.SeenNum(i));
                        float oldMode = wasAbs ? 1f : 0f;
                        BtcUndo.Push(Scope, label, delegate { BtcGameplay.Set(d.ModeIdx, oldMode); BtcGameplay.Set(d.NumIdx, oldNum); });
                    }
                }
                BtcReplayIcons.Hint(cr, new Rect(_off.x + cr.x, _off.y + cr.y, cr.width, cr.height), Tt("Switch between a multiplier (x) and an exact number", "Переключить между множителем (x) и точным числом"));
            }
            float w2 = w - gw - resetW - chipW;
            float cur = shown[ni];
            if (d.IsBool)
            {
                if (BtcVisualRows.Switch(ref y, w2, fs, 0, label, cur > 0.5f, null) && interact)
                {
                    float old = cur;
                    BtcGameplay.Set(ni, old > 0.5f ? 0f : 1f);
                    BtcUndo.Push(Scope, label, delegate { BtcGameplay.Set(ni, old); });
                }
            }
            else
            {
                string fmt = dn.Step >= 1f ? "0" : "0.##";
                string suffix = abs ? " " + Tt(d.UnitEn, d.UnitRu) : (dn.HasMapDefault && cur < 0f ? "" : "");
                float nv = BtcVisualRows.Slider(ref y, w2, fs, 0, id, label, cur, dn.Min, dn.Max, dn.Step, fmt, suffix);
                if (interact && Mathf.Abs(nv - cur) > 0.00001f)
                {
                    float old = cur;
                    BtcGameplay.Set(ni, nv);
                    BtcUndo.Push(Scope, label, delegate { BtcGameplay.Set(ni, old); }, id);
                }
                TypedValue(new Rect(w2 - fs - fs * 6f, rowY, fs * 5.6f, rowH), id, ni, cur, dn, interact, label);
            }
            if (crit > 0) BtcReplayUI.Fill(new Rect(fs - 5f, rowY, 3f, rowH), CritColor(crit));
            // tooltip: hint + base value
            Rect hit = new Rect(fs, rowY, (w2 - fs) * 0.42f, rowH);
            string tip = (d.HintEn != null ? Tt(d.HintEn, d.HintRu) + "\n" : "") + Tt("Vanilla: ", "Ваниль: ") + ValText(i, BtcGameplay.Vanilla)
                + (crit == 2 ? "\n" + Tt("Critical for gameplay", "Критично для геймплея") : (crit == 1 ? "\n" + Tt("Minor change", "Незначительное изменение") : ""));
            BtcReplayIcons.Hint(hit, new Rect(_off.x + hit.x, _off.y + hit.y, hit.width, hit.height), tip);
        }

        // ------------------------------------------------------------------ typed value: click the number of a slider row, type, Enter
        private string _typeId, _typeText;
        private int _typeFrames;

        /// <summary>Lets the player type an exact value over the value text of a slider row. vr = the value rect (window of the scroll view).</summary>
        private void TypedValue(Rect vr, string id, int ni, float shownValue, BtcGameplay.Def dn, bool interact, string label)
        {
            Event e = Event.current;
            if (_typeId == id)
            {
                BtcReplayUI.Fill(vr, new Color(0.02f, 0.03f, 0.04f, 1f));
                BtcReplayUI.Fill(new Rect(vr.x, vr.yMax - 2f, vr.width, 2f), BtcMenu.Accent);
                BtcUi.Field(vr);
                GUI.SetNextControlName("gp_type");
                string t = GUI.TextField(vr, _typeText, 9, _tf);
                if (_typeFrames == 0) GUI.FocusControl("gp_type");
                if (e.type == EventType.Repaint) _typeFrames++;
                if (t != _typeText) _typeText = t;
                bool enter = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter);
                bool esc = e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape;
                bool lost = _typeFrames > 3 && GUIUtility.keyboardControl == 0;
                if (enter || esc || lost)
                {
                    float v;
                    if (!esc && float.TryParse(_typeText.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v) && interact)
                    {
                        v = Mathf.Clamp(v, dn.Min, dn.Max);
                        float old = shownValue;
                        if (Mathf.Abs(v - old) > 0.00001f)
                        {
                            BtcGameplay.Set(ni, v);
                            BtcUndo.Push(Scope, label, delegate { BtcGameplay.Set(ni, old); });
                        }
                    }
                    _typeId = null;
                    GUIUtility.keyboardControl = 0;
                    if (enter || esc) e.Use();
                }
            }
            else if (interact && e.type == EventType.MouseDown && e.button == 0 && vr.Contains(e.mousePosition) && !BtcHotkeys.UiClickSwallowed)
            {
                _typeId = id;
                _typeText = shownValue.ToString(dn.Step >= 1f ? "0" : "0.##", CultureInfo.InvariantCulture);
                _typeFrames = 0;
                e.Use();
            }
            if (interact) BtcReplayIcons.Hint(vr, new Rect(_off.x + vr.x, _off.y + vr.y, vr.width, vr.height), Tt("Click the number to type an exact value", "Клик по числу - ввести точное значение"));
        }

        private void ResetRow(int i, float[] shown, float[] bas, string label)
        {
            BtcGameplay.Def d = BtcGameplay.Defs[i];
            float o = shown[i], om = d.Abs ? shown[d.ModeIdx] : 0f, on = d.Abs ? shown[d.NumIdx] : 0f;
            BtcGameplay.Set(i, bas[i]);
            if (d.Abs) { BtcGameplay.Set(d.ModeIdx, bas[d.ModeIdx]); BtcGameplay.Set(d.NumIdx, bas[d.NumIdx]); }
            bool abs = d.Abs;
            BtcSfx.Ok();
            BtcUndo.Push(Scope, Tt("Reset: ", "Сброс: ") + label, delegate { BtcGameplay.Set(i, o); if (abs) { BtcGameplay.Set(d.ModeIdx, om); BtcGameplay.Set(d.NumIdx, on); } });
        }

        // ------------------------------------------------------------------ changes page
        /// <summary>Rows where a differs from b, critical first. Returns how many were drawn. clickable: a click jumps to the rule on the Rules page.</summary>
        private int DiffRows(ref float y, float w, int fs, float[] a, float[] b, bool clickable, string q)
        {
            List<int> idx = new List<int>();
            for (int pass = 2; pass >= 1; pass--)
                for (int i = 0; i < BtcGameplay.Defs.Count; i++)
                {
                    BtcGameplay.Def d = BtcGameplay.Defs[i];
                    if (!RowChanged(i, a, b) || Crit(i, a[i]) != pass || !Match(d, q)) continue;
                    idx.Add(i);
                }
            float pad = fs, h = fs * 2.0f;
            for (int k = 0; k < idx.Count; k++)
            {
                int i = idx[k];
                BtcGameplay.Def d = BtcGameplay.Defs[i];
                int c = Crit(i, a[i]);
                Rect row = new Rect(pad, y, w - pad * 2f, h);
                bool hv = clickable && row.Contains(Event.current.mousePosition);
                BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, hv ? 0.07f : 0.03f));
                BtcReplayUI.Fill(new Rect(row.x, row.y, 3f, h), CritColor(c));
                BtcVisualRows.Txt(new Rect(row.x + fs * 0.8f, row.y, row.width * 0.5f, h), Tt(d.En, d.Ru), BtcVisualRows.Lbl, Color.white);
                float vr = row.xMax - fs * 0.5f, vwid = fs * 7.5f, gg = fs * 1.0f;
                BtcVisualRows.Txt(new Rect(vr - vwid, row.y, vwid, h), ValText(i, a), _val, CritColor(c));
                BtcReplayIcons.Glyph(BtcReplayIcons.ChevR, new Rect(vr - vwid - gg - fs * 0.3f, row.center.y - gg * 0.5f, gg, gg), new Color(1f, 1f, 1f, 0.5f));
                BtcVisualRows.Txt(new Rect(vr - vwid * 2f - gg - fs * 0.6f, row.y, vwid, h), ValText(i, b), _val, new Color(1f, 1f, 1f, 0.5f));
                if (clickable && BtcSfx.Btn(row, "", BtcReplayUI.sBtn)) { Query = Tt(d.En, d.Ru); Page = PRules; _scroll = Vector2.zero; }
                y += h + 2f;
            }
            return idx.Count;
        }

        private void PageChanges(ref float y, float w, int fs, float[] shown, float[] bas, bool baseMine)
        {
            Summary(ref y, w, fs, shown, bas, baseMine);
            string q = Query.Trim().ToLowerInvariant();
            int n = DiffRows(ref y, w, fs, shown, bas, true, q);
            if (!baseMine)
            {
                List<string> rec = new List<string>();
                BtcRecipes.AppendDiffLines(rec);
                if (rec.Count > 0)
                {
                    y += fs * 0.3f;
                    BtcUi.Section(ref y, w, fs, Tt("Recipes (details on the old pages)", "Рецепты (подробно на старых страницах)"));
                    for (int i = 0; i < rec.Count; i++)
                    {
                        if (q.Length > 0 && !rec[i].ToLowerInvariant().Contains(q)) continue;
                        Rect row = new Rect(fs, y, w - fs * 2f, fs * 1.8f);
                        BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, 0.03f));
                        BtcReplayUI.Fill(new Rect(row.x, row.y, 3f, row.height), Critical);
                        BtcVisualRows.Txt(new Rect(row.x + fs * 0.8f, row.y, row.width - fs, row.height), rec[i], BtcVisualRows.Lbl, Color.white);
                        y += fs * 1.8f + 2f;
                        n++;
                    }
                }
            }
            if (n == 0) BtcUi.Note(ref y, w, fs, Tt("No differences.", "Отличий нет."));
        }

        // ------------------------------------------------------------------ presets page
        private void PagePresets(ref float y, float w, int fs, float[] shown, bool interact)
        {
            BtcUi.Section(ref y, w, fs, Tt("Presets", "Пресеты"));
            float pad = fs, h = fs * 2.2f;
            for (int p = 0; p < BtcGameplay.Presets.Count; p++)
            {
                BtcGameplay.Preset pr = BtcGameplay.Presets[p];
                float[] arr = BtcGameplay.Decode(pr.Data);
                int crit, minor;
                Count(arr, BtcGameplay.Vanilla, out crit, out minor);
                Rect row = new Rect(pad, y, w - pad * 2f, h);
                BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, 0.03f));
                string name = pr.Name == "Vanilla" ? Tt("Vanilla", "Ваниль") : (pr.Name == "Competitive" ? Tt("Competitive (my config)", "Соревновательный (мой конфиг)") : pr.Name);
                BtcVisualRows.Txt(new Rect(row.x + fs * 0.7f, row.y, row.width * 0.34f, h), name, BtcVisualRows.Lbl, Color.white);
                BtcVisualRows.Txt(new Rect(row.x + row.width * 0.34f, row.y, fs * 7f, h), crit + " " + Tt("crit.", "крит."), BtcVisualRows.Lbl, crit > 0 ? Critical : new Color(1f, 1f, 1f, 0.45f));
                BtcVisualRows.Txt(new Rect(row.x + row.width * 0.34f + fs * 5.5f, row.y, fs * 7f, h), minor + " " + Tt("minor", "незн."), BtcVisualRows.Lbl, minor > 0 ? Minor : new Color(1f, 1f, 1f, 0.45f));
                float bw = fs * 2.0f, bx = row.xMax - fs * 0.4f - bw;
                int pi = p;
                float del = 0f;
                if (!pr.BuiltIn)
                {
                    del = fs * 2.2f;
                    bool conf = _delConfirm == p && Time.unscaledTime < _delUntil;
                    Rect dr = new Rect(row.xMax - fs * 0.4f - del + 4f, row.y + h * 0.1f, del - 4f, h * 0.8f);
                    if (BtcReplayIcons.Plain(dr, BtcReplayIcons.Cross, conf ? Critical : Color.white, conf ? Tt("Click again to delete", "Ещё раз - удалить") : Tt("Delete this preset", "Удалить пресет")))
                    {
                        if (conf) { _delConfirm = -1; BtcGameplay.DeletePreset(p); BtcSfx.Delete(); y += h + 2f; return; }
                        _delConfirm = p; _delUntil = Time.unscaledTime + 3f;
                    }
                }
                Rect ar = new Rect(bx - del, row.y + h * 0.1f, bw, h * 0.8f);
                if (IcoBtn(ar, BtcReplayIcons.Check, false, BtcMenu.Accent, Tt("Apply this preset", "Применить пресет")) && interact) ApplyArr(arr, pr.Name, shown);
                Rect cr = new Rect(ar.x - bw - 4f, ar.y, bw, ar.height);
                if (IcoBtn(cr, BtcReplayIcons.Eye, _cmp == p, BtcMenu.Accent, Tt("Compare with the current rules", "Сравнить с текущими правилами"))) _cmp = _cmp == p ? -1 : pi;
                y += h + 2f;
            }
            if (!interact) BtcUi.Note(ref y, w, fs, Tt("Applying needs the host in a lobby (or offline); a client applies a preset to a request instead.", "Применять может хост в лобби (или офлайн); клиент применяет пресет к запросу."));

            if (_cmp >= 0 && _cmp < BtcGameplay.Presets.Count)
            {
                y += fs * 0.4f;
                BtcUi.Section(ref y, w, fs, Tt("Current rules vs preset: ", "Текущие правила и пресет: ") + BtcGameplay.Presets[_cmp].Name);
                float[] other = BtcGameplay.Decode(BtcGameplay.Presets[_cmp].Data);
                int n = DiffRows(ref y, w, fs, shown, other, false, "");
                if (n == 0) BtcUi.Note(ref y, w, fs, Tt("They are identical.", "Они одинаковые."));
            }

            y += fs * 0.5f;
            BtcUi.Section(ref y, w, fs, Tt("Save and share", "Сохранить и поделиться"));
            Rect fr = new Rect(pad, y + h * 0.1f, w - pad * 2f - fs * 3f, h * 0.8f);
            BtcReplayUI.Fill(fr, new Color(1f, 1f, 1f, 0.06f));
            BtcUi.Field(fr);
            _presetName = GUI.TextField(new Rect(fr.x + fs * 0.4f, fr.y, fr.width - fs * 0.8f, fr.height), _presetName, 40, _tf);
            if (string.IsNullOrEmpty(_presetName) && GUIUtility.keyboardControl == 0) GUI.Label(new Rect(fr.x + fs * 0.5f, fr.y, fr.width, fr.height), Tt("Name of a new preset...", "Название нового пресета..."), BtcVisualRows.Dim);
            Rect sb = new Rect(fr.xMax + 6f, fr.y, fs * 2.0f, fr.height);
            if (IcoBtn(sb, BtcReplayIcons.Plus, false, BtcMenu.Accent, Tt("Save the current rules as a preset", "Сохранить текущие правила как пресет")) && _presetName.Trim().Length > 0)
            {
                BtcGameplay.SavePreset(_presetName, shown);
                BtcSfx.Ok();
                BtcMenu.Toast(Tt("Preset saved: ", "Пресет сохранён: ") + _presetName.Trim(), BtcReplayIcons.Check);
                _presetName = "";
                GUIUtility.keyboardControl = 0;
            }
            y += h + 4f;
            if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Copy, Tt("Copy the current rules as a code", "Скопировать текущие правила кодом"), Tt("Paste it to a friend or Discord; they use 'Paste a code'", "Отправьте другу или в Discord; он нажмёт 'Вставить код'")))
            {
                GUIUtility.systemCopyBuffer = BtcGameplay.Encode(shown);
                BtcSfx.Ok();
                BtcMenu.Toast(Tt("Rules code copied", "Код правил скопирован"), BtcReplayIcons.Copy);
            }
            if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Share, Tt("Paste a code from the clipboard", "Вставить код из буфера"), Tt("Applies the rules of the code like a preset", "Применяет правила из кода как пресет")))
            {
                string code = (GUIUtility.systemCopyBuffer ?? "").Trim();
                if (code.Length == 0 || code.IndexOf('=') < 0) { BtcSfx.Warn(); BtcMenu.Toast(Tt("The clipboard has no rules code", "В буфере нет кода правил"), BtcReplayIcons.Cross); }
                else if (!interact) { BtcSfx.Warn(); BtcMenu.Toast(Tt("Rules cannot be changed now", "Сейчас правила менять нельзя"), BtcReplayIcons.Lock); }
                else ApplyArr(BtcGameplay.Decode(code), Tt("pasted code", "вставленный код"), shown);
            }
        }

        private void ApplyArr(float[] arr, string name, float[] shown)
        {
            float[] old = (float[])shown.Clone();
            BtcGameplay.SetAll(arr);
            BtcSfx.Ok();
            BtcMenu.Toast(Tt("Applied: ", "Применено: ") + name, BtcReplayIcons.Check);
            BtcUndo.Push(Scope, Tt("Preset: ", "Пресет: ") + name, delegate { BtcGameplay.SetAll(old); });
        }

        // ------------------------------------------------------------------ requests page
        private void PageRequests(ref float y, float w, int fs, bool host, bool session, bool draft)
        {
            float pad = fs, h = fs * 2.2f;
            if (host)
            {
                BtcUi.Section(ref y, w, fs, Tt("Requests from players", "Запросы игроков"));
                if (BtcGameplay.Pending.Count == 0) { BtcUi.Note(ref y, w, fs, Tt("Nobody asked for rule changes. Requests appear here and as a card on screen.", "Никто не просил изменить правила. Запросы появятся здесь и карточкой на экране.")); return; }
                if (!BtcGameplay.CanEdit) BtcUi.Note(ref y, w, fs, Tt("Rules are locked now: you can only reject. Accept in the lobby.", "Правила сейчас заблокированы: можно только отклонить. Принимать можно в лобби."));
                for (int p = BtcGameplay.Pending.Count - 1; p >= 0; p--)
                {
                    BtcGameplay.PendingReq r = BtcGameplay.Pending[p];
                    int crit, minor;
                    Count(r.Vals, BtcGameplay.Raw, out crit, out minor);
                    Rect row = new Rect(pad, y, w - pad * 2f, h);
                    BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, 0.04f));
                    BtcReplayUI.Fill(new Rect(row.x, row.y, 3f, h), crit > 0 ? Critical : Minor);
                    bool op = _open == r;
                    BtcReplayIcons.Glyph(op ? BtcReplayIcons.ChevD : BtcReplayIcons.ChevR, new Rect(row.x + fs * 0.5f, row.center.y - fs * 0.5f, fs * 1.0f, fs * 1.0f), BtcMenu.Accent);
                    BtcVisualRows.Txt(new Rect(row.x + fs * 2.0f, row.y, row.width * 0.3f, h), r.Name, BtcVisualRows.Lbl, Color.white);
                    BtcVisualRows.Txt(new Rect(row.x + row.width * 0.3f, row.y, fs * 14f, h), crit + " " + Tt("critical", "критично") + ", " + minor + " " + Tt("minor", "незн."), BtcVisualRows.Lbl, crit > 0 ? Critical : Minor);
                    float bw = fs * 2.0f;
                    Rect rej = new Rect(row.xMax - fs * 0.4f - bw, row.y + h * 0.1f, bw, h * 0.8f);
                    Rect acc = new Rect(rej.x - bw - 4f, rej.y, bw, rej.height);
                    bool done = false;
                    if (IcoBtn(acc, BtcReplayIcons.Check, false, BtcMenu.Accent, Tt("Accept the request", "Принять запрос")))
                    {
                        if (BtcGameplay.CanEdit)
                        {
                            float[] old = (float[])BtcGameplay.Raw.Clone();
                            BtcGameplay.Resolve(r, true);
                            BtcSfx.Ok();
                            BtcUndo.Push(Scope, Tt("Accepted request of ", "Принят запрос от ") + r.Name, delegate { BtcGameplay.SetAll(old); });
                        }
                        else { BtcSfx.Warn(); BtcMenu.Toast(Tt("Rules are locked now", "Правила сейчас заблокированы"), BtcReplayIcons.Lock); }
                        done = true;
                    }
                    else if (IcoBtn(rej, BtcReplayIcons.Cross, false, Critical, Tt("Reject the request", "Отклонить запрос"))) { BtcGameplay.Resolve(r, false); BtcSfx.Delete(); done = true; }
                    else if (BtcSfx.Btn(new Rect(row.x, row.y, acc.x - row.x, h), "", BtcReplayUI.sBtn)) _open = op ? null : r;
                    y += h + 2f;
                    if (done) return;
                    if (op) DiffRows(ref y, w, fs, r.Vals, BtcGameplay.Raw, false, "");
                }
                return;
            }
            BtcUi.Section(ref y, w, fs, Tt("Propose rules to the host", "Предложить правила хосту"));
            if (!session) { BtcUi.Note(ref y, w, fs, Tt("Join a lobby to propose rule changes to its host.", "Зайдите в лобби, чтобы предложить хосту изменить правила.")); return; }
            string t1 = draft ? Tt("Back to the host's rules", "Назад к правилам хоста") : Tt("Make a request", "Составить запрос");
            Rect b1 = new Rect(pad, y, fs * 2.2f, h);
            BtcVisualRows.Txt(new Rect(b1.xMax + fs * 0.6f, y, w - fs * 5f, h), t1, BtcVisualRows.Lbl, Color.white);
            if (IcoBtn(b1, BtcReplayIcons.Chat, draft, BtcMenu.Accent, t1))
            {
                BtcGameplayUI._draft = !BtcGameplayUI._draft;
                if (BtcGameplayUI._draft) { BtcGameplay.Proposal = (float[])BtcGameplay.Raw.Clone(); BtcRecipes.Ensure(); System.Array.Copy(BtcRecipes.Cur, BtcRecipes.Prop, BtcRecipes.Cur.Length); }
                BtcSfx.Switch(BtcGameplayUI._draft);
                if (BtcGameplayUI._draft) Page = PRules;
            }
            y += h + 4f;
            if (draft)
            {
                bool differs = BtcGameplay.Encode(BtcGameplay.Proposal) != BtcGameplay.Encode(BtcGameplay.Raw);
                Rect b2 = new Rect(pad, y, fs * 2.2f, h);
                BtcVisualRows.Txt(new Rect(b2.xMax + fs * 0.6f, y, w - fs * 5f, h), Tt("Send to the host", "Отправить хосту"), BtcVisualRows.Lbl, differs ? Color.white : new Color(1f, 1f, 1f, 0.5f));
                if (IcoBtn(b2, BtcReplayIcons.Export, differs, BtcMenu.Accent, Tt("Send the request to the host", "Отправить запрос хосту")) && differs) { BtcGameplay.SendRequest(BtcGameplay.Proposal); BtcSfx.Ok(); }
                y += h + 4f;
                BtcUi.Note(ref y, w, fs, Tt("Change values on the Rules page: they are only a draft until the host accepts. What you changed:", "Меняйте значения на странице Правила: это только черновик, пока хост не примет. Что вы изменили:"));
                if (DiffRows(ref y, w, fs, BtcGameplay.Proposal, BtcGameplay.Raw, false, "") == 0) BtcUi.Note(ref y, w, fs, Tt("Nothing yet.", "Пока ничего."));
            }
            string st = BtcGameplay.MyReq == BtcGameplay.ReqState.Waiting ? Tt("Your request is waiting for the host.", "Ваш запрос ждёт решения хоста.")
                : BtcGameplay.MyReq == BtcGameplay.ReqState.Approved ? Tt("The host accepted your request.", "Хост принял ваш запрос.")
                : BtcGameplay.MyReq == BtcGameplay.ReqState.Rejected ? Tt("The host rejected your request.", "Хост отклонил ваш запрос.") : null;
            if (st != null) BtcUi.Status(ref y, w, fs, st, BtcGameplay.MyReq == BtcGameplay.ReqState.Rejected ? Critical : (BtcGameplay.MyReq == BtcGameplay.ReqState.Approved ? BtcMenu.Accent : Minor));
        }

        // ------------------------------------------------------------------ old pages
        private static void OpenOld(int sub)
        {
            BtcGameplayUI._subTab = sub;
            UseLegacy = true;
        }

        // ------------------------------------------------------------------ in-game cards (called from BtcGameplayUI.OnGUI)
        private static int _lastReq;

        /// <summary>Opens the menu on this tab, page and (for clients) the right comparison base.</summary>
        internal static void OpenPage(int page)
        {
            UseLegacy = false;
            Page = page;
            ResourcePackManager.GoTab(4);
        }

        /// <summary>Host card "players propose rules". Call every OnGUI frame.</summary>
        internal static void DrawRequestCard()
        {
            if (!BtcGameplay.IsHost || !BtcGameplay.InSession) { _lastReq = 0; return; }
            int n = BtcGameplay.Pending.Count;
            if (n > _lastReq) BtcSfx.Warn();
            _lastReq = n;
            if (n == 0) return;
            List<string> names = new List<string>();
            for (int i = 0; i < n; i++) names.Add(BtcGameplay.Pending[i].Name);
            string who = string.Join(", ", names.ToArray());
            BtcHud.Card("rulereq", BtcReplayIcons.Group, BtcHud.Warn, Tt("Rule request: " + who, "Запрос правил: " + who),
                Tt(n + " player(s) ask you to change the gameplay rules. Open the Requests page to see what exactly and accept or reject.", "Игроков просит изменить правила: " + n + ". Откройте страницу Запросы, чтобы увидеть детали и принять или отклонить."),
                null, false, Tt("Review requests", "Посмотреть запросы"), delegate { OpenPage(PRequests); });
        }
    }
}
