using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>Setups page of the UI editor: the whole mod look (every tab) as one named setup or one pasteable code, with switchable parts.</summary>
    internal sealed partial class BtcUiEditorTab
    {
        internal const int PSetup = 5;
        private string _setupName = "", _setupConfirm;

        private static string PartNames(List<string> keys)
        {
            List<BtcSetups.Part> all = BtcSetups.All();
            List<string> n = new List<string>();
            for (int i = 0; i < all.Count; i++) if (keys.Contains(all[i].Key)) n.Add(Tt(all[i].En, all[i].Ru).Split(':')[0]);
            return string.Join(", ", n.ToArray());
        }

        private void ApplySetup(string text, string label)
        {
            List<string> done = BtcSetups.ApplyText(text, true);
            if (done == null) { BtcSfx.Warn(); BtcMenu.Toast(Tt("This is not a BTC setup", "Это не сетап BTC"), BtcReplayIcons.Cross); return; }
            if (done.Count == 0) { BtcSfx.Warn(); BtcMenu.Toast(Tt("Nothing to apply: the setup has none of the selected parts", "Нечего применять: в сетапе нет выбранных частей"), BtcReplayIcons.Cross); return; }
            BtcSfx.Ok();
            BtcMenu.Toast(label + ": " + PartNames(done) + Tt(" (Ctrl+Z undoes)", " (Ctrl+Z отменит)"), BtcReplayIcons.Check);
        }

        private void PageSetups(ref float y, float w, int fs)
        {
            Event e = Event.current;
            if (BtcUi.Fold(ref y, w, fs, "uie:sparts", Tt("What a setup contains", "Что входит в сетап"), 0))
            {
                BtcUi.Note(ref y, w, fs, Tt("A setup is your whole look of the mod in one place. Switch the parts on and off: it works both when you copy or save and when you apply or paste.", "Сетап это весь ваш вид мода в одном месте. Включайте и выключайте части: это работает и когда вы копируете или сохраняете, и когда применяете или вставляете."));
                List<BtcSetups.Part> all = BtcSetups.All();
                for (int i = 0; i < all.Count; i++)
                {
                    BtcSetups.Part p = all[i];
                    bool on = BtcSetups.PartOn(p.Key, p.DefaultOn);
                    string hint = string.IsNullOrEmpty(p.HintEn) ? null : Tt(p.HintEn, p.HintRu);
                    if (BtcUi.Toggle(ref y, w, fs, Tt(p.En, p.Ru), on, hint)) { bool nv = !on; string key = p.Key; BtcSetups.SetPartOn(key, nv); BtcUndo.Push(Scope, Tt("Setup part: ", "Часть сетапа: ") + Tt(p.En, p.Ru), delegate { BtcSetups.SetPartOn(key, !nv); }); }
                }
                y += fs * 0.4f;
            }
            if (BtcUi.Fold(ref y, w, fs, "uie:sshare", Tt("Share as a code", "Поделиться кодом"), 0))
            {
                if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Copy, Tt("Copy my setup as one code", "Скопировать мой сетап одним кодом"), Tt("One line of text for Discord or a friend", "Одна строка текста для Discord или друга")))
                {
                    GUIUtility.systemCopyBuffer = BtcSetups.ToCode(BtcSetups.Build(true));
                    BtcSfx.Ok(); BtcMenu.Toast(Tt("Setup code copied", "Код сетапа скопирован"), BtcReplayIcons.Copy);
                }
                if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Import, Tt("Paste a setup code from the clipboard", "Вставить код сетапа из буфера обмена"), Tt("Applies the selected parts; Ctrl+Z undoes it all", "Применяет выбранные части; Ctrl+Z отменяет всё")))
                {
                    string text = BtcSetups.FromCode(GUIUtility.systemCopyBuffer);
                    if (text == null) { BtcSfx.Warn(); BtcMenu.Toast(Tt("The clipboard has no setup code", "В буфере обмена нет кода сетапа"), BtcReplayIcons.Cross); }
                    else ApplySetup(text, Tt("Applied", "Применено"));
                }
                y += fs * 0.4f;
            }
            if (BtcUi.Fold(ref y, w, fs, "uie:sown", Tt("My setups", "Мои сетапы"), 0))
            {
                List<BtcSetups.Entry> own = BtcSetups.Own();
                if (own.Count == 0) BtcUi.Note(ref y, w, fs, Tt("No saved setups yet. Save the current look below.", "Сохранённых сетапов пока нет. Сохраните текущий вид ниже."));
                for (int i = 0; i < own.Count; i++)
                {
                    BtcSetups.Entry p = own[i];
                    Rect row = new Rect(fs, y, w - fs * 2f, fs * 3f);
                    bool hover = row.Contains(e.mousePosition);
                    BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, hover ? 0.07f : 0.03f));
                    float g = fs * 1.2f;
                    BtcReplayIcons.Glyph(BtcReplayIcons.Star, new Rect(row.x + fs * 0.7f, row.y + fs * 0.45f, g, g), new Color(1f, 0.85f, 0.4f, 1f));
                    BtcVisualRows.Txt(new Rect(row.x + fs * 2.6f, row.y + fs * 0.1f, row.width - fs * 6f, fs * 1.6f), p.Name, BtcVisualRows.Lbl, Color.white);
                    BtcVisualRows.Txt(new Rect(row.x + fs * 2.6f, row.y + fs * 1.6f, row.width - fs * 6f, fs * 1.2f), BtcSetups.Contents(p.Text), BtcVisualRows.Dim, new Color(1f, 1f, 1f, 0.5f));
                    Rect tr = new Rect(row.xMax - fs * 2.2f, row.y + fs * 0.6f, fs * 1.8f, fs * 1.8f);
                    Rect cp = new Rect(tr.x - fs * 2.1f, tr.y, fs * 1.8f, fs * 1.8f);
                    bool conf = _setupConfirm == p.Name;
                    if (BtcReplayIcons.Plain(tr, BtcReplayIcons.Trash, conf ? new Color(1f, 0.45f, 0.45f, 1f) : Color.white, conf ? Tt("Click again to delete (Ctrl+Z restores)", "Ещё раз: удалить (Ctrl+Z вернёт)") : Tt("Delete this setup", "Удалить этот сетап")))
                    {
                        if (conf)
                        {
                            string name = p.Name, text = p.Text;
                            BtcSetups.Delete(name); _setupConfirm = null; BtcSfx.Delete();
                            BtcUndo.Push(Scope, Tt("Setup deleted: ", "Сетап удалён: ") + name, delegate { BtcSetups.Save(name, text); });
                        }
                        else _setupConfirm = p.Name;
                    }
                    if (BtcReplayIcons.Plain(cp, BtcReplayIcons.Copy, Color.white, Tt("Copy this setup as a code", "Скопировать этот сетап кодом")))
                    {
                        GUIUtility.systemCopyBuffer = BtcSetups.ToCode(p.Text);
                        BtcSfx.Ok(); BtcMenu.Toast(Tt("Setup code copied", "Код сетапа скопирован"), BtcReplayIcons.Copy);
                    }
                    if (BtcSfx.Btn(row, "", BtcReplayUI.sBtn)) ApplySetup(p.Text, p.Name);
                    y += row.height + 3f;
                }
                y += fs * 0.4f;
                Rect sr = new Rect(fs, y, Mathf.Min(w - fs * 2f - fs * 9f, fs * 22f), fs * 2.1f);
                BtcReplayUI.Fill(sr, new Color(1f, 1f, 1f, 0.06f));
                BtcUi.Field(sr);
                GUI.SetNextControlName("btc_uie_setup");
                _setupName = GUI.TextField(new Rect(sr.x + 4f, sr.y, sr.width - 8f, sr.height), _setupName, 30, _tf);
                if (_setupName.Length == 0 && GUIUtility.keyboardControl == 0) BtcVisualRows.Txt(new Rect(sr.x + 8f, sr.y, sr.width, sr.height), Tt("Name of the setup", "Название сетапа"), BtcVisualRows.Dim, new Color(1f, 1f, 1f, 0.35f));
                Rect sb = new Rect(sr.xMax + fs * 0.5f, sr.y, fs * 8f, sr.height);
                bool hv = sb.Contains(e.mousePosition);
                BtcReplayUI.Fill(sb, BtcMenu.AccentA(hv ? 0.3f : 0.18f));
                BtcVisualRows.Txt(sb, Tt("Save current", "Сохранить текущий"), BtcVisualRows.Lbl, BtcMenu.Accent);
                if (BtcSfx.Btn(sb, "", BtcReplayUI.sBtn))
                {
                    string nm = BtcUiEdit.SafeName(_setupName);
                    if (nm.Length == 0) { BtcSfx.Warn(); BtcMenu.Toast(Tt("Type a name first", "Сначала введите название"), BtcReplayIcons.Plus); }
                    else if (BtcSetups.Save(nm, BtcSetups.Build(true)))
                    {
                        _setupName = ""; GUIUtility.keyboardControl = 0; BtcSfx.Ok();
                        BtcUndo.Push(Scope, Tt("Setup saved: ", "Сетап сохранён: ") + nm, delegate { BtcSetups.Delete(nm); });
                        BtcMenu.Toast(Tt("Saved: ", "Сохранено: ") + nm, BtcReplayIcons.Check);
                    }
                    else BtcSfx.Warn();
                }
                y += sr.height + fs * 0.6f;
            }
            _h = y + fs;
        }
    }
}
