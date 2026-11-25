using Brix.External.Factories;
using Brix.Game.Utils;
using Brix.UI.Builder.Widget;
using Brix.UI.Icons;
using UnityEngine;
using UnityEngine.UI;

namespace BetterThanCastle
{
    /// C# game menu (replacement of GameMenu.lua, docs/gamemenu-cs.md): builds the SAME prefabs the Lua panels use
    /// (Factory.Instantiate of Layouts/Images/Texts/Buttons) with the values LUI/Panels/*.lua set, so the look is 1:1.
    /// Node names follow the Lua builder ("(Key) {LuaMenu} Panel @ handle") so DEV uidump diffs line up with the vanilla dump.
    /// Perf: only visible nodes are built (Lua also builds hidden skipMap children and every sidebar at load; we build sidebars on first open).
    internal static class BtcGhKit
    {
        // LUI2.lua colour constants
        internal static readonly Color RailBg = new Color32(30, 30, 30, 216);        // _bgColorDefault / Hover / AnySelected (0.12,0.12,0.12,0.85)
        internal static readonly Color Selected = new Color(0.1f, 0.1f, 0.1f, 1f);    // _bgColorSelected
        internal static readonly Color Yellow = new Color32(255, 197, 10, 255);       // CastleYellow
        // Accent of the HUD panels: vanilla yellow (the custom CastleRed of the author's Lua panels leaked into 0.6.0). Destructive buttons use PureRed/DeleteRed.
        internal static readonly Color Red = Yellow;
        internal static readonly Color Gray = new Color32(127, 127, 127, 255);
        internal static readonly Color Clear = new Color(0f, 0f, 0f, 0f);
        internal static readonly Color Divider = new Color32(127, 127, 127, 127);
        internal static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);

        internal static RectTransform Make(Factory.AssetKey key, Transform parent, string name)
        {
            GameObject go = Factory.Instantiate(key);
            if (go == null) { BtcLog.Error("GameHud", "no prefab " + key.Factory + "." + key.Name); go = new GameObject(name, typeof(RectTransform)); }
            go.name = "(" + key.Factory + "." + key.Name + ") {LuaMenu} " + name;
            RectTransform rt = go.transform as RectTransform;
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            go.SetActive(true);
            return rt;
        }

