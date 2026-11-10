using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Keyboard map page of the Hotkeys tab: every key is lit by what is bound to it (mod binds, vanilla binds, conflicts), a modifier filter shows
    /// the layer of Shift / Ctrl / Alt combos, a click selects a key and lists its binds, clicking a bind jumps to it in the list.
    /// </summary>
    internal static class BtcHotkeysKeyboard
    {
        private sealed class Use { public string key, title; public bool vanilla; public int mods; public string combo; }

        private struct Kc
        {
            public string label; public KeyCode code; public float x, y, w;
            public Kc(string l, KeyCode c, float x, float y, float w) { label = l; code = c; this.x = x; this.y = y; this.w = w; }
        }

        private static List<Kc> _layout;
        private static readonly Dictionary<KeyCode, List<Use>> Map = new Dictionary<KeyCode, List<Use>>();
        private static int _mode; // 0 all, 1 none, 2 shift, 3 ctrl, 4 alt
        private static KeyCode _sel = KeyCode.None;
        private static int _ver = -1, _verMode = -1;
        private static float _at;

        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }

        private static void Build()
        {
            _layout = new List<Kc>();
            float y = 0f;
            _layout.Add(new Kc("Esc", KeyCode.Escape, 0, y, 1));
            KeyCode[] f = { KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4, KeyCode.F5, KeyCode.F6, KeyCode.F7, KeyCode.F8, KeyCode.F9, KeyCode.F10, KeyCode.F11, KeyCode.F12 };
            for (int i = 0; i < 12; i++) _layout.Add(new Kc("F" + (i + 1), f[i], 2f + i + (i / 4) * 0.5f, y, 1));
            y = 1.4f;
            string row1 = "1234567890";
            _layout.Add(new Kc("`", KeyCode.BackQuote, 0, y, 1));
            for (int i = 0; i < 10; i++) _layout.Add(new Kc(row1[i].ToString(), KeyCode.Alpha0 + (row1[i] - '0'), 1 + i, y, 1));
            _layout.Add(new Kc("-", KeyCode.Minus, 11, y, 1)); _layout.Add(new Kc("=", KeyCode.Equals, 12, y, 1)); _layout.Add(new Kc("Bksp", KeyCode.Backspace, 13, y, 2));
            y = 2.4f;
            _layout.Add(new Kc("Tab", KeyCode.Tab, 0, y, 1.5f));
            string row2 = "QWERTYUIOP";
            for (int i = 0; i < row2.Length; i++) _layout.Add(new Kc(row2[i].ToString(), (KeyCode)Enum.Parse(typeof(KeyCode), row2[i].ToString()), 1.5f + i, y, 1));
            _layout.Add(new Kc("[", KeyCode.LeftBracket, 11.5f, y, 1)); _layout.Add(new Kc("]", KeyCode.RightBracket, 12.5f, y, 1)); _layout.Add(new Kc("\\", KeyCode.Backslash, 13.5f, y, 1.5f));
            y = 3.4f;
            _layout.Add(new Kc("Caps", KeyCode.CapsLock, 0, y, 1.75f));
            string row3 = "ASDFGHJKL";
            for (int i = 0; i < row3.Length; i++) _layout.Add(new Kc(row3[i].ToString(), (KeyCode)Enum.Parse(typeof(KeyCode), row3[i].ToString()), 1.75f + i, y, 1));
            _layout.Add(new Kc(";", KeyCode.Semicolon, 10.75f, y, 1)); _layout.Add(new Kc("'", KeyCode.Quote, 11.75f, y, 1)); _layout.Add(new Kc("Enter", KeyCode.Return, 12.75f, y, 2.25f));
            y = 4.4f;
            _layout.Add(new Kc("Shift", KeyCode.LeftShift, 0, y, 2.25f));
            string row4 = "ZXCVBNM";
            for (int i = 0; i < row4.Length; i++) _layout.Add(new Kc(row4[i].ToString(), (KeyCode)Enum.Parse(typeof(KeyCode), row4[i].ToString()), 2.25f + i, y, 1));
            _layout.Add(new Kc(",", KeyCode.Comma, 9.25f, y, 1)); _layout.Add(new Kc(".", KeyCode.Period, 10.25f, y, 1)); _layout.Add(new Kc("/", KeyCode.Slash, 11.25f, y, 1)); _layout.Add(new Kc("RShift", KeyCode.RightShift, 12.25f, y, 2.75f));
            y = 5.4f;
            _layout.Add(new Kc("Ctrl", KeyCode.LeftControl, 0, y, 1.25f)); _layout.Add(new Kc("Alt", KeyCode.LeftAlt, 2.5f, y, 1.25f));
            _layout.Add(new Kc("Space", KeyCode.Space, 3.75f, y, 6.25f)); _layout.Add(new Kc("RAlt", KeyCode.RightAlt, 10f, y, 1.25f)); _layout.Add(new Kc("RCtrl", KeyCode.RightControl, 13.75f, y, 1.25f));
            // navigation cluster
            float nx = 15.5f;
            _layout.Add(new Kc("Ins", KeyCode.Insert, nx, 1.4f, 1)); _layout.Add(new Kc("Home", KeyCode.Home, nx + 1, 1.4f, 1)); _layout.Add(new Kc("PgUp", KeyCode.PageUp, nx + 2, 1.4f, 1));
            _layout.Add(new Kc("Del", KeyCode.Delete, nx, 2.4f, 1)); _layout.Add(new Kc("End", KeyCode.End, nx + 1, 2.4f, 1)); _layout.Add(new Kc("PgDn", KeyCode.PageDown, nx + 2, 2.4f, 1));
            _layout.Add(new Kc("Up", KeyCode.UpArrow, nx + 1, 4.4f, 1));
            _layout.Add(new Kc("Lt", KeyCode.LeftArrow, nx, 5.4f, 1)); _layout.Add(new Kc("Dn", KeyCode.DownArrow, nx + 1, 5.4f, 1)); _layout.Add(new Kc("Rt", KeyCode.RightArrow, nx + 2, 5.4f, 1));
            // numpad
            float px = 19f;
            _layout.Add(new Kc("/", KeyCode.KeypadDivide, px + 1, 1.4f, 1)); _layout.Add(new Kc("*", KeyCode.KeypadMultiply, px + 2, 1.4f, 1)); _layout.Add(new Kc("-", KeyCode.KeypadMinus, px + 3, 1.4f, 1));
            int[][] pad = { new[] { 7, 8, 9 }, new[] { 4, 5, 6 }, new[] { 1, 2, 3 } };
            for (int r = 0; r < 3; r++) for (int c = 0; c < 3; c++) _layout.Add(new Kc(pad[r][c].ToString(), KeyCode.Keypad0 + pad[r][c], px + 1 + c, 2.4f + r, 1));
            _layout.Add(new Kc("+", KeyCode.KeypadPlus, px + 4 - 1 + 0f, 2.4f, 1));
            _layout.Add(new Kc("0", KeyCode.Keypad0, px + 1, 5.4f, 1)); _layout.Add(new Kc(".", KeyCode.KeypadPeriod, px + 2, 5.4f, 1)); _layout.Add(new Kc("Ent", KeyCode.KeypadEnter, px + 3, 5.4f, 1));
            // numpad plus sits right of the 3x3 block
            for (int i = 0; i < _layout.Count; i++) if (_layout[i].code == KeyCode.KeypadPlus) _layout[i] = new Kc("+", KeyCode.KeypadPlus, px + 4, 2.4f, 1);
        }

        private static int ModFlag(KeyCode k)
        {
            if (k == KeyCode.LeftShift || k == KeyCode.RightShift) return 1;
            if (k == KeyCode.LeftControl || k == KeyCode.RightControl) return 2;
            if (k == KeyCode.LeftAlt || k == KeyCode.RightAlt) return 4;
            return 0;
        }

        private static void Add(KeyCode[] c, Use u)
        {
            if (c == null || c.Length == 0) return;
            int mods = 0;
            for (int i = 0; i < c.Length - 1; i++) mods |= ModFlag(c[i]);
            u.mods = mods; u.combo = BtcHotkeys.ComboLabel(c, false);
            KeyCode last = c[c.Length - 1];
            List<Use> l;
            if (!Map.TryGetValue(last, out l)) { l = new List<Use>(); Map[last] = l; }
            l.Add(u);
            // modifiers held in a combo also mark their key in "all" mode
            if (_mode == 0) for (int i = 0; i < c.Length - 1; i++)
                {
                    if (!Map.TryGetValue(c[i], out l)) { l = new List<Use>(); Map[c[i]] = l; }
                    if (!l.Contains(u)) l.Add(u);
                }
        }

        private static bool Pass(Use u)
        {
            switch (_mode)
            {
                case 1: return u.mods == 0;
                case 2: return u.mods == 1;
                case 3: return u.mods == 2;
                case 4: return u.mods == 4;
                default: return true;
            }
        }

        private static void Refresh(BtcHotkeysTab tab)
        {
            int ver = BtcHotkeys.Version + BtcVanillaBinds.Actions().Count * 7919;
            if (ver == _ver && _mode == _verMode && Time.unscaledTime - _at < 2f) return;
            _ver = ver; _verMode = _mode; _at = Time.unscaledTime;
            Map.Clear();
            IList<BtcHotkeys.Def> defs = BtcHotkeys.Defs;
            for (int i = 0; i < defs.Count; i++)
            {
                if (defs[i].kind != BtcHotkeys.Kind.Key) continue;
                KeyCode[][] alts = defs[i].any ? new KeyCode[][] { BtcHotkeys.GetKeys(defs[i].id, null) } : BtcHotkeys.GetAlts(defs[i].id);
                for (int a = 0; a < alts.Length; a++) Add(alts[a], new Use { key = defs[i].id, title = defs[i].Title });
            }
            List<BtcVanillaBinds.Act> acts = BtcVanillaBinds.Actions();
            for (int i = 0; i < acts.Count; i++)
            {
                List<BindingsProvider.BindItem> its = BtcVanillaBinds.Items(acts[i].name);
                if (its == null) continue;
                for (int k = 0; k < its.Count; k++) Add(BtcVanillaBinds.Combo(its[k]), new Use { key = "v:" + acts[i].name, title = acts[i].title, vanilla = true });
            }
        }

        private static readonly Color Blue = new Color(0.45f, 0.65f, 1f, 1f), Amber = new Color(1f, 0.72f, 0.3f, 1f), Red = new Color(1f, 0.4f, 0.4f, 1f);

        private static void Stats(KeyCode k, out int mod, out int van, out bool conflict)
        {
            mod = 0; van = 0; conflict = false;
            List<Use> l;
            if (!Map.TryGetValue(k, out l)) return;
            Dictionary<string, int> combos = new Dictionary<string, int>();
            for (int i = 0; i < l.Count; i++)
            {
                if (!Pass(l[i])) continue;
                if (l[i].vanilla) van++; else { mod++; int c; combos.TryGetValue(l[i].combo, out c); combos[l[i].combo] = c + 1; }
            }
            foreach (KeyValuePair<string, int> kv in combos) if (kv.Value > 1) conflict = true;
        }

        internal static void Draw(BtcHotkeysTab tab, Rect c, int fs, KeyCode finderKey, float finderAt, List<string> finderIds)
        {
            if (_layout == null) Build();
            Refresh(tab);
            Event e = Event.current;
            float pad = fs, y = c.y + fs * 0.4f;
            tab.Txt(new Rect(c.x + pad, y, c.width * 0.4f, fs * 1.9f), Tt("Keyboard map", "Карта клавиатуры"), tab.Title, Color.white);
            // modifier layers
            string[] mn = { Tt("All", "Все"), Tt("No mod", "Без мод."), "Shift", "Ctrl", "Alt" };
            float mx = c.xMax - pad;
            for (int i = mn.Length - 1; i >= 0; i--)
            {
                float mw = Mathf.Max(fs * 3.4f, tab.ChipStyle.CalcSize(new GUIContent(mn[i])).x + fs * 1.2f);
                Rect mr = new Rect(mx - mw, y, mw, fs * 1.9f);
                bool on = _mode == i;
                BtcReplayUI.Fill(mr, on ? new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.25f) : new Color(1f, 1f, 1f, mr.Contains(e.mousePosition) ? 0.14f : 0.07f));
                tab.Txt(mr, mn[i], tab.ChipStyle, on ? BtcMenu.Accent : Color.white);
                if (i == 0) BtcReplayIcons.Hint(mr, Tt("Every bind that uses the key", "Все бинды, где участвует клавиша"));
                else BtcReplayIcons.Hint(mr, Tt("Only combos with exactly this modifier on the key", "Только комбинации с этим модификатором"));
                if (BtcSfx.Btn(mr, "", BtcReplayUI.sBtn)) _mode = i;
                mx -= mw + 3f;
            }
            y += fs * 2.6f;
            float u = Mathf.Min((c.width - pad * 2f) / 23.4f, fs * 3.4f), gap = Mathf.Max(2f, u * 0.06f);
            float x0 = c.x + pad;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
            string hoverTip = null; Rect hoverRect = default(Rect);
            for (int i = 0; i < _layout.Count; i++)
            {
                Kc k = _layout[i];
                Rect r = new Rect(x0 + k.x * u, y + k.y * u, k.w * u - gap, u - gap);
                int mod, van; bool conf;
                Stats(k.code, out mod, out van, out conf);
                Color bg = new Color(1f, 1f, 1f, 0.06f);
                if (conf) bg = new Color(Red.r, Red.g, Red.b, 0.5f);
                else if (mod > 0 && van > 0) bg = new Color(Amber.r, Amber.g, Amber.b, 0.38f);
                else if (mod > 0) bg = new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.32f);
                else if (van > 0) bg = new Color(Blue.r, Blue.g, Blue.b, 0.34f);
                bool hover = r.Contains(e.mousePosition);
                if (hover) bg = new Color(Mathf.Min(1f, bg.r + 0.15f), Mathf.Min(1f, bg.g + 0.15f), Mathf.Min(1f, bg.b + 0.15f), Mathf.Min(1f, bg.a + 0.12f));
                BtcReplayUI.Fill(r, bg);
                bool isMod = ((_mode == 2 && ModFlag(k.code) == 1) || (_mode == 3 && ModFlag(k.code) == 2) || (_mode == 4 && ModFlag(k.code) == 4));
                if (isMod || k.code == _sel) Frame(r, BtcMenu.Accent, 2f);
                if (k.code == finderKey && Time.unscaledTime - finderAt < 8f) Frame(r, new Color(1f, 1f, 1f, 0.4f + 0.6f * pulse), 2f);
                tab.Txt(new Rect(r.x + 3f, r.y + 1f, r.width - 4f, fs * 1.1f), k.label, tab.Dim, new Color(1f, 1f, 1f, 0.85f));
                if (mod + van > 0) tab.Txt(new Rect(r.x, r.y + u * 0.38f, r.width, u * 0.5f), (mod + van).ToString(), tab.ChipStyle, Color.white);
                if (hover) { hoverTip = TipOf(k, mod, van); hoverRect = r; }
                if (BtcSfx.Btn(r, "", BtcReplayUI.sBtn)) _sel = k.code;
            }
            // mouse buttons
            float my = y + 6.6f * u;
            string[] ml = { "LMB", "RMB", "MMB", "M4", "M5", "M6", "M7" };
            for (int i = 0; i < ml.Length; i++)
            {
                KeyCode code = KeyCode.Mouse0 + i;
                Rect r = new Rect(x0 + i * (u * 1.5f), my, u * 1.5f - gap, u * 0.8f);
                int mod, van; bool conf;
                Stats(code, out mod, out van, out conf);
                Color bg = new Color(1f, 1f, 1f, 0.06f);
                if (conf) bg = new Color(Red.r, Red.g, Red.b, 0.5f);
                else if (mod > 0 && van > 0) bg = new Color(Amber.r, Amber.g, Amber.b, 0.38f);
                else if (mod > 0) bg = new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.32f);
                else if (van > 0) bg = new Color(Blue.r, Blue.g, Blue.b, 0.34f);
                bool hover = r.Contains(e.mousePosition);
                BtcReplayUI.Fill(r, hover ? new Color(Mathf.Min(1f, bg.r + 0.15f), Mathf.Min(1f, bg.g + 0.15f), Mathf.Min(1f, bg.b + 0.15f), Mathf.Min(1f, bg.a + 0.12f)) : bg);
                if (code == _sel) Frame(r, BtcMenu.Accent, 2f);
                if (code == finderKey && Time.unscaledTime - finderAt < 8f) Frame(r, new Color(1f, 1f, 1f, 0.4f + 0.6f * pulse), 2f);
                tab.Txt(r, ml[i] + (mod + van > 0 ? "  " + (mod + van) : ""), tab.ChipStyle, Color.white);
                if (hover) { hoverTip = TipOf(new Kc(ml[i], code, 0, 0, 0), mod, van); hoverRect = r; }
                if (BtcSfx.Btn(r, "", BtcReplayUI.sBtn)) _sel = code;
            }
            // legend
            float lx = x0 + 7 * (u * 1.5f) + fs;
            LegendItem(tab, ref lx, my, fs, new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.6f), Tt("mod", "мод"));
            LegendItem(tab, ref lx, my, fs, new Color(Blue.r, Blue.g, Blue.b, 0.6f), Tt("vanilla", "ваниль"));
            LegendItem(tab, ref lx, my, fs, new Color(Amber.r, Amber.g, Amber.b, 0.6f), Tt("both", "оба"));
            LegendItem(tab, ref lx, my, fs, new Color(Red.r, Red.g, Red.b, 0.7f), Tt("conflict", "конфликт"));
            if (hoverTip != null) BtcReplayIcons.Hint(hoverRect, hoverTip);

            // selected key: list of binds
            float ly = my + u + fs * 0.8f;
            BtcReplayUI.Fill(new Rect(c.x + pad, ly - fs * 0.3f, c.width - pad * 2f, 1f), BtcMenu.Line);
            if (_sel == KeyCode.None)
            {
                tab.Txt(new Rect(c.x + pad, ly, c.width - pad * 2f, fs * 1.8f), Tt("Click a key to see its binds. Press a key on your keyboard: first press shows what is bound, a second press jumps to the bind.", "Кликни клавишу, чтобы увидеть её бинды. Нажми клавишу на клавиатуре: первое нажатие покажет, что назначено, второе - перейдёт к бинду."), tab.Dim, new Color(1f, 1f, 1f, 0.6f));
                return;
            }
            tab.Txt(new Rect(c.x + pad, ly, c.width, fs * 1.8f), BtcHotkeys.KeyName(_sel), tab.Title, Color.white);
            ly += fs * 2.2f;
            List<Use> l;
            int shown = 0;
            if (Map.TryGetValue(_sel, out l))
            {
                for (int i = 0; i < l.Count && ly + fs * 2f < c.yMax; i++)
                {
                    if (!Pass(l[i])) continue;
                    Rect row = new Rect(c.x + pad, ly, c.width - pad * 2f, fs * 1.9f);
                    bool hv = row.Contains(e.mousePosition);
                    BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, hv ? 0.1f : 0.04f));
                    BtcReplayUI.Fill(new Rect(row.x, row.y, 3f, row.height), l[i].vanilla ? Blue : BtcMenu.Accent);
                    tab.Txt(new Rect(row.x + fs * 0.8f, row.y, row.width * 0.5f, row.height), l[i].title + (l[i].vanilla ? "  (vanilla)" : ""), tab.Lbl, Color.white);
                    tab.Txt(new Rect(row.x + row.width * 0.55f, row.y, row.width * 0.4f, row.height), l[i].combo, tab.Lbl, new Color(1f, 1f, 1f, 0.6f));
                    BtcReplayIcons.Hint(row, Tt("Click: jump to this bind", "Клик: перейти к этому бинду"));
                    if (BtcSfx.Btn(row, "", BtcReplayUI.sBtn)) tab.Jump(l[i].key);
                    ly += fs * 2.1f; shown++;
                }
            }
            if (shown == 0) tab.Txt(new Rect(c.x + pad, ly, c.width, fs * 1.8f), Tt("Nothing is bound to this key" + (_mode > 0 ? " in this layer" : ""), "На эту клавишу ничего не назначено" + (_mode > 0 ? " в этом слое" : "")), tab.Dim, new Color(1f, 1f, 1f, 0.5f));
        }

        private static void LegendItem(BtcHotkeysTab tab, ref float x, float y, int fs, Color c, string text)
        {
            BtcReplayUI.Fill(new Rect(x, y + fs * 0.35f, fs * 0.9f, fs * 0.9f), c);
            float w = tab.Dim.CalcSize(new GUIContent(text)).x + fs * 0.6f;
            tab.Txt(new Rect(x + fs * 1.2f, y, w, fs * 1.6f), text, tab.Dim, new Color(1f, 1f, 1f, 0.7f));
            x += fs * 1.2f + w + fs * 0.8f;
        }

        private static void Frame(Rect r, Color c, float t)
        {
            BtcReplayUI.Fill(new Rect(r.x, r.y, r.width, t), c); BtcReplayUI.Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            BtcReplayUI.Fill(new Rect(r.x, r.y, t, r.height), c); BtcReplayUI.Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        private static string TipOf(Kc k, int mod, int van)
        {
            List<Use> l;
            if (!Map.TryGetValue(k.code, out l) || mod + van == 0) return BtcHotkeys.KeyName(k.code) + ": " + Tt("free", "свободна");
            string s = BtcHotkeys.KeyName(k.code) + ": ";
            int n = 0;
            for (int i = 0; i < l.Count; i++)
            {
                if (!Pass(l[i])) continue;
                if (n < 3) s += (n > 0 ? ", " : "") + l[i].title + " [" + l[i].combo + "]" + (l[i].vanilla ? "*" : "");
                n++;
            }
            if (n > 3) s += " +" + (n - 3);
            return s;
        }
    }
}
