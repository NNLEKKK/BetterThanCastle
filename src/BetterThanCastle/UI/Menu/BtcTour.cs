using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// First-run tour of the mod menu: four short steps that dim the screen around one thing at a time (the menu itself, the global search,
    /// Advanced Hotkeys, the UI editor) with Next / Back / Skip. Shown once when the menu is opened for the first time; "Show the tour again"
    /// in the UI editor restarts it. Progress is remembered (BtcMenuState), so closing the game in the middle resumes at the same step.
    /// </summary>
    internal static class BtcTour
    {
        private const int Steps = 5;
        private static GUIStyle _title, _text, _chip;
        private static Rect _skip, _back, _next;      // button rects of the last painted frame
        private static int _fs;

        internal static bool Done { get { return BtcMenuState.Bool("tour:done", false); } }
        internal static int Step { get { return Mathf.Clamp(BtcMenuState.Int("tour:step", 0), 0, Steps - 1); } set { BtcMenuState.SetInt("tour:step", value); } }

        internal static void Restart() { BtcMenuState.SetBool("tour:done", false); BtcMenuState.SetInt("tour:step", 0); }

        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }

        private static void Finish() { BtcMenuState.SetBool("tour:done", true); BtcSfx.Ok(); }

        private static void Styles(int fs)
        {
            BtcReplayUI.Ensure(fs);
            if (_title != null && _fs == fs) return;
            _fs = fs;
            GUIStyle b = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
            _title = new GUIStyle(b); _title.fontSize = Mathf.RoundToInt(fs * 1.15f); _title.fontStyle = FontStyle.Bold; _title.alignment = TextAnchor.MiddleLeft; _title.clipping = TextClipping.Clip;
            _chip = new GUIStyle(b); _chip.fontSize = Mathf.RoundToInt(fs * 0.9f); _chip.alignment = TextAnchor.MiddleCenter;
            _text = new GUIStyle(BtcReplayUI.sLabel); _text.fontSize = Mathf.RoundToInt(fs * 0.95f); _text.wordWrap = true; _text.alignment = TextAnchor.UpperLeft; _text.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
        }

        private static Rect Target(int step)
        {
            switch (step)
            {
                case 1: return BtcMenu.HeaderFree;
                case 2: return BtcMenu.TargetRects[3];
                case 3: return BtcMenu.TargetRects[12];
                case 4: return BtcMenu.TargetRects[10];
                default: return new Rect(0f, 0f, 0f, 0f);
            }
        }

        /// <summary>Clicks on the tour buttons. Must run BEFORE the menu content is drawn: the content swallows every MouseDown over the window, so buttons that are drawn last never see a click.</summary>
        internal static void Input()
        {
            if (Done) return;
            Event e = Event.current;
            if (e == null || e.button != 0) return;
            if (e.type != EventType.MouseDown && e.type != EventType.MouseUp) return;
            Vector2 p = e.mousePosition;
            int hit = _skip.Contains(p) ? 1 : (_back.width > 0f && _back.Contains(p) ? 2 : (_next.Contains(p) ? 3 : 0));
            if (hit == 0) return;
            if (e.type == EventType.MouseDown)
            {
                BtcSfx.Click();
                int step = Step;
                if (hit == 1) Finish();
                else if (hit == 2) Step = step - 1;
                else if (step == Steps - 1) Finish(); else Step = step + 1;
            }
            e.Use();
        }

        /// <summary>Call after the menu was drawn (the menu is open).</summary>
        internal static void Draw(int fs)
        {
            if (Done) return;
            Event e = Event.current;
            if (e == null) return;
            Styles(fs);
            int step = Step;
            Rect t = Target(step);
            bool has = t.width > 2f && t.height > 2f;
            GUI.depth = -1050;
            if (e.type == EventType.Repaint)
            {
                Color dim = new Color(0f, 0f, 0f, 0.62f);
                float W = Screen.width, H = Screen.height;
                if (has)
                {
                    Rect p = new Rect(t.x - fs * 0.4f, t.y - fs * 0.3f, t.width + fs * 0.8f, t.height + fs * 0.6f);
                    BtcReplayUI.Fill(new Rect(0f, 0f, W, p.y), dim);
                    BtcReplayUI.Fill(new Rect(0f, p.yMax, W, H - p.yMax), dim);
                    BtcReplayUI.Fill(new Rect(0f, p.y, p.x, p.height), dim);
                    BtcReplayUI.Fill(new Rect(p.xMax, p.y, W - p.xMax, p.height), dim);
                    Color a = BtcMenu.Accent;
                    BtcReplayUI.Fill(new Rect(p.x, p.y, p.width, 2f), a); BtcReplayUI.Fill(new Rect(p.x, p.yMax - 2f, p.width, 2f), a);
                    BtcReplayUI.Fill(new Rect(p.x, p.y, 2f, p.height), a); BtcReplayUI.Fill(new Rect(p.xMax - 2f, p.y, 2f, p.height), a);
                }
                else BtcReplayUI.Fill(new Rect(0f, 0f, W, H), dim);
            }

            string title, text;
            switch (step)
            {
                case 0:
                    title = Tt("Welcome to the BTC mod menu", "Добро пожаловать в мод-меню BTC");
                    text = Tt("This window opens with the green BTC button on the screen or with " + BtcHotkeys.ComboText("menu_toggle") + " in the game, the lobby and the main menu. Every tab remembers where you were.", "Это окно открывается зелёной кнопкой BTC на экране или клавишей " + BtcHotkeys.ComboText("menu_toggle") + " в игре, лобби и главном меню. Каждая вкладка помнит, где вы были.");
                    break;
                case 1:
                    title = Tt("Search everything", "Поиск по всему");
                    text = Tt("Type here (or press Ctrl+F) to find any tab, hotkey, visual setting or gameplay rule, in English or Russian, and jump straight to it.", "Пишите здесь (или нажмите Ctrl+F), чтобы найти любую вкладку, хоткей, настройку визуала или правило геймплея, по-английски или по-русски, и сразу перейти к ней.");
                    break;
                case 2:
                    title = Tt("Advanced Hotkeys", "Advanced Hotkeys");
                    text = Tt("Rebind any key of the mod and of the game, including the menu key. The keyboard map shows conflicts.", "Переназначайте любые клавиши мода и игры, включая клавишу меню. Карта клавиатуры показывает конфликты.");
                    break;
                case 3:
                    title = Tt("UI editor (" + BtcHotkeys.ComboText("ui_editor") + ")", "UI редактор (" + BtcHotkeys.ComboText("ui_editor") + ")");
                    text = Tt("Move and resize everything on the screen, switch icons to words, pick the accent colour, and save or share your whole setup as one code. " + BtcHotkeys.ComboText("ui_editor") + " opens it anywhere, even in a replay.", "Двигайте и меняйте размер всего на экране, переключайте иконки на слова, выбирайте цвет акцента и сохраняйте или передавайте весь свой сетап одним кодом. " + BtcHotkeys.ComboText("ui_editor") + " открывает его где угодно, даже в реплее.");
                    break;
                default:
                    title = Tt("What's new", "Что нового");
                    text = Tt("The changelog lists what every release changed, with links that jump to the exact tab or setting. A card also tells you when a new version comes out, even while you play.", "В ченджлоге перечислено, что изменил каждый релиз, со ссылками, которые переносят к нужной вкладке или настройке. А карточка сообщит о выходе новой версии, даже когда вы играете.");
                    break;
            }

            float w = Mathf.Min(fs * 26f, Screen.width - fs * 2f);
            float textH = _text.CalcHeight(new GUIContent(text), w - fs * 2f);
            float h = fs * 5.6f + textH;
            float x, y;
            if (has) { x = Mathf.Clamp(t.center.x - w * 0.3f, fs, Screen.width - w - fs); y = t.yMax + fs * 1.2f; if (y + h > Screen.height - fs) y = Mathf.Max(fs, t.y - h - fs); }
            else { x = (Screen.width - w) * 0.5f; y = (Screen.height - h) * 0.5f; }
            if (has && t.x < Screen.width * 0.25f) x = t.xMax + fs * 1.5f;   // a rail button: the card goes to its right
            Rect r = new Rect(x, y, w, h);
            if (has && t.x < Screen.width * 0.25f) r.y = Mathf.Clamp(t.y - fs, fs, Screen.height - h - fs);
            BtcReplayUI.Fill(r, BtcHud.Panel);
            BtcReplayUI.Fill(new Rect(r.x, r.y, r.width, 2f), BtcMenu.Accent);
            Color old = GUI.contentColor;
            GUI.contentColor = BtcMenu.Accent;
            GUI.Label(new Rect(r.x + fs, r.y + fs * 0.6f, r.width - fs * 6f, fs * 1.8f), title, _title);
            GUI.contentColor = new Color(1f, 1f, 1f, 0.4f);
            GUI.Label(new Rect(r.xMax - fs * 5f, r.y + fs * 0.6f, fs * 4f, fs * 1.8f), (step + 1) + " / " + Steps, _text);
            GUI.contentColor = old;
            GUI.Label(new Rect(r.x + fs, r.y + fs * 2.6f, r.width - fs * 2f, textH + 4f), text, _text);

            float by = r.yMax - fs * 2.5f, bh = fs * 1.9f;
            Rect skip = new Rect(r.x + fs, by, fs * 6f, bh);
            Rect next = new Rect(r.xMax - fs - fs * 7f, by, fs * 7f, bh);
            Rect back = new Rect(next.x - fs * 0.5f - fs * 5f, by, fs * 5f, bh);
            Chip(skip, Tt("Skip", "Пропустить"), new Color(1f, 1f, 1f, 0.7f), false);
            _skip = skip; _back = step > 0 ? back : new Rect(0f, 0f, 0f, 0f); _next = next;
            if (step > 0)
            {
                Chip(back, Tt("Back", "Назад"), new Color(1f, 1f, 1f, 0.85f), false);
            }
            Chip(next, step == Steps - 1 ? Tt("Done", "Готово") : Tt("Next", "Далее"), BtcMenu.Accent, true);
        }

        private static void Chip(Rect r, string label, Color c, bool strong)
        {
            bool hv = r.Contains(Event.current.mousePosition);
            BtcReplayUI.Fill(r, strong ? BtcMenu.AccentA(hv ? 0.32f : 0.2f) : new Color(1f, 1f, 1f, hv ? 0.14f : 0.07f));
            BtcVisualRows.Txt(r, label, _chip, c);
        }
    }
}
