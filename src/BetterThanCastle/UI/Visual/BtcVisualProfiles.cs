using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Profiles page of the Visuals tab: built-in presets, your own named profiles (visual-profiles/*.txt, plain "key=value" lines so they can be
    /// shared as text), copy / paste of a profile through the clipboard, reset. A profile stores every colored-HP setting and palette; everything is
    /// one Ctrl+Z step. Deletes keep the text inside the undo closure.
    /// </summary>
    internal static class BtcVisualProfiles
    {
        private static string _name = "";
        private static string _confirm;
        private static float _confirmUntil;
        private static List<FileInfo> _files = new List<FileInfo>();
        private static float _scanAt = -10f;
        private static Vector2 _scroll;
        private static GUIStyle _tf, _ttl;
        private static int _fs;

        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }

        private static string Dir
        {
            get
            {
                string d = Path.Combine(Path.GetDirectoryName(BtcPaths.Config("hotkeys.txt")), "visual-profiles");
                try { if (!Directory.Exists(d)) Directory.CreateDirectory(d); } catch (Exception) { }
                return d;
            }
        }

        private static void Scan()
        {
            if (Time.unscaledTime - _scanAt < 1.5f) return;
            _scanAt = Time.unscaledTime;
            try
            {
                List<FileInfo> l = new List<FileInfo>(new DirectoryInfo(Dir).GetFiles("*.txt"));
                l.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                _files = l;
            }
            catch (Exception) { _files = new List<FileInfo>(); }
        }

        private static string Safe(string n)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
            return n.Trim();
        }

        // ------------------------------------------------------------------ capture / apply
        internal static string Capture()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("# Better Than Castle: visuals profile\r\n");
            for (int i = 0; i < BtcVisualData.All.Count; i++)
            {
                BtcVisualSetting s = BtcVisualData.All[i];
                try { sb.Append(s.Key).Append('=').Append(s.Export()).Append("\r\n"); } catch (Exception) { }
            }
            for (int i = 0; i < BtcVisualData.Palettes.Count; i++)
            {
                BtcVisualPalette p = BtcVisualData.Palettes[i];
                try
                {
                    BtcPalSnap s = (BtcPalSnap)p.Snap();
                    StringBuilder q = new StringBuilder();
                    for (int k = 0; k < s.Pct.Length; k++) q.Append(k > 0 ? "," : "").Append(s.Pct[k].ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(':').Append(BtcVisualColor.Hex(s.Col[k]));
                    sb.Append(p.Key).Append('=').Append(q).Append(';').Append(s.Second.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
                }
                catch (Exception) { }
            }
            return sb.ToString();
        }

        /// <summary>Applies "key=value" lines (unknown keys are ignored). Returns the number of applied entries. One undo step.</summary>
        internal static int Apply(string text)
        {
            BtcVisualData.Ready();
            if (!BtcVisualData.Ready()) return 0;
            string before = Capture();
            int n = ApplyRaw(text);
            if (n > 0) BtcUndo.Push(BtcVisualRows.Scope, Tt("Profile applied", "Применение профиля"), delegate { ApplyRaw(before); });
            return n;
        }

        private static int ApplyRaw(string text)
        {
            string[] lines = text.Replace("\r", "").Split('\n');
            int n = 0;
            bool prev = BtcUndo.Suspended;
            BtcUndo.Suspended = true;
            try
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    string l = lines[i];
                    int eq = l.IndexOf('=');
                    if (l.Length == 0 || l[0] == '#' || eq <= 0) continue;
                    string k = l.Substring(0, eq), v = l.Substring(eq + 1);
                    BtcVisualSetting s = BtcVisualData.Find(k);
                    if (s != null) { try { s.Import(v); n++; } catch (Exception) { } continue; }
                    for (int pi = 0; pi < BtcVisualData.Palettes.Count; pi++)
                    {
                        BtcVisualPalette p = BtcVisualData.Palettes[pi];
                        if (p.Key != k) continue;
                        try
                        {
                            string[] parts = v.Split(';');
                            string[] stops = parts[0].Split(',');
                            BtcPalSnap sn = new BtcPalSnap { Pct = new float[stops.Length], Col = new Color[stops.Length] };
                            for (int j = 0; j < stops.Length; j++)
                            {
                                string[] pc = stops[j].Split(':');
                                sn.Pct[j] = float.Parse(pc[0], System.Globalization.CultureInfo.InvariantCulture);
                                Color c; BtcVisualColor.TryHex(pc[1], out c); sn.Col[j] = c;
                            }
                            float sec = 0f;
                            if (parts.Length > 1) float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out sec);
                            sn.Second = sec;
                            if (sn.Pct.Length > 0) { p.Restore(sn); n++; }
                        }
                        catch (Exception) { }
                    }
                }
            }
            finally { BtcUndo.Suspended = prev; }
            return n;
        }

        // ------------------------------------------------------------------ built-in presets
        private sealed class Preset { public string En, Ru; public int Icon; public string Tip, TipRu; public string[] Kv; }

        private static readonly string[] Numbers = { "n.hp.own", "n.hp.enemy", "n.st.own", "n.st.enemy", "n.rg.own", "n.rg.enemy", "n.b.hp.own", "n.b.hp.enemy", "n.b.rg.own", "n.b.rg.enemy" };
        private static readonly string[] Extras = { "c.on", "p.on", "b.on", "d.hit.own", "d.hit.enemy", "d.st.own", "d.st.enemy", "d.b.own", "d.b.enemy", "cr.on", "ammo.on" };

        private static string[] Make(string[] on, string[] off)
        {
            List<string> l = new List<string>();
            for (int i = 0; i < on.Length; i++) l.Add(on[i] + "=1");
            for (int i = 0; i < off.Length; i++) l.Add(off[i] + "=0");
            return l.ToArray();
        }

        private static Preset[] _presets;

        private static Preset[] Presets()
        {
            if (_presets != null) return _presets;
            _presets = new[]
            {
                new Preset { En = "Clean", Ru = "Чистый", Icon = BtcReplayIcons.Eye, Tip = "Only the colored rings: no numbers, no map, no paths", TipRu = "Только цветные кольца: без чисел, карт и путей",
                    Kv = Make(new[] { "ring.hp" }, Concat(Numbers, Extras, new[] { "vis.own", "vis.enemy" })) },
                new Preset { En = "Competitive", Ru = "Соревновательный", Icon = BtcReplayIcons.Swords, Tip = "Enemy rings, always-on rings, HP numbers, damage, crystal timer, routes", TipRu = "Кольца врагов, кольца всегда, числа HP, урон, таймер кристалла, маршруты",
                    Kv = Make(new[] { "ring.hp", "ring.enemy", "vis.own", "vis.enemy", "n.hp.own", "n.hp.enemy", "d.hit.own", "d.hit.enemy", "d.dps", "cr.on", "p.on", "ammo.on", "b.on" }, new[] { "n.st.own", "n.st.enemy", "alt.num" }) },
                new Preset { En = "Everything", Ru = "Всё включено", Icon = BtcReplayIcons.Bolt, Tip = "Every number, damage counter, map and route switched on", TipRu = "Все числа, счётчики урона, карты и маршруты включены",
                    Kv = Make(Concat(new[] { "ring.hp", "ring.stam", "ring.ammo", "ring.enemy", "vis.own", "vis.enemy", "vis.ammo" }, Numbers, Extras), new[] { "vis.alt" }) }
            };
            return _presets;
        }

        private static string[] Concat(params string[][] a)
        {
            List<string> l = new List<string>();
            for (int i = 0; i < a.Length; i++) l.AddRange(a[i]);
            return l.ToArray();
        }

        // ------------------------------------------------------------------ page
        private static void Styles(int fs)
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

        internal static void Draw(Rect c, int fs)
        {
            Styles(fs);
            Scan();
            Event e = Event.current;
            float pad = fs, ib = fs * 1.9f, y = c.y + fs * 0.4f;
            BtcVisualRows.Txt(new Rect(c.x + pad, y, c.width * 0.4f, ib), Tt("Profiles", "Профили"), _ttl, Color.white);
            float x = c.xMax - pad - ib;
            if (BtcReplayIcons.Plain(new Rect(x, y, ib, ib), BtcReplayIcons.Folder, Color.white, Tt("Open the profiles folder", "Открыть папку с профилями")))
                try { Application.OpenURL("file:///" + Dir.Replace('\\', '/')); } catch (Exception) { }
            x -= ib + 3f;
            if (BtcReplayIcons.Plain(new Rect(x, y, ib, ib), BtcReplayIcons.Import, Color.white, Tt("Paste a profile from the clipboard and apply it", "Вставить профиль из буфера обмена и применить")))
            {
                string t = GUIUtility.systemCopyBuffer ?? "";
                int n = t.Length > 0 ? Apply(t) : 0;
                BtcMenu.Toast(n > 0 ? Tt("Applied from the clipboard: " + n + " settings", "Применено из буфера: " + n + " настроек") : Tt("The clipboard has no profile text", "В буфере нет текста профиля"), -1);
            }
            x -= ib + 3f;
            if (BtcReplayIcons.Plain(new Rect(x, y, ib, ib), BtcReplayIcons.Export, Color.white, Tt("Copy the current look as profile text (share it anywhere)", "Скопировать текущий вид как текст профиля (можно поделиться)")))
            {
                try { GUIUtility.systemCopyBuffer = Capture(); BtcMenu.Toast(Tt("Profile text copied", "Текст профиля скопирован"), BtcReplayIcons.Copy); } catch (Exception) { }
            }
            y += ib + fs * 0.6f;

            // new profile
            Rect nr = new Rect(c.x + pad, y, Mathf.Min(c.width * 0.45f, fs * 24f), ib);
            BtcReplayUI.Fill(nr, new Color(1f, 1f, 1f, 0.06f));
            BtcUi.Field(nr);
            _name = GUI.TextField(new Rect(nr.x + 4f, nr.y, nr.width - 8f, ib), _name, 30, _tf);
            if (_name.Length == 0 && GUIUtility.keyboardControl == 0) BtcVisualRows.Txt(new Rect(nr.x + 8f, nr.y, nr.width, ib), Tt("Name of a new profile", "Название нового профиля"), BtcVisualRows.Dim, new Color(1f, 1f, 1f, 0.35f));
            bool can = Safe(_name).Length > 0;
            Rect sr = new Rect(nr.xMax + fs * 0.6f, y, ib, ib);
            if (BtcReplayIcons.Plain(sr, BtcReplayIcons.Plus, can ? BtcMenu.Accent : new Color(1f, 1f, 1f, 0.25f), Tt("Save the current look as a profile", "Сохранить текущий вид как профиль")) && can)
            {
                Save(Safe(_name)); _name = ""; GUIUtility.keyboardControl = 0;
                BtcSfx.Ok(); BtcMenu.Toast(Tt("Profile saved", "Профиль сохранён"), -1);
            }
            y += ib + fs * 0.4f;
            BtcVisualRows.Txt(new Rect(c.x + pad, y, c.width - pad * 2f, fs * 1.6f), Tt("A profile stores every colored-HP setting and palette (rings, numbers, paths, explosion map, damage, crystal timer, brightness).", "Профиль хранит все настройки цветных HP и палитры (кольца, числа, пути, карта взрывов, урон, таймер кристалла, яркость)."), BtcVisualRows.Dim, new Color(1f, 1f, 1f, 0.5f));
            y += fs * 2f;
            BtcReplayUI.Fill(new Rect(c.x + pad, y - fs * 0.2f, c.width - pad * 2f, 1f), BtcMenu.Line);

            float rh = fs * 2.4f;
            Rect view = new Rect(c.x, y, c.width, c.yMax - y);
            Preset[] pr = Presets();
            float total = rh * (_files.Count + pr.Length + 2) + fs * 4f;
            float w = view.width - (total > view.height ? 14f : 0f);
            _scroll = GUI.BeginScrollView(view, _scroll, new Rect(0f, 0f, w, total));
            float cy = fs * 0.2f;
            if (BtcVisualRows.Fold(ref cy, w, fs, "vis:prof:pre", BtcReplayIcons.Star, Tt("Presets", "Пресеты"), ""))
            {
                Row(ref cy, w, fs, rh, Tt("Defaults", "По умолчанию"), BtcReplayIcons.Refresh, Tt("Reset every colored-HP setting to defaults", "Сбросить все настройки цветных HP на умолчания"), null, -1);
                for (int i = 0; i < pr.Length; i++) Row(ref cy, w, fs, rh, Tt(pr[i].En, pr[i].Ru), pr[i].Icon, Tt(pr[i].Tip, pr[i].TipRu), null, i);
            }
            cy += fs * 0.4f;
            if (BtcVisualRows.Fold(ref cy, w, fs, "vis:prof:own", BtcReplayIcons.Folder, Tt("My profiles", "Мои профили"), _files.Count > 0 ? _files.Count.ToString() : ""))
            {
                if (_files.Count == 0) BtcUi.Note(ref cy, w, fs, Tt("No profiles yet: type a name above and press +.", "Профилей пока нет: введите название выше и нажмите +."));
                for (int i = 0; i < _files.Count; i++) Row(ref cy, w, fs, rh, Path.GetFileNameWithoutExtension(_files[i].Name), BtcReplayIcons.Palette, null, _files[i], -2);
            }
            GUI.EndScrollView();
        }

        private static void Save(string name)
        {
            string path = Path.Combine(Dir, name + ".txt");
            string prev = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
            BtcUndo.Push(BtcVisualRows.Scope, Tt("Profile: ", "Профиль: ") + name, delegate { try { if (prev == null) File.Delete(path); else File.WriteAllText(path, prev, new UTF8Encoding(false)); _scanAt = -10f; } catch (Exception) { } });
            try { File.WriteAllText(path, Capture(), new UTF8Encoding(false)); _scanAt = -10f; }
            catch (Exception ex) { BtcLog.Warn("Visual", "profile save: " + ex.Message); }
        }

        private static void DeleteProfile(FileInfo f)
        {
            try
            {
                string path = f.FullName, text = File.ReadAllText(path, Encoding.UTF8);
                BtcUndo.Push(BtcVisualRows.Scope, Tt("Profile deleted: ", "Удалён профиль: ") + Path.GetFileNameWithoutExtension(f.Name), delegate { try { File.WriteAllText(path, text, new UTF8Encoding(false)); _scanAt = -10f; } catch (Exception) { } });
                f.Delete();
            }
            catch (Exception) { }
        }

        /// <summary>preset: -1 defaults, >= 0 built-in index, -2 file profile.</summary>
        private static void Row(ref float y, float w, int fs, float rh, string title, int icon, string tip, FileInfo f, int preset)
        {
            Event e = Event.current;
            float pad = fs;
            Rect row = new Rect(pad, y, w - pad * 2f, rh);
            bool hover = row.Contains(e.mousePosition);
            BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, hover ? 0.07f : 0.035f));
            float ig = fs * 1.4f;
            BtcReplayIcons.Glyph(icon, new Rect(row.x + fs * 0.6f, row.center.y - ig * 0.5f, ig, ig), hover ? BtcMenu.Accent : new Color(1f, 1f, 1f, 0.6f));
            BtcVisualRows.Txt(new Rect(row.x + fs * 2.6f, row.y, row.width * 0.5f, rh), title, BtcVisualRows.Lbl, Color.white);
            if (f != null) BtcVisualRows.Txt(new Rect(row.x + row.width * 0.45f, row.y, row.width * 0.2f, rh), f.LastWriteTime.ToString("yyyy-MM-dd HH:mm"), BtcVisualRows.Dim, new Color(1f, 1f, 1f, 0.45f));
            float ib = fs * 1.7f, x = row.xMax - ib - fs * 0.4f, iy = row.y + (rh - ib) * 0.5f;
            if (f != null)
            {
                string key = "del:" + f.Name;
                bool conf = _confirm == key && Time.unscaledTime < _confirmUntil;
                if (BtcReplayIcons.Plain(new Rect(x, iy, ib, ib), BtcReplayIcons.Trash, conf ? new Color(1f, 0.45f, 0.45f, 1f) : Color.white, conf ? Tt("Click again to delete", "Ещё раз: удалить") : Tt("Delete this profile", "Удалить профиль")))
                {
                    if (conf) { BtcSfx.Delete(); DeleteProfile(f); _scanAt = -10f; _confirm = null; }
                    else { _confirm = key; _confirmUntil = Time.unscaledTime + 3f; }
                }
                x -= ib + 3f;
                if (BtcReplayIcons.Plain(new Rect(x, iy, ib, ib), BtcReplayIcons.Copy, Color.white, Tt("Copy the profile text to the clipboard", "Скопировать текст профиля в буфер обмена")))
                {
                    try { GUIUtility.systemCopyBuffer = File.ReadAllText(f.FullName, Encoding.UTF8); BtcMenu.Toast(Tt("Copied", "Скопировано"), BtcReplayIcons.Copy); } catch (Exception) { }
                }
                x -= ib + 3f;
                string ok = "ovr:" + f.Name;
                bool confO = _confirm == ok && Time.unscaledTime < _confirmUntil;
                if (BtcReplayIcons.Plain(new Rect(x, iy, ib, ib), BtcReplayIcons.ClipOut, confO ? new Color(1f, 0.85f, 0.4f, 1f) : Color.white, confO ? Tt("Click again: overwrite with the current look", "Ещё раз: перезаписать текущим видом") : Tt("Overwrite this profile with the current look", "Перезаписать профиль текущим видом")))
                {
                    if (confO) { Save(Path.GetFileNameWithoutExtension(f.Name)); _confirm = null; BtcSfx.Ok(); BtcMenu.Toast(Tt("Profile updated", "Профиль обновлён"), -1); }
                    else { _confirm = ok; _confirmUntil = Time.unscaledTime + 3f; }
                }
                x -= ib + 3f;
            }
            if (tip != null) BtcReplayIcons.Hint(new Rect(row.x, row.y, row.width - ib * 4f, rh), tip);
            if (BtcReplayIcons.Plain(new Rect(x, iy, ib, ib), BtcReplayIcons.Check, BtcMenu.Accent, preset == -1 ? Tt("Reset to defaults", "Сбросить на умолчания") : Tt("Apply", "Применить")))
            {
                BtcSfx.Ok();
                if (!BtcVisualData.Ready()) { }
                else if (preset == -1)
                {
                    string before = Capture();
                    BtcVisualData.Reset();
                    BtcUndo.Push(BtcVisualRows.Scope, Tt("Defaults restored", "Сброс на умолчания"), delegate { ApplyRaw(before); });
                    BtcMenu.Toast(Tt("Defaults restored", "Значения по умолчанию восстановлены"), -1);
                }
                else if (preset >= 0)
                {
                    int n = Apply(string.Join("\n", Presets()[preset].Kv));
                    BtcMenu.Toast(Tt("Preset applied: ", "Пресет применён: ") + title, -1);
                }
                else
                {
                    try { int n = Apply(File.ReadAllText(f.FullName, Encoding.UTF8)); BtcMenu.Toast(Tt("Profile applied: " + n + " settings", "Профиль применён: " + n + " настроек"), -1); }
                    catch (Exception ex) { BtcMenu.Toast(Tt("Could not read the profile", "Не удалось прочитать профиль"), -1); BtcLog.Warn("Visual", "profile apply: " + ex.Message); }
                }
            }
            y += rh + 3f;
        }
    }
}
