using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Resource packs tab (menu tab 0) in the new menu style. Pages on an in-tab icon rail: Packs (cards with preview, favourites pinned on top,
    /// search, rescan, open folder, unmatched-files report), Halloween hats, Tools (dumps) and All. Master switch in the header = "a pack is applied"
    /// (off = default game textures, on again restores the previous pack). Undo scope "packs".
    /// </summary>
    internal sealed class BtcPacksTab : BtcMenuTab
    {
        internal static readonly BtcPacksTab Instance = new BtcPacksTab();

        internal override int Id { get { return 0; } }
        internal override bool IsActive { get { return BtcMenu.CurrentId == 0; } }
        internal override string UndoScope { get { return Scope; } }

        internal const string Scope = "packs";
        private const int PPacks = 0, PHats = 1, PTools = 2, PAll = 3;
        private static readonly int[] Pages = { PPacks, PHats, PTools, PAll };

        /// <summary>The header master switch state: a pack is applied.</summary>
        internal static bool MasterOn { get { return ResourcePackManager.PackCurrent() != null; } }

        private int _page { get { return BtcMenuState.Int("packs:page", 0); } set { BtcMenuState.SetInt("packs:page", value); } }
        private Vector2 _origin;   // window-space origin of the scroll view content (tooltip anchors)
        private void H(Rect r, string tip) { BtcReplayIcons.Hint(r, new Rect(r.x + _origin.x, r.y + _origin.y, r.width, r.height), tip); }
        private Vector2 _scroll;
        private float _h;
        private string _q = "";
        private string _prevPack;
        private bool _flat;
        private bool _showUnmatched;
        private readonly List<string> _keys = new List<string>();
        private readonly HashSet<string> _favs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _favsLoaded;
        private readonly Dictionary<string, Info> _info = new Dictionary<string, Info>(StringComparer.OrdinalIgnoreCase);
        private int _loadFrame = -1;
        private List<string> _unmatched;
        private string _unmatchedFor;
        private int _unmatchedApplied = -1;
        private GUIStyle _ttl, _tf, _name, _sub;
        private int _fs;

        private sealed class Info { public int Files; public Texture2D Thumb; }

        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }
        private static string FavFile { get { return BtcPaths.Config("packs-ui.txt"); } }

        // ------------------------------------------------------------------ state
        private void LoadFavs()
        {
            if (_favsLoaded) return;
            _favsLoaded = true;
            try
            {
                if (!File.Exists(FavFile)) return;
                string[] l = File.ReadAllLines(FavFile, System.Text.Encoding.UTF8);
                for (int i = 0; i < l.Length; i++) if (l[i].StartsWith("fav=")) _favs.Add(l[i].Substring(4));
            }
            catch (Exception) { }
        }

        private void SaveFavs()
        {
            try
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                foreach (string f in _favs) sb.Append("fav=").Append(f).Append("\r\n");
                File.WriteAllText(FavFile, sb.ToString(), new System.Text.UTF8Encoding(false));
            }
            catch (Exception) { }
        }

        private void Rescan()
        {
            foreach (KeyValuePair<string, Info> kv in _info) if (kv.Value.Thumb != null) UnityEngine.Object.Destroy(kv.Value.Thumb);
            _info.Clear();
            _unmatched = null;
            ResourcePackManager.PackRescan();
        }

        /// <summary>Texture count + preview of one pack, loaded lazily, at most one pack per frame.</summary>
        private Info InfoOf(string name)
        {
            Info i;
            if (_info.TryGetValue(name, out i)) return i;
            if (_loadFrame == Time.frameCount) return null;
            _loadFrame = Time.frameCount;
            i = new Info();
            try
            {
                string dir = Path.Combine(ResourcePackManager.PackRoot, name);
                string[] pngs = Directory.GetFiles(dir, "*.png", SearchOption.AllDirectories);
                i.Files = pngs.Length;
                string pick = Path.Combine(dir, "thumbnail.png");   // the only preview source; the file is not a texture
                if (File.Exists(pick)) i.Files--; else pick = null;
                if (pick != null && new FileInfo(pick).Length <= 4000000)
                {
                    Texture2D t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    t.hideFlags = HideFlags.HideAndDontSave;
                    if (t.LoadImage(File.ReadAllBytes(pick))) i.Thumb = t; else UnityEngine.Object.Destroy(t);
                }
            }
            catch (Exception) { }
            _info[name] = i;
            return i;
        }

        private void Styles(int fs)
        {
            if (_tf != null && _fs == fs) return;
            _fs = fs;
            GUIStyle b = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
            _ttl = new GUIStyle(b); _ttl.fontSize = Mathf.RoundToInt(fs * 1.15f); _ttl.fontStyle = FontStyle.Bold; _ttl.alignment = TextAnchor.MiddleLeft; _ttl.normal.textColor = Color.white;
            _name = new GUIStyle(b); _name.fontSize = Mathf.RoundToInt(fs * 0.98f); _name.alignment = TextAnchor.MiddleLeft; _name.clipping = TextClipping.Clip; _name.normal.textColor = Color.white;
            _sub = new GUIStyle(_name); _sub.fontSize = Mathf.RoundToInt(fs * 0.78f); _sub.normal.textColor = new Color(1f, 1f, 1f, 0.5f);
            _tf = new GUIStyle(GUI.skin.textField); _tf.font = b.font; _tf.fontSize = Mathf.RoundToInt(fs * 0.9f); _tf.alignment = TextAnchor.MiddleLeft;
            _tf.normal.background = null; _tf.focused.background = null; _tf.hover.background = null; _tf.active.background = null;
            _tf.normal.textColor = Color.white; _tf.focused.textColor = Color.white; _tf.hover.textColor = Color.white; _tf.padding = new RectOffset(4, 4, 0, 0);
        }

        private static string PageName(int p)
        {
            switch (p)
            {
                case PPacks: return Tt("Resource packs", "Ресурспаки");
                case PHats: return Tt("Halloween hats", "Хэллоуинские шапки");
                case PTools: return Tt("Tools", "Инструменты");
                default: return Tt("All settings", "Все настройки");
            }
        }

        private static int PageIcon(int p)
        {
            switch (p) { case PPacks: return BtcReplayIcons.Grid; case PHats: return BtcReplayIcons.Pumpkin; case PTools: return BtcReplayIcons.Gear; default: return BtcReplayIcons.List; }
        }

        // ------------------------------------------------------------------ draw
        internal override void Draw(Rect area, int fs)
        {
            Styles(fs);
            LoadFavs();
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
            if (!ResourcePackManager.PacksReady)
            {
                float ny = content.y + fs;
                BtcUi.Note(ref ny, content.width, fs, Tt("Resource packs load together with the game...", "Ресурспаки загружаются вместе с игрой..."));
                return;
            }
            bool busy = _pendHas, pen = GUI.enabled;
            if (busy) GUI.enabled = false;
            DrawPage(content, fs);
            GUI.enabled = pen;
            if (busy)
            {
                DrawBusy(content, fs);
                if (e.type == EventType.Repaint && ++_busyFrames >= 3) RunPending();   // 3 painted frames: the overlay is on screen before the freeze
                return;
            }
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
                if (p == PAll) { BtcReplayUI.Fill(new Rect(x + itemW * 0.15f, y, itemW * 0.7f, 1f), BtcMenu.Line); y += fs * 0.5f; }
                Rect ir = new Rect(x, y, itemW, item);
                bool on = p == _page, hover = ir.Contains(e.mousePosition);
                if (on) { BtcReplayUI.Fill(ir, new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.14f)); BtcReplayUI.Fill(new Rect(r.x, y + item * 0.15f, 3f, item * 0.7f), BtcMenu.Accent); }
                else if (hover) BtcReplayUI.Fill(ir, new Color(1f, 1f, 1f, 0.07f));
                float g = item * 0.55f;
                BtcMenu.RailGlyph(ir, fs, PageIcon(p), PageName(p), on, hover);
                BtcReplayIcons.Hint(ir, PageName(p));
                if (BtcSfx.Btn(ir, "", BtcReplayUI.sBtn)) { _page = p; _scroll = Vector2.zero; }
                y += item + fs * 0.3f;
            }
        }

        private bool Matches(string s) { return _q.Length == 0 || s.IndexOf(_q, StringComparison.OrdinalIgnoreCase) >= 0; }

        private void DrawPage(Rect content, int fs)
        {
            float pad = fs, ib = fs * 1.9f;
            float y0 = content.y + fs * 0.4f;
            GUI.Label(new Rect(content.x + pad, y0, content.width * 0.4f, ib), PageName(_page), _ttl);

            // master switch: a pack is applied (off = default textures, on = the previous / first pack)
            string cur = ResourcePackManager.PackCurrent();
            bool master = cur != null;
            Rect sw = new Rect(content.xMax - pad - fs * 2.4f, y0 + (ib - fs * 1.2f) * 0.5f, fs * 2.4f, fs * 1.2f);
            string tip = master ? Tt("Resource pack ON: " + cur + " (click to use the default game textures)", "Ресурспак включён: " + cur + " (клик - вернуть стандартные текстуры)")
                : Tt("Default game textures (click to bring the previous pack back)", "Стандартные текстуры (клик - вернуть прошлый пак)");
            if (BtcUi.HeaderSwitch(sw, master, tip))
            {
                if (master) { _prevPack = cur; SetPackUndo(null); }
                else
                {
                    string back = _prevPack;
                    if (back == null || !ContainsPack(back)) { List<string> l = ResourcePackManager.PackNames(); back = l.Count > 0 ? l[0] : null; }
                    if (back != null) SetPackUndo(back); else BtcMenu.Toast(Tt("No packs yet: open the packs folder and add one", "Паков пока нет: откройте папку паков и добавьте"), BtcReplayIcons.Folder);
                }
                BtcSfx.Switch(ResourcePackManager.PackCurrent() != null);
            }

            // search + bulk + tools
            float y = y0 + ib + fs * 0.5f;
            Rect sr = new Rect(content.x + pad, y, Mathf.Min(content.width * 0.45f, fs * 24f), ib);
            BtcReplayUI.Fill(sr, new Color(1f, 1f, 1f, 0.06f));
            BtcReplayIcons.Glyph(BtcReplayIcons.Search, new Rect(sr.x + fs * 0.4f, sr.y + ib * 0.2f, ib * 0.6f, ib * 0.6f), new Color(1f, 1f, 1f, 0.5f));
            BtcUi.Field(sr);
            GUI.SetNextControlName("btc_packs_search");
            _q = GUI.TextField(new Rect(sr.x + ib * 0.95f, sr.y, sr.width - ib * 0.95f, ib), _q, 40, _tf);
            if (_q.Length == 0 && GUIUtility.keyboardControl == 0) GUI.Label(new Rect(sr.x + ib * 0.95f + 4f, sr.y, sr.width, ib), Tt("Search a pack or hat", "Поиск пака или шапки"), _sub);
            float fx = sr.xMax + fs * 0.6f;
            bool allOpen = true;
            for (int i = 0; i < _keys.Count; i++) if (!BtcUi.IsOpen(_keys[i])) { allOpen = false; break; }
            if (BtcReplayIcons.Plain(new Rect(fx, y, ib, ib), allOpen ? BtcReplayIcons.ChevD : BtcReplayIcons.ChevR, Color.white, allOpen ? Tt("Collapse all sections", "Свернуть все разделы") : Tt("Expand all sections", "Развернуть все разделы")))
                BtcUi.SetOpen(_keys, !allOpen);
            fx += ib + 3f;
            if (BtcReplayIcons.Plain(new Rect(fx, y, ib, ib), BtcReplayIcons.Refresh, Color.white, Tt("Rescan the packs folder", "Перечитать папку паков")))
            {
                Rescan();
                BtcMenu.Toast(Tt("Packs folder rescanned", "Папка паков перечитана"), BtcReplayIcons.Refresh);
            }
            fx += ib + 3f;
            if (BtcReplayIcons.Plain(new Rect(fx, y, ib, ib), BtcReplayIcons.Folder, Color.white, Tt("Open the packs folder", "Открыть папку паков"))) OpenDir(ResourcePackManager.PackRoot);
            y += ib + fs * 0.6f;
            BtcReplayUI.Fill(new Rect(content.x + pad, y - fs * 0.25f, content.width - pad * 2f, 1f), BtcMenu.Line);

            Rect view = new Rect(content.x, y, content.width, content.yMax - y);
            float vw = view.width - (_h > view.height ? 14f : 0f);
            _scroll = GUI.BeginScrollView(view, _scroll, new Rect(0f, 0f, vw, _h));
            _origin = new Vector2(view.x - _scroll.x, view.y - _scroll.y);
            float cy = fs * 0.2f;
            _keys.Clear();
            switch (_page)
            {
                case PAll: _flat = true; BodyPacks(ref cy, vw, fs); BodyHats(ref cy, vw, fs); BodyTools(ref cy, vw, fs); _flat = false; break;
                case PHats: BodyHats(ref cy, vw, fs); break;
                case PTools: BodyTools(ref cy, vw, fs); break;
                default: BodyPacks(ref cy, vw, fs); break;
            }
            GUI.EndScrollView();
            _h = cy + fs * 3f;
        }

        private bool F(ref float y, float w, int fs, string key, string text, int count)
        {
            if (_flat) return true;
            _keys.Add(key);
            return BtcUi.Fold(ref y, w, fs, key, text, count);
        }

        private static bool ContainsPack(string n)
        {
            List<string> l = ResourcePackManager.PackNames();
            for (int i = 0; i < l.Count; i++) if (string.Equals(l[i], n, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void OpenDir(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                System.Diagnostics.Process.Start("explorer.exe", "\"" + dir + "\"");
            }
            catch (Exception ex) { BtcLog.Warn("Packs", "open folder failed: " + ex.Message); }
        }

        // Switching a pack re-applies every texture in one frame (the game freezes for a moment). So the request only marks it pending:
        // the next frames draw a "applying" overlay first, then the real switch runs.
        private bool _pendHas, _pendRecord;
        private string _pendName;
        private int _busyFrames;

        private void SetPackUndo(string name) { Request(name, true); }

        private void Request(string name, bool record)
        {
            if (_pendHas) return;
            if (string.Equals(name, ResourcePackManager.PackCurrent(), StringComparison.OrdinalIgnoreCase)) return;
            _pendHas = true; _pendName = name; _pendRecord = record; _busyFrames = 0;
        }

        private void RunPending()
        {
            string name = _pendName, before = ResourcePackManager.PackCurrent();
            bool record = _pendRecord;
            _pendHas = false;
            ResourcePackManager.PackSet(name);
            _unmatched = null;
            string label = name ?? Tt("Default textures", "Стандартные текстуры");
            if (record) BtcUndo.Push(Scope, Tt("Pack: ", "Пак: ") + label, delegate { Request(before, false); });
            string msg = name == null ? Tt("Default textures restored", "Стандартные текстуры возвращены")
                : Tt("Pack applied: " + label + " (" + ResourcePackManager.PackApplied() + " textures replaced)", "Пак применён: " + label + " (заменено текстур: " + ResourcePackManager.PackApplied() + ")");
            BtcMenu.Toast(msg, BtcReplayIcons.Check);
            BtcSfx.Ok();
        }

        private void DrawBusy(Rect content, int fs)
        {
            Event e = Event.current;
            if (e.type == EventType.MouseDown || e.type == EventType.MouseUp) e.Use();
            if (e.type != EventType.Repaint) return;
            BtcReplayUI.Fill(content, new Color(0.02f, 0.025f, 0.03f, 0.82f));
            Vector2 c = content.center;
            float rad = fs * 2.2f, dot = fs * 0.7f;
            float t = Time.realtimeSinceStartup * 9f;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                float k = Mathf.Repeat(t - i, 8f) / 8f;   // trailing fade
                BtcReplayUI.Fill(new Rect(c.x + Mathf.Sin(a) * rad - dot * 0.5f, c.y - fs * 2.2f - Mathf.Cos(a) * rad - dot * 0.5f, dot, dot),
                    new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 1f - k * 0.85f));
            }
            string label = _pendName ?? Tt("Default textures", "Стандартные текстуры");
            GUI.Label(new Rect(content.x, c.y + fs * 1.2f, content.width, fs * 2f), Tt("Applying pack: ", "Применяется пак: ") + label, _ttlC());
            GUI.Label(new Rect(content.x, c.y + fs * 3.2f, content.width, fs * 1.5f), Tt("The game may freeze for a few seconds. This is normal, please wait.", "Игра может зависнуть на несколько секунд. Так и должно быть, подождите."), _subC());
        }

        private GUIStyle _smallCs;
        private GUIStyle SmallC()
        {
            int sz = Mathf.Max(9, Mathf.RoundToInt(_fs * 0.6f));
            if (_smallCs == null || _smallCs.fontSize != sz) { _smallCs = new GUIStyle(_sub); _smallCs.fontSize = sz; _smallCs.alignment = TextAnchor.MiddleCenter; _smallCs.normal.textColor = new Color(1f, 1f, 1f, 0.4f); }
            return _smallCs;
        }

        private GUIStyle _ttlCs, _subCs;
        private GUIStyle _ttlC() { if (_ttlCs == null || _ttlCs.fontSize != _ttl.fontSize) { _ttlCs = new GUIStyle(_ttl); _ttlCs.alignment = TextAnchor.MiddleCenter; } return _ttlCs; }
        private GUIStyle _subC() { if (_subCs == null || _subCs.fontSize != _sub.fontSize) { _subCs = new GUIStyle(_sub); _subCs.alignment = TextAnchor.MiddleCenter; } return _subCs; }

        // ------------------------------------------------------------------ Packs page
        private void BodyPacks(ref float y, float w, int fs)
        {
            string cur = ResourcePackManager.PackCurrent();
            List<string> names = ResourcePackManager.PackNames();
            if (F(ref y, w, fs, "packs:list", Tt("Packs", "Паки"), names.Count))
            {
                // order: default, favourites, the rest
                PackRow(ref y, w, fs, null, cur == null);
                for (int pass = 0; pass < 2; pass++)
                    for (int i = 0; i < names.Count; i++)
                    {
                        bool fav = _favs.Contains(names[i]);
                        if ((pass == 0) != fav || !Matches(names[i])) continue;
                        PackRow(ref y, w, fs, names[i], string.Equals(names[i], cur, StringComparison.OrdinalIgnoreCase));
                    }
                if (names.Count == 0)
                {
                    BtcUi.Note(ref y, w, fs, Tt("No packs found. Each folder inside the packs folder is one pack: put .png files named like the game textures into it (open the folder with the folder icon above, the Tools page can dump the game textures as a base).", "Паков нет. Каждая папка внутри папки паков - один пак: положите в неё .png с именами как у текстур игры (папка открывается значком выше, на странице Инструменты можно выгрузить текстуры игры как основу)."));
                    if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Folder, Tt("Open the packs folder", "Открыть папку паков"), ResourcePackManager.PackRoot)) OpenDir(ResourcePackManager.PackRoot);
                }
                y += fs * 0.4f;
            }
            if (cur != null && F(ref y, w, fs, "packs:status", Tt("Current pack", "Текущий пак"), 0))
            {
                int files = ResourcePackManager.PackFileCount(), applied = ResourcePackManager.PackApplied();
                if (_unmatched == null || _unmatchedFor != cur || _unmatchedApplied != applied) { _unmatched = ResourcePackManager.PackUnmatched(); _unmatchedFor = cur; _unmatchedApplied = applied; }
                BtcUi.Status(ref y, w, fs, Tt("Files in the pack: " + files + "   textures replaced: " + applied + "   without a match: " + _unmatched.Count, "Файлов в паке: " + files + "   текстур заменено: " + applied + "   без совпадения: " + _unmatched.Count), Color.white);
                if (_unmatched.Count > 0)
                {
                    if (BtcUi.Action(ref y, w, fs, _showUnmatched ? BtcReplayIcons.ChevD : BtcReplayIcons.ChevR, Tt("Files that match no texture", "Файлы без совпадения"), Tt("Usually a typo in the file name; the list is for pack authors", "Обычно опечатка в имени файла; список для авторов паков"))) _showUnmatched = !_showUnmatched;
                    if (_showUnmatched)
                    {
                        int n = Mathf.Min(_unmatched.Count, 60);
                        for (int i = 0; i < n; i++) { GUI.Label(new Rect(fs * 2.2f, y, w - fs * 3f, fs * 1.4f), _unmatched[i], _sub); y += fs * 1.4f; }
                        if (_unmatched.Count > n) { GUI.Label(new Rect(fs * 2.2f, y, w - fs * 3f, fs * 1.4f), "+" + (_unmatched.Count - n), _sub); y += fs * 1.4f; }
                    }
                }
                y += fs * 0.4f;
            }
        }

        private void PackRow(ref float y, float w, int fs, string name, bool active)
        {
            Event e = Event.current;
            float pad = fs, rh = fs * 3.5f;
            Rect row = new Rect(pad, y, w - pad * 2f, rh);
            bool hover = row.Contains(e.mousePosition);
            BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, active ? 0.07f : (hover ? 0.06f : 0.03f)));
            if (active) BtcReplayUI.Fill(new Rect(row.x, row.y + rh * 0.15f, 3f, rh * 0.7f), BtcMenu.Accent);
            float th = rh - fs * 0.8f;
            Rect tr = new Rect(row.x + fs * 0.8f, row.y + fs * 0.4f, th * 1.7f, th);   // 16:9 thumbnail slot
            Info inf = name == null ? null : InfoOf(name);
            if (inf != null && inf.Thumb != null) GUI.DrawTexture(tr, inf.Thumb, ScaleMode.ScaleAndCrop);
            else
            {
                BtcReplayUI.Fill(tr, new Color(1f, 1f, 1f, 0.05f));
                float g = th * 0.42f;
                bool dflt = name == null;
                BtcReplayIcons.Glyph(dflt ? BtcReplayIcons.Monitor : BtcReplayIcons.Image, new Rect(tr.center.x - g * 0.5f, tr.y + th * 0.12f, g, g), new Color(1f, 1f, 1f, 0.35f));
                if (!dflt) GUI.Label(new Rect(tr.x, tr.y + th * 0.58f, tr.width, th * 0.32f), Tt("no thumbnail", "нет миниатюры"), SmallC());
                H(tr, dflt ? Tt("Default game textures", "Стандартные текстуры игры") : Tt("Put thumbnail.png into the pack folder to show a preview here", "Положите thumbnail.png в папку пака, и здесь появится превью"));
            }
            float tx = tr.xMax + fs * 0.9f, gut = fs * 3.6f;
            GUI.Label(new Rect(tx, row.y + fs * 0.45f, row.xMax - tx - gut, fs * 1.5f), name ?? Tt("Default (game textures)", "По умолчанию (текстуры игры)"), _name);
            string sub = name == null ? Tt("No replacements, vanilla look", "Без замен, ванильный вид") : (inf != null ? inf.Files + Tt(" textures", " текстур") : "...");
            if (active) sub = Tt("Active", "Активен") + "   " + sub;
            GUI.Label(new Rect(tx, row.y + fs * 1.95f, row.xMax - tx - gut, fs * 1.3f), sub, _sub);
            if (active)
            {
                float cg = fs * 1.1f;
                BtcReplayIcons.Glyph(BtcReplayIcons.Check, new Rect(row.xMax - gut - cg * 0.4f, row.center.y - cg * 0.5f, cg, cg), BtcMenu.Accent);
            }
            if (name != null)
            {
                bool fav = _favs.Contains(name);
                Rect sr = new Rect(row.xMax - fs * 2.3f, row.center.y - fs * 0.9f, fs * 1.8f, fs * 1.8f);
                bool sh = sr.Contains(e.mousePosition);
                BtcReplayIcons.Glyph(BtcReplayIcons.Star, new Rect(sr.x + fs * 0.2f, sr.y + fs * 0.2f, fs * 1.4f, fs * 1.4f), fav ? new Color(1f, 0.82f, 0.3f, 1f) : new Color(1f, 1f, 1f, sh ? 0.8f : 0.25f));
                H(sr, fav ? Tt("Unpin from the top", "Открепить сверху") : Tt("Pin to the top", "Закрепить сверху"));
                if (BtcSfx.Btn(sr, "", BtcReplayUI.sBtn))
                {
                    bool was = fav;
                    if (was) _favs.Remove(name); else _favs.Add(name);
                    SaveFavs();
                    string nm = name;
                    BtcUndo.Push(Scope, Tt("Pin: ", "Закрепление: ") + nm, delegate { if (was) _favs.Add(nm); else _favs.Remove(nm); SaveFavs(); });
                }
            }
            if (BtcSfx.Btn(row, "", BtcReplayUI.sBtn) && !active) { SetPackUndo(name); BtcMenu.Toast(Tt("Pack: ", "Пак: ") + (name ?? Tt("Default", "По умолчанию")), BtcReplayIcons.Check); }
            y += rh + 3f;
        }

        // ------------------------------------------------------------------ Hats page
        private void BodyHats(ref float y, float w, int fs)
        {
            int n = ResourcePackManager.HatCount, on = 0;
            for (int i = 0; i < n; i++) if (ResourcePackManager.HatOn(i)) on++;
            if (!F(ref y, w, fs, "packs:hats", Tt("Hat types", "Типы шапок"), n)) return;
            BtcUi.Note(ref y, w, fs, Tt("Halloween hats on units. Each unit type can be switched on or off separately.", "Хэллоуинские шапки на юнитах. Для каждого типа юнитов включаются отдельно."));
            bool allOn = on == n;
            if (_q.Length == 0 && BtcUi.Toggle(ref y, w, fs, Tt("All types", "Все типы") + "  " + on + "/" + n, allOn, null))
            {
                bool[] was = new bool[n];
                for (int i = 0; i < n; i++) { was[i] = ResourcePackManager.HatOn(i); ResourcePackManager.UiHatSet(ResourcePackManager.HatHandle(i), !allOn); }
                BtcUndo.Push(Scope, Tt("All hat types", "Все типы шапок"), delegate { for (int i = 0; i < n; i++) ResourcePackManager.UiHatSet(ResourcePackManager.HatHandle(i), was[i]); });
            }
            for (int i = 0; i < n; i++)
            {
                string label = ResourcePackManager.HatLabel(i), handle = ResourcePackManager.HatHandle(i);
                if (!Matches(label)) continue;
                bool v = ResourcePackManager.HatOn(i);
                BtcUi.ToggleUndo(ref y, w, fs, Scope, label, v, null, delegate (bool nv) { ResourcePackManager.UiHatSet(handle, nv); });
            }
            y += fs * 0.4f;
        }

        // ------------------------------------------------------------------ Tools page
        private void BodyTools(ref float y, float w, int fs)
        {
            if (!F(ref y, w, fs, "packs:tools", Tt("Modder tools", "Инструменты моддера"), 0)) return;
            BtcUi.Note(ref y, w, fs, Tt("Dumps are written to BetterThanCastle/debug/dump. The texture dump is the base for making your own pack.", "Выгрузки пишутся в BetterThanCastle/debug/dump. Выгрузка текстур - основа для своего пака."));
            if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Export, Tt("Dump textures", "Выгрузить текстуры"), Tt("List of the game textures plus their original PNGs", "Список текстур игры и их оригинальные PNG")))
            {
                ResourcePackManager.UiDumpTextures();
                BtcMenu.Toast(Tt("Texture dump started", "Выгрузка текстур запущена"), BtcReplayIcons.Export);
            }
            if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Export, Tt("Dump buildings", "Выгрузить здания"), Tt("HP components of non-bricktron objects: buildings.txt", "Компоненты HP небрикотронных объектов: buildings.txt")))
            {
                ResourcePackManager.UiDumpBuildings();
                BtcMenu.Toast(Tt("Building dump started", "Выгрузка зданий запущена"), BtcReplayIcons.Export);
            }
            if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Folder, Tt("Open the dump folder", "Открыть папку выгрузок"), null)) OpenDir(ResourcePackManager.DumpDir);
            y += fs * 0.4f;
        }
    }
}