        internal static void Rect(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        internal static void Stretch(RectTransform rt, float inset = 0f)
        {
            Rect(rt, Vector2.zero, Vector2.one, Mid, Vector2.zero, new Vector2(-inset, -inset));
        }

        internal static void Centre(RectTransform rt, float w, float h)
        {
            Rect(rt, Mid, Mid, Mid, Vector2.zero, new Vector2(w, h));
        }

        internal static LayoutElement Le(RectTransform rt, float minW, float minH, float prefW, float prefH, float flexW, float flexH, bool ignore)
        {
            LayoutElement le = rt.GetComponent<LayoutElement>();
            if (le == null) le = rt.gameObject.AddComponent<LayoutElement>();
            le.minWidth = minW; le.minHeight = minH; le.preferredWidth = prefW; le.preferredHeight = prefH;
            le.flexibleWidth = flexW; le.flexibleHeight = flexH; le.ignoreLayout = ignore;
            return le;
        }

        internal static Image Img(RectTransform rt, Sprite sprite, Color c, bool on)
        {
            Image img = rt.GetComponent<Image>();
            if (img == null) img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite; // null = no sprite, like the Lua panels (the prefab default is UI_ColorWhite)
            img.color = c;
            img.enabled = on;
            return img;
        }

        internal static Text Txt(RectTransform rt, string s, int size, Color c, TextAnchor align)
        {
            Text t = rt.GetComponent<Text>();
            if (t == null) return null;
            t.text = s; t.fontSize = size; t.color = c; t.alignment = align;
            return t;
        }

        internal static void Group(RectTransform rt, int l, int r, int t, int b, float spacing, TextAnchor align)
        {
            HorizontalOrVerticalLayoutGroup g = rt.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (g == null) return;
            g.padding = new RectOffset(l, r, t, b); g.spacing = spacing; g.childAlignment = align;
        }

        /// Empty layout node (Lua Vertical/Horizontal with the image disabled).
        internal static RectTransform Box(Factory.AssetKey key, Transform parent, string name, int l, int r, int t, int b, float spacing, TextAnchor align)
        {
            RectTransform rt = Make(key, parent, name);
            Stretch(rt);
            Img(rt, null, new Color32(0, 0, 0, 192), false);
            Group(rt, l, r, t, b, spacing, align);
            return rt;
        }

        internal static Sprite Icon(string key)
        {
            try
            {
                if (IconKey.KeyExists(key)) return IconKey.FromString(key).Get64();
                if (IconKey.KeyExists("_" + key)) return IconKey.FromString("_" + key).Get64();
            }
            catch { }
            return null;
        }

        private static Font _korolev;
        internal static Font Korolev
        {
            get
            {
                if (_korolev == null) { try { LayoutPanelManager m = Brix.Components.BrixSingleton<LayoutPanelManager>.Instance; if (m != null) _korolev = m.KorolevLight; } catch { } }
                return _korolev;
            }
        }

        /// FrameAnimatorWidgetComponent.Refresh with a HighlightMode (LUI2 FrameHighlight): None 0, Bottom 1, Top 2, Left 4, Right 8, All 15.
        internal static void Highlight(FrameAnimatorWidgetComponent fa, int flag)
        {
            if (fa == null) return;
            Animator a = fa._frameAnimator;
            if (a == null) return;
            if (a.isActiveAndEnabled && a.isInitialized)
            {
                AnimatorManager.Remove(a);
                if (a.GetInteger("Flag") != flag) a.SetInteger("Flag", flag);
            }
            else fa._v = flag;
        }

        /// The Buttons.Standard child of every ButtonPanel (stretched, ignores layout).
        private static BtcGhButton Btn(RectTransform p, string name)
        {
            RectTransform btn = Make(Buttons.Standard, p, name);
            Stretch(btn);
            Le(btn, 64f, 64f, 64f, 64f, -1f, -1f, true);
            BtcGhButton b = btn.gameObject.AddComponent<BtcGhButton>();
            b.Frame = btn.GetComponent<FrameAnimatorWidgetComponent>() ?? btn.GetComponentInChildren<FrameAnimatorWidgetComponent>(true);
            return b;
        }

        /// lyt_hover (pad 0,4,4,0) with img_hoverBg, optional lbl_hoverHotkey (size 10, UpperRight, inset 4) and lbl_hover (size 11, LowerCenter).
        private static void Hover(RectTransform p, BtcGhButton b, string label, bool hasLabel, string hotkey, bool hasHotkey, Color hotkeyColor, int hotkeySize, TextAnchor hotkeyAlign, bool hasBg)
        {
            RectTransform hov = Make(Layouts.Horizontal, p, "ButtonPanel @ _p.lyt_hover");
            Stretch(hov);
            Img(hov, null, new Color32(0, 0, 0, 192), false);
            Le(hov, -1f, -1f, -1f, -1f, -1f, -1f, true);
            Group(hov, 0, 4, 4, 0, 2f, TextAnchor.UpperLeft);
            if (hasBg)
            {
                RectTransform hbg = Make(Images.Default, hov, "ButtonPanel @ _p.lyt_hover.img_hoverBg");
                Stretch(hbg);
                b.HoverBg = Img(hbg, null, Clear, true);
                Le(hbg, 64f, 64f, -1f, -1f, -1f, -1f, true);
            }
            if (hasHotkey)
            {
                RectTransform hk = Make(Texts.Default, hov, "ButtonPanel @ _p.lyt_hover.lbl_hoverHotkey");
                Stretch(hk, 4f);
                b.HotkeyLabel = Txt(hk, hotkey ?? "", hotkeySize, hotkeyColor, hotkeyAlign);
                Le(hk, 64f, 64f, -1f, -1f, -1f, -1f, true);
            }
            if (hasLabel)
            {
                RectTransform hl = Make(Texts.Default, hov, "ButtonPanel @ _p.lyt_hover.lbl_hover");
                Stretch(hl);
                b.HoverLabel = Txt(hl, label ?? "", 11, Clear, TextAnchor.LowerCenter);
                Le(hl, 64f, 64f, -1f, -1f, -1f, -1f, true);
            }
        }

        private static RectTransform Root(Transform parent, float w, float h, Color bg, bool bgOn)
        {
            RectTransform p = Make(Layouts.Horizontal, parent, "ButtonPanel @ _p");
            Rect(p, new Vector2(0f, 1f), new Vector2(0f, 1f), Mid, Vector2.zero, new Vector2(w, h));
            Img(p, null, bg, bgOn);
            Le(p, w, h, w, h, -1f, -1f, false);
            Group(p, 0, 0, 0, 0, 2f, TextAnchor.MiddleLeft);
            return p;
        }

        private static Image FgImage(RectTransform p, Sprite icon, Color c, float size)
        {
            RectTransform fg = Make(Images.Default, p, "ButtonPanel @ _p.img_fg");
            Centre(fg, size, size);
            Le(fg, 64f, 64f, -1f, -1f, -1f, -1f, true);
            return Img(fg, icon, c, true);
        }

        /// LeftBarMenu AddMenuHandleToggle -> LUI/Panels/ButtonPanel.lua: 48x48 rail button (bg 30,30,30,216, icon 32x32, hover label
        /// lower centre size 11, hotkey label upper right size 10, Buttons.Standard with the yellow frame on top).
        internal static RectTransform RailButton(Transform parent, Sprite icon, Color iconColor, string label, string hotkey, Color hotkeyColor, out BtcGhButton button)
        {
            RectTransform p = Root(parent, 48f, 48f, RailBg, true);
            Image fg = null;
            button = null;
            BtcGhButton b = null;
            // children order as the Lua panel: lyt_hover, img_fg, btn (bgImg is skipped/off in the rail)
            RectTransform tmp = p;
            b = Btn(p, "ButtonPanel @ _p.btn");
            Hover(p, b, label, true, hotkey, true, hotkeyColor, 10, TextAnchor.UpperRight, true);
            fg = FgImage(p, icon, iconColor, 32f);
            b.transform.SetAsLastSibling();
            b.Root = p.GetComponent<Image>(); b.Icon = fg; b.HoverColor = BtcGhButton.FgHover;
            b.BgColor = delegate { return RailBg; };
            button = b;
            return tmp;
        }

        /// LeftBarMenu AddDisplayHandle (task limit): no button, front label in the middle, hotkey label used as a caption.
        internal static RectTransform DisplayButton(Transform parent, Sprite icon, string caption, Color color, out Text count)
        {
            RectTransform p = Root(parent, 48f, 48f, RailBg, true);
            RectTransform hov = Make(Layouts.Horizontal, p, "ButtonPanel @ _p.lyt_hover");
            Stretch(hov);
            Img(hov, null, new Color32(0, 0, 0, 192), false);
            Le(hov, -1f, -1f, -1f, -1f, -1f, -1f, true);
            Group(hov, 0, 4, 4, 0, 2f, TextAnchor.UpperLeft);
            RectTransform hk = Make(Texts.Default, hov, "ButtonPanel @ _p.lyt_hover.lbl_hoverHotkey");
            Stretch(hk, 4f);
            Txt(hk, caption, 12, color, TextAnchor.UpperCenter);
            Le(hk, 64f, 64f, -1f, -1f, -1f, -1f, true);
            FgImage(p, icon, Clear, 32f);
            RectTransform fl = Make(Texts.Default, p, "ButtonPanel @ _p.lbl_fg");
            Stretch(fl);
            count = Txt(fl, "", 12, color, TextAnchor.MiddleCenter);
            Le(fl, 64f, 64f, -1f, -1f, -1f, -1f, true);
            return p;
        }

        /// SidebarFloatingMenu AddMenuHandleToggle (ButtonPanel with bgImg as the icon, no fg image/label): grid cell button.
        internal static BtcGhButton GridButton(Transform parent, Sprite icon, Color iconColor, float iconSize, string label, bool hasLabel, string hotkey, bool hasHotkey)
        {
            RectTransform p = Root(parent, 64f, 64f, Clear, true);
            RectTransform bg = Make(Images.Default, p, "ButtonPanel @ _p.bgImg");
            Centre(bg, iconSize, iconSize);
            Le(bg, 64f, 64f, -1f, -1f, -1f, -1f, true);
            Image icn = Img(bg, icon, iconColor, true);
            BtcGhButton b = Btn(p, "ButtonPanel @ _p.btn");
            Hover(p, b, label, hasLabel, hotkey, hasHotkey, Yellow, 10, TextAnchor.UpperRight, true);
            b.transform.SetAsLastSibling();
            b.Icon = icn; b.HoverColor = RailBg;
            return b;
        }

        /// HeaderPanel leftBtn/rightBtn: 64x64 close button, image off, UI_X yellow, hover bg (0,0,0,0.75), label "Close".
        internal static BtcGhButton CloseButton(Transform parent)
        {
            RectTransform p = Root(parent, 64f, 64f, new Color32(0, 0, 0, 127), false);
            BtcGhButton b = Btn(p, "ButtonPanel @ _p.btn");
            Hover(p, b, BtcGameHud.Loc("##gamemenu_taskbar_close"), true, null, false, Yellow, 10, TextAnchor.UpperRight, true);
            Image fg = FgImage(p, Icon("UI_X"), Yellow, 32f);
            b.transform.SetAsLastSibling();
            b.Icon = fg; b.HoverColor = new Color(0f, 0f, 0f, 0.75f);
            return b;
        }

        /// SpacerPanel / DividerPanel (an image with a LayoutElement; raycasts off).
        internal static Image Spacer(Transform parent, float minH, float flexH, Color c, bool on)
        {
            RectTransform sp = Make(Images.Default, parent, "");
            Rect(sp, new Vector2(0f, 1f), new Vector2(0f, 1f), Mid, Vector2.zero, new Vector2(48f, minH < 0f ? 0f : minH));
            Image img = Img(sp, null, c, on);
            img.raycastTarget = false;
            Le(sp, -1f, minH, 0f, 0f, 1f, flexH, false);
            return img;
        }

        /// LabeledDividerPanel as SidebarFloatingMenu.AddSection uses it: label size 14 pivot (0,0.5) + grey 1 px line.
        internal static RectTransform SectionHeader(Transform parent, string label, Color color)
        {
            RectTransform h = Box(Layouts.Horizontal, parent, "", 0, 0, 0, 0, 0f, TextAnchor.MiddleCenter);
            Le(h, -1f, 32f, -1f, 32f, 1f, -1f, false);
            RectTransform lbl = Make(Texts.Default, h, "");
            Rect(lbl, Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(64f, 64f));
            Txt(lbl, label, 14, color, TextAnchor.MiddleLeft);
            Le(lbl, -1f, 32f, -1f, 32f, -1f, -1f, false);
            RectTransform line = Make(Images.Default, h, "");
            Rect(line, Vector2.zero, Vector2.zero, Mid, Vector2.zero, new Vector2(64f, 64f));
            Img(line, null, Divider, true).raycastTarget = false;
            Le(line, -1f, 1f, -1f, 1f, 1f, -1f, false);
            return h;
        }

        /// LayoutPanels.Grid of a SidebarFloatingMenu section.
        internal static RectTransform Grid(Transform parent, float cw, float ch)
        {
            RectTransform g = Make(Layouts.Grid, parent, "");
            Stretch(g);
            Img(g, null, new Color32(0, 0, 0, 192), false);
            Le(g, -1f, 0f, -1f, -1f, 1f, -1f, false);
            GridLayoutGroup gl = g.GetComponent<GridLayoutGroup>();
            if (gl != null) { gl.cellSize = new Vector2(cw, ch); gl.padding = new RectOffset(0, 0, 0, 0); gl.childAlignment = TextAnchor.UpperLeft; }
            return g;
        }
    }
}

