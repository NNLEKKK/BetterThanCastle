using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Profiles page of the Hotkeys tab: named snapshots of every mod bind (and the vanilla keyboard binds) saved as UTF-8 text files next to
    /// hotkeys.txt (hotkey-profiles/*.txt, same "id=value" lines, so they can be shared as text). Apply, overwrite, delete, copy to the clipboard,
    /// paste from the clipboard.
    /// </summary>
    internal static class BtcHotkeysProfiles
    {
        private static string _name = "";
        private static string _confirm;
        private static float _confirmUntil;
        private static List<FileInfo> _files = new List<FileInfo>();
        private static float _scanAt = -10f;
        private static Vector2 _scroll;

        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }

        private static string Dir
        {
            get
            {
                string d = Path.Combine(Path.GetDirectoryName(BtcPaths.Config("hotkeys.txt")), "hotkey-profiles");
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

        internal static string Snapshot()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("# Better Than Castle: hotkey profile\r\n");
            List<string> m = BtcHotkeys.ExportLines();
            for (int i = 0; i < m.Count; i++) sb.Append(m[i]).Append("\r\n");
            List<string> v = BtcVanillaBinds.ExportLines();
            for (int i = 0; i < v.Count; i++) sb.Append(v[i]).Append("\r\n");
            return sb.ToString();
        }

        internal static int Apply(string text)
        {
            string[] lines = text.Replace(((char)13).ToString(), "").Split(new char[] { (char)10 });
            int n = 0;
            BtcUndo.Group("hotkeys", Tt("Profile applied", "Применение профиля"), delegate
            {
                n = BtcHotkeys.ImportLines(lines);
                n += BtcVanillaBinds.ImportLines(lines);
            });
            return n;
        }

        private static string Pretty(FileInfo f) { return Path.GetFileNameWithoutExtension(f.Name); }

        internal static void Draw(BtcHotkeysTab tab, Rect c, int fs)
        {
            Scan();
            Event e = Event.current;
            float pad = fs, ib = fs * 1.9f, y = c.y + fs * 0.4f;
            tab.Txt(new Rect(c.x + pad, y, c.width * 0.4f, ib), Tt("Profiles", "Профили"), tab.Title, Color.white);
            float x = c.xMax - pad - ib;
            if (BtcReplayIcons.Plain(new Rect(x, y, ib, ib), BtcReplayIcons.Folder, Color.white, Tt("Open the profiles folder", "Открыть папку с профилями")))
                try { Application.OpenURL("file:///" + Dir.Replace('\\', '/')); } catch (Exception) { }
            x -= ib + 3f;
            if (BtcReplayIcons.Plain(new Rect(x, y, ib, ib), BtcReplayIcons.ClipIn, Color.white, Tt("Paste a profile from the clipboard and apply it", "Вставить профиль из буфера обмена и применить")))
            {
                string t = GUIUtility.systemCopyBuffer ?? "";
                int n = t.Length > 0 ? Apply(t) : 0;
                tab.Toast(n > 0 ? Tt("Applied from the clipboard: " + n + " settings", "Применено из буфера: " + n + " настроек") : Tt("The clipboard has no profile text", "В буфере нет текста профиля"));
            }
            y += ib + fs * 0.6f;

            // new profile
            Rect nr = new Rect(c.x + pad, y, Mathf.Min(c.width * 0.45f, fs * 24f), ib);
            BtcReplayUI.Fill(nr, new Color(1f, 1f, 1f, 0.06f));
            tab.FieldRect = nr;
            _name = GUI.TextField(new Rect(nr.x + 4f, nr.y, nr.width - 8f, ib), _name, 30, tab.Field);
            if (_name.Length == 0 && GUIUtility.keyboardControl == 0) tab.Txt(new Rect(nr.x + 8f, nr.y, nr.width, ib), Tt("Name of a new profile", "Название нового профиля"), tab.Dim, new Color(1f, 1f, 1f, 0.35f));
            bool can = Safe(_name).Length > 0;
            Rect sr = new Rect(nr.xMax + fs * 0.6f, y, ib, ib);
            if (BtcReplayIcons.Plain(sr, BtcReplayIcons.Plus, can ? BtcMenu.Accent : new Color(1f, 1f, 1f, 0.25f), Tt("Save the current binds as a profile", "Сохранить текущие бинды как профиль")) && can)
            {
                Save(Safe(_name)); _name = ""; GUIUtility.keyboardControl = 0;
                BtcSfx.Ok(); tab.Toast(Tt("Profile saved", "Профиль сохранён"));
            }
            y += ib + fs * 0.4f;
            tab.Txt(new Rect(c.x + pad, y, c.width - pad * 2f, fs * 1.6f), Tt("A profile stores every mod bind and the vanilla keyboard binds. Share one as text: copy it and paste it on another PC.", "Профиль хранит все бинды мода и ванильные клавиатурные бинды. Им можно делиться текстом: скопируй и вставь на другом ПК."), tab.Dim, new Color(1f, 1f, 1f, 0.5f));
            y += fs * 2f;
            BtcReplayUI.Fill(new Rect(c.x + pad, y - fs * 0.2f, c.width - pad * 2f, 1f), BtcMenu.Line);

            float rh = fs * 2.4f;
            Rect view = new Rect(c.x, y, c.width, c.yMax - y);
            float total = rh * (_files.Count + 1) + fs;
            _scroll = GUI.BeginScrollView(view, _scroll, new Rect(0f, 0f, view.width - (total > view.height ? 14f : 0f), total));
            float cy = fs * 0.2f, w = view.width - (total > view.height ? 14f : 0f);
            Row(tab, ref cy, w, fs, rh, Tt("Defaults", "По умолчанию"), null, null);
            for (int i = 0; i < _files.Count; i++) Row(tab, ref cy, w, fs, rh, Pretty(_files[i]), _files[i], null);
            GUI.EndScrollView();
        }

        private static void Save(string name)
        {
            string path = Path.Combine(Dir, name + ".txt");
            string prev = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
            BtcUndo.Push("hotkeys", Tt("Profile: ", "Профиль: ") + name, delegate { try { if (prev == null) File.Delete(path); else File.WriteAllText(path, prev, new UTF8Encoding(false)); _scanAt = -10f; } catch (Exception) { } });
            try { File.WriteAllText(path, Snapshot(), new UTF8Encoding(false)); _scanAt = -10f; }
            catch (Exception ex) { BtcLog.Warn("Hotkeys", "profile save: " + ex.Message); }
        }

private static void DeleteProfile(FileInfo f)        {            try            {                string path = f.FullName, text = File.ReadAllText(path, Encoding.UTF8);                BtcUndo.Push("hotkeys", Tt("Profile deleted: ", "Удалён профиль: ") + Pretty(f), delegate { try { File.WriteAllText(path, text, new UTF8Encoding(false)); _scanAt = -10f; } catch (Exception) { } });                f.Delete();            }            catch (Exception) { }        }
        private static void Row(BtcHotkeysTab tab, ref float y, float w, int fs, float rh, string title, FileInfo f, object unused)
        {
            Event e = Event.current;
            float pad = fs;
            Rect row = new Rect(pad, y, w - pad * 2f, rh);
            BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, row.Contains(e.mousePosition) ? 0.07f : 0.035f));
            tab.Txt(new Rect(row.x + fs * 0.8f, row.y, row.width * 0.5f, rh), title, tab.Lbl, Color.white);
            if (f != null) tab.Txt(new Rect(row.x + row.width * 0.45f, row.y, row.width * 0.2f, rh), f.LastWriteTime.ToString("yyyy-MM-dd HH:mm"), tab.Dim, new Color(1f, 1f, 1f, 0.45f));
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
                    try { GUIUtility.systemCopyBuffer = File.ReadAllText(f.FullName, Encoding.UTF8); tab.Toast(Tt("Copied", "Скопировано")); } catch (Exception) { }
                }
                x -= ib + 3f;
                string ok = "ovr:" + f.Name;
                bool confO = _confirm == ok && Time.unscaledTime < _confirmUntil;
                if (BtcReplayIcons.Plain(new Rect(x, iy, ib, ib), BtcReplayIcons.ClipOut, confO ? new Color(1f, 0.85f, 0.4f, 1f) : Color.white, confO ? Tt("Click again: overwrite with the current binds", "Ещё раз: перезаписать текущими биндами") : Tt("Overwrite this profile with the current binds", "Перезаписать профиль текущими биндами")))
                {
                    if (confO) { Save(Path.GetFileNameWithoutExtension(f.Name)); _confirm = null; BtcSfx.Ok(); tab.Toast(Tt("Profile updated", "Профиль обновлён")); }
                    else { _confirm = ok; _confirmUntil = Time.unscaledTime + 3f; }
                }
                x -= ib + 3f;
            }
            if (BtcReplayIcons.Plain(new Rect(x, iy, ib, ib), BtcReplayIcons.Check, BtcMenu.Accent, f == null ? Tt("Reset every bind to defaults", "Сбросить все бинды по умолчанию") : Tt("Apply this profile", "Применить профиль")))
            {
                if (f == null) { BtcSfx.Delete(); BtcUndo.Group("hotkeys", Tt("Defaults restored", "Сброс на умолчания"), delegate { BtcHotkeys.ResetAll(); BtcVanillaBinds.ResetAll(); }); tab.Toast(Tt("Defaults restored", "Значения по умолчанию восстановлены")); }
                else
                {
                    try { BtcSfx.Ok(); int n = Apply(File.ReadAllText(f.FullName, Encoding.UTF8)); tab.Toast(Tt("Profile applied: " + n + " settings", "Профиль применён: " + n + " настроек")); }
                    catch (Exception ex) { tab.Toast(Tt("Could not read the profile", "Не удалось прочитать профиль")); BtcLog.Warn("Hotkeys", "profile apply: " + ex.Message); }
                }
            }
            y += rh + 3f;
        }
    }
}
