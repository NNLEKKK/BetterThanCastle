using System.Globalization;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>Gameplay tab page "Base shields" (PShield): a plain-language summary of the configured rules, the live barrier list during a match,
    /// a link to the personal look/sound settings (Visuals > World overlays > Base shields) and the rule rows (group "shield", dependent rows hidden while their parent is off).</summary>
    internal static class BtcShieldPage
    {
        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }

        private static float V(float[] a, string id, float def)
        {
            int i = BtcGameplay.Idx(id);
            return a != null && i >= 0 && i < a.Length ? a[i] : def;
        }

        private static bool On(float[] a, string id) { return V(a, id, 0f) > 0.5f; }

        /// <summary>Row visibility inside the shield group: everything but the master row needs the master row on; sub-values need their parent.</summary>
        internal static bool Vis(BtcGameplay.Def d, float[] shown)
        {
            if (d.Id == "shield.on") return true;
            if (!On(shown, "shield.on")) return false;
            if (d.Id == "shield.shardN") return On(shown, "shield.shards");
            if (d.Id == "shield.mageR" || d.Id == "shield.mageD") return On(shown, "shield.mage");
            if (d.Id == "shield.team") return On(shown, "shield.shards") || On(shown, "shield.mage");
            return true;
        }

        /// <summary>What the configured rules mean, one short line each.</summary>
        internal static string[] Summary(float[] a)
        {
            if (!On(a, "shield.on")) return new[] { Tt("Off: no barriers, vanilla fights.", "Выключено: барьеров нет, бои как в ванили.") };
            bool home = On(a, "shield.home"), capt = On(a, "shield.capt");
            string where = home && capt ? Tt("home and captured crystals", "домашний и захваченные кристаллы") : (home ? Tt("home crystals only", "только домашние кристаллы") : (capt ? Tt("captured crystals only", "только захваченные кристаллы") : Tt("no crystal (both switches are off)", "ни одного кристалла (оба переключателя выкл)")));
            string r = V(a, "shield.radius", 20f).ToString("0", CultureInfo.InvariantCulture);
            string l1 = Tt("Barriers around " + where + ", radius " + r + ".", "Барьеры вокруг: " + where + ", радиус " + r + ".");
            bool peace = On(a, "shield.peace"), sh = On(a, "shield.shards"), mg = On(a, "shield.mage"), team = On(a, "shield.team");
            string l2;
            if (!sh && !mg) l2 = peace ? Tt("Up during peace time, down when it ends.", "Активны в мирное время, падают, когда мир кончается.") : Tt("Always up (no drop rule): bases cannot be attacked at all.", "Активны всегда (нет правил снятия): базы вообще нельзя атаковать.");
            else
            {
                string who = team ? Tt("a team", "команда") : Tt("a player", "игрок");
                string n = V(a, "shield.shardN", 2f).ToString("0", CultureInfo.InvariantCulture);
                string s1 = sh ? Tt(who + " holding " + n + " captured crystals", who + ", держащая " + n + " захваченных кристалла") : null;
                if (sh && !team) s1 = Tt("a player holding " + n + " captured crystals", "игрок, держащий " + n + " захваченных кристалла");
                string s2 = mg ? Tt("an artificer of the attacker sees the barrier", "артефактор атакующего видит барьер") : null;
                string cond = s1 != null && s2 != null ? s1 + Tt(", or ", ", или ") + s2 : (s1 ?? s2);
                l2 = (peace ? Tt("Up during peace. After that a barrier falls for the attacker when: ", "Активны в мирное время. Потом барьер падает для атакующего, если: ")
                            : Tt("A barrier falls for the attacker when: ", "Барьер падает для атакующего, если: ")) + cond + ".";
            }
            string l3 = Tt("Enemies caught inside: powerless, " + V(a, "shield.dps", 20f).ToString("0", CultureInfo.InvariantCulture) + " HP/s until they leave.",
                           "Враги, оказавшиеся внутри: бессильны, " + V(a, "shield.dps", 20f).ToString("0", CultureInfo.InvariantCulture) + " ХП/с, пока не выйдут.");
            if (mg) l3 += Tt(" Barrier back " + V(a, "shield.mageD", 5f).ToString("0", CultureInfo.InvariantCulture) + " s after the artificer left.", " Барьер возвращается через " + V(a, "shield.mageD", 5f).ToString("0", CultureInfo.InvariantCulture) + " с после ухода артефактора.");
            return new[] { l1, l2, l3 };
        }

        /// <summary>Summary card at the top of the page.</summary>
        internal static void Card(ref float y, float w, int fs, float[] shown, Vector2 off)
        {
            string[] lines = Summary(shown);
            float pad = fs, lh = fs * 1.5f;
            bool on = On(shown, "shield.on");
            float h = fs * 0.8f + lines.Length * lh;
            Rect card = new Rect(pad, y, w - pad * 2f, h);
            Color c = on ? BtcGameplayTab.Critical : new Color(1f, 1f, 1f, 0.4f);
            BtcReplayUI.Fill(card, new Color(1f, 1f, 1f, 0.04f));
            BtcReplayUI.Fill(new Rect(card.x, card.y + fs * 0.25f, 3f, h - fs * 0.5f), c);
            float ig = fs * 1.6f;
            BtcReplayIcons.Glyph(BtcReplayIcons.Shield, new Rect(card.x + fs * 0.7f, card.y + fs * 0.4f, ig, ig), c);
            for (int i = 0; i < lines.Length; i++)
                BtcVisualRows.Txt(new Rect(card.x + fs * 3f, card.y + fs * 0.4f + i * lh, card.width - fs * 3.3f, lh), lines[i], BtcVisualRows.Lbl, new Color(1f, 1f, 1f, i == 0 ? 0.92f : 0.75f));
            if (on) BtcReplayIcons.Hint(card, new Rect(off.x + card.x, off.y + card.y, card.width, card.height),
                Tt("Critical rule: changes who can attack whom. Peace time comes from the advanced timer (Timer page).", "Критичное правило: меняет, кто кого может атаковать. Мирное время берётся из продвинутого таймера (страница Таймер)."));
            y += h + fs * 0.4f;

            // live barriers during a match
            string[] rows = BtcShieldFx.Rows;
            if (BtcShield.Bars.Count == 0 || rows == null || rows.Length == 0) return;
            BtcVisualRows.Txt(new Rect(pad + fs * 0.4f, y, w, fs * 1.5f), Tt("Now in this match:", "Сейчас в матче:"), BtcVisualRows.Dim, new Color(1f, 1f, 1f, 0.6f));
            y += fs * 1.5f;
            Color[] dots = BtcShieldFx.Dots;
            for (int i = 0; i < rows.Length; i++)
            {
                if (dots != null && i < dots.Length) BtcReplayUI.Fill(new Rect(pad + fs * 0.6f, y + fs * 0.45f, fs * 0.6f, fs * 0.6f), dots[i]);
                BtcVisualRows.Txt(new Rect(pad + fs * 1.6f, y, w - pad * 2f - fs * 1.6f, fs * 1.5f), rows[i], BtcVisualRows.Lbl, new Color(1f, 1f, 1f, 0.85f));
                y += fs * 1.5f;
            }
            y += fs * 0.4f;
        }

        // ------------------------------------------------------------------ link to the personal look/sound settings (Visuals tab)
        /// <summary>Small clickable line: how barriers look and sound for you lives in Visuals > World overlays > Base shields.</summary>
        internal static void Link(ref float y, float w, int fs, Vector2 off)
        {
            float pad = fs, h = fs * 1.7f;
            Rect r = new Rect(pad, y, w - pad * 2f, h);
            bool hv = r.Contains(Event.current.mousePosition);
            if (hv) BtcReplayUI.Fill(r, new Color(1f, 1f, 1f, 0.05f));
            float g = fs * 1.1f;
            Color c = hv ? Color.white : BtcMenu.Accent;
            BtcReplayIcons.Glyph(BtcReplayIcons.Eye, new Rect(r.x + fs * 0.4f, r.center.y - g * 0.5f, g, g), c);
            BtcVisualRows.Txt(new Rect(r.x + fs * 2f, r.y, r.width - fs * 4f, h),
                Tt("Barrier opacity, sounds and the HUD list (only for you): Visuals > World overlays", "Прозрачность, звуки и список барьеров на экране (только для вас): Визуал > Оверлеи мира"),
                BtcVisualRows.Dim, c);
            BtcReplayIcons.Glyph(BtcReplayIcons.ChevR, new Rect(r.xMax - fs * 1.4f, r.center.y - g * 0.5f, g, g), c);
            BtcReplayIcons.Hint(r, new Rect(off.x + r.x, off.y + r.y, r.width, r.height), Tt("Open these settings in the Visuals tab", "Открыть эти настройки во вкладке Визуал"));
            if (BtcSfx.Btn(r, "", BtcReplayUI.sBtn))
            {
                BtcUi.SetOpen("vis:world:shield", true);
                BtcChangelog.Go("tab:1:1");
            }
            y += h + fs * 0.3f;
        }
    }

    internal sealed partial class BtcGameplayTab
    {
        private void PageShield(ref float y, float w, int fs, float[] shown, float[] bas, bool interact, bool baseMine)
        {
            BtcShieldPage.Card(ref y, w, fs, shown, _off);
            BtcShieldPage.Link(ref y, w, fs, _off);
            PageRules(ref y, w, fs, shown, bas, interact, baseMine, "shield");
        }
    }
}
