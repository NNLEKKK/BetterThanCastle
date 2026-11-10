using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Profile tab (menu tab 6) in the new menu style: how other players see you (nick colours, lobby animation, avatar, player-list plate).
    /// Layout: header (title, apply / revert, master switch), a pinned live preview of your lobby row, an in-tab icon rail with pages
    /// (Nick, Style, Animation, Avatar, Plate) and a scrolling page. The nick engine is the legacy BtcNick (see BtcNickApi.cs); undo scope "profile".
    /// Master switch = the mod styling of nick / plate / avatar (off = vanilla look for the others, settings are kept).
    /// </summary>
    internal sealed partial class BtcProfileTab : BtcMenuTab
    {
        internal static readonly BtcProfileTab Instance = new BtcProfileTab();

        internal override int Id { get { return 6; } }
        internal override bool IsActive { get { return BtcMenu.CurrentId == 6; } }
        internal override string UndoScope { get { return Scope; } }
        internal const string Scope = "profile";

        private const int PNick = 0, PStyle = 1, PAnim = 2, PAvatar = 3, PPlate = 4, PSets = 5;
        private static readonly int[] PageIds = { PNick, PStyle, PAnim, PAvatar, PPlate, PSets };

        private Vector2 _scroll;
        private float _h;
        private int _fs;
        private bool _force;                 // an undo changed the nick: apply it again
        private GUIStyle _ttl, _lbl, _dim, _tf, _rich, _center;
        private Texture2D _px;
        private readonly List<string> _keys = new List<string>();

        private int Page
        {
            get { return Mathf.Clamp(BtcMenuState.Int("profile:page", 0), 0, PageIds.Length - 1); }
            set { BtcMenuState.SetInt("profile:page", value); }
        }

        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }

        private static string PageName(int p)
        {
            switch (p)
            {
                case PNick: return Tt("Nick", "Ник");
                case PStyle: return Tt("Colours and gradients", "Цвета и градиенты");
                case PAnim: return Tt("Animation", "Анимация");
                case PAvatar: return Tt("Avatar", "Аватар");
                case PPlate: return Tt("Plate", "Плашка");
                default: return Tt("Profiles", "Профили");
            }
        }

        private static int PageIcon(int p)
        {
            switch (p)
            {
                case PNick: return BtcReplayIcons.Chat;
                case PStyle: return BtcReplayIcons.Palette;
                case PAnim: return BtcReplayIcons.Bolt;
                case PAvatar: return BtcReplayIcons.Image;
                case PPlate: return BtcReplayIcons.Plate;
                default: return BtcReplayIcons.Folder;
            }
        }

        private void Styles(int fs)
        {
            if (_ttl != null && _fs == fs) return;
            _fs = fs;
            GUIStyle b = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
            _ttl = new GUIStyle(b); _ttl.fontSize = Mathf.RoundToInt(fs * 1.15f); _ttl.fontStyle = FontStyle.Bold; _ttl.alignment = TextAnchor.MiddleLeft; _ttl.normal.textColor = Color.white;
            _lbl = new GUIStyle(b); _lbl.fontSize = Mathf.RoundToInt(fs * 0.95f); _lbl.alignment = TextAnchor.MiddleLeft; _lbl.normal.textColor = Color.white; _lbl.clipping = TextClipping.Clip;
            _dim = new GUIStyle(_lbl); _dim.fontSize = Mathf.RoundToInt(fs * 0.78f); _dim.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
            _center = new GUIStyle(_lbl); _center.alignment = TextAnchor.MiddleCenter;
            _rich = new GUIStyle(b); _rich.fontSize = Mathf.RoundToInt(fs * 1.5f); _rich.alignment = TextAnchor.MiddleLeft; _rich.richText = true; _rich.normal.textColor = Color.white; _rich.clipping = TextClipping.Clip;
            _tf = new GUIStyle(GUI.skin.textField); _tf.font = b.font; _tf.fontSize = Mathf.RoundToInt(fs * 1.0f); _tf.alignment = TextAnchor.MiddleLeft;
            _tf.normal.background = null; _tf.focused.background = null; _tf.hover.background = null; _tf.active.background = null;
            _tf.normal.textColor = Color.white; _tf.focused.textColor = Color.white; _tf.hover.textColor = Color.white; _tf.padding = new RectOffset(6, 6, 0, 0);
            if (_px == null) { _px = new Texture2D(1, 1); _px.SetPixel(0, 0, Color.white); _px.Apply(); }
        }

        private static void Txt(Rect r, string s, GUIStyle st, Color c)
        {
            Color old = GUI.contentColor;
            GUI.contentColor = c;
            GUI.Label(r, s, st);
            GUI.contentColor = old;
        }

        internal static Color Rgb(int c)
        {
            return new Color(((c >> 16) & 255) / 255f, ((c >> 8) & 255) / 255f, (c & 255) / 255f, 1f);
        }

        internal static int ToRgb(Color c)
        {
            return Mathf.RoundToInt(c.r * 255f) << 16 | Mathf.RoundToInt(c.g * 255f) << 8 | Mathf.RoundToInt(c.b * 255f);
        }

        // ------------------------------------------------------------------ draw
        internal override void Draw(Rect area, int fs)
        {
            Styles(fs);
            BtcReplayUI.Ensure(fs);
            Event e = Event.current;
            BtcNick.EnsureReady();
            if (e.type == EventType.KeyDown && e.keyCode == BtcMenu.MenuKeyMain)
            {
                GUIUtility.keyboardControl = 0;
                BtcMenu.PgDownFrame = Time.frameCount;
                ResourcePackManager.CloseMenu();
                e.Use();
                return;
            }
            BtcUi.ClickAway(e);
            bool inLobby;
            bool can = BtcNick.EditAllowed(out inLobby);
            BtcNick.Pending = false;
            BtcAvatarTick();

            float railW = BtcMenu.SubRailWidth(fs);
            Rect rail = new Rect(area.x, area.y, railW, area.height);
            DrawRail(rail, fs);
            Rect content = new Rect(area.x + railW, area.y, area.width - railW, area.height);
            BtcReplayUI.Fill(new Rect(content.x - 1f, content.y, 1f, content.height), BtcMenu.Line);

            float pad = fs, ib = fs * 1.9f;
            float y0 = content.y + fs * 0.4f;
            int page = Page;
            Txt(new Rect(content.x + pad, y0, content.width * 0.4f, ib), PageName(page), _ttl, Color.white);
            float left = DrawHeaderIcons(content, fs, y0, ib, can);
            float tw = _ttl.CalcSize(new GUIContent(PageName(page))).x + fs * 1.5f;
            float sx = content.x + pad + Mathf.Max(tw, fs * 10f);
            DrawGlobalSearch(new Rect(sx, y0, Mathf.Min(fs * 26f, left - fs - sx), ib), fs);

            float y = y0 + ib + fs * 0.5f;
            y += DrawPreview(new Rect(content.x + pad, y, content.width - pad * 2f, 0f), fs, inLobby, page == PPlate) + fs * 0.5f;
            BtcReplayUI.Fill(new Rect(content.x + pad, y - fs * 0.15f, content.width - pad * 2f, 1f), BtcMenu.Line);

            Rect view = new Rect(content.x, y, content.width, content.yMax - y);
            float vw = view.width - (_h > view.height ? 14f : 0f);
            _scroll = GUI.BeginScrollView(view, _scroll, new Rect(0f, 0f, vw, _h));
            float cy = fs * 0.3f;
            _keys.Clear();
            Body(page, ref cy, vw, fs, can, inLobby);
            GUI.EndScrollView();
            _h = cy + fs * 3f;

            if ((BtcNick.Pending || _force) && BtcNick.AutoApply && can) BtcNick.ApplyNow();
            _force = false;
            BtcReplayIcons.DrawTip(fs, area.xMax, false);
        }

        private static void BtcAvatarTick() { try { BtcAvatar.Tick(); } catch (Exception) { } }

        private void DrawRail(Rect r, int fs)
        {
            Event e = Event.current;
            int page = Page;
            float item = r.width * 0.82f, x = r.x + (r.width - item) * 0.5f, y = r.y + fs * 0.6f, itemW = item;
            BtcMenu.RailGeometry(r, fs, ref item, ref itemW, ref x);
            for (int i = 0; i < PageIds.Length; i++)
            {
                int p = PageIds[i];
                Rect ir = new Rect(x, y, itemW, item);
                bool on = p == page, hover = ir.Contains(e.mousePosition);
                if (on) { BtcReplayUI.Fill(ir, new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.14f)); BtcReplayUI.Fill(new Rect(r.x, y + item * 0.15f, 3f, item * 0.7f), BtcMenu.Accent); }
                else if (hover) BtcReplayUI.Fill(ir, new Color(1f, 1f, 1f, 0.07f));
                float g = item * 0.55f;
                BtcMenu.RailGlyph(ir, fs, PageIcon(p), PageName(p), on, hover);
                BtcReplayIcons.Hint(ir, PageName(p));
                if (BtcSfx.Btn(ir, "", BtcReplayUI.sBtn)) { Page = p; _scroll = Vector2.zero; GUIUtility.keyboardControl = 0; }
                y += item + fs * 0.3f;
            }
        }

        private float DrawHeaderIcons(Rect content, int fs, float y0, float ib, bool can)
        {
            float pad = fs;
            float x = content.xMax - pad - fs * 2.4f;
            bool on = BtcNick.StyleOn;
            Rect sw = new Rect(x, y0 + (ib - fs * 1.2f) * 0.5f, fs * 2.4f, fs * 1.2f);
            string tip = on ? Tt("Mod styling is ON: others with the mod see your coloured nick, plate and avatar (click to show the vanilla look)", "Оформление мода включено: игроки с модом видят цветной ник, плашку и аватар (клик - показывать ванильный вид)")
                : Tt("Mod styling is OFF: everybody sees a plain nick, default plate and Steam avatar. Settings are kept (click to turn on)", "Оформление мода выключено: все видят обычный ник, стандартную плашку и аватар Steam. Настройки сохранены (клик - включить)");
            if (BtcUi.HeaderSwitch(sw, on, tip))
            {
                bool was = on;
                BtcNick.SetStyleOn(!on);
                BtcSfx.Switch(!on);
                BtcUndo.Push(Scope, Tt("Mod styling", "Оформление мода"), delegate { BtcNick.SetStyleOn(was); });
            }
            x = sw.x - fs * 0.8f - ib;
            bool dirty = BtcNick.Dirty;
            Color ac = (dirty && can) ? BtcMenu.Accent : new Color(1f, 1f, 1f, 0.3f);
            string atip = !can ? Tt("The nick can be changed only in the main menu or in the lobby", "Ник можно менять только в главном меню или в лобби")
                : (dirty ? Tt("Apply the nick now", "Применить ник сейчас") : Tt("The nick is applied", "Ник применён"));
            if (BtcReplayIcons.Plain(new Rect(x, y0, ib, ib), BtcReplayIcons.Check, ac, atip) && dirty && can) { BtcNick.ApplyNow(); BtcSfx.Ok(); }
            x -= ib + 3f;
            if (BtcReplayIcons.Plain(new Rect(x, y0, ib, ib), BtcReplayIcons.Refresh, dirty ? Color.white : new Color(1f, 1f, 1f, 0.3f), Tt("Revert to the applied nick", "Вернуть применённый ник")) && dirty)
            {
                string[] snap = BtcNick.Snapshot();
                BtcNick.Revert();
                PushNick(Tt("Revert nick", "Откат ника"), snap);
            }
            x -= ib + fs * 0.6f;
            bool allOpen = true;
            for (int i = 0; i < _keys.Count; i++) if (!BtcUi.IsOpen(_keys[i])) { allOpen = false; break; }
            if (_keys.Count > 0 && BtcReplayIcons.Plain(new Rect(x, y0, ib, ib), allOpen ? BtcReplayIcons.ChevD : BtcReplayIcons.ChevR, Color.white, allOpen ? Tt("Collapse all sections", "Свернуть все разделы") : Tt("Expand all sections", "Развернуть все разделы")))
                BtcUi.SetOpen(_keys, !allOpen);
            return x;
        }

        // ------------------------------------------------------------------ preview
        /// <summary>Pinned live preview of the player's lobby row: avatar, nick (animation frame when it runs), plate. Returns its height.</summary>
        private float DrawPreview(Rect r, int fs, bool inLobby, bool plateEditor)
        {
            bool on = BtcNick.StyleOn;
            float h = fs * 3.2f;
            Rect card = new Rect(r.x, r.y, r.width, h);
            BtcReplayUI.Fill(card, new Color(0f, 0f, 0f, 0.3f));
            float av = h - fs * 0.8f;
            Rect ar = new Rect(card.x + fs * 0.5f, card.y + fs * 0.4f, av, av);
            BtcReplayUI.Fill(ar, new Color(1f, 1f, 1f, 0.08f));
            Texture2D tex = on ? BtcAvatar.LocalTex : null;
            if (tex != null) GUI.DrawTexture(new Rect(ar.x + 1f, ar.y + 1f, ar.width - 2f, ar.height - 2f), tex, ScaleMode.ScaleToFit, true);
            else BtcReplayIcons.Glyph(BtcReplayIcons.Group, new Rect(ar.x + av * 0.2f, ar.y + av * 0.2f, av * 0.6f, av * 0.6f), new Color(1f, 1f, 1f, 0.3f));
            float tx = ar.xMax + fs * 0.8f;
            Txt(new Rect(tx, card.y, card.xMax - tx - fs * 0.5f, h * 0.62f), BtcNick.LiveTags(inLobby), _rich, Color.white);
            string sub = on ? Tt("This is your row in the lobby list", "Так выглядит ваша строка в списке лобби") : Tt("Mod styling is off: vanilla look", "Оформление мода выключено: ванильный вид");
            Txt(new Rect(tx, card.y + h * 0.58f, card.xMax - tx - fs * 0.5f, h * 0.36f), sub, _dim, new Color(1f, 1f, 1f, 0.5f));
            float ph = DrawPlateCanvas(new Rect(r.x, r.y + h + 3f, r.width, 0f), fs, plateEditor, BtcNick.PlainText.Length > 0 ? BtcNick.LiveTags(inLobby) : "Nick");
            return h + 3f + ph;
        }

        // ------------------------------------------------------------------ pages
        private void Body(int page, ref float y, float w, int fs, bool can, bool inLobby)
        {
            if (_gq.Trim().Length > 0) { SearchPage(ref y, w, fs); return; }
            if (!BtcNick.StyleOn)
                BtcUi.Status(ref y, w, fs, Tt("Mod styling is off: others see a vanilla nick, plate and avatar", "Оформление мода выключено: другие видят ванильный ник, плашку и аватар"), new Color(0.95f, 0.78f, 0.1f, 1f));
            if (!can && page != PAvatar && page != PPlate)
                BtcUi.Status(ref y, w, fs, Tt("The nick can be changed only in the main menu or in the lobby", "Ник можно менять только в главном меню или в лобби"), new Color(0.95f, 0.78f, 0.1f, 1f));
            switch (page)
            {
                case PNick: NickPage(ref y, w, fs, can); break;
                case PStyle: StylePage(ref y, w, fs); break;
                case PAnim: AnimPage(ref y, w, fs, can, inLobby); break;
                case PAvatar: AvatarPage(ref y, w, fs); break;
                case PPlate: PlatePage(ref y, w, fs); break;
                default: SetsPage(ref y, w, fs); break;
            }
        }

        private string KeyAdd(string k) { _keys.Add(k); return k; }

        // ------------------------------------------------------------------ undo helpers
        private void PushNick(string label, string[] before)
        {
            BtcUndo.Push(Scope, label, delegate { BtcNick.Restore(before); _force = true; });
        }
    }
}
