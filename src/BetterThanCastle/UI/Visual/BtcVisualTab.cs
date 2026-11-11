using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// "Visuals" tab (menu tab 1, formerly "Overlays / Colored HP"): everything that changes how the game looks. Pages on the left rail:
    /// Rings (colored HP rings, palettes, numbers, entity centre), World (overlays, routes, explosion map, block highlight), Plates and HUD
    /// (unit plates, HUD panels, damage counter, crystal timer, chat), Statistics (match statistics bar and panel), Screen (window, intro, menu,
    /// brightness) and Profiles (presets, own profiles, share as text). Simple mode shows switches and choices; Expert adds sliders, colours,
    /// palettes' fine tuning and shader overrides. Settings of the colored-HP config come from the registry (ResourcePackManager.Visual.cs), the other
    /// features keep their own draw code and are rendered through the callback rows of BtcVisualRows.
    /// </summary>
    internal sealed class BtcVisualTab : BtcMenuTab
    {
        internal const int PRings = 0, PWorld = 1, PPlates = 2, PStats = 3, PScreen = 4, PProf = 5, PAll = 6, PFav = 7;
        private static readonly BtcVisualTab Inst = new BtcVisualTab();
        internal static BtcVisualTab Instance { get { return Inst; } }

        internal override int Id { get { return 1; } }
        internal override bool IsActive { get { return ResourcePackManager.VisualActive; } }
        internal override string UndoScope { get { return BtcVisualRows.Scope; } }

        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }

        private int _page { get { return BtcMenuState.Int("visual:page", 0); } set { BtcMenuState.SetInt("visual:page", value); } }
        private string _q = "";
        private Vector2 _scroll;
        private float _h = 100f;
        private Rect _searchRect;
        private GUIStyle _tf, _ttl;
        private int _fs;
        private readonly List<string> _keys = new List<string>();
        private bool _hooked;
#if DEV
        private int _devLast = -1;
