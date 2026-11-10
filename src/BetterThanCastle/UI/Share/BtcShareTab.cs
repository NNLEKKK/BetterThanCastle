using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Share tab (menu tab 5) in the new menu style: delivery of the map to clients (.btcs instead of the vanilla zip, never the thumbnail),
    /// .btcs settings, import by link and the list of saves and maps (pack, copy file, create link, back to a normal save).
    /// Logic stays in BtcShareUI / BtcSaveFormat / BtcReplayShare; this class only draws. Undo scope "share".
    /// </summary>
    internal sealed class BtcShareTab : BtcMenuTab
    {
        internal static readonly BtcShareTab Instance = new BtcShareTab();

        internal override int Id { get { return 5; } }
        internal override bool IsActive { get { return BtcMenu.CurrentId == 5; } }
        internal override string UndoScope { get { return Scope; } }

        internal const string Scope = "share";

        private Vector2 _scroll;
        private float _h;
        private GUIStyle _ttl, _lab, _dim, _tf;
        private int _fs;
        private Vector2 _origin;
        private readonly List<string> _keys = new List<string>();
        private string _confirm;
        private float _confirmUntil;
        private bool _thumbDirty;

        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }

        private string Query { get { return BtcMenuState.Get("share:q", ""); } set { BtcMenuState.Set("share:q", value ?? ""); } }
        private int Filter { get { return BtcMenuState.Int("share:filter", 0); } set { BtcMenuState.SetInt("share:filter", value); } }

        private void Styles(int fs)
        {
            if (_ttl != null && _fs == fs) return;
            _fs = fs;
            GUIStyle b = BtcReplayUI.sBarLabel ?? BtcReplayUI.sLabel;
            _ttl = new GUIStyle(b); _ttl.fontSize = Mathf.RoundToInt(fs * 1.15f); _ttl.fontStyle = FontStyle.Bold; _ttl.alignment = TextAnchor.MiddleLeft; _ttl.normal.textColor = Color.white;
            _lab = new GUIStyle(b); _lab.fontSize = Mathf.RoundToInt(fs * 0.95f); _lab.alignment = TextAnchor.MiddleLeft; _lab.clipping = TextClipping.Clip; _lab.normal.textColor = Color.white;
            _dim = new GUIStyle(_lab); _dim.fontSize = Mathf.RoundToInt(fs * 0.8f); _dim.normal.textColor = new Color(1f, 1f, 1f, 0.5f);
            _tf = new GUIStyle(GUI.skin.textField); _tf.font = b.font; _tf.fontSize = Mathf.RoundToInt(fs * 0.9f); _tf.alignment = TextAnchor.MiddleLeft; _tf.padding = new RectOffset(4, 4, 0, 0);
            _tf.normal.background = null; _tf.focused.background = null; _tf.hover.background = null; _tf.active.background = null;
            _tf.normal.textColor = Color.white; _tf.focused.textColor = Color.white; _tf.hover.textColor = Color.white;
        }

        internal override void Draw(Rect area, int fs)
        {
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
            BtcUi.ClickAway(e);
            BtcSaveFormat.LoadSettings();
            BtcReplayShare.Poll();
            BtcShareUI.PollJobs();
            BtcShareUI.PollImport();
            if (!BtcShareUI.listLoaded) BtcShareUI.Refresh();

            float pad = fs, ib = fs * 1.9f;
            float y0 = area.y + fs * 0.4f;
            GUI.Label(new Rect(area.x + pad, y0, area.width * 0.5f, ib), Tt("Share saves and maps", "Обмен сейвами и картами"), _ttl);

            // master switch (like Advanced Hotkeys): off = the host uses the plain vanilla map transfer to clients
            bool son = BtcSaveFormat.ShareOn;
            Rect sw = new Rect(area.xMax - pad - fs * 2.4f, y0 + (ib - fs * 1.2f) * 0.5f, fs * 2.4f, fs * 1.2f);
            if (BtcUi.HeaderSwitch(sw, son, son ? Tt("Share improvements are ON (click to use the vanilla map transfer)", "Улучшения передачи включены (клик - ванильная передача карты)") : Tt("Share improvements are OFF: vanilla map transfer (click to turn on)", "Улучшения передачи выключены: ванильная передача карты (клик - включить)")))
            {
                BtcSaveFormat.ShareOn = !son; BtcSaveFormat.SaveSettings(); BtcSfx.Switch(!son);
                BtcUndo.Push(Scope, Tt("Share switch", "Переключатель обмена"), delegate { BtcSaveFormat.ShareOn = son; BtcSaveFormat.SaveSettings(); });
            }

            float y = y0 + ib + fs * 0.5f;
            float fx = area.x + pad;
            bool allOpen = true;
            for (int i = 0; i < _keys.Count; i++) if (!BtcUi.IsOpen(_keys[i])) { allOpen = false; break; }
            if (BtcReplayIcons.Plain(new Rect(fx, y, ib, ib), allOpen ? BtcReplayIcons.ChevD : BtcReplayIcons.ChevR, Color.white, allOpen ? Tt("Collapse all sections", "Свернуть все разделы") : Tt("Expand all sections", "Развернуть все разделы")))
                BtcUi.SetOpen(_keys, !allOpen);
            if (BtcReplayIcons.Plain(new Rect(fx + ib + 3f, y, ib, ib), BtcReplayIcons.Refresh, Color.white, Tt("Rescan the saves and maps folders", "Пересканировать папки сейвов и карт")))
                BtcShareUI.Refresh();
            // search + filter chips
            float sx = fx + (ib + 3f) * 2f + fs * 0.6f;
            string[] fn = { Tt("All", "Все"), Tt("Saves", "Сейвы"), Tt("Maps", "Карты") };
            float cw = fs * 4.6f;
            float chipsW = (cw + 3f) * fn.Length;
            Rect sr = new Rect(sx, y + (ib - fs * 1.6f) * 0.5f, area.xMax - pad - sx - chipsW - fs * 0.6f, fs * 1.6f);
            BtcReplayUI.Fill(sr, new Color(1f, 1f, 1f, 0.06f));
            BtcUi.Field(sr);
            string q = GUI.TextField(new Rect(sr.x + fs * 0.4f, sr.y, sr.width - fs * 0.8f, sr.height), Query, 64, _tf);
            if (q != Query) Query = q;
            if (string.IsNullOrEmpty(Query) && GUIUtility.keyboardControl == 0) GUI.Label(new Rect(sr.x + fs * 0.5f, sr.y, sr.width, sr.height), Tt("Search saves and maps...", "Поиск сейвов и карт..."), _dim);
            for (int i = 0; i < fn.Length; i++)
            {
                Rect cr = new Rect(sr.xMax + fs * 0.6f + i * (cw + 3f), sr.y, cw, sr.height);
                bool on = Filter == i, hov = cr.Contains(e.mousePosition);
                BtcReplayUI.Fill(cr, on ? new Color(BtcMenu.Accent.r, BtcMenu.Accent.g, BtcMenu.Accent.b, 0.25f) : new Color(1f, 1f, 1f, hov ? 0.12f : 0.06f));
                GUIStyle cs = new GUIStyle(_lab); cs.alignment = TextAnchor.MiddleCenter;
                GUI.Label(cr, fn[i], cs);
                if (BtcSfx.Btn(cr, "", BtcReplayUI.sBtn)) Filter = i;
            }
            y += ib + fs * 0.6f;
            BtcReplayUI.Fill(new Rect(area.x + pad, y - fs * 0.25f, area.width - pad * 2f, 1f), BtcMenu.Line);

            Rect view = new Rect(area.x, y, area.width, area.yMax - y);
            float vw = view.width - (_h > view.height ? 14f : 0f);
            _origin = new Vector2(view.x, view.y) - _scroll;
            _scroll = GUI.BeginScrollView(view, _scroll, new Rect(0f, 0f, vw, _h));
            float cy = fs * 0.2f;
            _keys.Clear();
            Body(ref cy, vw, fs);
            GUI.EndScrollView();
            _h = cy + fs * 3f;
            BtcReplayIcons.DrawTip(fs, area.xMax, false);
        }

        private string KeyAdd(string k) { _keys.Add(k); return k; }

        private void Body(ref float y, float w, int fs)
        {
            if (BtcUi.Fold(ref y, w, fs, KeyAdd("share:deliver"), Tt("Map delivery to clients", "Передача карты клиентам"), 0))
            {
                BtcUi.ToggleUndo(ref y, w, fs, Scope, Tt("Send the map as .btcs", "Передавать карту как .btcs"), BtcSaveFormat.SendBtcs,
                    Tt("Smaller and faster than the vanilla zip; the client unpacks it automatically like a normal download", "Меньше и быстрее ванильного zip; клиент распаковывает сам, как обычную загрузку"),
                    delegate (bool v) { BtcSaveFormat.SendBtcs = v; BtcSaveFormat.SaveSettings(); });
                BtcUi.ToggleUndo(ref y, w, fs, Scope, Tt("Never send the thumbnail", "Никогда не передавать тамбнейл"), !BtcSaveFormat.SendThumb,
                    Tt("Clients do not need the preview picture; less data to download", "Клиентам не нужна картинка-превью; меньше данных"),
                    delegate (bool v) { BtcSaveFormat.SendThumb = !v; BtcSaveFormat.SaveSettings(); });
                string st = !BtcSaveFormat.ShareOn ? Tt("The master switch in the header is OFF: the vanilla transfer is used.", "Главный переключатель в шапке выключен: используется ванильная передача.") : !BtcBc.BcEnabled ? Tt("BetterConnection is OFF: the vanilla transfer is used, nothing changes on the wire.", "BetterConnection выключен: используется ванильная передача, в сети ничего не меняется.")
                    : Tt("Works for clients that have BetterConnection. Vanilla clients still get the usual zip.", "Работает для клиентов с BetterConnection. Ванильные клиенты получают обычный zip.");
                BtcUi.Note(ref y, w, fs, st);
                y += fs * 0.3f;
            }
            if (BtcUi.Fold(ref y, w, fs, KeyAdd("share:btcs"), Tt(".btcs files", "Файлы .btcs"), 0))
            {
                BtcUi.Note(ref y, w, fs, Tt("Saves and maps as single compact .btcs files: copy the file, make a download link, or turn it back into a normal save. The .btcs lies next to the original folder. Heavy work runs in the background. Extra files of the mod (infinity heal, carried wards) are packed too.", "Сейвы и карты в виде одного компактного файла .btcs: скопировать файл, создать ссылку или превратить обратно в обычный сейв. Файл .btcs лежит рядом с исходной папкой. Тяжёлая работа идёт в фоне. Дополнительные файлы мода (бесконечное лечение, носимые варды) тоже упаковываются."));
                BtcUi.ToggleUndo(ref y, w, fs, Scope, Tt("Store thumbnail inside .btcs", "Хранить тамбнейл внутри .btcs"), BtcSaveFormat.IncludeThumb,
                    Tt("Off = the thumbnail is read from the normal folder", "Выкл. = тамбнейл читается из обычной папки"),
                    delegate (bool v) { BtcSaveFormat.IncludeThumb = v; BtcSaveFormat.SaveSettings(); });
                if (BtcSaveFormat.IncludeThumb)
                {
                    int f = Mathf.Clamp(BtcSaveFormat.ThumbFactor, 1, 15);
                    int d = BtcUi.Stepper(ref y, w, fs, Tt("Thumbnail downscale", "Уменьшение тамбнейла"), f == 1 ? Tt("original", "оригинал") : f + "x");
                    if (d != 0)
                    {
                        int nf = Mathf.Clamp(f + d, 1, 15);
                        BtcSaveFormat.ThumbFactor = nf; BtcSaveFormat.SaveSettings();
                        BtcUndo.Push(Scope, Tt("Thumbnail downscale", "Уменьшение тамбнейла"), delegate { BtcSaveFormat.ThumbFactor = f; BtcSaveFormat.SaveSettings(); }, "share:thumbf");
                    }
                    BtcUi.Note(ref y, w, fs, Tt("Heightmap.png always stays in full quality. Only the thumbnail is reduced (1x = original, 15x = about 15 times smaller side).", "Heightmap.png всегда остаётся в полном качестве. Уменьшается только тамбнейл (1x = оригинал, 15x = сторона примерно в 15 раз меньше)."));
                }
                y += fs * 0.3f;
            }
            if (BtcUi.Fold(ref y, w, fs, KeyAdd("share:import"), Tt("Import by link", "Импорт по ссылке"), 0))
                ImportBlock(ref y, w, fs);
            if (BtcUi.Fold(ref y, w, fs, KeyAdd("share:list"), Tt("Saves and maps", "Сейвы и карты"), BtcShareUI.list.Count))
                ListBlock(ref y, w, fs);
        }

        private void ImportBlock(ref float y, float w, int fs)
        {
            float pad = fs, h = fs * 2.1f, bw = fs * 6f;
            Rect fr = new Rect(pad, y + h * 0.1f, w - pad * 2f - bw * 2f - fs, h * 0.8f);
            BtcReplayUI.Fill(fr, new Color(1f, 1f, 1f, 0.06f));
            BtcUi.Field(fr);
            BtcShareUI.importUrl = GUI.TextField(new Rect(fr.x + fs * 0.4f, fr.y, fr.width - fs * 0.8f, fr.height), BtcShareUI.importUrl, 2048, _tf);
            if (BtcTextBtn(new Rect(fr.xMax + fs * 0.5f, fr.y, bw, fr.height), Tt("Paste", "Вставить"), Color.white))
            {
                string c = GUIUtility.systemCopyBuffer;
                if (!string.IsNullOrEmpty(c)) BtcShareUI.importUrl = c.Trim();
            }
            bool busy = BtcShareUI.dl != null;
            if (BtcTextBtn(new Rect(fr.xMax + fs + bw, fr.y, bw, fr.height), busy ? Tt("Loading...", "Загрузка...") : Tt("Import", "Импорт"), busy ? new Color(1f, 1f, 1f, 0.4f) : BtcMenu.Accent) && !busy)
            {
                string u = (BtcShareUI.importUrl ?? string.Empty).Trim();
                if (u.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    BtcShareUI.dl = new WWW(u);
                    BtcShareUI.Say(Tt("Downloading...", "Скачиваю..."));
                }
                else { BtcSfx.Warn(); BtcShareUI.Say(Tt("Paste a link starting with https://", "Вставьте ссылку, начинающуюся с https://")); }
            }
            y += h + 3f;
            BtcUi.Note(ref y, w, fs, Tt("Needs a direct file link (catbox links from the link button work). Replays can be imported the same way.", "Нужна прямая ссылка на файл (подходят ссылки catbox, которые даёт кнопка ссылки). Реплеи импортируются так же."));
            if (!string.IsNullOrEmpty(BtcShareUI.msg) && Time.unscaledTime < BtcShareUI.msgUntil) BtcUi.Status(ref y, w, fs, BtcShareUI.msg, BtcMenu.Accent);
            y += fs * 0.3f;
        }

        private bool BtcTextBtn(Rect r, string text, Color c)
        {
            bool hov = r.Contains(Event.current.mousePosition);
            BtcReplayUI.Fill(r, new Color(1f, 1f, 1f, hov ? 0.14f : 0.07f));
            GUIStyle cs = new GUIStyle(_lab); cs.alignment = TextAnchor.MiddleCenter; cs.normal.textColor = c;
            GUI.Label(r, text, cs);
            return BtcSfx.Btn(r, "", BtcReplayUI.sBtn);
        }

        // icon button with a tooltip anchored in window space (we are inside a scroll view)
        private bool IconBtn(Rect r, int icon, Color c, string tip)
        {
            bool hov = r.Contains(Event.current.mousePosition);
            if (hov) BtcReplayUI.Fill(r, new Color(1f, 1f, 1f, 0.1f));
            float g = r.height * 0.62f;
            BtcReplayIcons.Glyph(icon, new Rect(r.center.x - g * 0.5f, r.center.y - g * 0.5f, g, g), hov ? Color.white : c);
            Rect a = new Rect(r.x + _origin.x, r.y + _origin.y, r.width, r.height);
            BtcReplayIcons.Hint(r, a, tip);
            return BtcSfx.Btn(r, "", BtcReplayUI.sBtn);
        }

        private void ListBlock(ref float y, float w, int fs)
        {
            string q = (Query ?? "").Trim().ToLowerInvariant();
            int flt = Filter, shown = 0;
            float pad = fs, rh = fs * 2.3f;
            for (int i = 0; i < BtcShareUI.list.Count; i++)
            {
                BtcShareUI.Entry en = BtcShareUI.list[i];
                if (flt == 1 && en.isMap) continue;
                if (flt == 2 && !en.isMap) continue;
                if (q.Length > 0 && en.name.ToLowerInvariant().IndexOf(q, StringComparison.Ordinal) < 0) continue;
                shown++;
                Rect row = new Rect(pad, y, w - pad * 2f, rh);
                BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, shown % 2 == 0 ? 0.045f : 0.025f));
                bool busy = en.job == 1;
                bool okBtcs = en.hasBtcs && en.err == null;
                string kind = (en.isMap ? Tt("Map", "Карта") : Tt("Save", "Сейв")) + (en.origin.Length > 0 ? " · " + en.origin : "");
                string info;
                Color ic = new Color(1f, 1f, 1f, 0.5f);
                if (busy) info = en.jobKind == "pack" ? Tt("packing...", "сжимаю...") : Tt("converting...", "превращаю...");
                else if (en.err != null) { info = ".btcs: " + en.err; ic = new Color(1f, 0.45f, 0.4f, 1f); }
                else info = en.hasBtcs ? ".btcs " + BtcShareUI.Size(en.btcsBytes) : "";
                float bs = rh * 0.8f, gap = 3f;
                float bx = row.xMax - fs * 0.4f - bs;
                float nameW = row.width - bs * 4f - gap * 4f - fs * 1.2f;
                GUI.Label(new Rect(row.x + fs * 0.7f, row.y, nameW, rh * 0.55f), en.name, _lab);
                GUI.Label(new Rect(row.x + fs * 0.7f, row.y + rh * 0.5f, nameW, rh * 0.45f), kind + (info.Length > 0 ? "   " + info : ""), _dim);
                if (info.Length > 0 && en.err != null) { Color oc = GUI.contentColor; GUI.contentColor = ic; GUI.Label(new Rect(row.x + fs * 0.7f, row.y + rh * 0.5f, nameW, rh * 0.45f), kind + "   " + info, _dim); GUI.contentColor = oc; }
                float by = row.y + rh * 0.1f;
                Color ac = BtcMenu.Accent;
                // right to left: to normal, link, copy file, pack
                Rect r1 = new Rect(bx, by, bs, bs); bx -= bs + gap;
                Rect r2 = new Rect(bx, by, bs, bs); bx -= bs + gap;
                Rect r3 = new Rect(bx, by, bs, bs); bx -= bs + gap;
                Rect r4 = new Rect(bx, by, bs, bs);
                if (!busy && en.hasFolder && IconBtn(r4, BtcReplayIcons.Export, ac, en.hasBtcs ? Tt("Re-pack to .btcs", "Пересжать в .btcs") : Tt("Pack to .btcs", "Сжать в .btcs"))) BtcShareUI.StartPack(en);
                int ls = okBtcs ? BtcReplayShare.State(en.btcs) : 0;
                if (okBtcs && !busy)
                {
                    bool copied = BtcReplayShare.FileCopied(en.btcs);
                    if (IconBtn(r3, BtcReplayIcons.Copy, copied ? ac : Color.white, BtcReplayShare.UiCopyFile(copied))) BtcReplayShare.CopyFile(en.btcs);
                    if (ls == 1 || ls == 2) IconBtn(r2, BtcReplayIcons.Share, new Color(1f, 1f, 1f, 0.35f), ls == 1 ? BtcReplayShare.UiUploading() : BtcReplayShare.UiCreate());
                    else if (IconBtn(r2, BtcReplayIcons.Share, Color.white, ls == 3 ? BtcReplayShare.UiRetry() : BtcReplayShare.UiCreate())) BtcReplayShare.Create(en.btcs);
                    string ck = "n:" + en.btcs;
                    bool conf = _confirm == ck && Time.unscaledTime < _confirmUntil;
                    if (IconBtn(r1, BtcReplayIcons.Import, conf ? new Color(1f, 0.45f, 0.4f, 1f) : Color.white, conf ? Tt("Click again to overwrite the normal folder", "Клик ещё раз: перезаписать обычную папку") : (en.isMap ? Tt("To a normal map", "В обычную карту") : Tt("To a normal save", "В обычный сейв"))))
                    {
                        if (!en.hasFolder || conf) { _confirm = null; BtcShareUI.StartNormal(en); }
                        else { _confirm = ck; _confirmUntil = Time.unscaledTime + 3f; BtcSfx.Warn(); }
                    }
                }
                y += rh + 3f;
                if (ls == 2 || ls == 3)
                {
                    float lh = fs * 2f;
                    Rect lr = new Rect(pad, y, w - pad * 2f, lh);
                    BtcReplayUI.Fill(lr, new Color(1f, 1f, 1f, 0.025f));
                    if (ls == 2)
                    {
                        GUI.Label(new Rect(lr.x + fs * 1.4f, lr.y, lr.width - fs * 8f, lh), BtcReplayShare.Url(en.btcs) + (BtcReplayShare.IsTemp(en.btcs) ? BtcReplayShare.UiTempNote() : string.Empty), _dim);
                        bool cp = BtcReplayShare.Copied(en.btcs);
                        if (BtcTextBtn(new Rect(lr.xMax - fs * 6.5f, lr.y + lh * 0.1f, fs * 6f, lh * 0.8f), BtcReplayShare.UiCopy(cp), cp ? BtcMenu.Accent : Color.white)) BtcReplayShare.Copy(en.btcs);
                    }
                    else GUI.Label(new Rect(lr.x + fs * 1.4f, lr.y, lr.width - fs * 2f, lh), BtcReplayShare.UiFailed(BtcReplayShare.Error(en.btcs)), _dim);
                    y += lh + 3f;
                }
            }
            if (shown == 0) BtcUi.Note(ref y, w, fs, BtcShareUI.list.Count == 0 ? Tt("No saves or maps found in the Info folder.", "В папке Info не найдено сейвов и карт.") : Tt("Nothing matches the search.", "По запросу ничего не найдено."));
            y += fs * 0.4f;
        }
    }
}
