using System;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Bug report tab (rail icon: bug). A form + automatically collected diagnostics (BtcReportData). "Send" saves the full report under
    /// <user folder>/reports, copies it to the clipboard and opens a prefilled GitHub issue (no token is shipped in the DLL; the browser account of
    /// the player does the sending). Nothing leaves the PC without the player pressing the button.
    /// </summary>
    internal sealed class BtcReportTab : BtcMenuTab
    {
        internal static bool TabActive;
        internal static readonly BtcReportTab Instance = new BtcReportTab();

        private const string Scope = "report";
        private readonly BtcReportData.Form _f = new BtcReportData.Form { Area = 8, Severity = 1 };
        private Vector2 _scroll;
        private float _h = 100f;
        private bool _preview;
        private string _status, _lastFile;
        private Color _statusCol = Color.white;
        private GUIStyle _lbl, _dim, _field, _chip;
        private int _fs;

        internal override int Id { get { return 9; } }
        internal override bool IsActive { get { return TabActive; } }
        internal override string UndoScope { get { return Scope; } }

        /// <summary>Called when the tab opens: guess the area from the tab the player came from.</summary>
        internal static void OnOpen(int fromTab)
        {
            // indexed by tab id (packs, visuals, BC, hotkeys, gameplay, share, profile, replay, duel) -> BtcReportData.AreaKeys index
            int[] map = { 6, 2, 4, 1, 3, 8, 7, 0, 5 };
            int a = fromTab >= 0 && fromTab <= 8 ? map[fromTab] : 8;
            if (Instance._f.Title.Length == 0 && Instance._f.What.Length == 0) Instance._f.Area = a;
        }

        private static string T(string en, string ru) { return BtcReplayUI.T(en, ru); }

        private void Styles(int fs)
        {
            if (_lbl != null && _fs == fs) return;
            _fs = fs;
            GUIStyle b = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
            _lbl = new GUIStyle(b); _lbl.fontSize = Mathf.RoundToInt(fs * 0.95f); _lbl.alignment = TextAnchor.MiddleLeft; _lbl.normal.textColor = Color.white; _lbl.clipping = TextClipping.Clip;
            _dim = new GUIStyle(_lbl); _dim.fontSize = Mathf.RoundToInt(fs * 0.78f); _dim.normal.textColor = new Color(1f, 1f, 1f, 0.5f);
            _field = new GUIStyle(GUI.skin.textField); _field.font = b.font; _field.fontSize = Mathf.RoundToInt(fs * 0.9f); _field.alignment = TextAnchor.UpperLeft;
            _field.normal.background = null; _field.focused.background = null; _field.hover.background = null; _field.active.background = null;
            _field.normal.textColor = Color.white; _field.focused.textColor = Color.white; _field.hover.textColor = Color.white;
            _field.padding = new RectOffset(Mathf.RoundToInt(fs * 0.5f), Mathf.RoundToInt(fs * 0.5f), Mathf.RoundToInt(fs * 0.35f), Mathf.RoundToInt(fs * 0.35f));
            _field.wordWrap = true; _field.clipping = TextClipping.Clip;
            _chip = new GUIStyle(_lbl); _chip.alignment = TextAnchor.MiddleCenter; _chip.fontSize = Mathf.RoundToInt(fs * 0.85f);
        }

        private void Label(float x, ref float y, float w, string s)
        {
            GUI.Label(new Rect(x, y, w, _fs * 1.5f), s, _dim);
            y += _fs * 1.5f;
        }

        /// <summary>Row of choice chips (wraps). Returns the clicked index or -1.</summary>
        private int Chips(ref float y, float w, string[] names, int active, Color on)
        {
            Event e = Event.current;
            float pad = _fs, x = pad, h = _fs * 1.9f;
            int clicked = -1;
            for (int i = 0; i < names.Length; i++)
            {
                float cw = _chip.CalcSize(new GUIContent(names[i])).x + _fs * 1.6f;
                if (x + cw > w - pad && x > pad) { x = pad; y += h + _fs * 0.35f; }
                Rect r = new Rect(x, y, cw, h);
                bool isOn = i == active, hover = r.Contains(e.mousePosition);
                BtcReplayUI.Fill(r, isOn ? new Color(on.r, on.g, on.b, 0.2f) : new Color(1f, 1f, 1f, hover ? 0.09f : 0.04f));
                if (isOn) BtcReplayUI.Fill(new Rect(r.x, r.yMax - 2f, r.width, 2f), on);
                Color oc = GUI.contentColor;
                GUI.contentColor = isOn ? Color.white : new Color(1f, 1f, 1f, 0.7f);
                GUI.Label(r, names[i], _chip);
                GUI.contentColor = oc;
                if (BtcSfx.Btn(r, "", BtcReplayUI.sBtn)) clicked = i;
                x += cw + _fs * 0.35f;
            }
            y += h + _fs * 0.6f;
            return clicked;
        }

        /// <summary>A text field / area on a flat panel with a placeholder. Returns the edited text.</summary>
        private string Edit(ref float y, float w, string name, string text, float rows, int max, string hint)
        {
            float pad = _fs, h = _fs * (rows * 1.25f + 0.9f);
            Rect r = new Rect(pad, y, w - pad * 2f, h);
            bool focus = GUI.GetNameOfFocusedControl() == name;
            BtcReplayUI.Fill(r, new Color(1f, 1f, 1f, focus ? 0.09f : 0.05f));
            BtcReplayUI.Fill(new Rect(r.x, r.yMax - 1f, r.width, focus ? 2f : 1f), focus ? BtcMenu.Accent : BtcMenu.Line);
            BtcUi.Field(r);
            GUI.SetNextControlName(name);
            string t = rows > 1.5f ? GUI.TextArea(r, text, max, _field) : GUI.TextField(r, text, max, _field);
            if (text.Length == 0 && !focus) GUI.Label(new Rect(r.x + _fs * 0.5f, r.y + _fs * 0.35f, r.width - _fs, _fs * 1.4f), hint, _dim);
            y += h + _fs * 0.6f;
            return t;
        }

        private bool Valid { get { return _f.Title.Trim().Length >= 4 && _f.What.Trim().Length >= 10; } }

        private string Missing()
        {
            if (_f.Title.Trim().Length < 4) return T("Add a short title (4+ characters)", "Добавь короткий заголовок (от 4 символов)");
            if (_f.What.Trim().Length < 10) return T("Describe what happened (10+ characters)", "Опиши, что произошло (от 10 символов)");
            return null;
        }

        internal override void Draw(Rect area, int fs)
        {
            BtcReplayUI.Ensure(fs);
            Styles(fs);
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == BtcMenu.MenuKeyMain)
            {
                GUIUtility.keyboardControl = 0;
                BtcMenu.PgDownFrame = Time.frameCount;
                ResourcePackManager.CloseMenu();
                e.Use();
                return;
            }
            BtcSfx.Typing(e);
            Rect view = new Rect(0f, 0f, area.width - (_h > area.height ? 14f : 0f), _h);
            _scroll = GUI.BeginScrollView(area, _scroll, view);
            BtcUi.ClickAway(e);
            float w = view.width, y = fs * 0.4f;
            try { Body(ref y, w, fs); }
            catch (Exception ex) { if (e.type == EventType.Repaint) BtcLog.Error("Report", "draw: " + ex); }
            BtcReplayIcons.DrawTip(fs, w, BtcReplayIcons.TipAnchor.y - _scroll.y < fs * 3f); // below the icon near the top edge, else it hides under the view edge
            GUI.EndScrollView();
            _h = y + fs * 2f;
        }

        private void Body(ref float y, float w, int fs)
        {
            BtcUi.Note(ref y, w, fs, T("Found a bug? Describe it here. The report adds system info and the latest errors from the log, so we can usually fix it without asking you anything. Nothing is sent until you press the button; you can switch any attached part off.",
                "Нашёл баг? Опиши его здесь. К отчёту автоматически добавятся данные о системе и последние ошибки из лога, чтобы мы могли чинить без лишних вопросов. Ничего не отправляется, пока ты не нажмёшь кнопку; любую часть вложения можно отключить."));
            y += fs * 0.3f;

            BtcUi.Section(ref y, w, fs, T("What went wrong", "Что пошло не так"));
            Label(fs, ref y, w, T("Where", "Где"));
            string[] an = new string[BtcReportData.AreaEn.Length];
            for (int i = 0; i < an.Length; i++) an[i] = T(BtcReportData.AreaEn[i], BtcReportData.AreaRu[i]);
            int c = Chips(ref y, w, an, _f.Area, BtcMenu.Accent);
            if (c >= 0 && c != _f.Area) { int old = _f.Area; _f.Area = c; BtcUndo.Push(Scope, T("area", "область"), delegate { _f.Area = old; }); }

            Label(fs, ref y, w, T("How bad", "Насколько плохо"));
            string[] sn = new string[BtcReportData.SevEn.Length];
            for (int i = 0; i < sn.Length; i++) sn[i] = T(BtcReportData.SevEn[i], BtcReportData.SevRu[i]);
            c = Chips(ref y, w, sn, _f.Severity, _f.Severity == 0 ? new Color(1f, 0.45f, 0.4f) : BtcMenu.Accent);
            if (c >= 0 && c != _f.Severity) { int old = _f.Severity; _f.Severity = c; BtcUndo.Push(Scope, T("severity", "серьёзность"), delegate { _f.Severity = old; }); }

            Label(fs, ref y, w, T("Short title", "Короткий заголовок"));
            _f.Title = Edit(ref y, w, "btcrep_title", _f.Title, 1f, 90, T("e.g. Replay freezes when I click the timeline", "например: реплей зависает при клике по таймлайну"));
            Label(fs, ref y, w, T("What happened", "Что произошло"));
            _f.What = Edit(ref y, w, "btcrep_what", _f.What, 5f, 3000, T("Describe what you saw", "Опиши, что ты увидел"));
            Label(fs, ref y, w, T("Steps to reproduce (optional)", "Как повторить (необязательно)"));
            _f.Steps = Edit(ref y, w, "btcrep_steps", _f.Steps, 4f, 3000, T("1. ...  2. ...  3. ...", "1. ...  2. ...  3. ..."));
            Label(fs, ref y, w, T("What you expected (optional)", "Что ожидал (необязательно)"));
            _f.Expected = Edit(ref y, w, "btcrep_exp", _f.Expected, 1f, 400, T("What should have happened", "Что должно было произойти"));

            if (BtcUi.Fold(ref y, w, fs, "report:attach", T("Attached info", "Вложения"), BtcReportData.Sections.Count))
            {
                for (int i = 0; i < BtcReportData.Sections.Count; i++)
                {
                    BtcReportData.Section s = BtcReportData.Sections[i];
                    string hint = s.On ? T(BtcReportData.Lines(BtcReportData.Text(s)) + " lines", BtcReportData.Lines(BtcReportData.Text(s)) + " строк") : null;
                    BtcReportData.Section sec = s;
                    BtcUi.ToggleUndo(ref y, w, fs, Scope, T(s.En, s.Ru), s.On, hint, delegate (bool v) { sec.On = v; });
                }
                BtcUi.Note(ref y, w, fs, T("Your Windows user name is removed from paths. Only the mod's own log and settings are read; no files are uploaded by the game.",
                    "Имя пользователя Windows вырезается из путей. Читается только лог и настройки мода; игра ничего не загружает сама."));
            }

            if (_preview)
            {
                BtcUi.Section(ref y, w, fs, T("Report preview", "Предпросмотр отчёта"));
                string full = BtcReportData.Full(_f);
                string[] ln = full.Replace("\r", "").Split('\n');
                int n = Mathf.Min(ln.Length, 40);
                float lh = fs * 1.15f;
                Rect pr = new Rect(fs, y, w - fs * 2f, n * lh + fs * 0.8f);
                BtcReplayUI.Fill(pr, new Color(0f, 0f, 0f, 0.35f));
                for (int i = 0; i < n; i++) GUI.Label(new Rect(pr.x + fs * 0.5f, pr.y + fs * 0.4f + i * lh, pr.width - fs, lh), ln[i], _dim);
                y += pr.height + fs * 0.3f;
                if (ln.Length > n) { GUI.Label(new Rect(fs, y, w - fs * 2f, fs * 1.4f), "... +" + (ln.Length - n) + T(" more lines (copy or save to see everything)", " строк ещё (скопируй или сохрани, чтобы увидеть всё)"), _dim); y += fs * 1.6f; }
            }

            y += fs * 0.4f;
            DrawActions(ref y, w, fs);
            if (_status != null)
            {
                Color oc = GUI.contentColor; GUI.contentColor = _statusCol;
                GUI.Label(new Rect(fs, y, w - fs * 2f, fs * 1.6f), _status, _lbl);
                GUI.contentColor = oc;
                y += fs * 1.8f;
            }
            if (_lastFile != null) { GUI.Label(new Rect(fs, y, w - fs * 2f, fs * 1.4f), T("Saved: ", "Сохранено: ") + _lastFile, _dim); y += fs * 1.6f; }
        }

        private void DrawActions(ref float y, float w, int fs)
        {
            Event e = Event.current;
            float h = fs * 2.6f, x = fs;
            bool ok = Valid;
            // primary: send
            string sendTxt = T("Send to GitHub", "Отправить на GitHub");
            float bw = _lbl.CalcSize(new GUIContent(sendTxt)).x + fs * 4.2f;
            Rect pr = new Rect(x, y, bw, h);
            bool hov = pr.Contains(e.mousePosition);
            Color ac = BtcMenu.Accent;
            BtcReplayUI.Fill(pr, ok ? new Color(ac.r, ac.g, ac.b, hov ? 1f : 0.85f) : new Color(1f, 1f, 1f, 0.08f));
            Color ink = ok ? new Color(0.03f, 0.06f, 0.04f, 1f) : new Color(1f, 1f, 1f, 0.35f);
            BtcReplayIcons.Glyph(BtcReplayIcons.Share, new Rect(pr.x + fs * 0.8f, pr.center.y - fs * 0.7f, fs * 1.4f, fs * 1.4f), ink);
            Color oc = GUI.contentColor; GUI.contentColor = ink;
            GUI.Label(new Rect(pr.x + fs * 2.6f, pr.y, pr.width - fs * 2.6f, pr.height), sendTxt, _lbl);
            GUI.contentColor = oc;
            BtcReplayIcons.Hint(pr, ok ? T("Saves the report, copies it to the clipboard and opens a prefilled GitHub issue. Press Ctrl+V in the issue to paste the full diagnostics.",
                "Сохраняет отчёт, копирует в буфер и открывает готовую заявку на GitHub. В заявке нажми Ctrl+V, чтобы вставить полную диагностику.") : Missing());
            if (BtcSfx.Btn(pr, "", BtcReplayUI.sBtn))
            {
                if (!ok) { BtcSfx.Warn(); SetStatus(Missing(), new Color(1f, 0.75f, 0.35f)); }
                else Send();
            }
            x = pr.xMax + fs * 0.8f;
            float ib = h;
            if (Icon(ref x, y, ib, BtcReplayIcons.Copy, T("Copy the full report to the clipboard", "Скопировать полный отчёт в буфер"), ok)) CopyOnly();
            if (Icon(ref x, y, ib, BtcReplayIcons.Export, T("Save the report as a file (reports folder)", "Сохранить отчёт в файл (папка reports)"), ok)) SaveOnly();
            if (Icon(ref x, y, ib, BtcReplayIcons.Folder, T("Open the reports folder", "Открыть папку с отчётами"), true)) OpenFolder();
            if (Icon(ref x, y, ib, BtcReplayIcons.Eye, _preview ? T("Hide the preview", "Скрыть предпросмотр") : T("Preview exactly what will be attached", "Показать, что именно будет приложено"), true, _preview)) { _preview = !_preview; BtcSfx.Switch(_preview); }
            if (Icon(ref x, y, ib, BtcReplayIcons.Trash, T("Clear the form (Ctrl+Z restores it)", "Очистить форму (Ctrl+Z вернёт)"), _f.Title.Length + _f.What.Length + _f.Steps.Length + _f.Expected.Length > 0)) ClearForm();
            y += h + fs * 0.6f;
        }

        private bool Icon(ref float x, float y, float s, int icon, string tip, bool enabled) { return Icon(ref x, y, s, icon, tip, enabled, false); }

        private bool Icon(ref float x, float y, float s, int icon, string tip, bool enabled, bool on)
        {
            Event e = Event.current;
            Rect r = new Rect(x, y, s, s);
            x += s + _fs * 0.4f;
            bool hov = r.Contains(e.mousePosition) && enabled;
            BtcReplayUI.Fill(r, on ? new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.2f) : new Color(1f, 1f, 1f, hov ? 0.12f : 0.05f));
            float g = s * 0.55f;
            BtcReplayIcons.Glyph(icon, new Rect(r.center.x - g * 0.5f, r.center.y - g * 0.5f, g, g), on ? BtcMenu.Accent : new Color(1f, 1f, 1f, enabled ? (hov ? 1f : 0.7f) : 0.22f));
            BtcReplayIcons.Hint(r, tip);
            bool prev = GUI.enabled; GUI.enabled = enabled;
            bool c = BtcSfx.Btn(r, "", BtcReplayUI.sBtn);
            GUI.enabled = prev;
            return c;
        }

        private void SetStatus(string s, Color c) { _status = s; _statusCol = c; }

        private void Send()
        {
            try
            {
                string full = BtcReportData.Full(_f);
                _lastFile = BtcReportData.Save(_f);
                GUIUtility.systemCopyBuffer = full;
                Application.OpenURL(BtcReportData.IssueUrl(_f));
                BtcSfx.Ok();
                BtcMenu.Toast(T("GitHub opened: press Ctrl+V in the issue to paste the diagnostics", "GitHub открыт: нажми Ctrl+V в заявке, чтобы вставить диагностику"), BtcReplayIcons.Bug);
                SetStatus(T("Report saved and copied. In the browser: Ctrl+V pastes the full diagnostics, then press Submit.", "Отчёт сохранён и скопирован. В браузере: Ctrl+V вставит полную диагностику, потом нажми Submit."), BtcMenu.Accent);
                BtcLog.Info("Report", "sent via issue url, file " + _lastFile);
            }
            catch (Exception ex) { BtcLog.Error("Report", "send: " + ex); SetStatus(T("Could not open the browser: ", "Не удалось открыть браузер: ") + ex.Message, new Color(1f, 0.5f, 0.45f)); BtcSfx.Warn(); }
        }

        private void CopyOnly()
        {
            try
            {
                GUIUtility.systemCopyBuffer = BtcReportData.Full(_f);
                BtcSfx.Ok();
                BtcMenu.Toast(T("Report copied to the clipboard", "Отчёт скопирован в буфер"), BtcReplayIcons.Copy);
            }
            catch (Exception ex) { BtcLog.Error("Report", "copy: " + ex); }
        }

        private void SaveOnly()
        {
            try
            {
                _lastFile = BtcReportData.Save(_f);
                BtcSfx.Ok();
                BtcMenu.Toast(T("Report saved", "Отчёт сохранён"), BtcReplayIcons.Export);
            }
            catch (Exception ex) { BtcLog.Error("Report", "save: " + ex); SetStatus(ex.Message, new Color(1f, 0.5f, 0.45f)); }
        }

        private void OpenFolder()
        {
            try
            {
                System.IO.Directory.CreateDirectory(BtcReportData.ReportDir);
                Application.OpenURL("file:///" + BtcReportData.ReportDir.Replace('\\', '/'));
            }
            catch (Exception ex) { BtcLog.Error("Report", "folder: " + ex); }
        }

        private void ClearForm()
        {
            string t = _f.Title, w = _f.What, s = _f.Steps, x = _f.Expected;
            _f.Title = ""; _f.What = ""; _f.Steps = ""; _f.Expected = "";
            BtcSfx.Delete();
            BtcUndo.Push(Scope, T("clear form", "очистка формы"), delegate { _f.Title = t; _f.What = w; _f.Steps = s; _f.Expected = x; });
        }
    }
}
