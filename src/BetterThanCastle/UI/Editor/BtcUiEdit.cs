using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Data and engine of the UI editor (menu tab 12): per-element HUD customisation (enabled, scale, opacity, position as a fraction of the screen
    /// height), global HUD / menu scale, rail style (icons / icons + text / text), accent colour, built-in and own presets.
    /// Everything lives in configs/ui-editor.txt; <see cref="Apply"/> pushes the values into the live HUD slots (BtcHud.Slot), the menu shell and the
    /// launcher button. The master switch (<see cref="On"/>) off = the default look is applied, the stored values stay for later.
    /// </summary>
    internal static class BtcUiEdit
    {
        internal sealed class E
        {
            public bool En = true;
            public float Scale = 1f, Alpha = 1f, Nx, Ny;
            public E Clone() { return (E)MemberwiseClone(); }
        }

        /// <summary>One element the editor can place. Anchor: 0 top-left, 1 top-centre, 2 top-right, 3 left column (stack index in Row).</summary>
        internal sealed class Elem
        {
            public string Id, En, Ru, HintEn, HintRu;
            public int Icon, Anchor, Row;
            public float W, H;          // size at scale 1 in units of the HUD font size
            public float DefScale = 1f, DefNx, DefNy;
        }

        internal static readonly Elem[] Elems =
        {
            new Elem { Id = "launcher", En = "BTC button", Ru = "Кнопка BTC", HintEn = "Opens the mod menu", HintRu = "Открывает мод-меню", Icon = BtcReplayIcons.Layout, Anchor = 0, W = 3.6f, H = 2.0f, DefNx = -0.0133f, DefNy = 0.9444f },
            new Elem { Id = "cards", En = "Notice cards", Ru = "Карточки-уведомления", HintEn = "Top cards: rules, editor panel, pause owner...", HintRu = "Верхние карточки: правила, эдитор-панель, пауза...", Icon = BtcReplayIcons.News, Anchor = 1, W = 34f, H = 2.3f, DefNy = -0.0086f },
            new Elem { Id = "notice", En = "Short messages", Ru = "Короткие сообщения", HintEn = "One-line messages under the cards", HintRu = "Однострочные сообщения под карточками", Icon = BtcReplayIcons.Chat, Anchor = 1, W = 18f, H = 2.0f, DefNy = -0.0102f },
            new Elem { Id = "groups", En = "Control groups panel", Ru = "Панель контрольных групп", HintEn = "Groups and camera slots", HintRu = "Группы и слоты камеры", Icon = BtcReplayIcons.Group, Anchor = 2, W = 11f, H = 8f, DefScale = 0.4f, DefNx = -0.0036f, DefNy = 0.8052f },
            new Elem { Id = "pauseplate", En = "Pause plate", Ru = "Плашка паузы", HintEn = "Connection / pause state", HintRu = "Состояние связи и паузы", Icon = BtcReplayIcons.Pause, Anchor = 3, Row = 0, W = 16f, H = 1.9f, DefScale = 0.7f, DefNx = -0.0133f },
            new Elem { Id = "clientpause", En = "Client pause plate", Ru = "Плашка паузы клиентов", HintEn = "Whether clients may pause", HintRu = "Могут ли клиенты ставить паузу", Icon = BtcReplayIcons.Lock, Anchor = 3, Row = 1, W = 17f, H = 1.9f, DefScale = 0.4f, DefNx = -0.0133f, DefNy = -0.0016f },
            new Elem { Id = "ping", En = "Ping chip", Ru = "Чип пинга", HintEn = "Worst ping, hover for the list", HintRu = "Худший пинг, по наведению список", Icon = BtcReplayIcons.Wave, Anchor = 3, Row = 2, W = 7f, H = 1.9f, DefScale = 0.4523f, DefNx = -0.0128f, DefNy = 0.0217f },
        };

        internal static Elem ElemOf(string id) { for (int i = 0; i < Elems.Length; i++) if (Elems[i].Id == id) return Elems[i]; return null; }

        // ------------------------------------------------------------------ stored values
        private static readonly Dictionary<string, E> Cfg = new Dictionary<string, E>();
        private static bool _loaded, _on = true;
        private static float _hud = 1f, _menu = 1f;
        private static int _rail, _sub;
        internal static readonly Color DefaultAccent = new Color(0.55f, 0.85f, 0.55f, 1f);
        private static Color _accent = DefaultAccent;

        internal static string FilePath { get { return BtcPaths.Config("ui-editor.txt"); } }
        internal static string PresetDir { get { return BtcPaths.Config("ui-presets"); } }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                if (File.Exists(FilePath)) Parse(File.ReadAllText(FilePath, Encoding.UTF8), false);
            }
            catch (Exception e) { BtcLog.Error("UiEdit", "load: " + e.Message); }
            Apply();
        }

        /// <summary>Call once at boot and after the HUD slots exist (idempotent).</summary>
        internal static void Init() { EnsureLoaded(); }

        internal static bool On { get { EnsureLoaded(); return _on; } set { EnsureLoaded(); _on = value; Save(); Apply(); } }
        internal static float HudScaleRaw { get { EnsureLoaded(); return _hud; } set { _hud = Mathf.Clamp(value, 0.5f, 2f); Save(); } }
        internal static float MenuScaleRaw { get { EnsureLoaded(); return _menu; } set { _menu = Mathf.Clamp(value, 0.7f, 1.6f); Save(); } }
        internal static int SubRailModeRaw { get { EnsureLoaded(); return _sub; } set { _sub = Mathf.Clamp(value, 0, 2); Save(); } }
        internal static int SubRailMode { get { EnsureLoaded(); return _on ? _sub : 0; } }
        internal static int RailModeRaw { get { EnsureLoaded(); return _rail; } set { _rail = Mathf.Clamp(value, 0, 2); Save(); } }
        internal static Color AccentRaw { get { EnsureLoaded(); return _accent; } set { _accent = value; Save(); Apply(); } }

        /// <summary>Effective values (defaults while the master switch is off).</summary>
        internal static float HudScale { get { EnsureLoaded(); return _on ? _hud : 1f; } }
        internal static int RailMode { get { EnsureLoaded(); return _on ? _rail : 0; } }
        internal static int MenuFs
        {
            get
            {
                EnsureLoaded();
                return Mathf.Max(12, Mathf.RoundToInt(Mathf.Max(14, Screen.height / 38) * (_on ? _menu : 1f)));
            }
        }

        internal static E DefaultOf(string id)
        {
            Elem el = ElemOf(id);
            return new E { Scale = el != null ? el.DefScale : 1f, Nx = el != null ? el.DefNx : 0f, Ny = el != null ? el.DefNy : 0f };
        }

        internal static E Get(string id)
        {
            EnsureLoaded();
            E e;
            if (!Cfg.TryGetValue(id, out e)) { e = DefaultOf(id); Cfg[id] = e; }
            return e;
        }

        internal static void CfgSet(string id, E e) { EnsureLoaded(); Cfg[id] = e; }

        internal static bool IsDefault(string id)
        {
            E e = Get(id), d = DefaultOf(id);
            return e.En == d.En && Mathf.Abs(e.Scale - d.Scale) < 0.001f && Mathf.Abs(e.Alpha - d.Alpha) < 0.001f && Mathf.Abs(e.Nx - d.Nx) < 0.0005f && Mathf.Abs(e.Ny - d.Ny) < 0.0005f;
        }

        /// <summary>True when anything differs from the default look.</summary>
        internal static bool AnyChanged()
        {
            EnsureLoaded();
            if (Mathf.Abs(_hud - 1f) > 0.001f || Mathf.Abs(_menu - 1f) > 0.001f || _rail != 0 || ColorDiff(_accent, DefaultAccent)) return true;
            for (int i = 0; i < Elems.Length; i++) if (!IsDefault(Elems[i].Id)) return true;
            return false;
        }

        private static bool ColorDiff(Color a, Color b) { return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) > 0.01f; }

        internal static void Changed() { Save(); Apply(); }

        /// <summary>Pushes the values into the live objects.</summary>
        internal static void Apply()
        {
            if (!_loaded) return;
            try
            {
                for (int i = 0; i < Elems.Length; i++)
                {
                    BtcHud.Slot s = BtcHud.SlotOf(Elems[i].Id);
                    E e = _on ? Get(Elems[i].Id) : DefaultOf(Elems[i].Id);
                    s.Enabled = e.En; s.Scale = e.Scale; s.Alpha = e.Alpha; s.Norm = new Vector2(e.Nx, e.Ny);
                }
                Color na = _on ? _accent : DefaultAccent;
                bool accChanged = na != BtcMenu.Accent;
                BtcMenu.Accent = na;
                if (accChanged)
                {
                    if (BtcReplayUI.sHead != null) BtcReplayUI.sHead.normal.textColor = BtcMenu.AccentA(0.95f);
                    if (BtcShareUI.sHead != null) BtcShareUI.sHead.normal.textColor = BtcMenu.AccentA(0.95f);
                    ResourcePackManager.RecolorStyles();
                }
            }
            catch (Exception ex) { BtcLog.Error("UiEdit", "apply: " + ex.Message); }
        }

        // ------------------------------------------------------------------ file / text format
        private static string F(float v) { return v.ToString("0.####", CultureInfo.InvariantCulture); }

        internal static string Serialize()
        {
            EnsureLoaded();
            StringBuilder sb = new StringBuilder();
            sb.Append("on=").Append(_on ? "1" : "0").Append("\n");
            sb.Append("hud=").Append(F(_hud)).Append("\n");
            sb.Append("menu=").Append(F(_menu)).Append("\n");
            sb.Append("rail=").Append(_rail).Append("\n");
            sb.Append("sub=").Append(_sub).Append("\n");
            sb.Append("accent=").Append(BtcVisualColor.Hex(_accent)).Append("\n");
            for (int i = 0; i < Elems.Length; i++)
            {
                E e = Get(Elems[i].Id);
                sb.Append("el.").Append(Elems[i].Id).Append('=').Append(e.En ? 1 : 0).Append('|').Append(F(e.Scale)).Append('|').Append(F(e.Alpha)).Append('|').Append(F(e.Nx)).Append('|').Append(F(e.Ny)).Append("\n");
            }
            return sb.ToString();
        }

        private static float PF(string s, float def) { float v; return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : def; }

        /// <summary>Reads the text format. keepOn: the master switch of the current setup stays (presets must not flip it).</summary>
        internal static void Parse(string text, bool keepOn)
        {
            bool wasLoaded = _loaded;
            _loaded = true;
            bool on = _on;
            // a preset describes the whole look: start from the defaults
            _hud = 1f; _menu = 1f; _rail = 0; _sub = 0; _accent = DefaultAccent; Cfg.Clear();
            string[] lines = text.Replace("\r", "").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string ln = lines[i].Trim();
                int eq = ln.IndexOf('=');
                if (eq <= 0) continue;
                string k = ln.Substring(0, eq), v = ln.Substring(eq + 1);
                if (k == "on") _on = v == "1";
                else if (k == "hud") _hud = Mathf.Clamp(PF(v, 1f), 0.5f, 2f);
                else if (k == "menu") _menu = Mathf.Clamp(PF(v, 1f), 0.7f, 1.6f);
                else if (k == "rail") { int r; if (int.TryParse(v, out r)) _rail = Mathf.Clamp(r, 0, 2); }
                else if (k == "sub") { int r2; if (int.TryParse(v, out r2)) _sub = Mathf.Clamp(r2, 0, 2); }
                else if (k == "accent") { Color c; if (BtcVisualColor.TryHex(v, out c)) _accent = new Color(c.r, c.g, c.b, 1f); }
                else if (k.StartsWith("el.", StringComparison.Ordinal))
                {
                    string id = k.Substring(3);
                    if (ElemOf(id) == null) continue;
                    string[] p = v.Split('|');
                    E e = DefaultOf(id);
                    if (p.Length >= 5)
                    {
                        e.En = p[0] != "0"; e.Scale = Mathf.Clamp(PF(p[1], e.Scale), 0.3f, 3f); e.Alpha = Mathf.Clamp(PF(p[2], 1f), 0.1f, 1f);
                        e.Nx = Mathf.Clamp(PF(p[3], 0f), -3f, 3f); e.Ny = Mathf.Clamp(PF(p[4], 0f), -1.5f, 1.5f);
                    }
                    Cfg[id] = e;
                }
            }
            if (keepOn) _on = on;
            if (wasLoaded) { Save(); Apply(); }
        }

        internal static void Save()
        {
            if (!_loaded) return;
            try
            {
                string dir = Path.GetDirectoryName(FilePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(FilePath, Serialize(), new UTF8Encoding(false));
            }
            catch (Exception e) { BtcLog.Error("UiEdit", "save: " + e.Message); }
        }

        /// <summary>Restores a snapshot made by <see cref="Serialize"/> (undo).</summary>
        internal static void Restore(string snapshot) { Parse(snapshot, false); }

        // ------------------------------------------------------------------ presets
        internal sealed class Preset
        {
            public string Name, En, Ru, Text;
            public bool Own;
        }

        private static string Make(float hud, float menu, int rail, params string[] els)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("on=1\nhud=").Append(F(hud)).Append("\nmenu=").Append(F(menu)).Append("\nrail=").Append(rail).Append("\nsub=").Append(rail).Append("\n");
            for (int i = 0; i < els.Length; i++) sb.Append("el.").Append(els[i]).Append("\n");
            return sb.ToString();
        }

        internal static List<Preset> BuiltIn()
        {
            List<Preset> l = new List<Preset>();
            l.Add(new Preset { Name = "default", En = "Default: icons", Ru = "Обычный: иконки", Text = Make(1f, 1f, 0) });
            l.Add(new Preset { Name = "text", En = "Text menu: words instead of icons on the rail", Ru = "Текстовое меню: слова вместо иконок на рельсе", Text = Make(1f, 1f, 2) });
            l.Add(new Preset { Name = "both", En = "Icons and words on the rail", Ru = "Иконки и слова на рельсе", Text = Make(1f, 1f, 1) });
            l.Add(new Preset { Name = "compact", En = "Compact: smaller menu and HUD", Ru = "Компактный: меньше меню и HUD", Text = Make(0.8f, 0.9f, 0) });
            l.Add(new Preset { Name = "large", En = "Large: big text everywhere, words on the rail", Ru = "Крупный: большой текст везде, слова на рельсе", Text = Make(1.25f, 1.25f, 1) });
            l.Add(new Preset { Name = "quiet", En = "Quiet HUD: faint, small plates and button", Ru = "Тихий HUD: бледные маленькие плашки и кнопка", Text = Make(0.85f, 1f, 0, "launcher=1|0.8|0.45|0|0", "ping=1|0.6|0.6|0|0", "pauseplate=1|0.6|0.8|0|0", "clientpause=1|0.6|0.6|0|0", "notice=1|0.9|0.85|0|0") });
            l.Add(new Preset { Name = "violet", En = "Violet accent", Ru = "Фиолетовый акцент", Text = Make(1f, 1f, 0) + "accent=B58CFF\n" });
            l.Add(new Preset { Name = "ice", En = "Ice-blue accent, text menu", Ru = "Ледяной акцент, текстовое меню", Text = Make(1f, 1f, 2) + "accent=5CC8FF\n" });
            return l;
        }

        internal static List<Preset> OwnPresets()
        {
            List<Preset> l = new List<Preset>();
            try
            {
                if (!Directory.Exists(PresetDir)) return l;
                string[] files = Directory.GetFiles(PresetDir, "*.txt");
                Array.Sort(files);
                for (int i = 0; i < files.Length; i++)
                {
                    string n = Path.GetFileNameWithoutExtension(files[i]);
                    l.Add(new Preset { Name = n, En = n, Ru = n, Own = true, Text = File.ReadAllText(files[i], Encoding.UTF8) });
                }
            }
            catch (Exception) { }
            return l;
        }

        internal static string SafeName(string s)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in s.Trim()) if (char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_') sb.Append(c);
            return sb.ToString().Trim();
        }

        internal static bool SaveOwn(string name)
        {
            name = SafeName(name);
            if (name.Length == 0) return false;
            try
            {
                if (!Directory.Exists(PresetDir)) Directory.CreateDirectory(PresetDir);
                File.WriteAllText(Path.Combine(PresetDir, name + ".txt"), Serialize(), new UTF8Encoding(false));
                return true;
            }
            catch (Exception) { return false; }
        }

        internal static void DeleteOwn(string name)
        {
            try { string p = Path.Combine(PresetDir, name + ".txt"); if (File.Exists(p)) File.Delete(p); } catch (Exception) { }
        }

        /// <summary>Applies a preset text, keeping the master switch.</summary>
        internal static void ApplyPreset(string text) { Parse(text, true); }

        /// <summary>Back to the default look (keeps the master switch).</summary>
        internal static void ResetAll() { Parse("", true); }

        // ------------------------------------------------------------------ geometry shared by the editor
        /// <summary>Nominal (default-position) rectangle of an element at its CURRENT scale, in screen pixels, offset applied.</summary>
        internal static Rect ScreenRect(Elem el, E e, bool withOffset)
        {
            float W = Screen.width, H = Screen.height;
            float u = BtcHud.Fs * e.Scale;
            float w = el.W * u, h = el.H * u;
            float x, y;
            float off = BtcHud.Fs * 0.5f;
            switch (el.Anchor)
            {
                case 0: x = u * 0.6f; y = u * 0.5f; break;
                case 1: w = Mathf.Min(el.Id == "cards" ? W * 0.55f : W * 0.6f, w); x = (W - w) * 0.5f; y = el.Id == "cards" ? off : off + BtcHud.Fs * 2.9f; break;
                case 2: x = W - 45f - w; y = u * 0.5f; break;
                default:
                    {
                        // left column: every plate stacks under the previous ones (their default scale), under the BTC button
                        float yy = BtcHud.Fs * 0.6f + ColumnShift();
                        for (int i = 0; i < el.Row; i++)
                        {
                            for (int k = 0; k < Elems.Length; k++) if (Elems[k].Anchor == 3 && Elems[k].Row == i) { E ee = Get(Elems[k].Id); yy += Elems[k].H * BtcHud.Fs * ee.Scale + BtcHud.Fs * ee.Scale * 0.25f; }
                        }
                        x = BtcHud.Fs * 0.6f; y = yy; break;
                    }
            }
            Rect r = new Rect(x, y, w, h);
            if (withOffset) { r.x += e.Nx * H; r.y += e.Ny * H; }
            return r;
        }

        private static float ColumnShift()
        {
            E l = Get("launcher");
            if (!l.En || Mathf.Abs(l.Nx) > 0.05f || Mathf.Abs(l.Ny) > 0.05f) return 0f;
            return BtcHud.Fs * l.Scale * 2.7f;
        }
    }
}
