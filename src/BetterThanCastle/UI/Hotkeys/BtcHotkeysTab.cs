using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Advanced Hotkeys tab (menu tab 3) in the new menu style: category rail inside the tab, search, one row per bind with a chip per key combo
    /// (a bind can have any number of combos, "+" adds one), the vanilla key bindings editable in the same list, favourites, key finder
    /// (press a key: what is bound to it; press it again: jump to that bind), keyboard map page and profiles page.
    /// Data model and firing logic: BtcHotkeys (alternatives are stored as "A+B|C" and any of them fires the bind), vanilla: BtcVanillaBinds.
    /// </summary>
    internal sealed class BtcHotkeysTab : BtcMenuTab
    {
        internal const int PAll = 0, PCam = 1, PGrp = 2, POrd = 3, PMatch = 4, PSafe = 5, PVan = 6, PFav = 7, PKbd = 8, PProf = 9;

        private static readonly BtcHotkeysTab Instance_ = new BtcHotkeysTab();
        internal static BtcHotkeysTab Instance { get { return Instance_; } }

        internal override int Id { get { return 3; } }
        internal override bool IsActive { get { return BtcHotkeysUI.TabActive; } }
        internal override string UndoScope { get { return "hotkeys"; } }

        // ---- state
        private int _page { get { return BtcMenuState.Int("hotkeys:page", 0); } set { BtcMenuState.SetInt("hotkeys:page", value); } }
        private string _q = "";
        private bool _fConf, _fChanged;
        private Vector2 _scroll;
        private float _h = 100f;
        private string _confirm;
        private float _confirmUntil;
        private string _flashId;
        private float _flashUntil;
        private string _jumpId;
        private float _scrollTo = -1f;
        private string _toast;
        private float _toastUntil;
        private string _dragId;
        private bool _hooked;

        // key finder
        private KeyCode _fKey;
        private float _fAt;
        private int _fCycle;
        private int _fFrame = -1;
        private readonly List<string> _fIds = new List<string>();

        // caches
        private int _cacheVer = -1;
        private float _cacheAt;
        private readonly Dictionary<string, List<string>> _modOwners = new Dictionary<string, List<string>>();
        private readonly Dictionary<string, int> _pageCounts = new Dictionary<string, int>();
        private readonly HashSet<string> _favs = new HashSet<string>();
        private bool _favsLoaded;

        private GUIStyle _lbl, _dim, _chip, _tf, _ttl;
        private int _fs;

        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }

        // ------------------------------------------------------------------ pages
        private static readonly int[] PageIds = { PAll, PCam, PGrp, POrd, PMatch, PSafe, PVan, PFav, PKbd, PProf };

        private static int PageIcon(int p)
        {
            switch (p)
            {
                case PAll: return BtcReplayIcons.Grid;
                case PCam: return BtcReplayIcons.Camera;
                case PGrp: return BtcReplayIcons.Group;
                case POrd: return BtcReplayIcons.Cursor;
                case PMatch: return BtcReplayIcons.Wave;
                case PSafe: return BtcReplayIcons.Lock;
                case PVan: return BtcReplayIcons.Monitor;
                case PFav: return BtcReplayIcons.Star;
                case PKbd: return BtcReplayIcons.Keys;
                default: return BtcReplayIcons.Folder;
            }
        }

        private static string PageName(int p)
        {
            switch (p)
            {
                case PAll: return Tt("All binds", "Все бинды");
                case PCam: return Tt("Camera and view", "Камера и вид");
                case PGrp: return Tt("Control groups", "Группы управления");
                case POrd: return Tt("Orders and units", "Приказы и юниты");
                case PMatch: return Tt("Match and replay", "Матч и реплей");
                case PSafe: return Tt("Safety", "Защита");
                case PVan: return Tt("Vanilla binds", "Ванильные бинды");
                case PFav: return Tt("Favourites", "Избранное");
                case PKbd: return Tt("Keyboard map", "Карта клавиатуры");
                default: return Tt("Profiles", "Профили");
            }
        }

        internal static int PageOf(string group)
        {
            if (group.StartsWith("Camera") || group == "View") return PCam;
            if (group == "Control groups") return PGrp;
            if (group == "Safety") return PSafe;
            if (group == "Match statistics" || group == "Timer" || group == "Replay" || group == "Mod menu") return PMatch;
            return POrd;
        }

        // ------------------------------------------------------------------ styles
        private void Styles(int fs)
        {
            BtcReplayUI.Ensure(fs);
            if (_lbl != null && _fs == fs) return;
            _fs = fs;
            GUIStyle b = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
            _lbl = new GUIStyle(b); _lbl.fontSize = Mathf.RoundToInt(fs * 0.95f); _lbl.alignment = TextAnchor.MiddleLeft; _lbl.clipping = TextClipping.Clip; _lbl.normal.textColor = Color.white;
            _dim = new GUIStyle(_lbl); _dim.fontSize = Mathf.RoundToInt(fs * 0.78f);
            _chip = new GUIStyle(_lbl); _chip.fontSize = Mathf.RoundToInt(fs * 0.85f); _chip.alignment = TextAnchor.MiddleCenter;
            _ttl = new GUIStyle(_lbl); _ttl.fontSize = Mathf.RoundToInt(fs * 1.15f); _ttl.fontStyle = FontStyle.Bold;
            _tf = new GUIStyle(GUI.skin.textField); _tf.font = b.font; _tf.fontSize = Mathf.RoundToInt(fs * 0.9f); _tf.alignment = TextAnchor.MiddleLeft;
            _tf.normal.background = null; _tf.focused.background = null; _tf.hover.background = null; _tf.active.background = null;
            _tf.normal.textColor = Color.white; _tf.focused.textColor = Color.white; _tf.hover.textColor = Color.white; _tf.padding = new RectOffset(4, 4, 0, 0);
        }

        internal GUIStyle Lbl { get { return _lbl; } }
        internal GUIStyle Dim { get { return _dim; } }
        internal GUIStyle ChipStyle { get { return _chip; } }
        internal GUIStyle Field { get { return _tf; } }
        internal GUIStyle Title { get { return _ttl; } }

        internal void Txt(Rect r, string s, GUIStyle st, Color c)
        {
            Color old = GUI.contentColor;
            GUI.contentColor = c;
            GUI.Label(r, s, st);
            GUI.contentColor = old;
        }

        /// <summary>Rect of the text field drawn this frame (the click-away check uses it on the next event).</summary>
        internal Rect FieldRect;

        internal void Toast(string s) { BtcMenu.Toast(s, -1); }

        // ------------------------------------------------------------------ favourites
        private static string FavFile { get { return BtcPaths.Config("hotkeys-ui.txt"); } }

        private void LoadFavs()
        {
            if (_favsLoaded) return;
            _favsLoaded = true;
            try
            {
                if (!System.IO.File.Exists(FavFile)) return;
                string[] lines = System.IO.File.ReadAllLines(FavFile, System.Text.Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++) if (lines[i].StartsWith("fav=")) _favs.Add(lines[i].Substring(4).Trim());
            }
            catch (Exception) { }
        }

        private void SaveFavs()
        {
            try
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                foreach (string f in _favs) sb.Append("fav=").Append(f).Append("\r\n");
                System.IO.File.WriteAllText(FavFile, sb.ToString(), new System.Text.UTF8Encoding(false));
            }
            catch (Exception) { }
        }

        internal bool IsFav(string key) { LoadFavs(); return _favs.Contains(key); }

        // ------------------------------------------------------------------ owners / conflicts
        private void Refresh()
        {
            int ver = BtcHotkeys.Version;
            if (ver == _cacheVer && Time.unscaledTime - _cacheAt < 2f) return;
            _cacheVer = ver; _cacheAt = Time.unscaledTime;
            _modOwners.Clear();
            BuildVanOwners();
            IList<BtcHotkeys.Def> defs = BtcHotkeys.Defs;
            for (int i = 0; i < defs.Count; i++)
            {
                if (defs[i].kind != BtcHotkeys.Kind.Key || defs[i].any) continue;
                KeyCode[][] alts = BtcHotkeys.GetAlts(defs[i].id);
                for (int a = 0; a < alts.Length; a++)
                {
                    string k = BtcHotkeys.ComboKey(alts[a]);
                    List<string> l;
                    if (!_modOwners.TryGetValue(k, out l)) { l = new List<string>(); _modOwners[k] = l; }
                    if (!l.Contains(defs[i].id)) l.Add(defs[i].id);
                }
            }
        }

        private bool ChipConflict(string id, KeyCode[] combo)
        {
            List<string> l;
            return _modOwners.TryGetValue(BtcHotkeys.ComboKey(combo), out l) && (l.Count > 1);
        }

        private string OverlapTip(string id, KeyCode[] combo)
        {
            string s = "";
            List<string> l;
            if (_modOwners.TryGetValue(BtcHotkeys.ComboKey(combo), out l))
            {
                for (int i = 0; i < l.Count; i++)
                {
                    if (l[i] == id) continue;
                    BtcHotkeys.Def d = BtcHotkeys.GetDef(l[i]);
                    s += (s.Length > 0 ? ", " : "") + (d != null ? d.Title : l[i]);
                }
            }
            return s;
        }

        private readonly Dictionary<string, string> _vanOwners = new Dictionary<string, string>();

        private string VanillaUsing(KeyCode[] combo)
        {
            string s;
            return _vanOwners.TryGetValue(BtcHotkeys.ComboKey(combo), out s) ? s : "";
        }

        private void BuildVanOwners()
        {
            _vanOwners.Clear();
            List<BtcVanillaBinds.Act> acts = BtcVanillaBinds.Actions();
            for (int i = 0; i < acts.Count; i++)
            {
                List<BindingsProvider.BindItem> its = BtcVanillaBinds.Items(acts[i].name);
                if (its == null) continue;
                for (int k = 0; k < its.Count; k++)
                {
                    KeyCode[] c = BtcVanillaBinds.Combo(its[k]);
                    if (c == null) continue;
                    string key = BtcHotkeys.ComboKey(c), s;
                    _vanOwners.TryGetValue(key, out s);
                    if (s == null || s.IndexOf(acts[i].title, StringComparison.Ordinal) < 0) _vanOwners[key] = (s == null ? "" : s + ", ") + acts[i].title;
                }
            }
        }
        // ------------------------------------------------------------------ key finder
        private void PollFinder()
        {
            if (Time.frameCount == _fFrame) return;
            _fFrame = Time.frameCount;
            if (BtcHotkeys.Capturing || GUIUtility.keyboardControl != 0) return;
            KeyCode k = BtcHotkeys.AnyKeyDown();
            if (k == KeyCode.None || k == KeyCode.Mouse0 || k == KeyCode.Mouse1 || k == BtcMenu.MenuKeyMain || BtcVanillaBinds.IsModifier(k)) return;
            List<string> ids = new List<string>();
            List<string> mod = BtcHotkeys.IdsUsingKey(k);
            for (int i = 0; i < mod.Count; i++) ids.Add(mod[i]);
            List<BtcVanillaBinds.Act> acts = BtcVanillaBinds.Actions();
            for (int i = 0; i < acts.Count; i++)
            {
                List<BindingsProvider.BindItem> its = BtcVanillaBinds.Items(acts[i].name);
                if (its == null) continue;
                for (int n = 0; n < its.Count; n++)
                {
                    KeyCode[] c = BtcVanillaBinds.Combo(its[n]);
                    if (c != null && Array.IndexOf(c, k) >= 0) { ids.Add("v:" + acts[i].name); break; }
                }
            }
            bool again = k == _fKey && Time.unscaledTime - _fAt < 2.5f && ids.Count > 0;
            _fKey = k; _fAt = Time.unscaledTime;
            _fIds.Clear(); _fIds.AddRange(ids);
            if (ids.Count == 0) BtcSfx.Miss(); else if (again) BtcSfx.Jump(); else BtcSfx.Find();
            if (again)
            {
                _fCycle = (_fCycle + 1) % ids.Count;
                Jump(ids[_fCycle]);
            }
            else _fCycle = -1;
            if (ids.Count == 0) Toast(Tt("Key " + BtcHotkeys.KeyName(k) + " is not bound", "Клавиша " + BtcHotkeys.KeyName(k) + " никуда не назначена"));
            else
            {
                string names = "";
                for (int i = 0; i < ids.Count && i < 4; i++) names += (i > 0 ? ", " : "") + NameOf(ids[i]);
                if (ids.Count > 4) names += " +" + (ids.Count - 4);
                Toast(BtcHotkeys.KeyName(k) + ": " + names + Tt("  (press again to jump)", "  (нажми ещё раз - перейти)"));
            }
        }

        private static string NameOf(string key)
        {
            if (key.StartsWith("v:"))
            {
                List<BtcVanillaBinds.Act> acts = BtcVanillaBinds.Actions();
                for (int i = 0; i < acts.Count; i++) if (acts[i].name == key.Substring(2)) return acts[i].title + " (vanilla)";
                return key.Substring(2);
            }
            BtcHotkeys.Def d = BtcHotkeys.GetDef(key);
            return d != null ? d.Title : key;
        }

        /// <summary>Opens the page of a bind (mod id or "v:action"), scrolls to it and flashes it.</summary>
        internal void Jump(string key)
        {
            _q = ""; _fConf = false; _fChanged = false;
            if (key.StartsWith("v:"))
            {
                _page = PVan;
                List<BtcVanillaBinds.Act> acts = BtcVanillaBinds.Actions();
                for (int i = 0; i < acts.Count; i++) if (acts[i].name == key.Substring(2)) BtcUi.SetOpen("hkv:" + acts[i].cat, true);
            }
            else
            {
                BtcHotkeys.Def d = BtcHotkeys.GetDef(key);
                _page = d != null ? PageOf(d.group) : PAll;
                if (d != null) BtcUi.SetOpen("hk:" + d.group, true);
            }
            _jumpId = key; _flashId = key; _flashUntil = Time.unscaledTime + 3f; _scrollTo = -2f;
        }

        // ------------------------------------------------------------------ draw
        internal override void Draw(Rect area, int fs)
        {
            Styles(fs);
            LoadFavs();
            Refresh();
            Event e = Event.current;
            if (!_hooked)
            {
                _hooked = true;
                BtcHotkeys.ExternalCapture = OnVanillaCapture;
            }
            if (e.type == EventType.KeyDown && e.keyCode == BtcMenu.MenuKeyMain)
            {
                GUIUtility.keyboardControl = 0;
                BtcMenu.PgDownFrame = Time.frameCount;
                ResourcePackManager.CloseMenu();
                e.Use();
                return;
            }
            BtcSfx.Typing(e);
            // a click outside the text field (or Esc) leaves it, so keys can be pressed for the finder again
            if (GUIUtility.keyboardControl != 0 && ((e.type == EventType.MouseDown && !FieldRect.Contains(e.mousePosition)) || (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)))
            {
                GUIUtility.keyboardControl = 0;
                GUI.FocusControl("");
                if (e.type == EventType.KeyDown) e.Use();
            }
            FieldRect = default(Rect);
            PollFinder();
            float railW = BtcMenu.SubRailWidth(fs);
            Rect rail = new Rect(area.x, area.y, railW, area.height);
            bool popup = _resetOpen;
            if (popup) GUI.enabled = false;
            DrawRail(rail, fs);
            Rect content = new Rect(area.x + railW, area.y, area.width - railW, area.height);
            BtcReplayUI.Fill(new Rect(content.x - 1f, content.y, 1f, content.height), BtcMenu.Line);

            if (_page == PKbd) BtcHotkeysKeyboard.Draw(this, content, fs, _fKey, _fAt, _fIds);
            else if (_page == PProf) BtcHotkeysProfiles.Draw(this, content, fs);
            else DrawListPage(content, fs);
            if (popup) { GUI.enabled = true; DrawResetPopup(area, fs); }

            BtcReplayIcons.DrawTip(fs, area.xMax, false);
        }

        private void DrawRail(Rect r, int fs)
        {
            Event e = Event.current;
            float item = r.width * 0.82f, x = r.x + (r.width - item) * 0.5f, y = r.y + fs * 0.6f, itemW = item;
            BtcMenu.RailGeometry(r, fs, ref item, ref itemW, ref x);
            for (int i = 0; i < PageIds.Length; i++)
            {
                int p = PageIds[i];
                if (p == PVan || p == PKbd) { BtcReplayUI.Fill(new Rect(x + itemW * 0.15f, y, itemW * 0.7f, 1f), BtcMenu.Line); y += fs * 0.5f; }
                Rect ir = new Rect(x, y, itemW, item);
                bool on = p == _page, hover = ir.Contains(e.mousePosition);
                if (on) { BtcReplayUI.Fill(ir, new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.14f)); BtcReplayUI.Fill(new Rect(r.x, y + item * 0.15f, 3f, item * 0.7f), BtcMenu.Accent); }
                else if (hover) BtcReplayUI.Fill(ir, new Color(1f, 1f, 1f, 0.07f));
                float g = item * 0.55f;
                BtcMenu.RailGlyph(ir, fs, PageIcon(p), PageName(p), on, hover);
                if (p <= PSafe && PageHasConflict(p)) BtcReplayUI.Fill(new Rect(ir.xMax - fs * 0.55f, ir.y + fs * 0.1f, fs * 0.4f, fs * 0.4f), new Color(1f, 0.4f, 0.4f, 1f));
                BtcReplayIcons.Hint(ir, PageName(p));
                if (BtcSfx.Btn(ir, "", BtcReplayUI.sBtn)) { _page = p; _scroll = Vector2.zero; BtcHotkeys.CancelCapture(); }
                y += item + fs * 0.3f;
            }
        }

        // ------------------------------------------------------------------ reset popup (which binds to reset)
        private bool _resetOpen, _resetMod = true, _resetVan = true;

        private void DrawResetPopup(Rect area, int fs)
        {
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { _resetOpen = false; e.Use(); return; }
            BtcReplayUI.Fill(area, new Color(0f, 0f, 0f, 0.55f));
            float w = Mathf.Min(fs * 28f, area.width - fs * 2f), h = fs * 17.5f;
            Rect card = new Rect(area.center.x - w * 0.5f, area.center.y - h * 0.5f, w, h);
            BtcReplayUI.Fill(card, BtcHud.Panel);
            BtcReplayUI.Fill(new Rect(card.x, card.y, card.width, 2f), BtcMenu.Accent);
            GUIStyle t = new GUIStyle(_ttl);
            BtcVisualRows.Txt(new Rect(card.x + fs, card.y + fs * 0.6f, card.width - fs * 2f, fs * 2f), Tt("Reset binds to defaults", "Сбросить бинды по умолчанию"), t, Color.white);
            GUI.BeginGroup(card);
            float y = fs * 3.2f, gw = card.width;
            bool all = _resetMod && _resetVan;
            if (BtcUi.Toggle(ref y, gw, fs, Tt("Everything", "Всё сразу"), all, Tt("Mod binds and vanilla game binds", "Бинды мода и бинды ванильной игры"))) { bool nv = !all; _resetMod = nv; _resetVan = nv; }
            if (BtcUi.Toggle(ref y, gw, fs, Tt("Mod binds", "Бинды мода"), _resetMod, Tt("Every hotkey added by Better Than Castle", "Все хоткеи, добавленные Better Than Castle"))) _resetMod = !_resetMod;
            if (BtcUi.Toggle(ref y, gw, fs, Tt("Vanilla binds", "Ванильные бинды"), _resetVan, Tt("The game's own keys (userconfigs.lua)", "Собственные клавиши игры (userconfigs.lua)"))) _resetVan = !_resetVan;
            GUI.EndGroup();
            // buttons: apply (check) and cancel (cross)
            float bs = fs * 2.4f;
            Rect ok = new Rect(card.xMax - fs - bs * 2f - 6f, card.yMax - fs - bs, bs, bs);
            Rect no = new Rect(card.xMax - fs - bs, card.yMax - fs - bs, bs, bs);
            bool can = _resetMod || _resetVan;
            bool prev = GUI.enabled;
            GUI.enabled = can;
            BtcReplayIcons.Hint(ok, can ? Tt("Reset the selected binds (Ctrl+Z undoes it)", "Сбросить выбранные бинды (Ctrl+Z отменит)") : Tt("Tick what to reset", "Отметьте, что сбросить"));
            BtcReplayUI.Fill(ok, can ? BtcMenu.AccentA(ok.Contains(e.mousePosition) ? 0.35f : 0.22f) : new Color(1f, 1f, 1f, 0.05f));
            BtcReplayIcons.Glyph(BtcReplayIcons.Check, new Rect(ok.x + bs * 0.2f, ok.y + bs * 0.2f, bs * 0.6f, bs * 0.6f), can ? BtcMenu.Accent : new Color(1f, 1f, 1f, 0.25f));
            if (BtcSfx.Btn(ok, "", BtcReplayUI.sBtn))
            {
                bool m = _resetMod, v = _resetVan;
                BtcUndo.Group("hotkeys", Tt("Binds reset", "Сброс биндов"), delegate
                {
                    if (m) BtcHotkeys.ResetAll();
                    if (v) BtcVanillaBinds.ResetAll();
                });
                BtcSfx.Delete();
                Toast(m && v ? Tt("All binds reset", "Все бинды сброшены") : (m ? Tt("Mod binds reset", "Бинды мода сброшены") : Tt("Vanilla binds reset", "Ванильные бинды сброшены")));
                _resetOpen = false;
            }
            GUI.enabled = prev;
            BtcReplayUI.Fill(no, new Color(1f, 1f, 1f, no.Contains(e.mousePosition) ? 0.14f : 0.07f));
            BtcReplayIcons.Glyph(BtcReplayIcons.Cross, new Rect(no.x + bs * 0.2f, no.y + bs * 0.2f, bs * 0.6f, bs * 0.6f), new Color(1f, 1f, 1f, 0.7f));
            BtcReplayIcons.Hint(no, Tt("Cancel", "Отмена"));
            if (BtcSfx.Btn(no, "", BtcReplayUI.sBtn)) _resetOpen = false;
        }

        private bool PageHasConflict(int p)
        {
            IList<BtcHotkeys.Def> defs = BtcHotkeys.Defs;
            for (int i = 0; i < defs.Count; i++)
                if (defs[i].kind == BtcHotkeys.Kind.Key && (p == PAll || PageOf(defs[i].group) == p) && BtcHotkeys.IsConflict(defs[i].id)) return true;
            return false;
        }

        // ------------------------------------------------------------------ list pages
        private bool Matches(string text) { return _q.Length == 0 || text.IndexOf(_q, StringComparison.OrdinalIgnoreCase) >= 0; }

        private void DrawListPage(Rect content, int fs)
        {
            Event e = Event.current;
            float pad = fs, ib = fs * 1.9f;
            float y0 = content.y + fs * 0.4f;
            Txt(new Rect(content.x + pad, y0, content.width * 0.4f, ib), PageName(_page), _ttl, Color.white);

            // right: master switch + bulk icons
            float x = content.xMax - pad - ib, ty = y0;
            if (BtcReplayIcons.Plain(new Rect(x, ty, ib, ib), BtcReplayIcons.Folder, Color.white, Tt("Open the config folder (hotkeys.txt)", "Открыть папку с конфигом (hotkeys.txt)")))
                try { Application.OpenURL("file:///" + System.IO.Path.GetDirectoryName(BtcPaths.Config("hotkeys.txt")).Replace('\\', '/')); } catch (Exception) { }
            x -= ib + 3f;
            if (BtcReplayIcons.Plain(new Rect(x, ty, ib, ib), BtcReplayIcons.Refresh, Color.white, Tt("Reset binds to defaults (you choose which)", "Сбросить бинды по умолчанию (выберете, какие)")))
            {
                _resetOpen = true; _resetMod = true; _resetVan = true;
            }
            x -= ib + 3f;
            bool confC = _confirm == "clear" && Time.unscaledTime < _confirmUntil;
            if (BtcReplayIcons.Plain(new Rect(x, ty, ib, ib), BtcReplayIcons.Trash, confC ? new Color(1f, 0.45f, 0.45f, 1f) : Color.white, confC ? Tt("Click again: unbind every function", "Ещё раз: снять все бинды") : Tt("Unbind every function", "Снять все бинды")))
            {
                if (confC) { BtcSfx.Delete(); BtcHotkeys.ClearAll(); _confirm = null; Toast(Tt("All binds cleared", "Все бинды сняты")); } else { _confirm = "clear"; _confirmUntil = Time.unscaledTime + 3f; }
            }
            x -= ib + fs;
            bool master = BtcHotkeys.Enabled;
            Rect sw = new Rect(x - fs * 2.6f, ty + (ib - fs * 1.2f) * 0.5f, fs * 2.4f, fs * 1.2f);
            BtcReplayUI.Fill(sw, master ? new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.9f) : new Color(1f, 1f, 1f, 0.16f));
            float kk = sw.height - 4f;
            BtcReplayUI.Fill(new Rect(master ? sw.xMax - kk - 2f : sw.x + 2f, sw.y + 2f, kk, kk), master ? new Color(0.03f, 0.05f, 0.04f, 1f) : new Color(1f, 1f, 1f, 0.75f));
            BtcReplayIcons.Hint(sw, master ? Tt("Advanced Hotkeys are ON (click to disable all of them)", "Advanced Hotkeys включены (клик - отключить все)") : Tt("Advanced Hotkeys are OFF", "Advanced Hotkeys выключены"));
            if (BtcSfx.Btn(sw, "", BtcReplayUI.sBtn)) { BtcHotkeys.SetEnabled(!master); BtcSfx.Switch(!master); }

            // search + filters
            float y = y0 + ib + fs * 0.5f;
            Rect sr = new Rect(content.x + pad, y, Mathf.Min(content.width * 0.45f, fs * 24f), ib);
            BtcReplayUI.Fill(sr, new Color(1f, 1f, 1f, 0.06f));
            BtcReplayIcons.Glyph(BtcReplayIcons.Search, new Rect(sr.x + fs * 0.4f, sr.y + ib * 0.2f, ib * 0.6f, ib * 0.6f), new Color(1f, 1f, 1f, 0.5f));
            FieldRect = sr;
            GUI.SetNextControlName("btc_hk_search");
            _q = GUI.TextField(new Rect(sr.x + ib * 0.95f, sr.y, sr.width - ib * 0.95f, ib), _q, 40, _tf);
            if (_q.Length == 0 && GUIUtility.keyboardControl == 0) Txt(new Rect(sr.x + ib * 0.95f + 4f, sr.y, sr.width, ib), Tt("Search: name, key, mouse button", "Поиск: название, клавиша, кнопка мыши"), _dim, new Color(1f, 1f, 1f, 0.35f));
            float fx = sr.xMax + fs * 0.6f;
            int nConf = 0;
            IList<BtcHotkeys.Def> defs = BtcHotkeys.Defs;
            for (int i = 0; i < defs.Count; i++) if (defs[i].kind == BtcHotkeys.Kind.Key && BtcHotkeys.IsConflict(defs[i].id)) nConf++;
            if (BtcReplayIcons.Button(new Rect(fx, y, ib, ib), BtcReplayIcons.Bolt, _fConf, Tt("Only conflicts and overlaps with vanilla (orange): ", "Только конфликты и пересечения с ванилью (оранжевые): ") + nConf)) _fConf = !_fConf;
            if (nConf > 0) BtcReplayUI.Fill(new Rect(fx + ib - fs * 0.5f, y + fs * 0.1f, fs * 0.4f, fs * 0.4f), new Color(1f, 0.4f, 0.4f, 1f));
            fx += ib + 3f;
            if (BtcReplayIcons.Button(new Rect(fx, y, ib, ib), BtcReplayIcons.Check, _fChanged, Tt("Only binds changed from default", "Только изменённые бинды"))) _fChanged = !_fChanged;
            fx += ib + fs * 0.8f;
            bool allOpen = AllOpen();
            if (BtcReplayIcons.Plain(new Rect(fx, y, ib, ib), allOpen ? BtcReplayIcons.ChevD : BtcReplayIcons.ChevR, Color.white, allOpen ? Tt("Collapse all sections", "Свернуть все разделы") : Tt("Expand all sections", "Развернуть все разделы")))
                BtcUi.SetOpen(_sectionKeys, !allOpen);
            y += ib + fs * 0.6f;
            BtcReplayUI.Fill(new Rect(content.x + pad, y - fs * 0.25f, content.width - pad * 2f, 1f), BtcMenu.Line);

            Rect view = new Rect(content.x, y, content.width, content.yMax - y);
            float vw = view.width - (_h > view.height ? 14f : 0f);
            Rect inner = new Rect(0f, 0f, vw, _h);
            _scroll = GUI.BeginScrollView(view, _scroll, inner);
            float cy = fs * 0.2f;
            if (_page == PVan || _page == PAll || _page == PFav) { }
            DrawRows(ref cy, vw, fs);
            GUI.EndScrollView();
            _h = cy + fs * 3f;
            if (_scrollTo >= 0f) { _scroll.y = Mathf.Max(0f, _scrollTo - fs * 4f); _scrollTo = -1f; }
            else if (_scrollTo == -2f) _scrollTo = -3f; // wait one pass: rows draw first and report their y
            else if (_scrollTo == -3f) _scrollTo = -1f;
        }

        private readonly List<string> _sectionKeys = new List<string>();

        private int CountMod(string group)
        {
            IList<BtcHotkeys.Def> defs = BtcHotkeys.Defs;
            int n = 0;
            for (int i = 0; i < defs.Count; i++)
            {
                if (defs[i].group != group) continue;
                if (_page == PFav && !IsFav("m:" + defs[i].id)) continue;
                if (ModVisible(defs[i])) n++;
            }
            return n;
        }

        private int CountVan(string cat)
        {
            List<BtcVanillaBinds.Act> acts = BtcVanillaBinds.Actions();
            int n = 0;
            for (int i = 0; i < acts.Count; i++)
            {
                if (acts[i].cat != cat) continue;
                if (_page == PFav && !IsFav("v:" + acts[i].name)) continue;
                if (VanVisible(acts[i])) n++;
            }
            return n;
        }

        private bool AllOpen()
        {
            for (int i = 0; i < _sectionKeys.Count; i++) if (!BtcUi.IsOpen(_sectionKeys[i])) return false;
            return true;
        }

        private void DrawRows(ref float y, float w, int fs)
        {
            IList<BtcHotkeys.Def> defs = BtcHotkeys.Defs;
            int shown = 0;
            _sectionKeys.Clear();
            if (_page != PVan)
            {
                string lastGroup = null, lastSub = null;
                bool groupOpen = true;
                for (int i = 0; i < defs.Count; i++)
                {
                    BtcHotkeys.Def d = defs[i];
                    if (_page != PAll && _page != PFav && PageOf(d.group) != _page) continue;
                    if (_page == PFav && !IsFav("m:" + d.id)) continue;
                    if (!ModVisible(d)) continue;
                    if (d.group != lastGroup)
                    {
                        lastGroup = d.group; lastSub = null;
                        y += fs * 0.5f;
                        string fk = "hk:" + d.group;
                        _sectionKeys.Add(fk);
                        groupOpen = BtcUi.Fold(ref y, w, fs, fk, BtcLang.Tr(d.group), CountMod(d.group));
                    }
                    if (!groupOpen) continue;
                    if (!string.IsNullOrEmpty(d.sub) && d.sub != lastSub)
                    {
                        lastSub = d.sub;
                        Txt(new Rect(fs * 1.6f, y, w - fs * 3f, fs * 1.6f), BtcLang.Tr(d.sub), _dim, new Color(1f, 1f, 1f, 0.5f));
                        y += fs * 1.7f;
                    }
                    DrawModRow(ref y, w, fs, d);
                    shown++;
                }
            }
            if (_page == PVan || _page == PAll || _page == PFav)
            {
                List<BtcVanillaBinds.Act> acts = BtcVanillaBinds.Actions();
                if (acts.Count == 0 && _page == PVan)
                {
                    BtcUi.Note(ref y, w, fs, Tt("The vanilla bindings are not loaded yet. Open this tab once the game finished loading its controls (main menu or a game).", "Ванильные бинды ещё не загружены. Открой вкладку, когда игра загрузит управление (главное меню или игра)."));
                }
                string lastCat = null;
                bool catOpen = true;
                for (int i = 0; i < acts.Count; i++)
                {
                    BtcVanillaBinds.Act a = acts[i];
                    if (_page == PFav && !IsFav("v:" + a.name)) continue;
                    if (!VanVisible(a)) continue;
                    if (a.cat != lastCat)
                    {
                        lastCat = a.cat;
                        y += fs * 0.5f;
                        string fk = "hkv:" + a.cat;
                        _sectionKeys.Add(fk);
                        catOpen = BtcUi.Fold(ref y, w, fs, fk, Tt("Vanilla: ", "Ваниль: ") + a.cat, CountVan(a.cat));
                    }
                    if (!catOpen) { shown++; continue; }
                    DrawVanillaRow(ref y, w, fs, a);
                    shown++;
                }
                if (_page == PVan && acts.Count > 0)
                {
                    y += fs * 0.6f;
                    float bw = fs * 16f;
                    Rect rr = new Rect(fs, y, bw, fs * 2f);
                    BtcReplayUI.Fill(rr, new Color(1f, 1f, 1f, rr.Contains(Event.current.mousePosition) ? 0.12f : 0.06f));
                    bool conf = _confirm == "vanilla" && Time.unscaledTime < _confirmUntil;
                    Txt(rr, conf ? Tt("Click again to confirm", "Ещё раз для подтверждения") : Tt("Reset vanilla binds to defaults", "Сбросить ванильные бинды"), _chip, conf ? new Color(1f, 0.45f, 0.45f, 1f) : Color.white);
                    if (BtcSfx.Btn(rr, "", BtcReplayUI.sBtn))
                    {
                        _resetOpen = true; _resetMod = false; _resetVan = true;
                    }
                    y += fs * 2.4f;
                }
            }
            if (shown == 0 && _page != PVan)
            {
                BtcUi.Note(ref y, w, fs, _page == PFav ? Tt("No favourites yet: click the star of a bind.", "Избранного пока нет: нажми звёздочку у бинда.") : Tt("Nothing found.", "Ничего не найдено."));
            }
        }

        private bool ModVisible(BtcHotkeys.Def d)
        {
            if (_fConf && !(d.kind == BtcHotkeys.Kind.Key && (BtcHotkeys.IsConflict(d.id) || HasOverlap(d)))) return false;
            if (_fChanged && BtcHotkeys.IsDefault(d.id)) return false;
            if (_q.Length > 0)
            {
                string t = d.Title + " " + d.id + " " + BtcLang.Tr(d.group) + " " + (d.kind == BtcHotkeys.Kind.Key ? BtcHotkeys.ComboText(d.id) : "");
                if (!Matches(t)) return false;
            }
            return true;
        }

        /// <summary>A mod bind shares a key combo with a vanilla bind (shown in orange).</summary>
        private bool HasOverlap(BtcHotkeys.Def d)
        {
            if (d.kind != BtcHotkeys.Kind.Key || d.any) return false;
            KeyCode[][] alts = BtcHotkeys.GetAlts(d.id);
            for (int i = 0; i < alts.Length; i++) if (VanillaUsing(alts[i]).Length > 0) return true;
            return false;
        }

        private bool VanVisible(BtcVanillaBinds.Act a)
        {
            if (_fChanged) return false;
            if (_fConf)
            {
                List<BindingsProvider.BindItem> ii = BtcVanillaBinds.Items(a.name);
                bool ov = false;
                for (int i = 0; ii != null && i < ii.Count && !ov; i++) { KeyCode[] c = BtcVanillaBinds.Combo(ii[i]); ov = c != null && ModUsing(c).Length > 0; }
                if (!ov) return false;
            }
            if (_q.Length == 0) return true;
            string t = a.title + " " + a.cat + " vanilla";
            List<BindingsProvider.BindItem> its = BtcVanillaBinds.Items(a.name);
            if (its != null) for (int i = 0; i < its.Count; i++) t += " " + BtcVanillaBinds.Label(its[i]);
            return Matches(t);
        }

        // ------------------------------------------------------------------ rows
        private enum ChipKind { Normal, Conflict, Overlap, Listening, Dim, Add }
        private static readonly Color Orange = new Color(1f, 0.6f, 0.2f, 1f);

        private Color RowTint(string key, float h, ref Rect row)
        {
            bool flash = key == _flashId && Time.unscaledTime < _flashUntil;
            bool found = _fIds.Contains(key) && Time.unscaledTime - _fAt < 8f;
            Event e = Event.current;
            float a = row.Contains(e.mousePosition) ? 0.06f : 0.03f;
            Color c = new Color(1f, 1f, 1f, a);
            if (found) c = new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.12f);
            if (flash) { float p = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f); c = new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.15f + 0.25f * p); }
            if (key == _jumpId && _scrollTo == -3f) { }
            return c;
        }

        private bool Star(Rect r, string key)
        {
            bool on = IsFav(key);
            float g = r.width * 0.62f;
            Rect gr = new Rect(r.center.x - g * 0.5f, r.center.y - g * 0.5f, g, g);
            bool hover = r.Contains(Event.current.mousePosition);
            BtcReplayIcons.Glyph(BtcReplayIcons.Star, gr, on ? new Color(1f, 0.85f, 0.4f, 1f) : new Color(1f, 1f, 1f, hover ? 0.7f : 0.22f));
            BtcReplayIcons.Hint(r, on ? Tt("Remove from favourites", "Убрать из избранного") : Tt("Add to favourites", "В избранное"));
            if (BtcSfx.Btn(r, "", BtcReplayUI.sBtn)) { if (on) _favs.Remove(key); else _favs.Add(key); SaveFavs(); BtcSfx.Switch(!on); BtcUndo.Push("hotkeys", Tt("Favourite: ", "Избранное: ") + NameOf(key), delegate { if (on) _favs.Add(key); else _favs.Remove(key); SaveFavs(); }); return true; }
            return false;
        }

        /// <summary>Lays chips left to right and wraps; returns the rects and the block height.</summary>
        private static float Flow(float x, float w, float h, float gap, float[] widths, Rect[] rects, float y0)
        {
            float cx = x, cy = y0;
            for (int i = 0; i < widths.Length; i++)
            {
                if (cx > x && cx + widths[i] > x + w) { cx = x; cy += h + gap; }
                rects[i] = new Rect(cx, cy, widths[i], h);
                cx += widths[i] + gap;
            }
            return cy + h - y0;
        }

        /// <summary>One key chip. Returns 1 = clicked, 2 = remove (right click or the cross).</summary>
        private int Chip(Rect r, string text, ChipKind k, string tip, bool removable, bool flash)
        {
            Event e = Event.current;
            bool hover = r.Contains(e.mousePosition);
            Color bg = new Color(1f, 1f, 1f, hover ? 0.17f : 0.1f), tc = Color.white;
            if (k == ChipKind.Conflict) { bg = new Color(1f, 0.35f, 0.35f, hover ? 0.32f : 0.22f); tc = new Color(1f, 0.8f, 0.8f, 1f); }
            else if (k == ChipKind.Listening) { float p = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f); bg = new Color(1f, 0.65f, 0.2f, 0.25f + 0.2f * p); tc = new Color(1f, 0.85f, 0.5f, 1f); }
            else if (k == ChipKind.Overlap) { bg = new Color(Orange.r, Orange.g, Orange.b, hover ? 0.34f : 0.24f); tc = new Color(1f, 0.88f, 0.7f, 1f); }
            else if (k == ChipKind.Dim) { bg = new Color(1f, 1f, 1f, 0.05f); tc = new Color(1f, 1f, 1f, 0.45f); }
            else if (k == ChipKind.Add) { bg = new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, hover ? 0.3f : 0.14f); }
            if (flash) bg = new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.4f);
            BtcReplayUI.Fill(r, bg);
            float xz = removable ? _fs * 0.95f : 0f;
            if (k == ChipKind.Add)
            {
                float g = r.height * 0.5f;
                BtcReplayIcons.Glyph(BtcReplayIcons.Plus, new Rect(r.center.x - g * 0.5f, r.center.y - g * 0.5f, g, g), hover ? Color.white : BtcMenu.Accent);
            }
            else Txt(new Rect(r.x, r.y, r.width - xz, r.height), text, _chip, tc);
            if (tip != null) BtcReplayIcons.Hint(r, tip);
            int res = 0;
            if (removable)
            {
                Rect xr = new Rect(r.xMax - xz, r.y, xz, r.height);
                if (hover) BtcReplayIcons.Glyph(BtcReplayIcons.Cross, new Rect(xr.x + xz * 0.1f, xr.center.y - xz * 0.35f, xz * 0.7f, xz * 0.7f), new Color(1f, 1f, 1f, xr.Contains(e.mousePosition) ? 1f : 0.45f));
                if (BtcSfx.Btn(xr, "", BtcReplayUI.sBtn)) res = 2;
                r = new Rect(r.x, r.y, r.width - xz, r.height);
            }
            if (BtcSfx.Btn(r, "", BtcReplayUI.sBtn) && res == 0) res = 1;
            if (e.type == EventType.MouseDown && e.button == 1 && r.Contains(e.mousePosition) && k != ChipKind.Add) { res = 2; e.Use(); }
            return res;
        }

        private float ChipW(string s) { return Mathf.Max(_fs * 2.6f, _chip.CalcSize(new GUIContent(s)).x + _fs * 1.3f); }

        private void DrawModRow(ref float y, float w, int fs, BtcHotkeys.Def d)
        {
            Event e = Event.current;
            float pad = fs, rowW = w - pad * 2f, rh = fs * 2.3f;
            float starW = fs * 1.7f, titleW = Mathf.Max(fs * 11f, rowW * 0.34f), resetW = fs * 2f;
            float cx = pad + starW + titleW, cw = pad + rowW - cx - resetW;
            bool def = BtcHotkeys.IsDefault(d.id);
            float height = rh;
            Rect[] rects = null; string[] labels = null; ChipKind[] kinds = null; string[] tips = null; int nAlt = 0;
            bool capturing = BtcHotkeys.Capturing && BtcHotkeys.CaptureId == d.id;

            if (d.kind == BtcHotkeys.Kind.Key)
            {
                KeyCode[][] alts = d.any ? new KeyCode[][] { BtcHotkeys.GetKeys(d.id, null) } : BtcHotkeys.GetAlts(d.id);
                if (d.any && alts[0].Length == 0) alts = new KeyCode[0][];
                nAlt = alts.Length;
                int extra = d.any ? (nAlt == 0 ? 1 : 0) : 1; // the "+" / empty chip
                int total = nAlt + extra;
                labels = new string[total]; kinds = new ChipKind[total]; tips = new string[total];
                float[] widths = new float[total];
                for (int i = 0; i < nAlt; i++)
                {
                    labels[i] = BtcHotkeys.ComboLabel(alts[i], d.any);
                    bool cl = capturing && BtcHotkeys.CaptureAlt == i;
                    string vv = d.any ? "" : VanillaUsing(alts[i]);
                    kinds[i] = cl ? ChipKind.Listening : (!d.any && ChipConflict(d.id, alts[i]) ? ChipKind.Conflict : (vv.Length > 0 ? ChipKind.Overlap : ChipKind.Normal));
                    if (cl) labels[i] = Tt("Press a key... (Esc)", "Нажми клавишу... (Esc)");
                    string ov = d.any ? "" : OverlapTip(d.id, alts[i]);
                    tips[i] = Tt("Click: change   Right click or x: remove", "Клик: изменить   Правый клик или x: убрать")
                        + (ov.Length > 0 ? Tt("   CONFLICT with: ", "   КОНФЛИКТ с: ") + ov : "") + (vv.Length > 0 ? Tt("   OVERLAP with vanilla: ", "   ПЕРЕСЕЧЕНИЕ с ванилью: ") + vv : "");
                    widths[i] = ChipW(labels[i]) + fs * 0.95f;
                }
                if (extra == 1)
                {
                    bool addCap = capturing && (BtcHotkeys.CaptureAlt < 0 || BtcHotkeys.CaptureAlt >= nAlt);
                    if (d.any || nAlt == 0)
                    {
                        labels[total - 1] = addCap ? Tt("Press a key... (Esc)", "Нажми клавишу... (Esc)") : Tt("not set", "не задано");
                        kinds[total - 1] = addCap ? ChipKind.Listening : ChipKind.Dim;
                        tips[total - 1] = Tt("Click and press the key(s)", "Клик и нажми клавиши");
                        widths[total - 1] = ChipW(labels[total - 1]);
                    }
                    else
                    {
                        labels[total - 1] = addCap ? Tt("Press a key... (Esc)", "Нажми клавишу... (Esc)") : "+";
                        kinds[total - 1] = addCap ? ChipKind.Listening : ChipKind.Add;
                        tips[total - 1] = Tt("Add another key combo for this function", "Добавить ещё одну комбинацию для этой функции");
                        widths[total - 1] = addCap ? ChipW(labels[total - 1]) : fs * 2.2f;
                    }
                }
                rects = new Rect[total];
                height = Mathf.Max(rh, Flow(cx, cw, fs * 1.7f, 4f, widths, rects, 0f) + fs * 0.6f);
                for (int i = 0; i < total; i++) rects[i] = new Rect(rects[i].x, y + fs * 0.3f + rects[i].y, rects[i].width, rects[i].height);
            }

            Rect row = new Rect(pad, y, rowW, height);
            string rkey = "m:" + d.id;
            string idKey = d.id;
            Color tint = RowTint(idKey, height, ref row);
            BtcReplayUI.Fill(row, tint);
            if (_jumpId == idKey && _scrollTo == -3f) _scrollTo = y;
            if (d.kind == BtcHotkeys.Kind.Key && BtcHotkeys.IsConflict(d.id)) BtcReplayUI.Fill(new Rect(row.x, row.y, 3f, row.height), new Color(1f, 0.4f, 0.4f, 1f));
            else if (HasOverlap(d)) BtcReplayUI.Fill(new Rect(row.x, row.y, 3f, row.height), Orange);
            else if (!def) BtcReplayUI.Fill(new Rect(row.x, row.y, 3f, row.height), new Color(1f, 0.85f, 0.4f, 1f));
            Star(new Rect(row.x + fs * 0.3f, row.y + (rh - fs * 1.4f) * 0.5f, fs * 1.4f, fs * 1.4f), rkey);
            Txt(new Rect(pad + starW, y, titleW, rh), d.Title, _lbl, def ? Color.white : new Color(1f, 0.9f, 0.55f, 1f));

            if (d.kind == BtcHotkeys.Kind.Key)
            {
                for (int i = 0; i < rects.Length; i++)
                {
                    bool isAltChip = i < nAlt;
                    bool listening = kinds[i] == ChipKind.Listening;
                    int r = Chip(rects[i], labels[i], kinds[i], tips[i], isAltChip && !listening, d.id == _flashId && Time.unscaledTime < _flashUntil && isAltChip && _fKey != KeyCode.None && AltHasKey(d.id, i, _fKey));
                    if (r == 1)
                    {
                        if (listening) BtcHotkeys.CancelCapture();
                        else BtcHotkeys.BeginCapture(d.id, isAltChip ? i : -1);
                    }
                    else if (r == 2 && isAltChip)
                    {
                        BtcSfx.Delete(); if (d.any) BtcHotkeys.Unbind(d.id); else BtcHotkeys.RemoveAlt(d.id, i);
                    }
                }
            }
            else if (d.kind == BtcHotkeys.Kind.Bool)
            {
                bool on = BtcHotkeys.GetBool(d.id, true);
                Rect sw = new Rect(cx, y + (rh - fs * 1.2f) * 0.5f, fs * 2.4f, fs * 1.2f);
                BtcReplayUI.Fill(sw, on ? new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.9f) : new Color(1f, 1f, 1f, 0.16f));
                float k = sw.height - 4f;
                BtcReplayUI.Fill(new Rect(on ? sw.xMax - k - 2f : sw.x + 2f, sw.y + 2f, k, k), on ? new Color(0.03f, 0.05f, 0.04f, 1f) : new Color(1f, 1f, 1f, 0.75f));
                Txt(new Rect(sw.xMax + fs * 0.6f, y, fs * 6f, rh), BtcLang.Tr(on ? "On" : "Off"), _lbl, new Color(1f, 1f, 1f, 0.8f));
                if (BtcSfx.Btn(new Rect(cx, y, fs * 9f, rh), "", BtcReplayUI.sBtn)) { BtcHotkeys.ToggleBool(d.id); BtcSfx.Switch(!on); }
            }
            else if (d.kind == BtcHotkeys.Kind.Mode)
            {
                bool tog = BtcHotkeys.GetString(d.id, "hold").Equals("toggle", StringComparison.OrdinalIgnoreCase);
                string t = tog ? Tt("Toggle", "Переключатель") : Tt("Hold", "Удержание");
                Rect cr = new Rect(cx, y + fs * 0.3f, ChipW(t), fs * 1.7f);
                if (Chip(cr, t, ChipKind.Normal, tog ? Tt("Press to switch on / off", "Нажал - включил / выключил") : Tt("Active while the key is held", "Активно, пока клавиша зажата"), false, false) == 1) BtcHotkeys.ToggleMode(d.id);
            }
            else if (d.min == 0f && d.max == 1f && d.step >= 1f)
            {
                // a 0 / 1 setting is a switch, not a slider
                bool on = BtcHotkeys.GetFloat(d.id, 1f) >= 0.5f;
                Rect sw = new Rect(cx, y + (rh - fs * 1.2f) * 0.5f, fs * 2.4f, fs * 1.2f);
                BtcReplayUI.Fill(sw, on ? new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.9f) : new Color(1f, 1f, 1f, 0.16f));
                float k = sw.height - 4f;
                BtcReplayUI.Fill(new Rect(on ? sw.xMax - k - 2f : sw.x + 2f, sw.y + 2f, k, k), on ? new Color(0.03f, 0.05f, 0.04f, 1f) : new Color(1f, 1f, 1f, 0.75f));
                Txt(new Rect(sw.xMax + fs * 0.6f, y, fs * 6f, rh), BtcLang.Tr(on ? "On" : "Off"), _lbl, new Color(1f, 1f, 1f, 0.8f));
                if (BtcSfx.Btn(new Rect(cx, y, fs * 9f, rh), "", BtcReplayUI.sBtn)) { BtcHotkeys.SetFloat(d.id, on ? 0f : 1f); BtcSfx.Switch(!on); }
            }
            else
            {
                float v = BtcHotkeys.GetFloat(d.id, 1f);
                float bs = fs * 1.5f, sx = cx;
                Rect mr = new Rect(sx, y + (rh - bs) * 0.5f, bs, bs);
                Rect track = new Rect(mr.xMax + fs * 0.6f, y + rh * 0.5f - fs * 0.2f, Mathf.Min(cw * 0.5f, fs * 13f), fs * 0.4f);
                Rect pr = new Rect(track.xMax + fs * 0.6f, mr.y, bs, bs);
                float nv = Slider(track, d.id, v, d.min, d.max, d.step, fs);
                if (StepBtn(mr, false)) nv = Mathf.Clamp(Snap(v - d.step, d.step, d.min), d.min, d.max);
                if (StepBtn(pr, true)) nv = Mathf.Clamp(Snap(v + d.step, d.step, d.min), d.min, d.max);
                if (Mathf.Abs(nv - v) > 0.0001f) { BtcHotkeys.SetFloat(d.id, nv); BtcSfx.Tick(); }
                string vt = d.prefix + nv.ToString(d.format, CultureInfo.InvariantCulture);
                Rect vr = new Rect(pr.xMax + fs * 0.6f, mr.y, Mathf.Max(fs * 4f, ChipW(vt)), bs);
                BtcReplayUI.Fill(vr, new Color(1f, 1f, 1f, 0.08f));
                Txt(vr, vt, _chip, Color.white);
            }

            // reset
            Rect rr = new Rect(row.xMax - resetW, y + (rh - fs * 1.5f) * 0.5f, fs * 1.5f, fs * 1.5f);
            if (!def)
            {
                string dt = d.kind == BtcHotkeys.Kind.Key ? Tt("Reset to default: ", "Сбросить на: ") + (d.def.Length == 0 ? Tt("not set", "не задано") : d.def.Replace("Left", "").Replace("|", " | ").Replace("+", " + ")) : Tt("Reset to default", "Сбросить на значение по умолчанию");
                if (BtcReplayIcons.Plain(rr, BtcReplayIcons.Refresh, new Color(1f, 0.9f, 0.55f, 1f), dt)) BtcHotkeys.ResetBind(d.id);
            }
            y += height + 3f;
        }

        private static bool AltHasKey(string id, int alt, KeyCode k)
        {
            KeyCode[][] a = BtcHotkeys.GetAlts(id);
            return alt >= 0 && alt < a.Length && Array.IndexOf(a[alt], k) >= 0;
        }

        private static float Snap(float v, float step, float min)
        {
            return step > 0f ? min + Mathf.Round((v - min) / step) * step : v;
        }

        /// <summary>Small flat minus / plus button for the sliders.</summary>
        private bool StepBtn(Rect r, bool plus)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            BtcReplayUI.Fill(r, new Color(1f, 1f, 1f, hover ? 0.18f : 0.08f));
            Color c = hover ? Color.white : new Color(1f, 1f, 1f, 0.7f);
            BtcReplayUI.Fill(new Rect(r.x + r.width * 0.28f, r.center.y - 1f, r.width * 0.44f, 2f), c);
            if (plus) BtcReplayUI.Fill(new Rect(r.center.x - 1f, r.y + r.height * 0.28f, 2f, r.height * 0.44f), c);
            return BtcSfx.Btn(r, "", BtcReplayUI.sBtn);
        }

        private float Slider(Rect track, string id, float v, float min, float max, float step, int fs)
        {
            Event e = Event.current;
            Rect hit = new Rect(track.x - 6f, track.y - fs * 0.6f, track.width + 12f, track.height + fs * 1.2f);
            bool hov = hit.Contains(e.mousePosition) || _dragId == id;
            BtcReplayUI.Fill(track, new Color(1f, 1f, 1f, 0.12f));
            float f = Mathf.Clamp01((v - min) / Mathf.Max(0.0001f, max - min));
            BtcReplayUI.Fill(new Rect(track.x, track.y, track.width * f, track.height), new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, hov ? 1f : 0.8f));
            float kw = fs * 0.55f, kh = fs * 1.3f, kx = Mathf.Clamp(track.x + track.width * f - kw * 0.5f, track.x, track.xMax - kw);
            if (hov) BtcReplayUI.Fill(new Rect(kx - 2f, track.center.y - kh * 0.5f - 2f, kw + 4f, kh + 4f), new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.25f));
            BtcReplayUI.Fill(new Rect(kx, track.center.y - kh * 0.5f, kw, kh), hov ? Color.white : new Color(1f, 1f, 1f, 0.85f));
            if (e.rawType == EventType.MouseUp) _dragId = null;
            if (e.type == EventType.MouseDown && hit.Contains(e.mousePosition) && e.button == 0) { _dragId = id; }
            if (_dragId == id && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
            {
                float t = Mathf.Clamp01((e.mousePosition.x - track.x) / track.width);
                float nv = min + t * (max - min);
                if (step > 0f) nv = Mathf.Round(nv / step) * step;
                v = Mathf.Clamp(nv, min, max);
                e.Use();
            }
            return v;
        }

        private void DrawVanillaRow(ref float y, float w, int fs, BtcVanillaBinds.Act a)
        {
            float pad = fs, rowW = w - pad * 2f, rh = fs * 2.3f;
            float starW = fs * 1.7f, titleW = Mathf.Max(fs * 11f, rowW * 0.34f);
            float cx = pad + starW + titleW, cw = pad + rowW - cx - fs * 2f;
            List<BindingsProvider.BindItem> its = BtcVanillaBinds.Items(a.name);
            int nItems = its == null ? 0 : its.Count;
            string capId = "v:" + a.name;
            bool capturing = BtcHotkeys.Capturing && BtcHotkeys.CaptureId == capId;
            // empty slots are not shown as chips: a slot with no entries is "not set" and shows as an add chip
            List<int> idx = new List<int>();
            for (int i = 0; i < nItems; i++) if (its[i]._entries != null && its[i]._entries.Count > 0) idx.Add(i);
            int total = idx.Count + 1;
            string[] labels = new string[total]; ChipKind[] kinds = new ChipKind[total]; string[] tips = new string[total]; float[] widths = new float[total];
            for (int n = 0; n < idx.Count; n++)
            {
                BindingsProvider.BindItem it = its[idx[n]];
                KeyCode[] c = BtcVanillaBinds.Combo(it);
                bool cl = capturing && BtcHotkeys.CaptureAlt == idx[n];
                labels[n] = cl ? Tt("Press a key... (Esc)", "Нажми клавишу... (Esc)") : BtcVanillaBinds.Label(it);
                string mt = c != null ? ModUsing(c) : "";
                kinds[n] = cl ? ChipKind.Listening : (c == null ? ChipKind.Dim : (mt.Length > 0 ? ChipKind.Overlap : ChipKind.Normal));
                tips[n] = c == null ? Tt("A mouse bind: change it in the vanilla options menu", "Бинд мыши: меняется только в ванильных настройках")
                    : Tt("Click: change   Right click or x: remove", "Клик: изменить   Правый клик или x: убрать") + (mt.Length > 0 ? Tt("   Also in the mod: ", "   Ещё в моде: ") + mt : "");
                widths[n] = ChipW(labels[n]) + (c != null ? fs * 0.95f : 0f);
            }
            bool addCap = capturing && BtcHotkeys.CaptureAlt >= nItems;
            labels[total - 1] = addCap ? Tt("Press a key... (Esc)", "Нажми клавишу... (Esc)") : "+";
            kinds[total - 1] = addCap ? ChipKind.Listening : ChipKind.Add;
            tips[total - 1] = Tt("Add another key for this action (vanilla: a key or modifier + key)", "Добавить ещё клавишу для действия (в ваниле: клавиша или модификатор + клавиша)");
            widths[total - 1] = addCap ? ChipW(labels[total - 1]) : fs * 2.2f;
            Rect[] rects = new Rect[total];
            float height = Mathf.Max(rh, Flow(cx, cw, fs * 1.7f, 4f, widths, rects, 0f) + fs * 0.6f);
            Rect row = new Rect(pad, y, rowW, height);
            string key = "v:" + a.name;
            BtcReplayUI.Fill(row, RowTint(key, height, ref row));
            if (_jumpId == key && _scrollTo == -3f) _scrollTo = y;
            if (Array.IndexOf(kinds, ChipKind.Overlap) >= 0) BtcReplayUI.Fill(new Rect(row.x, row.y, 3f, row.height), Orange);
            Star(new Rect(row.x + fs * 0.3f, row.y + (rh - fs * 1.4f) * 0.5f, fs * 1.4f, fs * 1.4f), key);
            Txt(new Rect(pad + starW, y, titleW, rh), a.title, _lbl, Color.white);
            for (int i = 0; i < total; i++)
            {
                bool isItem = i < idx.Count;
                bool editable = isItem && BtcVanillaBinds.Editable(its[idx[i]]);
                Rect cr = new Rect(rects[i].x, y + fs * 0.3f + rects[i].y, rects[i].width, rects[i].height);
                int r = Chip(cr, labels[i], kinds[i], tips[i], editable && kinds[i] != ChipKind.Listening, false);
                if (r == 1 && (editable || !isItem))
                {
                    if (kinds[i] == ChipKind.Listening) BtcHotkeys.CancelCapture();
                    else BtcHotkeys.BeginCapture(capId, isItem ? idx[i] : nItems);
                }
                else if (r == 2 && editable)
                {
                    if (BtcVanillaBinds.Set(a.name, idx[i], null)) { BtcSfx.Delete(); Toast(Tt("Vanilla bind removed", "Ванильный бинд убран")); }
                }
            }
            y += height + 3f;
        }

        private string ModUsing(KeyCode[] combo)
        {
            List<string> l;
            if (!_modOwners.TryGetValue(BtcHotkeys.ComboKey(combo), out l)) return "";
            string s = "";
            for (int i = 0; i < l.Count; i++) { BtcHotkeys.Def d = BtcHotkeys.GetDef(l[i]); s += (s.Length > 0 ? ", " : "") + (d != null ? d.Title : l[i]); }
            return s;
        }

        private void OnVanillaCapture(string action, int alt, KeyCode[] combo)
        {
            for (int i = 0; i < combo.Length; i++)
            {
                if (combo[i] >= KeyCode.Mouse0 && combo[i] <= KeyCode.Mouse6) { BtcSfx.Warn(); Toast(Tt("Vanilla binds take keyboard keys only", "Ванильные бинды принимают только клавиши клавиатуры")); return; }
            }
            if (!BtcVanillaBinds.Fits(combo)) { BtcSfx.Warn(); Toast(Tt("Vanilla supports one key or a modifier + key", "Ваниль поддерживает одну клавишу или модификатор + клавишу")); return; }
            string clash = VanillaUsing(combo);
            if (BtcVanillaBinds.Set(action, alt, combo))
            {
                _cacheVer = -1;
                Toast(Tt("Vanilla bind saved", "Ванильный бинд сохранён") + (clash.Length > 0 ? Tt("  (also used by: ", "  (ещё занято: ") + clash + ")" : ""));
            }
            else { BtcSfx.Warn(); Toast(Tt("Could not save the vanilla bind (controls not loaded?)", "Не удалось сохранить ванильный бинд (управление не загружено?)")); }
        }
    }
}
