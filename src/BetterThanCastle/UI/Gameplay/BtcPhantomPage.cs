using System.Globalization;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>Gameplay tab page "Phantoms" (PPhantom): a plain-language summary of the configured rules, a link to the phantom vision row
    /// (Rules > Fog of war: vision) and the rule rows (group "phantom", dependent rows hidden while their parent is off).</summary>
    internal static class BtcPhantomPage
    {
        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }

        private static float V(float[] a, string id, float def)
        {
            int i = BtcGameplay.Idx(id);
            return a != null && i >= 0 && i < a.Length ? a[i] : def;
        }

        private static bool On(float[] a, string id) { return V(a, id, 0f) > 0.5f; }

        private static string N(float v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }

        internal static bool Vis(BtcGameplay.Def d, float[] shown)
        {
            if (d.Id == "phantom.on") return true;
            if (!On(shown, "phantom.on")) return false;
            if (d.Id == "phantom.delay") return !On(shown, "phantom.peace");
            return true;
        }

        internal static string[] Summary(float[] a)
        {
            if (!On(a, "phantom.on")) return new[] { Tt("Off: no phantoms, vanilla workers only.", "Выключено: фантомов нет, только ванильные рабочие.") };
            string n = N(V(a, "phantom.count", 2f)), cd = N(V(a, "phantom.cd", 15f)), pen = N(V(a, "phantom.pen", 0f)), vis = N(V(a, "vis.phantom", 0.25f));
            float delay = V(a, "phantom.delay", 0f);
            string when = On(a, "phantom.peace") ? Tt("when the peace time ends", "когда кончается мирное время")
                        : (delay <= 0f ? Tt("right at the start", "сразу на старте") : Tt("after " + BtcTimer.Clock(delay) + " of game time", "через " + BtcTimer.Clock(delay) + " игрового времени"));
            string fly = On(a, "phantom.fly") ? Tt("They fly in a straight line through anything and hover where they stop.", "Летают по прямой сквозь всё и висят там, где остановились.")
                       : On(a, "phantom.ghost") ? Tt("They walk through buildings, enemy units and the forest; blocks stop them.", "Ходят сквозь постройки, вражеских юнитов и лес; блоки их останавливают.") : null;
            string[] lines =
            {
                Tt(n + " phantoms per player at the home crystal, " + when + ".", n + " фантомов у каждого игрока у домашнего кристалла, " + when + "."),
                Tt("A dead phantom comes back after " + cd + " s" + (pen != "0" ? " as soon as the home crystal has " + pen + " energy to pay for it." : "."),
                   "Погибший фантом возвращается через " + cd + " с" + (pen != "0" ? ", как только в домашнем кристалле есть " + pen + " энергии на оплату." : ".")),
                Tt("Enemy arrows, bolts and shots fly through a phantom: it dies, the shot flies on at full damage.", "Вражеские стрелы, болты и снаряды пролетают сквозь фантома: он погибает, снаряд летит дальше с полным уроном."),
                Tt("Free: not in the bricktron limit, not in the spawn price.", "Бесплатные: не в лимите бриктронов, не в цене спавна."),
                Tt("1 HP, 0 stamina; fists do no damage to units, a weapon does.", "1 ХП, 0 стамины; кулаками не бьют юнитов, оружием бьют."),
                Tt("No damage to enemy buildings, no crystal capture, no call to arms.", "Не ломают вражеские постройки, не захватывают кристаллы, без призыва к оружию."),
                Tt("Vision x" + vis + " of a unit. No corpse: what it carried vanishes.", "Обзор x" + vis + " от юнита. Без трупа: то, что нёс, исчезает.")
            };
            if (fly == null) return lines;
            string[] all = new string[lines.Length + 1];
            lines.CopyTo(all, 0);
            all[lines.Length] = fly;
            return all;
        }

        internal static void Card(ref float y, float w, int fs, float[] shown, Vector2 off)
        {
            string[] lines = Summary(shown);
            float pad = fs, lh = fs * 1.5f;
            bool on = On(shown, "phantom.on");
            float h = fs * 0.8f + lines.Length * lh;
            Rect card = new Rect(pad, y, w - pad * 2f, h);
            Color c = on ? BtcGameplayTab.Critical : new Color(1f, 1f, 1f, 0.4f);
            BtcReplayUI.Fill(card, new Color(1f, 1f, 1f, 0.04f));
            BtcReplayUI.Fill(new Rect(card.x, card.y + fs * 0.25f, 3f, h - fs * 0.5f), c);
            float ig = fs * 1.6f;
            BtcReplayIcons.Glyph(BtcReplayIcons.Ghost, new Rect(card.x + fs * 0.7f, card.y + fs * 0.4f, ig, ig), c);
            for (int i = 0; i < lines.Length; i++)
                BtcVisualRows.Txt(new Rect(card.x + fs * 3f, card.y + fs * 0.4f + i * lh, card.width - fs * 3.3f, lh), lines[i], BtcVisualRows.Lbl, new Color(1f, 1f, 1f, i == 0 ? 0.92f : 0.75f));
            if (on) BtcReplayIcons.Hint(card, new Rect(off.x + card.x, off.y + card.y, card.width, card.height),
                Tt("Critical rule: adds units to every player. Peace time and the delay come from the advanced timer (Timer page). A custom look: put Phantom.png into your resource pack.",
                   "Критичное правило: добавляет юнитов каждому игроку. Мирное время и задержка берутся из продвинутого таймера (страница Таймер). Свой вид: положите Phantom.png в свой ресурспак."));
            y += h + fs * 0.4f;
        }
    }

    internal sealed partial class BtcGameplayTab
    {
        private void PagePhantom(ref float y, float w, int fs, float[] shown, float[] bas, bool interact, bool baseMine)
        {
            BtcPhantomPage.Card(ref y, w, fs, shown, _off);
            PageRules(ref y, w, fs, shown, bas, interact, baseMine, "phantom");
        }
    }
}
