using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Global search of the main mod menu: a field in the header (Ctrl+F focuses it) that looks through every tab, every hotkey, every visual
    /// setting and every gameplay rule. While it holds text the results replace the tab content; a click jumps to the exact tab / page / bind.
    /// The index is built once per language from the registries of the tabs (hotkey defs, visual settings, gameplay rules) plus a short list of
    /// curated entries for things that live inside tabs with their own draw code.
    /// </summary>
    internal static class BtcGlobalSearch
    {
        private sealed class Item
        {
            public string TitleEn, TitleRu, WhereEn, WhereRu, Hay;
            public string Title { get { return BtcLang.Russian ? TitleRu : TitleEn; } }
            public string Where { get { return BtcLang.Russian ? WhereRu : WhereEn; } }
            public int Icon, Kind;      // kind: 0 tab, 1 curated, 2 hotkey, 3 visual setting, 4 gameplay rule
            public Action Go;
        }

        private static string _q = "";
        private static readonly List<Item> Index = new List<Item>();
        private static bool _built;
        private static GUIStyle _tf, _lbl, _dim;
        private static int _fs;
        private static Vector2 _scroll;
        private static float _h = 100f;
        private static readonly List<Item> Hits = new List<Item>();
        private static string _hitsFor;

        internal static bool Active { get { return _q.Length > 0; } }
#if DEV
        internal static void DevSet(string q) { _q = q; _hitsFor = null; }
#endif
        internal static void Clear() { _q = ""; _hitsFor = null; if (GUI.GetNameOfFocusedControl() == "btc_gsearch") GUIUtility.keyboardControl = 0; }

        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }

        // ------------------------------------------------------------------ index
        private static void Add(int kind, int icon, string en, string ru, string whereEn, string whereRu, Action go, string extra)
        {
            if (string.IsNullOrEmpty(ru)) ru = en;
            if (string.IsNullOrEmpty(whereRu)) whereRu = whereEn;
            Index.Add(new Item { Kind = kind, Icon = icon, TitleEn = en, TitleRu = ru, WhereEn = whereEn, WhereRu = whereRu, Go = go, Hay = (en + " " + ru + " " + whereEn + " " + whereRu + " " + extra).ToLowerInvariant() });
        }

        private static Action Link(string target) { return delegate { BtcChangelog.Go(target); }; }

        // every entry knows both languages, so a search in English finds Russian UI and the other way round
        private static void Build()
        {
            _built = true;
            Index.Clear();
            int[] ids = { 0, 1, 2, 3, 4, 11, 5, 6, 7, 8, 9, 10, 12, 13 };
            int[] icons = { BtcReplayIcons.Blueprint, BtcReplayIcons.Eye, BtcReplayIcons.Wave, BtcReplayIcons.Keys, BtcReplayIcons.Gear, BtcReplayIcons.Chart, BtcReplayIcons.Share, BtcReplayIcons.Group, BtcReplayIcons.Play, BtcReplayIcons.Swords, BtcReplayIcons.Bug, BtcReplayIcons.News, BtcReplayIcons.Layout, BtcReplayIcons.Chat };
            for (int i = 0; i < ids.Length; i++)
            {
                int id = ids[i];
                Add(0, icons[i], BtcChangelog.TabNameEn[id], BtcChangelog.TabNameRu[id], "Tab", "Вкладка", Link("tab:" + id), "");
            }
            // curated: pages and features inside tabs (tab = where it lives)
            string G = "Gameplay", GR = "Геймплей", P = "Profile", PR = "Профиль", V = "Visuals", VR = "Визуал", R = "Replay", RR = "Реплеи", E = "UI editor", ER = "UI редактор", H = "Advanced Hotkeys", K = "Resource Packs", KR = "Ресурс-паки", S = "Statistics", SR = "Статистика", B = "BetterConnection", D = "Duel", DR = "Дуэль", SH = "Share", SHR = "Обмен";
            Add(1, BtcReplayIcons.Map, "Map symmetry and scale", "Симметрия и скейл карты", G, GR, delegate { OpenGameplay(5, null); }, "mirror axis map зеркало ось карта preset пресет карты");
            Add(1, BtcReplayIcons.Group, "Handicap", "Фора", G, GR, delegate { OpenGameplay(6, null); }, "income damage hp start workers доход урон здоровье рабочие");
            Add(1, BtcReplayIcons.Gear, "Recipes", "Рецепты", G, GR, delegate { OpenGameplay(4, null); }, "craft ingredients крафт ингредиенты");
            Add(1, BtcReplayIcons.Swords, "Rule presets", "Пресеты правил", G, GR, delegate { OpenGameplay(2, null); }, "competitive compare соревновательный сравнение");
            Add(1, BtcReplayIcons.Group, "Rule requests", "Запросы правил", G, GR, delegate { OpenGameplay(3, null); }, "propose host предложить хост");
            Add(1, BtcReplayIcons.Chart, "Changes against vanilla", "Отличия от ванили", G, GR, delegate { OpenGameplay(1, null); }, "differences changed отличия изменения");
            Add(1, BtcReplayIcons.Pause, "Game timer", "Игровой таймер", G, GR, delegate { OpenGameplay(7, null); }, "peace time advanced timer мирное время длина игры");
            Add(1, BtcReplayIcons.Palette, "Nick colours and gradients", "Цвета и градиенты ника", P, PR, Link("tab:6"), "nick name gradient colour letters ник имя градиент цвет буквы");
            Add(1, BtcReplayIcons.Play, "Nick animation", "Анимация ника", P, PR, Link("tab:6"), "animation animate анимация");
            Add(1, BtcReplayIcons.Image, "Avatar", "Аватар", P, PR, Link("tab:6"), "picture image картинка");
            Add(1, BtcReplayIcons.Plate, "Lobby plate editor", "Редактор плашки в лобби", P, PR, Link("tab:6"), "plate lobby preview плашка лобби превью");
            Add(1, BtcReplayIcons.Star, "Profiles (saved looks)", "Профили (сохранённый вид)", P, PR, Link("tab:6"), "save load share сохранить загрузить");
            Add(1, BtcReplayIcons.Ring, "Colored HP rings", "Цветные кольца HP", V, VR, Link("tab:1:0"), "health ring здоровье кольцо");
            Add(1, BtcReplayIcons.Map, "World overlays", "Оверлеи мира", V, VR, Link("tab:1:1"), "routes explosion block highlight маршруты взрыв подсветка блоков");
            Add(1, BtcReplayIcons.Plate, "Unit plates and HUD panels", "Плашки юнитов и панели HUD", V, VR, Link("tab:1:2"), "damage counter crystal timer chat счётчик урона таймер кристалла чат");
            Add(1, BtcReplayIcons.Chart, "Match statistics bar", "Полоса статистики матча", V, VR, Link("tab:1:3"), "bar panel полоса панель");
            Add(1, BtcReplayIcons.Monitor, "Screen: window mode, intro, brightness", "Экран: оконный режим, интро, яркость", V, VR, Link("tab:1:4"), "gamma fullscreen window skip intro гамма окно");
            Add(1, BtcReplayIcons.Star, "Visual profiles", "Профили визуала", V, VR, Link("tab:1:5"), "preset copy paste пресет копировать");
            Add(1, BtcReplayIcons.Chart, "APM / EPM graph", "График APM / EPM", R, RR, Link("tab:12"), "actions per minute apm epm graph действия в минуту график");
            Add(1, BtcReplayIcons.Layout, "Replay bar, keys and minimap layout", "Расположение полосы, клавиш и миникарты реплея", R, RR, Link("tab:12"), "timeline bar f7 таймлайн полоса");
            Add(1, BtcReplayIcons.Play, "Replay library", "Библиотека реплеев", R, RR, Link("tab:7"), "folders favourites sorting thumbnails папки избранное сортировка");
            Add(1, BtcReplayIcons.Gif, "GIF clips", "GIF-клипы", R, RR, Link("tab:7"), "clip gif клип");
            Add(1, BtcReplayIcons.Speaker, "UI sounds volume", "Громкость звуков интерфейса", "Menu header", "Шапка меню", Link("tab:0"), "sound volume speaker звук громкость динамик");
            Add(1, BtcReplayIcons.Globe, "Language", "Язык", "Menu header", "Шапка меню", Link("tab:0"), "english russian английский русский");
            Add(1, BtcReplayIcons.Palette, "Accent colour of the menu", "Цвет акцента меню", E, ER, Link("tab:12"), "theme colour green тема цвет");
            Add(1, BtcReplayIcons.Layout, "Menu and page buttons: icons or words", "Кнопки меню и страниц: иконки или слова", E, ER, Link("tab:12"), "rail icons text words иконки слова текст подвкладки");
            Add(1, BtcReplayIcons.Layout, "BTC button position", "Положение кнопки BTC", E, ER, Link("tab:12"), "launcher button кнопка");
            Add(1, BtcReplayIcons.Layout, "HUD elements: ping, plates, cards", "Элементы HUD: пинг, плашки, карточки", E, ER, Link("tab:12"), "ping pause plate cards notice пинг пауза плашка карточки");
            Add(1, BtcReplayIcons.Keys, "Menu key", "Клавиша меню", H, H, Link("tab:3:menu_toggle"), "pagedown open close открыть закрыть");
            Add(1, BtcReplayIcons.Keys, "Keyboard map and key finder", "Карта клавиатуры и поиск по клавише", H, H, Link("tab:3"), "conflict конфликт");
            Add(1, BtcReplayIcons.Blueprint, "Resource packs", "Ресурс-паки", K, KR, Link("tab:0"), "textures hats thumbnail текстуры шляпы миниатюра");
            Add(1, BtcReplayIcons.Wave, "BetterConnection: ping, heartbeat, reconnect", "BetterConnection: пинг, сердцебиение, переподключение", B, B, Link("tab:2"), "network multiplayer сеть мультиплеер");
            Add(1, BtcReplayIcons.Chart, "Statistics table and graph", "Таблица и график статистики", S, SR, Link("tab:11"), "players stats игроки показатели");
            Add(1, BtcReplayIcons.Swords, "Duel against a bot", "Дуэль против бота", D, DR, Link("tab:8"), "bot difficulty бот сложность");
            Add(1, BtcReplayIcons.Share, "Send maps to clients (.btcs)", "Отправка карт клиентам (.btcs)", SH, SHR, Link("tab:5"), "map thumbnail карта миниатюра");
            Add(1, BtcReplayIcons.Bug, "Report a bug", "Сообщить о баге", "Menu header", "Шапка меню", Link("tab:9"), "issue github ошибка");
            // hotkeys
            try
            {
                IList<BtcHotkeys.Def> defs = BtcHotkeys.Defs;
                for (int i = 0; i < defs.Count; i++)
                {
                    BtcHotkeys.Def d = defs[i];
                    string suffix = d.num > 0 ? " " + d.num : "";
                    Add(2, BtcReplayIcons.Keys, d.label + suffix, BtcLang.RuOf(d.label) + suffix, H + " > " + d.group, H + " > " + BtcLang.RuOf(d.group), Link("tab:3:" + d.id), d.id);
                }
            }
            catch (Exception e) { BtcLog.Error("GlobalSearch", "hotkeys: " + e.Message); }
            // visual settings
            try
            {
                ResourcePackManager.EnsureVisualBuilt();
                string[] pages = { "rings", "world", "plates", "stats", "screen" };
                string[] pn = { "Rings", "World overlays", "Plates and HUD", "Match statistics", "Screen" };
                string[] pnr = { "Кольца", "Оверлеи мира", "Плашки и HUD", "Статистика матча", "Экран" };
                for (int i = 0; i < BtcVisualData.All.Count; i++)
                {
                    BtcVisualSetting s = BtcVisualData.All[i];
                    int pi = Array.IndexOf(pages, s.Page);
                    if (pi < 0) continue;
                    Add(3, BtcReplayIcons.Eye, s.En, s.Ru, V + " > " + pn[pi], VR + " > " + pnr[pi], Link("tab:1:" + pi), s.Key);
                }
            }
            catch (Exception e) { BtcLog.Error("GlobalSearch", "visual: " + e.Message); }
            // gameplay rules
            try
            {
                for (int i = 0; i < BtcGameplay.Defs.Count; i++)
                {
                    BtcGameplay.Def d = BtcGameplay.Defs[i];
                    OpenGameplayQuery q = new OpenGameplayQuery { Text = d.En };
                    Add(4, BtcReplayIcons.Gear, d.En, d.Ru, G + " > Rules", GR + " > Правила", q.Open, d.Id + " " + d.Group + " " + d.HintEn + " " + d.HintRu);
                }
            }
            catch (Exception e) { BtcLog.Error("GlobalSearch", "gameplay: " + e.Message); }
        }

        private sealed class OpenGameplayQuery
        {
            public string Text;
            public void Open() { OpenGameplay(0, Text); }
        }

        private static void OpenGameplay(int page, string query)
        {
            if (!ResourcePackManager.GoTab(4)) return;
            BtcGameplayTab.Page = page;
            BtcGameplayTab.Query = query ?? "";
        }

        // ------------------------------------------------------------------ field in the header
        private static void Styles(int fs)
        {
            BtcVisualRows.Styles(fs);
            BtcReplayUI.Ensure(fs);
            if (_tf != null && _fs == fs) return;
            _fs = fs;
            GUIStyle b = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
            _tf = new GUIStyle(GUI.skin.textField); _tf.font = b.font; _tf.fontSize = Mathf.RoundToInt(fs * 0.9f); _tf.alignment = TextAnchor.MiddleLeft;
            _tf.normal.background = null; _tf.focused.background = null; _tf.hover.background = null; _tf.active.background = null;
            _tf.normal.textColor = Color.white; _tf.focused.textColor = Color.white; _tf.hover.textColor = Color.white; _tf.padding = new RectOffset(4, 4, 0, 0);
            _lbl = new GUIStyle(b); _lbl.fontSize = Mathf.RoundToInt(fs * 0.95f); _lbl.alignment = TextAnchor.MiddleLeft; _lbl.clipping = TextClipping.Clip; _lbl.normal.textColor = Color.white;
            _dim = new GUIStyle(_lbl); _dim.fontSize = Mathf.RoundToInt(fs * 0.8f); _dim.alignment = TextAnchor.MiddleRight;
        }

        /// <summary>Draws the search field in the free strip of the header (call after the header and the corner buttons).</summary>
        internal static void DrawField(int fs)
        {
            Rect r = BtcMenu.HeaderFree;
            if (r.width < fs * 6f) return;
            Styles(fs);
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.control && e.keyCode == KeyCode.F)
            {
                GUI.FocusControl("btc_gsearch");
                e.Use();
            }
            bool focused = GUI.GetNameOfFocusedControl() == "btc_gsearch";
            BtcReplayUI.Fill(r, new Color(1f, 1f, 1f, focused ? 0.1f : 0.06f));
            if (focused) BtcReplayUI.Fill(new Rect(r.x, r.yMax - 2f, r.width, 2f), BtcMenu.Accent);
            float ig = r.height * 0.55f;
            BtcReplayIcons.Glyph(BtcReplayIcons.Search, new Rect(r.x + fs * 0.5f, r.center.y - ig * 0.5f, ig, ig), new Color(1f, 1f, 1f, focused ? 0.8f : 0.5f));
            float tx = r.x + fs * 0.5f + ig + fs * 0.3f;
            Rect tf = new Rect(tx, r.y, r.xMax - tx - r.height, r.height);
            if (focused && e.type == EventType.KeyDown)
            {
                if (e.keyCode == BtcMenu.MenuKeyMain)
                {
                    GUIUtility.keyboardControl = 0;
                    BtcMenu.PgDownFrame = Time.frameCount;
                    ResourcePackManager.CloseMenu();
                    e.Use();
                    return;
                }
                if (e.keyCode == KeyCode.Escape) { _q = ""; GUIUtility.keyboardControl = 0; e.Use(); return; }
            }
            BtcUi.Field(r);
            GUI.SetNextControlName("btc_gsearch");
            string nq = GUI.TextField(tf, _q, 40, _tf);
            if (nq != _q) { _q = nq; _scroll = Vector2.zero; }
            if (_q.Length == 0 && !focused) BtcVisualRows.Txt(new Rect(tx + 4f, r.y, r.width, r.height), Tt("Search everywhere (Ctrl+F)", "Поиск везде (Ctrl+F)"), BtcVisualRows.Dim, new Color(1f, 1f, 1f, 0.35f));
            if (_q.Length > 0)
            {
                Rect x = new Rect(r.xMax - r.height, r.y, r.height, r.height);
                bool hv = x.Contains(e.mousePosition);
                BtcReplayIcons.Glyph(BtcReplayIcons.Cross, new Rect(x.x + x.width * 0.25f, x.y + x.height * 0.25f, x.width * 0.5f, x.height * 0.5f), new Color(1f, 1f, 1f, hv ? 0.9f : 0.5f));
                if (BtcSfx.Btn(x, "", BtcReplayUI.sBtn)) { _q = ""; GUIUtility.keyboardControl = 0; }
            }
        }

        // ------------------------------------------------------------------ results
        private static void Search()
        {
            if (_hitsFor == _q) return;
            _hitsFor = _q;
            if (!_built) Build();
            Hits.Clear();
            string[] tok = _q.Trim().ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tok.Length == 0) return;
            List<KeyValuePair<int, Item>> scored = new List<KeyValuePair<int, Item>>();
            for (int i = 0; i < Index.Count; i++)
            {
                Item it = Index[i];
                bool all = true;
                for (int t = 0; t < tok.Length && all; t++) if (it.Hay.IndexOf(tok[t], StringComparison.Ordinal) < 0) all = false;
                if (!all) continue;
                int score = it.Kind * 10;
                string title = it.Title.ToLowerInvariant();
                if (title.StartsWith(tok[0], StringComparison.Ordinal)) score -= 6;
                else if (title.Contains(tok[0])) score -= 3;
                scored.Add(new KeyValuePair<int, Item>(score, it));
            }
            scored.Sort(delegate (KeyValuePair<int, Item> a, KeyValuePair<int, Item> b) { return a.Key.CompareTo(b.Key); });
            for (int i = 0; i < scored.Count && i < 80; i++) Hits.Add(scored[i].Value);
        }

        private static string KindName(int k)
        {
            switch (k) { case 0: return Tt("Tabs", "Вкладки"); case 1: return Tt("Features", "Функции"); case 2: return Tt("Hotkeys", "Хоткеи"); case 3: return Tt("Visual settings", "Настройки визуала"); default: return Tt("Gameplay rules", "Правила геймплея"); }
        }

        /// <summary>Draws the results over the tab content area.</summary>
        internal static void DrawResults(Rect area, int fs)
        {
            Styles(fs);
            Search();
            Event e = Event.current;
            GUIStyle st = BtcVisualRows.Lbl;
            BtcVisualRows.Styles(fs);
            float vw = area.width - (_h > area.height ? 14f : 0f);
            _scroll = GUI.BeginScrollView(area, _scroll, new Rect(0f, 0f, vw, _h));
            float y = fs * 0.5f;
            if (Hits.Count == 0)
            {
                BtcVisualRows.Txt(new Rect(fs, y, vw - fs * 2f, fs * 2f), Tt("Nothing found. Try another word (English or Russian).", "Ничего не найдено. Попробуйте другое слово (по-английски или по-русски)."), BtcVisualRows.Dim, new Color(1f, 1f, 1f, 0.6f));
                y += fs * 2.5f;
            }
            int lastKind = -1;
            Item go = null;
            for (int i = 0; i < Hits.Count; i++)
            {
                Item it = Hits[i];
                if (it.Kind != lastKind)
                {
                    lastKind = it.Kind;
                    BtcUi.Section(ref y, vw, fs, KindName(it.Kind));
                }
                Rect r = new Rect(fs, y, vw - fs * 2f, fs * 2.1f);
                bool hover = r.Contains(e.mousePosition);
                BtcReplayUI.Fill(r, new Color(1f, 1f, 1f, hover ? 0.09f : 0.04f));
                float ig = fs * 1.3f;
                BtcReplayIcons.Glyph(it.Icon, new Rect(r.x + fs * 0.6f, r.center.y - ig * 0.5f, ig, ig), hover ? BtcMenu.Accent : new Color(1f, 1f, 1f, 0.6f));
                BtcVisualRows.Txt(new Rect(r.x + fs * 2.4f, r.y, r.width * 0.55f, r.height), it.Title, st, Color.white);
                BtcVisualRows.Txt(new Rect(r.x + r.width * 0.55f, r.y, r.width * 0.45f - fs * 0.6f, r.height), it.Where, _dim, new Color(1f, 1f, 1f, 0.55f));
                if (BtcSfx.Btn(r, "", BtcReplayUI.sBtn)) go = it;
                y += r.height + 2f;
            }
            _h = y + fs;
            GUI.EndScrollView();
            if (go != null)
            {
                _q = ""; _hitsFor = null; GUIUtility.keyboardControl = 0;
                try { go.Go(); } catch (Exception ex) { BtcLog.Error("GlobalSearch", "go: " + ex.Message); }
            }
        }
    }
}
