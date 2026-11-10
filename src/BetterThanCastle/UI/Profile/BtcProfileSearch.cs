using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Profile tab: global search in the header. While the field has text the page body is replaced by a list of matching sections / settings of EVERY page
    /// (and saved profiles); a click jumps to the page, opens the section and scrolls to it.
    /// </summary>
    internal sealed partial class BtcProfileTab
    {
        private string _gq = "";
        private string _jumpKey;
        private readonly Dictionary<string, float> _foldY = new Dictionary<string, float>();

        private struct Hit
        {
            internal int Page; internal string Key; internal string En, Ru, Words;
            internal Hit(int page, string key, string en, string ru, string words) { Page = page; Key = key; En = en; Ru = ru; Words = words; }
        }

        // every section and the settings inside it; Words = extra keywords in both languages
        private static readonly Hit[] Index =
        {
            new Hit(PNick, "profile:slot", "Which nick: long or short", "Какой ник: длинный или короткий", "slot long short betterconnection vanilla слот длинный короткий"),
            new Hit(PNick, "profile:text", "Nick text", "Текст ника", "type name length limit counter длина лимит счётчик"),
            new Hit(PNick, "profile:paint", "Colour the letters", "Покраска букв", "paint select palette colour color letter drag picker hex цвет палитра буквы выделение"),
            new Hit(PNick, "profile:opts", "Auto-apply and keep the nick", "Автоприменение и запоминание ника", "auto apply save keep remember автоприменение запоминать сохранение"),
            new Hit(PNick, "profile:hist", "Recent nicks", "Недавние ники", "history история"),
            new Hit(PStyle, "profile:presets", "Gradient presets", "Готовые градиенты", "rainbow fire ocean neon gold радуга огонь пресет"),
            new Hit(PStyle, "profile:stops", "Your gradient: stops, reverse, repeat", "Ваш градиент: точки, обратный, повторы", "custom edit reverse repeat stop colour точка реверс повтор свой"),
            new Hit(PStyle, "profile:applygrad", "Apply the gradient to the nick", "Применить градиент к нику", "selection whole выделение весь ник"),
            new Hit(PAnim, "profile:anim", "Lobby animation and effect", "Анимация в лобби и эффект", "wave shine flicker crossfade scroll волна блик мерцание"),
            new Hit(PAnim, "profile:animtune", "Animation speed and updates", "Скорость и частота анимации", "speed rate repeat updates скорость частота"),
            new Hit(PAvatar, "profile:avcur", "Your avatar", "Ваш аватар", "steam picture картинка"),
            new Hit(PAvatar, "profile:avfiles", "Avatar pictures and folder", "Картинки аватара и папка", "png jpg gif folder file папка файл"),
            new Hit(PPlate, "profile:pl:bg", "Plate background", "Фон плашки", "style cycle rainbow pulse neon gradient colour фон градиент цвет"),
            new Hit(PPlate, "profile:pl:frame", "Plate frame", "Рамка плашки", "border thickness рамка толщина"),
            new Hit(PPlate, "profile:pl:nick", "Nick on the plate: font, size, position, outline", "Ник на плашке: шрифт, размер, положение, обводка", "font style bold italic align position outline size шрифт начертание обводка положение размер"),
            new Hit(PPlate, "profile:pl:fx", "Letter effects", "Эффекты букв", "glitch hop shake wave rainbow rock эффекты глитч дрожь"),
            new Hit(PPlate, "profile:pl:size", "Plate height and avatar size", "Высота плашки и размер аватарки", "height scale avatar size высота масштаб аватарка размер"),
            new Hit(PPlate, "profile:pl:img", "Plate image", "Картинка плашки", "opacity darken crop stretch gif image картинка непрозрачность затемнение"),
            new Hit(PSets, "profile:sets:new", "Save the current look as a profile", "Сохранить текущий вид как профиль", "save copy paste import export clipboard сохранить копировать вставить буфер"),
            new Hit(PSets, "profile:sets:list", "My profiles", "Мои профили", "load apply delete загрузить применить удалить"),
        };

        private static bool Has(string text, string q) { return text != null && text.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0; }

        private bool KeyAtJump(string k, float y)
        {
            _foldY[k] = y;
            if (_jumpKey == k) { _scroll.y = Mathf.Max(0f, y - _fs * 0.5f); _jumpKey = null; }
            return true;
        }

        private string KeyAdd(string k, float y)
        {
            _keys.Add(k);
            KeyAtJump(k, y);
            return k;
        }

        private void Jump(int page, string key)
        {
            Page = page;
            _gq = "";
            GUIUtility.keyboardControl = 0;
            if (key != null) { BtcUi.SetOpen(key, true); _jumpKey = key; }
            _scroll = Vector2.zero;
        }

        /// <summary>The search field in the header row between the title and the icons. Returns nothing; it edits _gq.</summary>
        private void DrawGlobalSearch(Rect r, int fs)
        {
            if (r.width < fs * 7f) return;
            float ib = r.height;
            BtcReplayUI.Fill(r, new Color(1f, 1f, 1f, _gq.Length > 0 ? 0.1f : 0.06f));
            BtcReplayIcons.Glyph(BtcReplayIcons.Search, new Rect(r.x + fs * 0.4f, r.y + ib * 0.2f, ib * 0.6f, ib * 0.6f), new Color(1f, 1f, 1f, 0.5f));
            BtcUi.Field(r);
            GUI.SetNextControlName("btc_prof_gsearch");
            bool clear = _gq.Length > 0;
            float cw = clear ? ib : 0f;
            string old = _gq;
            _gq = GUI.TextField(new Rect(r.x + ib * 0.95f, r.y, r.width - ib * 0.95f - cw, ib), _gq, 40, _tf);
            if (_gq.Length == 0 && GUIUtility.keyboardControl == 0) Txt(new Rect(r.x + ib * 0.95f + 4f, r.y, r.width, ib), Tt("Search the whole tab", "Поиск по всей вкладке"), _dim, new Color(1f, 1f, 1f, 0.35f));
            if (clear && BtcReplayIcons.Plain(new Rect(r.xMax - ib, r.y, ib, ib), BtcReplayIcons.Cross, Color.white, Tt("Clear the search", "Очистить поиск"))) { _gq = ""; GUIUtility.keyboardControl = 0; }
            if (old != _gq) _scroll = Vector2.zero;
        }

        private void SearchPage(ref float y, float w, int fs)
        {
            float pad = fs;
            string q = _gq.Trim();
            int found = 0;
            List<BtcProfileSets.Entry> sets = null;
            for (int i = 0; i < Index.Length; i++)
            {
                Hit h = Index[i];
                if (!(Has(h.En, q) || Has(h.Ru, q) || Has(h.Words, q) || Has(PageName(h.Page), q))) continue;
                found++;
                if (HitRow(ref y, w, fs, PageIcon(h.Page), BtcReplayUI.T(h.En, h.Ru), PageName(h.Page))) { Jump(h.Page, h.Key); return; }
            }
            sets = Sets();
            for (int i = 0; i < sets.Count; i++)
            {
                if (!(Has(sets[i].Name, q) || Has(BtcNick.Plain(NickOf(sets[i].Text)), q))) continue;
                found++;
                if (HitRow(ref y, w, fs, BtcReplayIcons.Folder, Tt("Profile: ", "Профиль: ") + sets[i].Name, PageName(PSets))) { Jump(PSets, "profile:sets:list"); return; }
            }
            if (found == 0) BtcUi.Note(ref y, w, fs, Tt("Nothing found", "Ничего не найдено"));
        }

        private bool HitRow(ref float y, float w, int fs, int icon, string label, string where)
        {
            float pad = fs, rh = fs * 2.5f;
            Rect row = new Rect(pad, y, w - pad * 2f, rh);
            bool hover = row.Contains(Event.current.mousePosition);
            BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, hover ? 0.08f : 0.035f));
            float g = fs * 1.2f;
            BtcReplayIcons.Glyph(icon, new Rect(row.x + fs * 0.7f, row.center.y - g * 0.5f, g, g), hover ? Color.white : BtcMenu.Accent);
            Txt(new Rect(row.x + fs * 2.6f, row.y, row.width * 0.62f - fs * 2.6f, rh), label, _lbl, Color.white);
            Txt(new Rect(row.x + row.width * 0.62f, row.y, row.width * 0.38f - fs * 0.8f, rh), where, _rightDim(), new Color(1f, 1f, 1f, 0.55f));
            y += rh + 3f;
            return BtcSfx.Btn(row, "", BtcReplayUI.sBtn);
        }
    }
}
