using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>Profile tab: the Profiles page. Save the whole look (nick, colours, animation, plate, avatar) under a name, load it with one click, copy it as text for a friend, paste one back. Every action is undoable.</summary>
    internal sealed partial class BtcProfileTab
    {
        private string _setName = "";
        private List<BtcProfileSets.Entry> _sets;
        private float _setsAt = -10f;
        private string _confirmSet;
        private float _confirmSetUntil;

        private void ReloadSets() { _setsAt = -10f; }

        private List<BtcProfileSets.Entry> Sets()
        {
            if (_sets == null || Time.unscaledTime - _setsAt > 1.5f)
            {
                _sets = BtcProfileSets.List();
                _setsAt = Time.unscaledTime;
            }
            return _sets;
        }

        private void ApplySet(string text, string label)
        {
            string before = BtcProfileSets.Capture();
            BtcProfileSets.Apply(text);
            _force = true;
            _selA = _selB = -1;
            BtcUndo.Push(Scope, label, delegate { BtcProfileSets.Apply(before); _force = true; });
            BtcMenu.Toast(label, BtcReplayIcons.Check);
            BtcSfx.Ok();
        }

        private string _q = "";

        private List<BtcProfileSets.Entry> FilterSets(List<BtcProfileSets.Entry> all)
        {
            if (_q.Length == 0) return all;
            List<BtcProfileSets.Entry> r = new List<BtcProfileSets.Entry>();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Name.IndexOf(_q, StringComparison.OrdinalIgnoreCase) >= 0 || BtcNick.Plain(NickOf(all[i].Text)).IndexOf(_q, StringComparison.OrdinalIgnoreCase) >= 0) r.Add(all[i]);
            }
            return r;
        }

        /// <summary>Search row of the Profiles page (same look as in Hotkeys): magnifier, field, clear button; matches the profile name and the plain nick.</summary>
        private void DrawSearch(ref float y, float w, int fs)
        {
            float pad = fs, ib = fs * 1.9f;
            Rect sr = new Rect(pad, y, Mathf.Min(w - pad * 2f, fs * 28f), ib);
            BtcReplayUI.Fill(sr, new Color(1f, 1f, 1f, 0.06f));
            BtcReplayIcons.Glyph(BtcReplayIcons.Search, new Rect(sr.x + fs * 0.4f, sr.y + ib * 0.2f, ib * 0.6f, ib * 0.6f), new Color(1f, 1f, 1f, 0.5f));
            Vector2 sp = GUIUtility.GUIToScreenPoint(new Vector2(sr.x, sr.y));
            BtcUi.Field(new Rect(sp.x, sp.y, sr.width, sr.height));
            GUI.SetNextControlName("btc_prof_search");
            bool clear = _q.Length > 0;
            float cw = clear ? ib : 0f;
            _q = GUI.TextField(new Rect(sr.x + ib * 0.95f, sr.y, sr.width - ib * 0.95f - cw, ib), _q, 40, _tf);
            if (_q.Length == 0 && GUIUtility.keyboardControl == 0) Txt(new Rect(sr.x + ib * 0.95f + 4f, sr.y, sr.width, ib), Tt("Search: profile name or nick", "Поиск: название профиля или ник"), _dim, new Color(1f, 1f, 1f, 0.35f));
            if (clear && BtcReplayIcons.Plain(new Rect(sr.xMax - ib, sr.y, ib, ib), BtcReplayIcons.Cross, Color.white, Tt("Clear the search", "Очистить поиск"))) { _q = ""; GUIUtility.keyboardControl = 0; }
            y += ib + fs * 0.5f;
        }

        private static string NickOf(string text)
        {
            Dictionary<string, string> kv = BtcProfileSets.Parse(text);
            string v;
            if (kv.TryGetValue("nick", out v) && v.Length > 0) return v;
            if (kv.TryGetValue("snick", out v) && v.Length > 0) return v;
            return "?";
        }

        private void SetsPage(ref float y, float w, int fs)
        {
            float pad = fs, ib = fs * 1.9f;
            List<BtcProfileSets.Entry> sets = FilterSets(Sets());


            if (BtcUi.Fold(ref y, w, fs, KeyAdd("profile:sets:new", y), Tt("Save the current look", "Сохранить текущий вид"), 0))
            {
                Rect row = new Rect(pad, y, w - pad * 2f, fs * 2.3f);
                BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, 0.06f));
                Vector2 sp = GUIUtility.GUIToScreenPoint(new Vector2(row.x, row.y));
                BtcUi.Field(new Rect(sp.x, sp.y, row.width, row.height));
                GUI.SetNextControlName("btc_prof_setname");
                _setName = GUI.TextField(row, _setName, 40, _tf);
                if (_setName.Length == 0 && GUI.GetNameOfFocusedControl() != "btc_prof_setname") Txt(new Rect(row.x + 8f, row.y, row.width, row.height), Tt("Profile name", "Название профиля"), _lbl, new Color(1f, 1f, 1f, 0.35f));
                y += row.height + 4f;
                string name = BtcProfileSets.Safe(_setName);
                bool exists = name.Length > 0 && BtcProfileSets.Exists(name);
                if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Plus, exists ? Tt("Overwrite this profile", "Перезаписать этот профиль") : Tt("Save as a new profile", "Сохранить как новый профиль"), name.Length == 0 ? Tt("Type a name first", "Сначала введите название") : null) && name.Length > 0)
                {
                    string old = null;
                    foreach (BtcProfileSets.Entry e in sets) if (string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase)) old = e.Text;
                    BtcProfileSets.Write(name, BtcProfileSets.Capture());
                    string nm = name, o = old;
                    BtcUndo.Push(Scope, Tt("Save profile ", "Сохранение профиля ") + nm, delegate { if (o == null) BtcProfileSets.Delete(nm); else BtcProfileSets.Write(nm, o); ReloadSets(); });
                    ReloadSets();
                    BtcSfx.Ok();
                    BtcMenu.Toast(Tt("Profile saved: ", "Профиль сохранён: ") + nm, BtcReplayIcons.Check);
                    _setName = "";
                    GUIUtility.keyboardControl = 0;
                }
                if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Copy, Tt("Copy the current look as text", "Скопировать текущий вид текстом"), Tt("Send it to a friend; they paste it below", "Отправьте другу, он вставит его кнопкой ниже")))
                {
                    GUIUtility.systemCopyBuffer = BtcProfileSets.Capture();
                    BtcSfx.Ok();
                    BtcMenu.Toast(Tt("Copied to the clipboard", "Скопировано в буфер обмена"), BtcReplayIcons.Copy);
                }
                if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Import, Tt("Paste a look from the clipboard", "Вставить вид из буфера обмена"), null))
                {
                    string t = GUIUtility.systemCopyBuffer;
                    if (BtcProfileSets.Looks(t)) ApplySet(t, Tt("Pasted a profile", "Профиль вставлен"));
                    else { BtcSfx.Warn(); BtcMenu.Toast(Tt("The clipboard has no profile", "В буфере нет профиля"), BtcReplayIcons.Cross); }
                }
                y += fs * 0.3f;
            }

            if (BtcUi.Fold(ref y, w, fs, KeyAdd("profile:sets:list", y), Tt("My profiles", "Мои профили"), sets.Count))
            {
                if (sets.Count == 0 && _q.Length > 0) BtcUi.Note(ref y, w, fs, Tt("Nothing found", "Ничего не найдено"));
                else if (sets.Count == 0) BtcUi.Note(ref y, w, fs, Tt("No saved profiles yet. A profile stores your nick, colours, animation, plate, plate image and avatar.", "Сохранённых профилей пока нет. Профиль хранит ник, цвета, анимацию, плашку, картинку плашки и аватар."));
                for (int i = 0; i < sets.Count; i++)
                {
                    BtcProfileSets.Entry e = sets[i];
                    float rh = fs * 3.2f;
                    Rect row = new Rect(pad, y, w - pad * 2f, rh);
                    bool hover = row.Contains(Event.current.mousePosition);
                    BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, hover ? 0.08f : 0.035f));
                    float bx = row.xMax - ib - fs * 0.3f, by = row.y + (rh - ib) * 0.5f;
                    // sub-buttons first (they sit over the full-row button)
                    string nm = e.Name, tx = e.Text;
                    if (BtcReplayIcons.Plain(new Rect(bx, by, ib, ib), BtcReplayIcons.Trash, _confirmSet == nm && Time.unscaledTime < _confirmSetUntil ? new Color(1f, 0.45f, 0.45f, 1f) : Color.white, _confirmSet == nm && Time.unscaledTime < _confirmSetUntil ? Tt("Click again to delete (Ctrl+Z restores it)", "Ещё раз - удалить (Ctrl+Z вернёт)") : Tt("Delete this profile", "Удалить этот профиль")))
                    {
                        if (_confirmSet == nm && Time.unscaledTime < _confirmSetUntil)
                        {
                            BtcProfileSets.Delete(nm);
                            BtcUndo.Push(Scope, Tt("Delete profile ", "Удаление профиля ") + nm, delegate { BtcProfileSets.Write(nm, tx); ReloadSets(); });
                            _confirmSet = null; ReloadSets(); BtcSfx.Delete();
                            return;
                        }
                        _confirmSet = nm; _confirmSetUntil = Time.unscaledTime + 3f;
                    }
                    bx -= ib + 3f;
                    if (BtcReplayIcons.Plain(new Rect(bx, by, ib, ib), BtcReplayIcons.Copy, Color.white, Tt("Copy this profile as text", "Скопировать профиль текстом")))
                    {
                        GUIUtility.systemCopyBuffer = tx;
                        BtcSfx.Ok();
                        BtcMenu.Toast(Tt("Copied to the clipboard", "Скопировано в буфер обмена"), BtcReplayIcons.Copy);
                    }
                    bx -= ib + 3f;
                    if (BtcReplayIcons.Plain(new Rect(bx, by, ib, ib), BtcReplayIcons.Refresh, Color.white, Tt("Overwrite with the current look", "Перезаписать текущим видом")))
                    {
                        BtcProfileSets.Write(nm, BtcProfileSets.Capture());
                        BtcUndo.Push(Scope, Tt("Update profile ", "Обновление профиля ") + nm, delegate { BtcProfileSets.Write(nm, tx); ReloadSets(); });
                        ReloadSets(); BtcSfx.Ok();
                        BtcMenu.Toast(Tt("Profile updated: ", "Профиль обновлён: ") + nm, BtcReplayIcons.Check);
                    }
                    Txt(new Rect(row.x + fs * 0.8f, row.y + fs * 0.2f, bx - row.x - fs, rh * 0.5f), NickOf(tx), _richSmall(), Color.white);
                    Txt(new Rect(row.x + fs * 0.8f, row.y + rh * 0.55f, bx - row.x - fs, rh * 0.4f), nm, _dim, new Color(1f, 1f, 1f, 0.6f));
                    BtcReplayIcons.Hint(row, Tt("Click: apply this profile", "Клик: применить этот профиль"));
                    if (BtcSfx.Btn(row, "", BtcReplayUI.sBtn)) ApplySet(tx, Tt("Applied profile ", "Применён профиль ") + nm);
                    y += rh + 3f;
                }
            }
        }
    }
}
