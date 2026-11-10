using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Handicap page of the Gameplay tab: YOUR personal handicap (you can only make the game harder for yourself, never better than the host's
    /// rules), the optional caps as a switch plus a slider, a reset icon, undo, and the list of players who play with a handicap.
    /// Data, saving and the wire (msgs 87/88) stay in BtcHandicap; this page edits `BtcHandicap.Mine` through accessors.
    /// </summary>
    internal sealed partial class BtcGameplayTab
    {
        private void HandicapSlider(ref float y, float w, int fs, int[] mine, int i, string label, string suffix)
        {
            int[] def = BtcHandicap.Defaults, lo = BtcHandicap.Lows, hi = BtcHandicap.Highs;
            int cur = mine[i];
            float nv = BtcVisualRows.Slider(ref y, w, fs, 0, "gp:hc" + i, label, cur, lo[i], hi[i], 5f, "0", suffix);
            if (cur != def[i]) BtcReplayUI.Fill(new Rect(fs - 5f, y - fs * 2.1f - 3f, 3f, fs * 2.1f), Critical);
            int v = Mathf.Clamp(Mathf.RoundToInt(nv / 5f) * 5, lo[i], hi[i]);
            if (v != cur)
            {
                int old = cur;
                mine[i] = v;
                BtcHandicap.MineEdited();
                BtcUndo.Push(Scope, label, delegate { mine[i] = old; BtcHandicap.MineEdited(); }, "gp:hc" + i);
            }
        }

        /// <summary>Optional limit (-1 = off): a switch and, when on, a slider.</summary>
        private void HandicapOptional(ref float y, float w, int fs, int[] mine, int i, string label, int onValue, int min, int max)
        {
            int cur = mine[i];
            bool on = cur >= 0;
            if (BtcVisualRows.Switch(ref y, w, fs, 0, label, on, null))
            {
                int old = cur;
                mine[i] = on ? -1 : onValue;
                BtcHandicap.MineEdited();
                BtcUndo.Push(Scope, label, delegate { mine[i] = old; BtcHandicap.MineEdited(); });
            }
            if (on)
            {
                float nv = BtcVisualRows.Slider(ref y, w, fs, 1, "gp:hc" + i, label, cur, min, max, 1f, "0", "");
                BtcReplayUI.Fill(new Rect(fs - 5f, y - fs * 2.1f - 3f, 3f, fs * 2.1f), Critical);
                int v = Mathf.Clamp(Mathf.RoundToInt(nv), min, max);
                if (v != cur)
                {
                    int old = cur;
                    mine[i] = v;
                    BtcHandicap.MineEdited();
                    BtcUndo.Push(Scope, label, delegate { mine[i] = old; BtcHandicap.MineEdited(); }, "gp:hc" + i);
                }
            }
        }

        private void PageHandicap(ref float y, float w, int fs)
        {
            int[] mine = BtcHandicap.Mine;
            int[] def = BtcHandicap.Defaults;
            bool changed = false;
            for (int i = 0; i < def.Length; i++) if (mine[i] != def[i]) changed = true;

            float pad = fs, h = fs * 2.3f;
            Rect card = new Rect(pad, y, w - pad * 2f, h);
            BtcReplayUI.Fill(card, new Color(1f, 1f, 1f, 0.04f));
            BtcReplayUI.Fill(new Rect(card.x, card.y + h * 0.12f, 3f, h * 0.76f), changed ? Critical : BtcMenu.Accent);
            float ig = fs * 1.3f;
            BtcReplayIcons.Glyph(BtcReplayIcons.Group, new Rect(card.x + fs * 0.7f, card.center.y - ig * 0.5f, ig, ig), changed ? Critical : BtcMenu.Accent);
            BtcVisualRows.Txt(new Rect(card.x + fs * 2.4f, card.y, card.width - fs * 5.6f, h), changed ? Tt("You play with a handicap; everybody sees it next to your name", "Вы играете с форой; все видят её рядом с вашим ником") : Tt("No handicap: you play by the host's rules", "Форы нет: вы играете по правилам хоста"), BtcVisualRows.Lbl, Color.white);
            if (changed && IcoBtn(new Rect(card.xMax - fs * 2.6f, card.y + h * 0.1f, fs * 2.2f, h * 0.8f), BtcReplayIcons.Undo, false, BtcMenu.Accent, Tt("Reset the handicap", "Сбросить фору")))
            {
                int[] old = (int[])mine.Clone();
                Array.Copy(def, mine, def.Length);
                BtcHandicap.MineEdited();
                BtcSfx.Ok();
                BtcUndo.Push(Scope, Tt("Reset handicap", "Сброс форы"), delegate { Array.Copy(old, mine, old.Length); BtcHandicap.MineEdited(); });
            }
            y += h + fs * 0.4f;
            BtcUi.Note(ref y, w, fs, Tt("A personal handicap can only make the game harder for YOU, never better than the host's rules. It works only with BetterConnection.", "Личная фора может только усложнить игру ВАМ, лучше правил хоста она не сделает. Работает только с BetterConnection."));
            if (!BtcBc.BcEnabled) BtcUi.Status(ref y, w, fs, Tt("BetterConnection is off: the handicap is saved but not applied", "BetterConnection выключен: фора сохранена, но не применяется"), Minor);

            BtcUi.Section(ref y, w, fs, Tt("Your units and economy", "Ваши юниты и экономика"));
            HandicapSlider(ref y, w, fs, mine, 0, Tt("Damage of your units", "Урон ваших юнитов"), " %");
            HandicapSlider(ref y, w, fs, mine, 1, Tt("HP of your units", "ХП ваших юнитов"), " %");
            HandicapSlider(ref y, w, fs, mine, 2, Tt("Crystal energy income", "Доход энергии с кристаллов"), " %");
            HandicapSlider(ref y, w, fs, mine, 3, Tt("Extra energy lost per death (flat)", "Доп. потеря энергии за смерть (штучно)"), "");

            BtcUi.Section(ref y, w, fs, Tt("Limits (can only lower the mode's values)", "Ограничения (могут только снизить значения режима)"));
            HandicapOptional(ref y, w, fs, mine, 4, Tt("Own bricktron cap (new bricktrons only; existing stay)", "Свой кап бриктронов (только новые; существующие остаются)"), 30, 1, 100);
            HandicapOptional(ref y, w, fs, mine, 5, Tt("Starting workers (match start, host: original conquest and sandbox)", "Стартовые рабочие (старт матча, хост: оригинальные conquest и sandbox)"), 6, 0, 100);

            List<KeyValuePair<string, int[]>> players = BtcHandicap.Players();
            BtcUi.Section(ref y, w, fs, Tt("Players with a handicap", "Игроки с форой"));
            if (players.Count == 0) BtcUi.Note(ref y, w, fs, Tt("Nobody.", "Нет таких."));
            for (int i = 0; i < players.Count; i++)
            {
                Rect row = new Rect(pad, y, w - pad * 2f, fs * 2.0f);
                BtcReplayUI.Fill(row, new Color(1f, 1f, 1f, 0.03f));
                BtcReplayUI.Fill(new Rect(row.x, row.y, 3f, row.height), Critical);
                BtcVisualRows.Txt(new Rect(row.x + fs * 0.8f, row.y, row.width * 0.3f, row.height), players[i].Key, BtcVisualRows.Lbl, Color.white);
                BtcVisualRows.Txt(new Rect(row.x + row.width * 0.3f, row.y, row.width * 0.7f - fs * 0.5f, row.height), BtcHandicap.SummaryOf(players[i].Value), BtcVisualRows.Dim, new Color(1f, 1f, 1f, 0.7f));
                y += row.height + 2f;
            }
        }
    }
}