namespace BetterThanCastle
{
    /// ButtonPanel.lua behaviour on a BtcGhKit button: hover shows the hover background and the yellow hover label, sounds
    /// Main_Menu_Rollover on enter AND exit (rolloveron/rolloveroff), Main_Menu_Select on action, click on pointer up inside.
    /// Background colour by state comes from BgColor, highlight frame from Highlight. Refresh() only runs on events (no Update).
    internal class BtcGhButton : UnityEngine.MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler,
        UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler
    {
        internal static readonly UnityEngine.Color FgHover = new UnityEngine.Color(1f / 3f, 1f / 3f, 1f / 3f, 0.5f);
        internal UnityEngine.UI.Image Root, HoverBg, Icon;
        internal UnityEngine.UI.Text HoverLabel, HotkeyLabel;
        internal Brix.UI.Builder.Widget.FrameAnimatorWidgetComponent Frame;
        internal UnityEngine.Color HoverColor = FgHover;
        internal System.Func<string> HotkeyFn;
        internal System.Action OnAction;
        internal System.Func<UnityEngine.Color> BgColor, IconColor;
        internal System.Func<UnityEngine.Sprite> IconSprite;
        internal System.Func<bool> Visible;
        internal System.Func<int> Highlight;
        internal UnityEngine.GameObject Panel; // the _p root toggled by Visible (defaults to the parent of the button)
        internal bool Hovering, Pressing, Silent;
        internal System.Func<string> Tip;   // BTC: hover tip (drawn by BtcTerraformTool.OnGUI next to the cursor)
        private int _hl = -1;
        private static BtcGhButton _tipOwner;

        /// <summary>Tip of the hovered button, or null.</summary>
        internal static string HoverTip
        {
            get
            {
                if (_tipOwner == null || !_tipOwner.Hovering || !_tipOwner.isActiveAndEnabled || _tipOwner.Tip == null) return null;
                return _tipOwner.Tip();
            }
        }

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { Hovering = true; if (Tip != null) _tipOwner = this; if (!Silent) Play("Main_Menu_Rollover"); RefreshHover(); }
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { Hovering = false; Pressing = false; if (_tipOwner == this) _tipOwner = null; if (!Silent) Play("Main_Menu_Rollover"); RefreshHover(); }
        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e) { Pressing = true; RefreshBg(); }
        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e)
        {
            bool click = Pressing && Hovering && e.button == UnityEngine.EventSystems.PointerEventData.InputButton.Left;
            Pressing = false;
            if (click && OnAction != null)
            {
                Play("Main_Menu_Select");
                try { OnAction(); } catch (System.Exception ex) { BtcLog.Error("GameHud", "button action: " + ex); }
            }
            RefreshBg();
        }

        private void OnDisable() { Hovering = false; Pressing = false; if (_tipOwner == this) _tipOwner = null; }

        private void RefreshHover()
        {
            if (HoverBg != null) HoverBg.color = Hovering ? HoverColor : BtcGhKit.Clear;
            if (HoverLabel != null) HoverLabel.color = Hovering ? BtcGhKit.Yellow : BtcGhKit.Clear;
            RefreshBg();
        }

        private void RefreshBg()
        {
            if (Root != null && BgColor != null) Root.color = Pressing && Hovering ? UnityEngine.Color.white : BgColor();
        }

        internal void Refresh()
        {
            if (Visible != null)
            {
                UnityEngine.GameObject p = Panel != null ? Panel : transform.parent.gameObject;
                bool v = Visible();
                if (p.activeSelf != v) p.SetActive(v);
                if (!v) return;
            }
            RefreshHover();
            if (Icon != null && IconColor != null) Icon.color = IconColor();
            if (Icon != null && IconSprite != null) { UnityEngine.Sprite sp = IconSprite(); if (Icon.sprite != sp) Icon.sprite = sp; }
            if (HotkeyLabel != null && HotkeyFn != null) { string hk = HotkeyFn(); if (HotkeyLabel.text != hk) HotkeyLabel.text = hk; }
            RefreshHighlight();
        }

        internal void RefreshHighlight()
        {
            if (Highlight == null) return;
            int h = Highlight();
            if (h == _hl) return;
            _hl = h;
            BtcGhKit.Highlight(Frame, h);
        }

        private void OnEnable() { _hl = -1; }

        internal static void Play(string ev)
        {
            try { Brix.Audio.SoundEngine.Play(ev); } catch { }
        }
    }
}
