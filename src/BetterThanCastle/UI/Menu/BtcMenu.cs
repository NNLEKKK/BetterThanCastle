using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// A tab of the main mod menu in the new architecture: one class per tab, the shell (BtcMenu) draws the window chrome (icon rail, header)
    /// and hands the tab its content rectangle. The tab owns its scrolling and its state. Legacy tabs (packs, overlays, BetterConnection, hotkeys,
    /// gameplay, share, profile, duel) are still drawn by ResourcePackManager.DrawMenu until they are migrated one by one: add a BtcMenuTab subclass,
    /// register it in BtcMenu.EnsureRegistered and remove its branch from the legacy dispatch (see docs/menu.md).
    /// </summary>
    internal abstract class BtcMenuTab
    {
        /// <summary>Legacy tab id (0 packs, 1 overlays, 2 BC, 3 hotkeys, 4 gameplay, 5 share, 6 profile, 7 replay, 8 duel).</summary>
        internal abstract int Id { get; }
        internal abstract bool IsActive { get; }
        /// <summary>Undo scope of the tab (Ctrl+Z): see BtcUndo. null = the tab has nothing to undo yet (every new tab must define one).</summary>
        internal virtual string UndoScope { get { return null; } }
        /// <summary>area = the content rectangle under the header (the shell drew the background); fs = base font size.</summary>
        internal abstract void Draw(Rect area, int fs);
    }

    /// <summary>The menu shell: tab registry, icon rail, header and the shared look (colours below). Draw calls come from ResourcePackManager.DrawMenu.</summary>
    internal static class BtcMenu
    {
        internal static Color Accent = new Color(0.55f, 0.85f, 0.55f, 1f);   // the UI editor can recolour it
        /// <summary>The accent with another alpha (use instead of a hard-coded green so the UI editor colour reaches everywhere).</summary>
        internal static Color AccentA(float a) { return new Color(Accent.r, Accent.g, Accent.b, a); }
        internal static readonly Color Bg = new Color(0.035f, 0.042f, 0.052f, 0.985f);
        internal static readonly Color RailBg = new Color(0.02f, 0.025f, 0.032f, 1f);
        internal static readonly Color Line = new Color(1f, 1f, 1f, 0.08f);

        private static readonly List<BtcMenuTab> Tabs = new List<BtcMenuTab>();
        private static bool _registered;

        private static void EnsureRegistered()
        {
            if (_registered) return;
            _registered = true;
            Tabs.Add(new BtcReplayTab());
            Tabs.Add(BtcPacksTab.Instance);
            Tabs.Add(BtcBcTab.Instance);
            Tabs.Add(BtcHotkeysTab.Instance);
            Tabs.Add(BtcVisualTab.Instance);
            Tabs.Add(BtcProfileTab.Instance);
            Tabs.Add(BtcReportTab.Instance);
            Tabs.Add(BtcChangelogTab.Instance);
            Tabs.Add(BtcStatsTab.Instance);
            Tabs.Add(BtcShareTab.Instance);
            Tabs.Add(BtcDuelTab.Instance);
            Tabs.Add(BtcGameplayTab.Instance);
            Tabs.Add(BtcUiEditorTab.Instance);
            Tabs.Add(BtcNoticesTab.Instance);
        }

        /// <summary>Id of the tab that is open (kept by ResourcePackManager.SelectTab).</summary>
        internal static int CurrentId;

        /// <summary>The migrated tab that is open now, or null (a legacy tab or none).</summary>
        internal static BtcMenuTab ActiveTab()
        {
            EnsureRegistered();
            for (int i = 0; i < Tabs.Count; i++) if (Tabs[i].IsActive) return Tabs[i];
            return null;
        }

        // legacy tab id -> rail icon
        private static readonly int[] IconOf =
        {
            BtcReplayIcons.Blueprint, BtcReplayIcons.Eye, BtcReplayIcons.Wave, BtcReplayIcons.Keys, BtcReplayIcons.Gear,
            BtcReplayIcons.Share, BtcReplayIcons.Group, BtcReplayIcons.Play, BtcReplayIcons.Swords,
            BtcReplayIcons.Bug, BtcReplayIcons.News, BtcReplayIcons.Chart, BtcReplayIcons.Layout, BtcReplayIcons.Chat
        };

        private static int Fs { get { return BtcUiEdit.MenuFs; } }
        internal static float RailWidth(int fs) { return BtcUiEdit.RailMode == 0 ? fs * 3.4f : fs * (BtcUiEdit.RailMode == 1 ? 10.5f : 9f); }

        /// <summary>A tab handled PgDown itself (a focused text field swallows it); the Update hotkey must not toggle the menu again in the next frames.</summary>
        internal static int PgDownFrame = -100;
        internal static bool PgDownRecentlyHandled { get { return Time.frameCount - PgDownFrame <= 2; } }

        private static readonly KeyCode[] DefaultMenuKeys = { KeyCode.PageDown };

        /// <summary>The bound open/close keys (Advanced Hotkeys, id menu_toggle); PgDown when the bind is cleared so the menu can never be locked out.</summary>
        internal static KeyCode[] MenuKeys
        {
            get
            {
                try { KeyCode[] k = BtcHotkeys.GetKeys("menu_toggle", DefaultMenuKeys); if (k != null && k.Length > 0) return k; }
                catch (Exception) { }
                return DefaultMenuKeys;
            }
        }

        /// <summary>The last key of the combo: what a focused text field must recognise to close the menu.</summary>
        internal static KeyCode MenuKeyMain { get { KeyCode[] k = MenuKeys; return k[k.Length - 1]; } }

        /// <summary>Modifiers held and the main key went down this frame (no hotkey suppression: the menu key works everywhere).</summary>
        internal static bool MenuKeyDown()
        {
            KeyCode[] k = MenuKeys;
            for (int i = 0; i < k.Length - 1; i++) if (!Input.GetKey(k[i])) return false;
            return Input.GetKeyDown(k[k.Length - 1]);
        }

        // ---- in-tab page rails (Visuals, Hotkeys, Gameplay, Packs, Profile, UI editor): icons / icons + words / words, set in the UI editor
        internal static float SubRailWidth(int fs) { int m = BtcUiEdit.SubRailMode; return m == 0 ? fs * 3.2f : fs * (m == 1 ? 9.5f : 8f); }

        internal static void RailGeometry(Rect r, int fs, ref float item, ref float itemW, ref float x)
        {
            if (BtcUiEdit.SubRailMode == 0) return;
            item = fs * 2.3f; itemW = r.width * 0.9f; x = r.x + r.width * 0.05f;
        }

        internal static void RailGlyph(Rect ir, int fs, int icon, string name, bool on, bool hover)
        {
            Color c = on ? Accent : new Color(1f, 1f, 1f, hover ? 0.95f : 0.55f);
            int mode = BtcUiEdit.SubRailMode;
            float g = ir.height * 0.55f;
            if (mode == 0) { BtcReplayIcons.Glyph(icon, new Rect(ir.center.x - g * 0.5f, ir.center.y - g * 0.5f, g, g), c); return; }
            float tx = ir.x + fs * 0.7f;
            if (mode == 1) { BtcReplayIcons.Glyph(icon, new Rect(tx, ir.center.y - g * 0.5f, g, g), c); tx += g + fs * 0.7f; }
            BtcReplayUI.Ensure(fs);
            GUIStyle st = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
            if (_railLbl == null) _railLbl = new GUIStyle(st);
            _railLbl.font = st.font; _railLbl.fontSize = Mathf.RoundToInt(fs * 0.92f); _railLbl.fontStyle = on ? FontStyle.Bold : FontStyle.Normal; _railLbl.alignment = TextAnchor.MiddleLeft; _railLbl.clipping = TextClipping.Clip;
            Color oc = GUI.contentColor;
            GUI.contentColor = c;
            GUI.Label(new Rect(tx, ir.y, ir.xMax - tx - fs * 0.3f, ir.height), name, _railLbl);
            GUI.contentColor = oc;
        }

        private static readonly KeyCode[] DefaultEditorKeys = { KeyCode.F7 };

        /// <summary>The UI editor key (hotkey id ui_editor, F7): works in the game, the lobby, the main menu and replays.</summary>
        internal static bool UiEditorKeyDown()
        {
            KeyCode[] k = null;
            try { k = BtcHotkeys.GetKeys("ui_editor", DefaultEditorKeys); } catch (Exception) { }
            if (k == null || k.Length == 0) return false;
            for (int i = 0; i < k.Length - 1; i++) if (!Input.GetKey(k[i])) return false;
            return Input.GetKeyDown(k[k.Length - 1]);
        }

        private static string _tip;
        private static Rect _tipAnchor;
        private static GUIStyle _right, _center, _railLbl;

        internal static void DrawBackground(Rect content)
        {
            BtcReplayUI.Ensure(Fs);
            BtcReplayUI.Fill(new Rect(0f, 0f, Screen.width, Screen.height), Bg);
            BtcReplayUI.Fill(new Rect(content.x - 1f, 0f, 1f, Screen.height), Line);
        }

        /// <summary>Free strip of the header between the title and the version label: the global search field lives here.</summary>
        internal static Rect HeaderFree;
        /// <summary>Last drawn rect of every rail / corner button by tab id (the first-run tour points at them).</summary>
        internal static readonly Rect[] TargetRects = new Rect[16];

        internal static void DrawHeader(Rect content, int fs, string title)
        {
            BtcReplayUI.Ensure(fs);
            GUIStyle st = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
            if (_right == null) _right = new GUIStyle(st);
            _right.font = st.font;
            _right.alignment = TextAnchor.MiddleRight;
            int old = st.fontSize;
            st.fontSize = Mathf.RoundToInt(fs * 1.5f);
            Color oc = GUI.contentColor;
            GUI.contentColor = Color.white;
            float h = fs * 3.5f;
            float titleW = st.CalcSize(new GUIContent(title)).x; GUI.Label(new Rect(content.x + fs * 1.2f, content.y, content.width * 0.6f, h), title, st);
            st.fontSize = old;
            _right.fontSize = Mathf.RoundToInt(fs * 0.8f);
            GUI.contentColor = new Color(1f, 1f, 1f, 0.35f);
            // right edge stays clear of the four corner buttons (sound, changelog, bug, language) and of the language chips when they are open
            float reserve = fs * 1.2f + fs * 2.55f * 6f + (_langOpen ? fs * 7f : fs * 0.6f);
            GUI.Label(new Rect(content.x, content.y, content.width - reserve, h), "v" + BtcPlugin.Version, _right);   // ends left of the corner buttons
            float vEnd = content.xMax - reserve - fs * 3.8f, sx = content.x + fs * 1.2f + titleW + fs * 2f;
            HeaderFree = new Rect(sx, content.y + (h - fs * 2.2f) * 0.5f, Mathf.Min(fs * 20f, vEnd - sx), fs * 2.2f);
            GUI.contentColor = oc;
            BtcReplayUI.Fill(new Rect(content.x + fs, content.y + h - 1f, content.width - fs * 2f, 1f), Line);
        }

        /// <summary>True when the whole feature of a tab is switched off by its header master switch (the rail greys the icon).</summary>
        internal static bool FeatureOff(int id)
        {
            try
            {
                switch (id)
                {
                    case 0: return !BtcPacksTab.MasterOn;
                    case 1: return BtcVisualData.Ready() && !BtcVisualData.GetMaster() && !BtcOverlays.Master;
                    case 2: return !BtcBc.BcEnabled;
                    case 3: return !BtcHotkeys.Enabled;
                    case 4: return !BtcGameplay.RulesOn;
                    case 6: return !BtcNick.StyleOn;
                    case 11: return !BtcStatsTab.MasterOn;
                    case 12: return !BtcUiEdit.On;
                    case 5: return !BtcSaveFormat.ShareOn;
                }
            }
            catch (Exception) { }
            return false;
        }

        /// <summary>The vertical icon rail. Returns the clicked tab id or -1. names are indexed by tab id.</summary>
        internal static int DrawRail(Rect r, int fs, int[] ids, string[] names, int active, Func<int, bool> enabled)
        {
            BtcReplayUI.Ensure(fs);
            Event e = Event.current;
            BtcReplayUI.Fill(r, RailBg);
            GUIStyle st = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
            if (_center == null) _center = new GUIStyle(st);
            _center.font = st.font;
            _center.fontSize = Mathf.RoundToInt(fs * 1.1f);
            _center.fontStyle = FontStyle.Bold;
            _center.alignment = TextAnchor.MiddleCenter;
            Color oc = GUI.contentColor;
            GUI.contentColor = Accent;
            GUI.Label(new Rect(r.x, r.y + fs * 0.6f, r.width, fs * 2.2f), "BTC", _center);
            GUI.contentColor = oc;
            int rmode = BtcUiEdit.RailMode;
            float item = rmode == 0 ? r.width * 0.82f : fs * 2.6f, x = r.x + (r.width - (rmode == 0 ? item : r.width * 0.88f)) * 0.5f, y = r.y + fs * 3.6f;
            float itemW = rmode == 0 ? item : r.width * 0.88f;
            int clicked = -1;
            for (int i = 0; i < ids.Length; i++)
            {
                int id = ids[i];
                if (id == 9 || id == 10 || id == 12 || id == 13) continue; // report / changelog / UI editor live in the top corner
                Rect ir = new Rect(x, y, itemW, item);
                if (id >= 0 && id < TargetRects.Length) TargetRects[id] = ir;
                bool on = id == active, ok = enabled == null || enabled(id), hover = ir.Contains(e.mousePosition);
                if (on)
                {
                    BtcReplayUI.Fill(ir, new Color(Accent.r, Accent.g, Accent.b, 0.14f));
                    BtcReplayUI.Fill(new Rect(r.x, y + item * 0.15f, 3f, item * 0.7f), Accent);
                }
                else if (hover && ok) BtcReplayUI.Fill(ir, new Color(1f, 1f, 1f, 0.07f));
                bool off = ok && FeatureOff(id);   // the tab's master switch is off: grey icon, still clickable
                float a = ok ? (off ? (hover ? 0.4f : 0.24f) : (hover ? 0.95f : 0.55f)) : 0.2f;
                int icon = id >= 0 && id < IconOf.Length ? IconOf[id] : BtcReplayIcons.Layout;
                Color c = on ? (off ? new Color(0.62f, 0.68f, 0.64f, 1f) : Accent) : new Color(1f, 1f, 1f, a);
                float g = item * 0.55f;
                if (rmode == 0) BtcReplayIcons.Glyph(icon, new Rect(ir.center.x - g * 0.5f, ir.center.y - g * 0.5f, g, g), c);
                else
                {
                    float tx = ir.x + fs * 0.9f;
                    if (rmode == 1) { BtcReplayIcons.Glyph(icon, new Rect(tx, ir.center.y - g * 0.5f, g, g), c); tx += g + fs * 0.8f; }
                    if (_railLbl == null) _railLbl = new GUIStyle(_center);
                    _railLbl.font = _center.font; _railLbl.fontSize = Mathf.RoundToInt(fs * 0.95f); _railLbl.fontStyle = on ? FontStyle.Bold : FontStyle.Normal; _railLbl.alignment = TextAnchor.MiddleLeft; _railLbl.clipping = TextClipping.Clip;
                    Color oc2 = GUI.contentColor;
                    GUI.contentColor = c;
                    GUI.Label(new Rect(tx, ir.y, ir.xMax - tx - fs * 0.4f, ir.height), id >= 0 && id < names.Length ? names[id] : "", _railLbl);
                    GUI.contentColor = oc2;
                }
                if (id == 10 && !on && BtcChangelog.HasUnseen) BtcReplayUI.Fill(new Rect(ir.xMax - item * 0.28f, ir.y + item * 0.12f, item * 0.16f, item * 0.16f), Accent); // "new" dot
                if (hover && rmode == 0 && e.type == EventType.Repaint && id >= 0 && id < names.Length) { _tip = off ? names[id] + " (" + BtcReplayUI.T("off", "выкл") + ")" : names[id]; _tipAnchor = ir; }
                bool prev = GUI.enabled;
                GUI.enabled = ok;
                if (BtcSfx.Btn(ir, "", BtcReplayUI.sBtn)) clicked = id;
                GUI.enabled = prev;
                y += item + fs * 0.3f;
            }
            return clicked;
        }

        // technical buttons (language, report a bug, changelog) live in the top right corner of the header, not in the rail
        private static bool _langOpen;
        private static readonly string[] LangShort = { "EN", "RU" };
        private static readonly string[] LangName = { "English", "Русский" };

        /// <summary>Header corner buttons: language (globe, expands EN/RU chips to its left), bug report, changelog. Returns the clicked tab id (9 or 10) or -1.</summary>
        internal static int DrawCorner(Rect content, int fs, int active, string[] names)
        {
            BtcReplayUI.Ensure(fs);
            Event e = Event.current;
            float h = fs * 3.5f, sm = fs * 2.2f, gap = fs * 0.35f;
            float y = content.y + (h - sm) * 0.5f, x = content.xMax - fs * 1.2f - sm;
            int clicked = -1;
            {   // UI sound level (off / low / medium / high): rightmost
                Rect sp = new Rect(x, y, sm, sm);
                bool sh = sp.Contains(e.mousePosition);
                int lvl = BtcSfx.Level;
                BtcReplayUI.Fill(sp, new Color(1f, 1f, 1f, sh ? 0.12f : 0.06f));
                float sg = sm * 0.6f;
                Rect sgr = new Rect(sp.center.x - sg * 0.5f, sp.center.y - sg * 0.5f, sg, sg);
                BtcReplayIcons.Glyph(BtcReplayIcons.Speaker, sgr, lvl == 0 ? new Color(1f, 1f, 1f, 0.3f) : new Color(1f, 1f, 1f, sh ? 0.95f : 0.65f));
                if (lvl == 0) BtcReplayIcons.Glyph(BtcReplayIcons.Cross, new Rect(sgr.center.x, sgr.center.y - sg * 0.1f, sg * 0.55f, sg * 0.55f), new Color(1f, 0.45f, 0.45f, 0.9f));
                else for (int b = 0; b < lvl; b++) BtcReplayUI.Fill(new Rect(sp.x + sm * 0.12f + b * sm * 0.22f, sp.yMax - sm * 0.16f, sm * 0.16f, sm * 0.08f), Accent);
                if (sh && e.type == EventType.Repaint) { _tip = BtcSfx.LevelName; _tipAnchor = sp; }
                if (BtcSfx.Btn(sp, "", BtcReplayUI.sBtn)) BtcSfx.CycleLevel();
                x -= sm + gap;
            }
            int[] cids = { 13, 10, 12, 9 };
            for (int k = 0; k < cids.Length; k++)   // right to left: changelog, UI editor, bug
            {
                int cid = cids[k], cicon = cid == 9 ? BtcReplayIcons.Bug : (cid == 12 ? BtcReplayIcons.Layout : (cid == 13 ? BtcReplayIcons.Chat : BtcReplayIcons.News));
                Rect cr = new Rect(x, y, sm, sm);
                if (cid >= 0 && cid < TargetRects.Length) TargetRects[cid] = cr;
                bool con = cid == active, chov = cr.Contains(e.mousePosition);
                Color cc = cid == 9 ? new Color(1f, 0.38f, 0.36f, 1f) : Accent;
                BtcReplayUI.Fill(cr, new Color(cc.r, cc.g, cc.b, con ? 0.28f : (chov ? 0.2f : 0.1f)));
                float cg = sm * 0.6f;
                BtcReplayIcons.Glyph(cicon, new Rect(cr.center.x - cg * 0.5f, cr.center.y - cg * 0.5f, cg, cg), new Color(cc.r, cc.g, cc.b, chov || con ? 1f : 0.8f));
                if (cid == 13 && !con && BtcHud.Unseen > 0) BtcReplayUI.Fill(new Rect(cr.xMax - sm * 0.28f, cr.y + sm * 0.06f, sm * 0.22f, sm * 0.22f), new Color(1f, 0.85f, 0.4f, 1f));
                if (cid == 10 && !con && BtcChangelog.HasUnseen) BtcReplayUI.Fill(new Rect(cr.xMax - sm * 0.28f, cr.y + sm * 0.06f, sm * 0.22f, sm * 0.22f), Accent);
                if (chov && e.type == EventType.Repaint && cid < names.Length) { _tip = names[cid]; _tipAnchor = cr; }
                if (BtcSfx.Btn(cr, "", BtcReplayUI.sBtn)) clicked = cid;
                x -= sm + gap;
            }
            // language globe; a click expands the EN / RU chips to the left of it
            int cur = BtcLang.Russian ? 1 : 0;
            Rect gl = new Rect(x, y, sm, sm);
            bool hov = gl.Contains(e.mousePosition);
            if (_langOpen && e.type == EventType.MouseDown)
            {
                bool inside = gl.Contains(e.mousePosition);
                float cx0 = gl.x - LangShort.Length * (sm * 1.3f + gap);
                if (new Rect(cx0, y, LangShort.Length * (sm * 1.3f + gap), sm).Contains(e.mousePosition)) inside = true;
                if (!inside) _langOpen = false;
            }
            BtcReplayUI.Fill(gl, _langOpen ? new Color(Accent.r, Accent.g, Accent.b, 0.22f) : (hov ? new Color(1f, 1f, 1f, 0.12f) : new Color(1f, 1f, 1f, 0.06f)));
            float g = sm * 0.6f;
            BtcReplayIcons.Glyph(BtcReplayIcons.Globe, new Rect(gl.center.x - g * 0.5f, gl.center.y - g * 0.5f, g, g), _langOpen ? Accent : new Color(1f, 1f, 1f, hov ? 0.95f : 0.65f));
            if (hov && e.type == EventType.Repaint && !_langOpen) { _tip = BtcReplayUI.T("Language: ", "Язык: ") + LangName[cur]; _tipAnchor = gl; }
            if (BtcSfx.Btn(gl, "", BtcReplayUI.sBtn)) _langOpen = !_langOpen;
            if (_langOpen && _center != null)
            {
                float cw = sm * 1.3f;
                for (int i = LangShort.Length - 1; i >= 0; i--)
                {
                    Rect ch = new Rect(gl.x - (LangShort.Length - i) * (cw + gap) + gap, y, cw, sm);
                    bool on = i == cur, chh = ch.Contains(e.mousePosition);
                    BtcReplayUI.Fill(ch, on ? new Color(Accent.r, Accent.g, Accent.b, 0.25f) : new Color(1f, 1f, 1f, chh ? 0.12f : 0.06f));
                    Color oc = GUI.contentColor;
                    GUI.contentColor = on ? Color.white : new Color(1f, 1f, 1f, 0.75f);
                    GUI.Label(ch, LangShort[i], _center);
                    GUI.contentColor = oc;
                    if (chh && e.type == EventType.Repaint) { _tip = LangName[i]; _tipAnchor = ch; }
                    if (BtcSfx.Btn(ch, "", BtcReplayUI.sBtn)) { BtcLang.SetRussian(i == 1); _langOpen = false; }
                }
            }
            return clicked;
        }

        /// <summary>Rail tooltip, drawn last so no tab content covers it (called after DrawMenu).</summary>
        private static string _toast;
        private static int _toastIcon = -1;
        private static float _toastAt, _toastUntil;

        /// <summary>A short message at the bottom of the menu (optional icon id from BtcReplayIcons, -1 = none). Used by undo and by the tabs.</summary>
        internal static void Toast(string text, int icon)
        {
            _toast = text; _toastIcon = icon; _toastAt = Time.unscaledTime; _toastUntil = Time.unscaledTime + 3.2f;
        }

        /// <summary>Ctrl+Z for the tab that is open (not while a text field or a key capture is active). Call before the tab draws.</summary>
        internal static void HandleUndo(BtcMenuTab tab, Event e)
        {
            if (tab == null || tab.UndoScope == null || !BtcHotkeys.MenuOpen || e.type != EventType.KeyDown || e.keyCode != KeyCode.Z) return;
            if (!(e.control || e.command) || GUIUtility.keyboardControl != 0 || BtcHotkeys.Capturing) return;
            BtcUndo.Undo(tab.UndoScope);
            e.Use();
        }

        private static void DrawToast(int fs)
        {
            if (_toast == null || Time.unscaledTime > _toastUntil || Event.current.type != EventType.Repaint) return;
            GUIStyle st = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
            float fade = Mathf.Clamp01((_toastUntil - Time.unscaledTime) / 0.4f) * Mathf.Clamp01((Time.unscaledTime - _toastAt) / 0.08f + 0.2f);
            float iw = _toastIcon >= 0 ? fs * 2f : 0f, w = _toast.Length * fs * 0.6f + fs * 2f + iw, h = fs * 2.3f;
            w = Mathf.Min(w, Screen.width - fs * 6f);
            Rect t = new Rect((Screen.width + RailWidth(fs)) * 0.5f - w * 0.5f, Screen.height - h - fs * 2.2f, w, h);
            BtcReplayUI.Fill(t, new Color(0.02f, 0.025f, 0.03f, 0.96f * fade));
            BtcReplayUI.Fill(new Rect(t.x, t.y, 3f, t.height), new Color(Accent.r, Accent.g, Accent.b, fade));
            if (_toastIcon >= 0) BtcReplayIcons.Glyph(_toastIcon, new Rect(t.x + fs * 0.5f, t.y + (h - fs * 1.6f) * 0.5f, fs * 1.6f, fs * 1.6f), new Color(Accent.r, Accent.g, Accent.b, fade));
            Color oc = GUI.contentColor;
            GUI.contentColor = new Color(1f, 1f, 1f, fade);
            GUI.Label(new Rect(t.x + fs * 0.8f + iw, t.y, t.width - fs * 1.2f - iw, t.height), _toast, st);
            GUI.contentColor = oc;
        }

        internal static void DrawTip()
        {
            DrawToast(Fs);
            if (_tip == null) return;
            if (Event.current.type != EventType.Repaint) return;
            int fs = Fs;
            GUIStyle st = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
            float w = _tip.Length * fs * 0.62f + fs * 1.4f, h = fs * 1.7f;
            Rect t = new Rect(_tipAnchor.xMax + fs * 0.6f, _tipAnchor.center.y - h * 0.5f, w, h);
            if (t.xMax > Screen.width - fs * 0.5f)   // near the right edge (header corner buttons): put it under the anchor, kept on screen
                t = new Rect(Mathf.Max(fs * 0.5f, Mathf.Min(_tipAnchor.center.x - w * 0.5f, Screen.width - w - fs * 0.5f)), _tipAnchor.yMax + fs * 0.4f, w, h);
            BtcReplayUI.Fill(t, new Color(0f, 0f, 0f, 0.92f));
            BtcReplayUI.Fill(new Rect(t.x, t.y, 2f, t.height), Accent);
            GUI.Label(new Rect(t.x + fs * 0.7f, t.y, t.width, t.height), _tip, st);
            _tip = null;
        }
    }
}