#endif

        private static readonly int[] Pages = { PFav, PAll, PRings, PWorld, PPlates, PStats, PScreen, PProf };

        private static int PageIcon(int p)
        {
            switch (p)
            {
                case PRings: return BtcReplayIcons.Ring;
                case PWorld: return BtcReplayIcons.Map;
                case PPlates: return BtcReplayIcons.Plate;
                case PStats: return BtcReplayIcons.Chart;
                case PScreen: return BtcReplayIcons.Monitor;
                case PAll: return BtcReplayIcons.Grid;
                case PFav: return BtcReplayIcons.Star;
                default: return BtcReplayIcons.Folder;
            }
        }

        private static string PageName(int p)
        {
            switch (p)
            {
                case PRings: return Tt("Rings", "Кольца");
                case PWorld: return Tt("World overlays", "Оверлеи мира");
                case PPlates: return Tt("Plates and HUD", "Плашки и HUD");
                case PStats: return Tt("Match statistics", "Статистика матча");
                case PScreen: return Tt("Screen", "Экран");
                case PAll: return Tt("All settings", "Все настройки");
                case PFav: return Tt("Favourites", "Избранное");
                default: return Tt("Profiles", "Профили");
            }
        }

        private static string PageKey(int p)
        {
            switch (p) { case PRings: return "rings"; case PWorld: return "world"; case PPlates: return "plates"; case PStats: return "stats"; case PScreen: return "screen"; default: return "prof"; }
        }

        internal bool Expert { get { return !BtcUi.IsOpen("visual:simple"); } }

        private void Styles(int fs)
        {
            BtcVisualRows.Styles(fs);
            if (_tf != null && _fs == fs) return;
            _fs = fs;
            GUIStyle b = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
            _ttl = new GUIStyle(BtcVisualRows.Lbl); _ttl.fontSize = Mathf.RoundToInt(fs * 1.15f); _ttl.fontStyle = FontStyle.Bold;
            _tf = new GUIStyle(GUI.skin.textField); _tf.font = b.font; _tf.fontSize = Mathf.RoundToInt(fs * 0.9f); _tf.alignment = TextAnchor.MiddleLeft;
            _tf.normal.background = null; _tf.focused.background = null; _tf.hover.background = null; _tf.active.background = null;
            _tf.normal.textColor = Color.white; _tf.focused.textColor = Color.white; _tf.hover.textColor = Color.white; _tf.padding = new RectOffset(4, 4, 0, 0);
        }

        internal void SetPage(int p) { _page = p; _scroll = Vector2.zero; BtcVisualColor.Close(); }

        // ------------------------------------------------------------------ draw
        internal override void Draw(Rect area, int fs)
        {
            Styles(fs);
            ResourcePackManager.EnsureVisualBuilt();
            if (!_hooked) { _hooked = true; BtcVisualRows.GoPage = SetPage; }
            BtcVisualRows.ExpireForced();
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
            float railW = BtcMenu.SubRailWidth(fs);
            Rect rail = new Rect(area.x, area.y, railW, area.height);
            DrawRail(rail, fs);
            Rect content = new Rect(area.x + railW, area.y, area.width - railW, area.height);
            BtcReplayUI.Fill(new Rect(content.x - 1f, content.y, 1f, content.height), BtcMenu.Line);

            if (!BtcVisualData.Ready())
            {
                BtcVisualRows.Txt(new Rect(content.x + fs, content.y + fs, content.width - fs * 2f, fs * 2f), Tt("Visual settings load when a game or the main menu is ready...", "Настройки загружаются вместе с игрой..."), BtcVisualRows.Dim, new Color(1f, 1f, 1f, 0.6f));
                BtcReplayIcons.DrawTip(fs, area.xMax, false);
                return;
            }
#if DEV
            int devKey = _page * 2 + (Expert ? 1 : 0);
            if (devKey != _devLast) { _devLast = devKey; BtcLog.Info("VisualDev", "drew page " + _page + " expert=" + Expert + " h=" + _h); }
#endif
            BtcVisualRows.Expert = Expert;
            if (_page == PProf) BtcVisualProfiles.Draw(content, fs);
            else DrawPage(content, fs);
            BtcReplayIcons.DrawTip(fs, area.xMax, false);
        }

        private void DrawRail(Rect r, int fs)
        {
            Event e = Event.current;
            float item = r.width * 0.82f, x = r.x + (r.width - item) * 0.5f, y = r.y + fs * 0.6f, itemW = item;
            BtcMenu.RailGeometry(r, fs, ref item, ref itemW, ref x);
            for (int i = 0; i < Pages.Length; i++)
            {
                int p = Pages[i];
                if (p == PRings || p == PProf) { BtcReplayUI.Fill(new Rect(x + itemW * 0.15f, y, itemW * 0.7f, 1f), BtcMenu.Line); y += fs * 0.5f; }
                Rect ir = new Rect(x, y, itemW, item);
                bool on = p == _page, hover = ir.Contains(e.mousePosition);
                if (on) { BtcReplayUI.Fill(ir, new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.14f)); BtcReplayUI.Fill(new Rect(r.x, y + item * 0.15f, 3f, item * 0.7f), BtcMenu.Accent); }
                else if (hover) BtcReplayUI.Fill(ir, new Color(1f, 1f, 1f, 0.07f));
                float g = item * 0.55f;
                BtcMenu.RailGlyph(ir, fs, PageIcon(p), PageName(p), on, hover);
                BtcReplayIcons.Hint(ir, PageName(p));
                if (BtcSfx.Btn(ir, "", BtcReplayUI.sBtn)) { SetPage(p); }
                y += item + fs * 0.3f;
            }
        }

        private bool Matches(string text) { return _q.Length == 0 || text.IndexOf(_q, StringComparison.OrdinalIgnoreCase) >= 0; }

        private void DrawPage(Rect content, int fs)
        {
            float pad = fs, ib = fs * 1.9f;
            float y0 = content.y + fs * 0.4f;
            BtcVisualRows.Txt(new Rect(content.x + pad, y0, content.width * 0.4f, ib), PageName(_page), _ttl, Color.white);

            // right: master switch (rings) + Simple / Expert
            float x = content.xMax - pad - ib;
            bool exp = Expert;
            if (BtcReplayIcons.Button(new Rect(x, y0, ib, ib), BtcReplayIcons.Gear, exp, Tt("Expert: every slider, colour and sub-option", "Эксперт: все ползунки, цвета и подопции"))) { BtcUi.SetOpen("visual:simple", false); }
            x -= ib + 3f;
            if (BtcReplayIcons.Button(new Rect(x, y0, ib, ib), BtcReplayIcons.Eye, !exp, Tt("Simple: only the main switches", "Просто: только главные переключатели"))) { BtcUi.SetOpen("visual:simple", true); }
            x -= ib + fs;
            {
                // ONE master switch for every visual (colored HP rings + world overlays), in the header of every page (rule: docs/menu.md)
                bool hpOn = BtcVisualData.GetMaster(), ovOn = BtcOverlays.Master;
                bool master = hpOn || ovOn;
                Rect sw = new Rect(x - fs * 2.6f, y0 + (ib - fs * 1.2f) * 0.5f, fs * 2.4f, fs * 1.2f);
                BtcReplayUI.Fill(sw, master ? new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.9f) : new Color(1f, 1f, 1f, 0.16f));
                float kk = sw.height - 4f;
                BtcReplayUI.Fill(new Rect(master ? sw.xMax - kk - 2f : sw.x + 2f, sw.y + 2f, kk, kk), master ? new Color(0.03f, 0.05f, 0.04f, 1f) : new Color(1f, 1f, 1f, 0.75f));
                BtcReplayIcons.Hint(sw, master ? Tt("All visuals are ON (click to turn colored rings and world overlays off)", "Все визуалы включены (клик - выключить цветные кольца и оверлеи мира)") : Tt("All visuals are OFF (vanilla look). Click to turn them back on", "Все визуалы выключены (ванильный вид). Клик - включить обратно"));
                if (BtcSfx.Btn(sw, "", BtcReplayUI.sBtn))
                {
                    BtcSfx.Switch(!master);
                    if (master) { _prevHp = hpOn; _prevOv = ovOn; BtcVisualData.SetMaster(false); BtcOverlays.Master = false; }
                    else
                    {
                        bool nh = _prevHp, no = _prevOv;
                        if (!nh && !no) { nh = true; no = true; }
                        BtcVisualData.SetMaster(nh); BtcOverlays.Master = no;
                    }
                    BtcUndo.Push(BtcVisualRows.Scope, Tt("All visuals", "Все визуалы"), delegate { BtcVisualData.SetMaster(hpOn); BtcOverlays.Master = ovOn; });
                }
            }

            // search + bulk
            float y = y0 + ib + fs * 0.5f;
            Rect sr = new Rect(content.x + pad, y, Mathf.Min(content.width * 0.45f, fs * 24f), ib);
            BtcReplayUI.Fill(sr, new Color(1f, 1f, 1f, 0.06f));
            BtcReplayIcons.Glyph(BtcReplayIcons.Search, new Rect(sr.x + fs * 0.4f, sr.y + ib * 0.2f, ib * 0.6f, ib * 0.6f), new Color(1f, 1f, 1f, 0.5f));
            BtcUi.Field(sr);
            GUI.SetNextControlName("btc_vis_search");
            _q = GUI.TextField(new Rect(sr.x + ib * 0.95f, sr.y, sr.width - ib * 0.95f, ib), _q, 40, _tf);
            if (_q.Length == 0 && GUIUtility.keyboardControl == 0) BtcVisualRows.Txt(new Rect(sr.x + ib * 0.95f + 4f, sr.y, sr.width, ib), Tt("Search a setting", "Поиск настройки"), BtcVisualRows.Dim, new Color(1f, 1f, 1f, 0.35f));
            float fx = sr.xMax + fs * 0.6f;
            bool allOpen = true;
            for (int i = 0; i < _keys.Count; i++) if (!BtcUi.IsOpen(_keys[i])) { allOpen = false; break; }
            if (BtcReplayIcons.Plain(new Rect(fx, y, ib, ib), allOpen ? BtcReplayIcons.ChevD : BtcReplayIcons.ChevR, Color.white, allOpen ? Tt("Collapse all sections", "Свернуть все разделы") : Tt("Expand all sections", "Развернуть все разделы")))
                BtcUi.SetOpen(_keys, !allOpen);
            fx += ib + 3f;
            if (_page == PRings || _page == PWorld || _page == PPlates || _page == PAll)
            {
                bool armed = BtcVisualData.ResetArmed();
                if (BtcReplayIcons.Plain(new Rect(fx, y, ib, ib), BtcReplayIcons.Refresh, armed ? new Color(1f, 0.45f, 0.45f, 1f) : Color.white, armed ? Tt("Click again: reset all colored-HP settings to defaults", "Ещё раз: сбросить все настройки цветных HP") : Tt("Reset colored-HP settings to defaults", "Сбросить настройки цветных HP на умолчания")))
                {
                    if (armed)
                    {
                        string before = BtcVisualProfiles.Capture();
                        BtcSfx.Delete(); BtcVisualData.Reset();
                        BtcUndo.Push(BtcVisualRows.Scope, Tt("Defaults restored", "Сброс на умолчания"), delegate { BtcVisualProfiles.Apply(before); });
                        BtcMenu.Toast(Tt("Defaults restored", "Значения по умолчанию восстановлены"), -1);
                    }
                    else BtcVisualData.ArmReset();
                }
            }
            y += ib + fs * 0.6f;
            BtcReplayUI.Fill(new Rect(content.x + pad, y - fs * 0.25f, content.width - pad * 2f, 1f), BtcMenu.Line);

            Rect view = new Rect(content.x, y, content.width, content.yMax - y);
            float vw = view.width - (_h > view.height ? 14f : 0f);
            _scroll = GUI.BeginScrollView(view, _scroll, new Rect(0f, 0f, vw, _h));
            float cy = fs * 0.2f;
            _keys.Clear();
            BtcVisualRows.PageId = _page;
            if (_q.Length > 0) DrawSearch(ref cy, vw, fs);
            else
            {
                switch (_page)
                {
                    case PAll: _flat = true; PageRings(ref cy, vw, fs); PageWorld(ref cy, vw, fs); PagePlates(ref cy, vw, fs); PageStats(ref cy, vw, fs); PageScreen(ref cy, vw, fs); _flat = false; break;
                    case PFav: PageFav(ref cy, vw, fs); break;
                    case PRings: PageRings(ref cy, vw, fs); break;
                    case PWorld: PageWorld(ref cy, vw, fs); break;
                    case PPlates: PagePlates(ref cy, vw, fs); break;
                    case PStats: PageStats(ref cy, vw, fs); break;
                    default: PageScreen(ref cy, vw, fs); break;
                }
            }
            GUI.EndScrollView();
            _h = cy + fs * 3f;
        }

        // ------------------------------------------------------------------ search
        private void DrawSearch(ref float y, float w, int fs)
        {
            int n = 0;
            string lastSec = null;
            bool oldE = BtcVisualRows.Expert;
            BtcVisualRows.Expert = true;
            for (int i = 0; i < BtcVisualData.All.Count; i++)
            {
                BtcVisualSetting s = BtcVisualData.All[i];
                if (!Matches(s.Label) && !Matches(s.Key)) continue;
                string sec = s.Page + "/" + s.Section;
                if (sec != lastSec)
                {
                    lastSec = sec;
                    BtcVisualRows.Txt(new Rect(fs, y, w - fs * 2f, fs * 1.6f), PageNameOf(s.Page) + "  >  " + SectionName(s.Page, s.Section), BtcVisualRows.Dim, new Color(1f, 1f, 1f, 0.5f));
                    y += fs * 1.7f;
                }
                BtcVisualRows.IgnoreVis = true;
                BtcVisualRows.Setting(ref y, w, fs, s);
                BtcVisualRows.IgnoreVis = false;
                n++;
            }
            BtcVisualRows.Expert = oldE;
            if (n == 0) BtcUi.Note(ref y, w, fs, Tt("Nothing found. Search covers the colored-HP settings; overlays, plates and statistics are on their pages.", "Ничего не найдено. Поиск идёт по настройкам цветных HP; оверлеи, плашки и статистика лежат на своих страницах."));
            else BtcUi.Note(ref y, w, fs, Tt("Search covers the colored-HP settings; overlays, plates and statistics are on their pages.", "Поиск идёт по настройкам цветных HP; оверлеи, плашки и статистика лежат на своих страницах."));
        }

        private static string PageNameOf(string page)
        {
            switch (page) { case "rings": return PageName(PRings); case "world": return PageName(PWorld); case "plates": return PageName(PPlates); default: return PageName(PScreen); }
        }

        private static string SectionName(string page, string sec)
        {
            switch (sec)
            {
                case "what": return Tt("What is colored", "Что красим");
                case "pal": return Tt("Palettes", "Палитры");
                case "vis": return Tt("Visibility", "Видимость");
                case "ammo": return Tt("Bomb counter", "Счётчик бомб");
                case "num": return Tt("HP / stamina numbers", "Числа HP / стамины");
                case "center": return Tt("Entity centre", "Центр сущности");
                case "adv": return Tt("Advanced", "Дополнительно");
                case "path": return Tt("Routes", "Маршруты");
                case "bomb": return Tt("Explosion map", "Карта взрывов");
                case "dmg": return Tt("Damage counter", "Счётчик урона");
                case "crystal": return Tt("Crystal timer", "Таймер кристалла");
                case "gametimer": return Tt("Game timer", "Игровой таймер");
                case "shield": return Tt("Base shields (barriers)", "Защита базы (барьеры)");
                case "menu": return Tt("Menu behaviour", "Поведение меню");
                default: return Tt("Brightness", "Яркость");
            }
        }

        // ------------------------------------------------------------------ sections
        private static int IconOfSection(string sec)
        {
            switch (sec)
            {
                case "what": return BtcReplayIcons.Ring;
                case "pal": return BtcReplayIcons.Palette;
                case "vis": return BtcReplayIcons.Eye;
                case "ammo": return BtcReplayIcons.Bomb;
                case "num": return BtcReplayIcons.Num;
                case "center": return BtcReplayIcons.Target;
                case "adv": return BtcReplayIcons.Gear;
                case "path": return BtcReplayIcons.PathI;
                case "bomb": return BtcReplayIcons.Bomb;
                case "dmg": return BtcReplayIcons.Swords;
                case "crystal": return BtcReplayIcons.Diamond;
                case "shield": return BtcReplayIcons.Shield;
                case "menu": return BtcReplayIcons.Lock;
                default: return BtcReplayIcons.Sun;
            }
        }

        private bool Sec(ref float y, float w, int fs, string page, string sec)
        {
            string key = "vis:" + page + ":" + sec;
            _keys.Add(key);
            int on = 0, total = 0;
            for (int i = 0; i < BtcVisualData.All.Count; i++)
            {
                BtcVisualSetting s = BtcVisualData.All[i];
                if (s.Page != page || s.Section != sec || s.Kind != VKind.Bool) continue;
                total++;
                if (s.GetB()) on++;
            }
            return FoldF(ref y, w, fs, key, IconOfSection(sec), SectionName(page, sec), total > 0 ? on + "/" + total : "");
        }

        private void Settings(ref float y, float w, int fs, string page, string sec)
        {
            bool any = false;
            for (int i = 0; i < BtcVisualData.All.Count; i++)
            {
                BtcVisualSetting s = BtcVisualData.All[i];
                if (s.Page == page && s.Section == sec) any |= BtcVisualRows.Setting(ref y, w, fs, s);
            }
            if (!any && !BtcVisualRows.Expert && !_flat) BtcUi.Note(ref y, w, fs, Tt("More options in Expert mode (gear icon above).", "Больше опций в режиме «Эксперт» (значок шестерёнки сверху)."));
            y += fs * 0.5f;
        }

        // ------------------------------------------------------------------ flat mode ("All" page), favourites
        private bool _flat;
        private bool _prevHp = true, _prevOv = true;

        /// <summary>Fold header, or nothing in the flat "All" page (the content is always shown, without section headers).</summary>
        private bool FoldF(ref float y, float w, int fs, string key, int icon, string text, string right)
        {
            if (_flat) { y += fs * 0.3f; return true; }
            return BtcVisualRows.Fold(ref y, w, fs, key, icon, text, right);
        }

        private static bool SectionInfo(string key, out int page, out string name, out int icon)
        {
            page = PRings; name = key; icon = BtcReplayIcons.Grid;
            string[] p = key.Split(':');
            if (p.Length < 3) return false;
            switch (p[1])
            {
                case "rings": page = PRings; break;
                case "world": page = PWorld; break;
                case "plates": page = PPlates; break;
                case "stats": page = PStats; break;
                default: page = PScreen; break;
            }
            string sec = p[2];
            if (sec.StartsWith("ov") && sec.Length == 3) { int i = sec[2] - '0'; name = Tt(BtcOverlays.Names[i], OverlayRu[i]); icon = OverlayIcon[i]; }
            else if (sec == "units") { name = Tt("Unit plates and HUD panels", "Плашки юнитов и панели HUD"); icon = BtcReplayIcons.Plate; }
            else if (sec == "chat") { name = Tt("Chat", "Чат"); icon = BtcReplayIcons.Chat; }
            else if (sec == "all") { name = Tt("Match statistics", "Статистика матча"); icon = BtcReplayIcons.Chart; }
            else if (sec == "display") { name = Tt("Window", "Окно"); icon = BtcReplayIcons.Monitor; }
            else { name = SectionName(p[1], sec); icon = IconOfSection(sec); }
            return true;
        }

        private void PageFav(ref float y, float w, int fs)
        {
            IList<string> favs = BtcVisualRows.Favs;
            if (favs.Count == 0)
            {
                BtcUi.Note(ref y, w, fs, Tt("Nothing here yet. Click the star next to any setting, palette or section and it appears on this page.", "Здесь пока пусто. Нажмите звёздочку рядом с любой настройкой, палитрой или разделом, и она появится на этой странице."));
                return;
            }
            bool oldE = BtcVisualRows.Expert;
            BtcVisualRows.Expert = true;
            List<string> list = new List<string>(favs);
            for (int i = 0; i < list.Count; i++)
            {
                string k = list[i];
                if (k.StartsWith("vis:"))
                {
                    int page, icon; string name;
                    if (!SectionInfo(k, out page, out name, out icon)) continue;
                    FavLink(ref y, w, fs, k, page, name, icon);
                }
                else if (k.StartsWith("pal:"))
                {
                    for (int j = 0; j < BtcVisualData.Palettes.Count; j++)
                        if ("pal:" + BtcVisualData.Palettes[j].Key == k) { BtcVisualPalette p = BtcVisualData.Palettes[j]; BtcVisualRows.IgnoreVis = true; DrawPalette(ref y, w, fs, p); BtcVisualRows.IgnoreVis = false; }
                }
                else
                {
                    BtcVisualSetting s = BtcVisualData.Find(k);
                    if (s == null) continue;
                    BtcVisualRows.IgnoreVis = true;
                    BtcVisualRows.Setting(ref y, w, fs, s);
                    BtcVisualRows.IgnoreVis = false;
                }
            }
            BtcVisualRows.Expert = oldE;
        }

        private void FavLink(ref float y, float w, int fs, string key, int page, string name, int icon)
        {
            Event e = Event.current;
            float pad = fs, h = fs * 2.1f;
            Rect row = new Rect(pad, y, w - pad * 2f - fs * 2.2f, h);
            bool hover = row.Contains(e.mousePosition);
            BtcReplayUI.Fill(row, new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, hover ? 0.16f : 0.08f));
            float ig = fs * 1.3f;
            BtcReplayIcons.Glyph(icon, new Rect(row.x + fs * 0.6f, row.center.y - ig * 0.5f, ig, ig), BtcMenu.Accent);
            BtcVisualRows.Txt(new Rect(row.x + fs * 2.6f, row.y, row.width * 0.6f, h), name, BtcVisualRows.Lbl, Color.white);
            GUIStyle rs = new GUIStyle(BtcVisualRows.Dim); rs.alignment = TextAnchor.MiddleRight;
            BtcVisualRows.Txt(new Rect(row.x, row.y, row.width - fs * 2f, h), PageName(page), rs, new Color(1f, 1f, 1f, 0.5f));
            BtcReplayIcons.Glyph(BtcReplayIcons.ChevR, new Rect(row.xMax - fs * 1.6f, row.center.y - fs * 0.5f, fs * 1.4f, fs), hover ? Color.white : BtcMenu.Accent);
            BtcVisualRows.Star(new Rect(w - fs * 2.6f, y + (h - fs * 1.5f) * 0.5f, fs * 1.5f, fs * 1.5f), key, name);
            y += h + 3f;
            BtcReplayIcons.Hint(row, Tt("Open this section", "Открыть этот раздел"));
            if (BtcSfx.Btn(row, "", BtcReplayUI.sBtn)) { BtcUi.SetOpen(key, true); SetPage(page); }
        }

        // ------------------------------------------------------------------ pages
        private void PageRings(ref float y, float w, int fs)
        {
            if (Sec(ref y, w, fs, "rings", "what")) Settings(ref y, w, fs, "rings", "what");
            if (Sec(ref y, w, fs, "rings", "pal")) { DrawPalettes(ref y, w, fs); y += fs * 0.5f; }
            if (Sec(ref y, w, fs, "rings", "vis")) Settings(ref y, w, fs, "rings", "vis");
            if (Sec(ref y, w, fs, "rings", "ammo")) Settings(ref y, w, fs, "rings", "ammo");
            if (Sec(ref y, w, fs, "rings", "num")) Settings(ref y, w, fs, "rings", "num");
            if (Sec(ref y, w, fs, "rings", "center")) Settings(ref y, w, fs, "rings", "center");
            if (BtcVisualRows.Expert && Sec(ref y, w, fs, "rings", "adv")) { Settings(ref y, w, fs, "rings", "adv"); DrawOverrides(ref y, w, fs); }
        }

        private static readonly string[] OverlayRu = { "Радиус пассивки мага", "Траектория бомбы", "Таймер пути", "Траектория смэша", "Подсветка блока" };
        private static readonly int[] OverlayIcon = { BtcReplayIcons.Ring, BtcReplayIcons.Bomb, BtcReplayIcons.Slow, BtcReplayIcons.Swords, BtcReplayIcons.Grid };

        private void PageWorld(ref float y, float w, int fs)
        {
            for (int i = 0; i < 5; i++)
            {
                string key = "vis:world:ov" + i;
                _keys.Add(key);
                if (!FoldF(ref y, w, fs, key, OverlayIcon[i], Tt(BtcOverlays.Names[i], OverlayRu[i]), "")) continue;
                BtcVisualRows.Prefix = "ov" + i;
                float yy = y;
                if (i == 4)
                {
                    BtcBlockMarks.DrawSettings(
                        (l, on) => BtcVisualRows.Check(ref yy, w, fs, l, on),
                        (l, v) => BtcVisualRows.Step(ref yy, w, fs, l + ": " + v),
                        (string l, int id, float v, float mn, float mx, float st, string fmt, string suf) => BtcVisualRows.SliderCb(ref yy, w, fs, l, id, v, mn, mx, st, fmt, suf),
                        (string l, int id) => BtcVisualRows.ColorCb(ref yy, w, fs, l, id));
                }
                else
                {
                    int idx = i;
                    BtcOverlays.DrawOne(idx, (l, on) => BtcVisualRows.Check(ref yy, w, fs, l, on), (l, v) => BtcVisualRows.Step(ref yy, w, fs, l + ": " + v));
                }
                y = yy + fs * 0.5f;
            }
            if (Sec(ref y, w, fs, "world", "path")) Settings(ref y, w, fs, "world", "path");
            if (Sec(ref y, w, fs, "world", "bomb")) Settings(ref y, w, fs, "world", "bomb");
            if (Sec(ref y, w, fs, "world", "shield")) Settings(ref y, w, fs, "world", "shield");
        }

        private void PagePlates(ref float y, float w, int fs)
        {
            string key = "vis:plates:units";
            _keys.Add(key);
            if (FoldF(ref y, w, fs, key, BtcReplayIcons.Plate, Tt("Unit plates and HUD panels", "Плашки юнитов и панели HUD"), ""))
            {
                string[] tabs = BtcInterface.TabNames;
                string[] names = new string[tabs.Length];
                for (int i = 0; i < tabs.Length; i++) names[i] = BtcLang.Tr(tabs[i]);
                int c = BtcVisualRows.Choice(ref y, w, fs, 0, Tt("Panel group", "Группа панелей"), BtcInterface.Tab, names, null, null);
                if (c >= 0) BtcInterface.Tab = c;
                BtcVisualRows.Prefix = "pl" + BtcInterface.Tab;
                float yy = y;
                if (BtcInterface.Tab == 0)
                    BtcUnitPlates.DrawSettings((string l, int id, float v, float mn, float mx, float st, string fmt, string suf) => BtcVisualRows.SliderCb(ref yy, w, fs, l, id, v, mn, mx, st, fmt, suf));
                else
                {
                    bool any = BtcInterface.DrawTab((string l, int id, float v, float mn, float mx, float st, string fmt, string suf) => BtcVisualRows.SliderCb(ref yy, w, fs, l, id, v, mn, mx, st, fmt, suf), BtcInterface.Tab);
                    if (!any) BtcUi.Note(ref yy, w, fs, Tt("No panels here (HUD panels appear in a match)", "Здесь нет панелей (панели HUD появляются в матче)"));
                }
                if (!BtcVisualRows.Expert) BtcUi.Note(ref yy, w, fs, Tt("Sizes and positions of the plates are in Expert mode.", "Размеры и положение плашек - в режиме «Эксперт»."));
                y = yy + fs * 0.5f;
            }
            if (Sec(ref y, w, fs, "plates", "dmg")) Settings(ref y, w, fs, "plates", "dmg");
            if (Sec(ref y, w, fs, "plates", "crystal")) Settings(ref y, w, fs, "plates", "crystal");
            if (Sec(ref y, w, fs, "plates", "gametimer")) Settings(ref y, w, fs, "plates", "gametimer");
            string ck = "vis:plates:chat";
            _keys.Add(ck);
            if (FoldF(ref y, w, fs, ck, BtcReplayIcons.Chat, Tt("Chat", "Чат"), ""))
            {
                BtcVisualRows.Prefix = "chat";
                float yy = y;
                BtcGameChatFade.DrawSettings((l, on) => BtcVisualRows.Check(ref yy, w, fs, l, on), (string l, int id, float v, float mn, float mx, float st, string fmt, string suf) => BtcVisualRows.SliderCb(ref yy, w, fs, l, id, v, mn, mx, st, fmt, suf));
                y = yy + fs * 0.5f;
            }
        }

        private void PageStats(ref float y, float w, int fs)
        {
            string key = "vis:stats:all";
            _keys.Add(key);
            if (!FoldF(ref y, w, fs, key, BtcReplayIcons.Chart, Tt("Match statistics", "Статистика матча"), "")) return;
            BtcVisualRows.Prefix = "ms";
            float yy = y;
            BtcMatchStatsUI.DrawSettings((l, on) => BtcVisualRows.Check(ref yy, w, fs, l, on), (l, v) => BtcVisualRows.Step(ref yy, w, fs, l + ": " + v),
                (string l, int id, float v, float mn, float mx, float st, string fmt, string suf) => BtcVisualRows.SliderCb(ref yy, w, fs, l, id, v, mn, mx, st, fmt, suf));
            if (!BtcVisualRows.Expert) BtcUi.Note(ref yy, w, fs, Tt("Scale, opacity and offsets of the bar and panel are in Expert mode.", "Масштаб, прозрачность и отступы бара и панели - в режиме «Эксперт»."));
            y = yy + fs * 0.5f;
        }

        private void PageScreen(ref float y, float w, int fs)
        {
            string key = "vis:screen:display";
            _keys.Add(key);
            if (FoldF(ref y, w, fs, key, BtcReplayIcons.Monitor, Tt("Window", "Окно"), ""))
            {
                int cur = BtcDisplay.Mode - 1;
                string[] tips = { Tt("Fullscreen", "Полный экран"), Tt("Windowed", "Окно"), Tt("Borderless", "Без рамки") };
                int c = BtcVisualRows.Choice(ref y, w, fs, 0, Tt("Window mode", "Режим окна"), cur, null, new[] { BtcReplayIcons.Monitor, BtcReplayIcons.Layout, BtcReplayIcons.Grid }, tips);
                if (c >= 0 && c != cur)
                {
                    int oldMode = BtcDisplay.Mode;
                    BtcDisplay.Apply(c + 1, 1);
                    BtcUndo.Push(BtcVisualRows.Scope, Tt("Window mode", "Режим окна"), delegate { BtcDisplay.Apply(oldMode, 1); });
                }
                bool skip = BtcConfig.SkipIntro != null && BtcConfig.SkipIntro.Value;
                if (BtcVisualRows.Switch(ref y, w, fs, 0, Tt("Skip intro (logo screens on startup)", "Пропускать интро (заставки при запуске)"), skip, null) && BtcConfig.SkipIntro != null)
                {
                    BtcConfig.SkipIntro.Value = !skip;
                    BtcUndo.Push(BtcVisualRows.Scope, Tt("Skip intro", "Пропуск интро"), delegate { BtcConfig.SkipIntro.Value = skip; });
                }
                bool dis = BtcVisualData.GetDiscord();
                if (BtcVisualRows.Switch(ref y, w, fs, 0, "Discord Rich Presence", dis, null))
                {
                    BtcVisualData.SetDiscord(!dis);
                    BtcUndo.Push(BtcVisualRows.Scope, "Discord Rich Presence", delegate { BtcVisualData.SetDiscord(dis); });
                }
                y += fs * 0.5f;
            }
            if (Sec(ref y, w, fs, "screen", "menu")) Settings(ref y, w, fs, "screen", "menu");
            if (Sec(ref y, w, fs, "screen", "light")) Settings(ref y, w, fs, "screen", "light");
        }

        // ------------------------------------------------------------------ shader overrides (Expert)
        private void DrawOverrides(ref float y, float w, int fs)
        {
            string[] heads = { Tt("Own overrides (ring shader properties)", "Свои переопределения (свойства шейдера колец)"), Tt("Enemy overrides (only if enemies are colored)", "Переопределения врагов (если враги раскрашены)") };
            for (int t = 0; t < 2; t++)
            {
                BtcVisualRows.Txt(new Rect(fs, y, w - fs * 2f, fs * 1.6f), heads[t], BtcVisualRows.Dim, new Color(1f, 1f, 1f, 0.6f));
                y += fs * 1.8f;
                for (int i = 0; i < BtcVisualData.OvNames.Length; i++)
                {
                    int team = t, idx = i;
                    bool on = BtcVisualData.OvOn(team, idx);
                    if (BtcVisualRows.Switch(ref y, w, fs, 0, BtcVisualData.OvNames[i], on, null))
                    {
                        object snap = on ? OvSnap(team, idx) : null;
                        BtcVisualData.OvToggle(team, idx);
                        BtcUndo.Push(BtcVisualRows.Scope, BtcVisualData.OvNames[i], delegate
                        {
                            BtcVisualData.OvToggle(team, idx);
                            if (snap != null) OvRestore(team, idx, snap);
                        });
                    }
                    if (!BtcVisualData.OvOn(team, idx)) continue;
                    if (BtcVisualData.OvIsColor(idx))
                    {
                        Color old = BtcVisualData.OvC(team, idx);
                        BtcVisualColor.Row(ref y, w, fs, "ov" + team + idx, BtcVisualData.OvNames[i], 1, old, c => BtcVisualData.OvSetC(team, idx, c), null);
                    }
                    else
                    {
                        bool mult = BtcVisualData.OvMult(team, idx);
                        float v = BtcVisualData.OvF(team, idx);
                        float nv = BtcVisualRows.Slider(ref y, w, fs, 1, "ovf" + team + idx, mult ? Tt("Multiply by", "Умножить на") : Tt("Set to", "Задать"), v, mult ? 0.1f : 0f, mult ? 4f : Mathf.Max(1f, v * 2f), mult ? 0.05f : 0.01f, "0.00", "");
                        if (!Mathf.Approximately(nv, v))
                        {
                            BtcVisualData.OvSetF(team, idx, nv);
                            BtcUndo.Push(BtcVisualRows.Scope, BtcVisualData.OvNames[i], delegate { BtcVisualData.OvSetF(team, idx, v); }, "ovf" + team + idx);
                        }
                    }
                }
                y += fs * 0.4f;
            }
        }

        private static object OvSnap(int team, int idx) { return BtcVisualData.OvIsColor(idx) ? (object)BtcVisualData.OvC(team, idx) : (object)BtcVisualData.OvF(team, idx); }

        private static void OvRestore(int team, int idx, object s)
        {
            if (s is Color) BtcVisualData.OvSetC(team, idx, (Color)s); else BtcVisualData.OvSetF(team, idx, (float)s);
        }

        // ------------------------------------------------------------------ palettes
        private string _palOpen;
        private int _selStop = -1;
        private int _dragStop = -1;
        private object _dragSnap;

        private void DrawPalettes(ref float y, float w, int fs)
        {
            for (int i = 0; i < BtcVisualData.Palettes.Count; i++)
            {
                BtcVisualPalette p = BtcVisualData.Palettes[i];
                if (p.Key == "pal.bo")
                {
                    BtcVisualSetting bld = BtcVisualData.Find("pal.bld");
                    if (bld != null) BtcVisualRows.Setting(ref y, w, fs, bld);
                }
                DrawPalette(ref y, w, fs, p);
            }
        }

        internal static Color PalAt(BtcVisualPalette p, float pct)
        {
            int best = -1, low = 0; float bp = -1f, lp = 1e9f;
            int n = p.Count();
            for (int i = 0; i < n; i++)
            {
                float pc = p.Pct(i);
                if (pc <= pct && pc > bp) { bp = pc; best = i; }
                if (pc < lp) { lp = pc; low = i; }
            }
            return p.Col(best >= 0 ? best : low);
        }

        private static void Strip(Rect r, BtcVisualPalette p)
        {
            int n = p.Count();
            int[] ord = Order(p);
            for (int k = 0; k < n; k++)
            {
                float a = k == 0 ? 0f : p.Pct(ord[k]), b = k == n - 1 ? 100f : p.Pct(ord[k + 1]);
                if (b <= a) continue;
                Color c = p.Col(ord[k]); c.a = 1f;
                BtcReplayUI.Fill(new Rect(r.x + r.width * a / 100f, r.y, Mathf.Max(1f, r.width * (b - a) / 100f), r.height), c);
            }
        }

        private static int[] Order(BtcVisualPalette p)
        {
            int n = p.Count();
            int[] o = new int[n];
            for (int i = 0; i < n; i++) o[i] = i;
            Array.Sort(o, (a, b) => p.Pct(a).CompareTo(p.Pct(b)));
            return o;
        }

        private void DrawPalette(ref float y, float w, int fs, BtcVisualPalette p)
        {
            if (p.Vis != null && !p.Vis()) return;
            Event e = Event.current;
            float pad = fs;
            bool open = _palOpen == p.Key;
            Rect hdr = new Rect(pad, y, w - pad * 2f, fs * 2.4f);
            bool hover = hdr.Contains(e.mousePosition);
            BtcReplayUI.Fill(hdr, new Color(1f, 1f, 1f, hover || open ? 0.07f : 0.03f));
            float g = fs;
            BtcReplayIcons.Glyph(open ? BtcReplayIcons.ChevD : BtcReplayIcons.ChevR, new Rect(hdr.x + fs * 0.5f, hdr.center.y - g * 0.5f, g * 1.4f, g), open || hover ? Color.white : BtcMenu.Accent);
            BtcVisualRows.Txt(new Rect(hdr.x + fs * 2.4f, hdr.y, hdr.width * 0.5f, hdr.height), p.Label, BtcVisualRows.Lbl, Color.white);
            Rect strip = new Rect(hdr.xMax - Mathf.Min(hdr.width * 0.42f, fs * 22f) - fs * 0.6f, hdr.y + hdr.height * 0.25f, Mathf.Min(hdr.width * 0.42f, fs * 22f), hdr.height * 0.5f);
            Strip(strip, p);
            BtcReplayIcons.Hint(hdr, Tt("Click to edit the colour bands", "Клик - редактировать цветовые полосы"));
            y += hdr.height + 3f;
            BtcVisualRows.Star(new Rect(hdr.xMax - fs * 1.9f, hdr.center.y - fs * 0.75f, fs * 1.5f, fs * 1.5f), "pal:" + p.Key, p.Label);
            if (BtcSfx.Btn(hdr, "", BtcReplayUI.sBtn))
            {
                _palOpen = open ? null : p.Key; _selStop = -1; BtcVisualColor.Close(); BtcSfx.Switch(!open);
            }
            if (!open) return;
            EditPalette(ref y, w, fs, p);
        }

        private void EditPalette(ref float y, float w, int fs, BtcVisualPalette p)
        {
            Event e = Event.current;
            float pad = fs;
            float ib = fs * 1.9f;
            float x0 = pad + fs * 1.2f;
            Rect panel = new Rect(pad, y, w - pad * 2f, 0f);
            float ph = fs * 12.2f;
            BtcReplayUI.Fill(new Rect(pad, y, w - pad * 2f, ph), new Color(0f, 0f, 0f, 0.22f));
            BtcReplayUI.Fill(new Rect(pad, y, 2f, ph), BtcMenu.Accent);
            Rect bar = new Rect(x0, y + fs * 1.0f, w - x0 - pad - ib - fs * 1.6f, fs * 2.6f);
            int n = p.Count();
            int[] ord = Order(p);
            Strip(bar, p);
            BtcReplayUI.Fill(new Rect(bar.x, bar.yMax, bar.width, 1f), new Color(1f, 1f, 1f, 0.15f));
            for (int t = 0; t <= 4; t++)
            {
                Rect tr = new Rect(bar.x + bar.width * t / 4f - fs * 1.5f, bar.yMax + 2f, fs * 3f, fs * 1.3f);
                GUIStyle c = new GUIStyle(BtcVisualRows.Dim); c.alignment = TextAnchor.UpperCenter;
                BtcVisualRows.Txt(tr, (t * 25) + "%", c, new Color(1f, 1f, 1f, 0.4f));
            }
            // handles first (they get the click before the segments)
            if (e.rawType == EventType.MouseUp) _dragStop = -1;
            for (int k = 0; k < n; k++)
            {
                int si = ord[k];
                float hx = bar.x + bar.width * p.Pct(si) / 100f;
                Rect hr = new Rect(hx - fs * 0.5f, bar.y - fs * 0.3f, fs, bar.height + fs * 0.6f);
                bool hov = hr.Contains(e.mousePosition) || _dragStop == si;
                BtcReplayUI.Fill(new Rect(hx - 1.5f, bar.y - fs * 0.3f, 3f, bar.height + fs * 0.6f), hov ? Color.white : new Color(1f, 1f, 1f, 0.75f));
                BtcReplayUI.Fill(new Rect(hx - fs * 0.35f, bar.y - fs * 0.45f, fs * 0.7f, fs * 0.4f), hov ? Color.white : new Color(1f, 1f, 1f, 0.75f));
                if (e.type == EventType.MouseDown && e.button == 0 && hr.Contains(e.mousePosition))
                {
                    _dragStop = si; _dragSnap = p.Snap(); SelectStop(p, si); e.Use();
                }
            }
            if (_dragStop >= 0 && (e.type == EventType.MouseDrag || e.type == EventType.MouseDown) && e.type != EventType.Used)
            {
                float v = Mathf.Clamp(Mathf.Round((e.mousePosition.x - bar.x) / bar.width * 100f), 0f, 100f);
                if (!Mathf.Approximately(v, p.Pct(_dragStop)))
                {
                    object snap = _dragSnap;
                    p.SetPct(_dragStop, v);
                    BtcUndo.Push(BtcVisualRows.Scope, p.Label, delegate { p.Restore(snap); }, "palpct:" + p.Key);
                }
                e.Use();
            }
            for (int k = 0; k < n; k++)
            {
                int si = ord[k];
                float a = k == 0 ? 0f : p.Pct(si), b = k == n - 1 ? 100f : p.Pct(ord[k + 1]);
                Rect seg = new Rect(bar.x + bar.width * a / 100f + fs * 0.5f, bar.y, Mathf.Max(2f, bar.width * (b - a) / 100f - fs), bar.height);
                if (si == _selStop) BtcReplayUI.Fill(new Rect(seg.x, bar.yMax - 3f, seg.width, 3f), Color.white);
                if (seg.width > 4f && BtcSfx.Btn(seg, "", BtcReplayUI.sBtn)) SelectStop(p, _selStop == si ? -1 : si);
            }
            // add stop
            Rect add = new Rect(bar.xMax + fs * 0.8f, bar.y + (bar.height - ib) * 0.5f, ib, ib);
            if (BtcReplayIcons.Plain(add, BtcReplayIcons.Plus, BtcMenu.Accent, Tt("Add a colour band", "Добавить цветовую полосу")))
            {
                object snap = p.Snap();
                p.Add();
                BtcUndo.Push(BtcVisualRows.Scope, p.Label, delegate { p.Restore(snap); });
                SelectStop(p, p.Count() - 1);
            }
            // ring preview
            float ry = bar.yMax + fs * 2.0f, rs = fs * 3.2f;
            float[] samples = { 100f, 75f, 50f, 25f, 10f };
            float sec = p.Second != null ? p.Second() : 0.75f;
            for (int s = 0; s < samples.Length; s++)
            {
                Rect rr = new Rect(x0 + s * (rs + fs * 2.2f), ry, rs, rs);
                Color mc = PalAt(p, samples[s]); mc.a = 1f;
                BtcReplayIcons.Glyph(BtcReplayIcons.Ring, rr, mc);
                float k2 = rs * 0.62f;
                BtcReplayIcons.Glyph(BtcReplayIcons.Ring, new Rect(rr.center.x - k2 * 0.5f, rr.center.y - k2 * 0.5f, k2, k2), new Color(mc.r * sec, mc.g * sec, mc.b * sec, 1f));
                GUIStyle c = new GUIStyle(BtcVisualRows.Dim); c.alignment = TextAnchor.UpperCenter;
                BtcVisualRows.Txt(new Rect(rr.x - fs, rr.yMax - fs * 0.2f, rr.width + fs * 2f, fs * 1.3f), samples[s] + "%", c, new Color(1f, 1f, 1f, 0.5f));
            }
            BtcReplayIcons.Hint(new Rect(x0, ry, samples.Length * (rs + fs * 2.2f), rs), Tt("How a ring looks at 100 / 75 / 50 / 25 / 10 %", "Как выглядит кольцо при 100 / 75 / 50 / 25 / 10 %"));
            y += ph + 3f;
            // selected stop
            if (_selStop >= 0 && _selStop < n)
            {
                int si = _selStop;
                float pv = p.Pct(si);
                float nv = BtcVisualRows.Slider(ref y, w, fs, 1, "palsel:" + p.Key, Tt("Band starts at HP", "Полоса начинается с HP"), pv, 0f, 100f, 1f, "0", "%");
                if (!Mathf.Approximately(nv, pv))
                {
                    object snap = p.Snap();
                    p.SetPct(si, nv);
                    BtcUndo.Push(BtcVisualRows.Scope, p.Label, delegate { p.Restore(snap); }, "palpct2:" + p.Key);
                }
                if (n > 1)
                {
                    Rect del = new Rect(w - pad - fs * 0.5f - ib, y - fs * 2.1f - 3f + (fs * 2.1f - ib) * 0.5f, ib, ib);
                    if (BtcReplayIcons.Plain(del, BtcReplayIcons.Trash, new Color(1f, 0.55f, 0.55f, 1f), Tt("Remove this colour band", "Удалить эту цветовую полосу")))
                    {
                        object snap = p.Snap();
                        BtcVisualColor.Close(); _selStop = -1;
                        p.Remove(si);
                        BtcSfx.Delete();
                        BtcUndo.Push(BtcVisualRows.Scope, p.Label, delegate { p.Restore(snap); });
                        return;
                    }
                }
                string ckey = "pal:" + p.Key + ":" + si;
                Color cur = p.Col(si);
                BtcVisualColor.Row(ref y, w, fs, ckey, Tt("Band colour", "Цвет полосы"), 1, cur, c => { Color o = p.Col(si); c.a = o.a; p.SetCol(si, c); }, null);
            }
            if (p.Second != null && BtcVisualRows.Expert)
            {
                float sv = p.Second();
                float nv = BtcVisualRows.Slider(ref y, w, fs, 1, "palsec:" + p.Key, Tt("2nd colour brightness", "Яркость 2-го цвета"), sv, 0f, 1.5f, 0.05f, "0.00", "");
                if (!Mathf.Approximately(nv, sv))
                {
                    object snap = p.Snap();
                    p.SetSecond(nv);
                    BtcUndo.Push(BtcVisualRows.Scope, p.Label, delegate { p.Restore(snap); }, "palsec:" + p.Key);
                }
            }
            y += fs * 0.4f;
        }

        private void SelectStop(BtcVisualPalette p, int si)
        {
            _selStop = si;
            if (si >= 0) BtcVisualColor.Open("pal:" + p.Key + ":" + si, p.Col(si)); else BtcVisualColor.Close();
        }
    }
}
