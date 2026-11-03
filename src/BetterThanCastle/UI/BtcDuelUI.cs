using System;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>Menu tab "Duel" (id 8 in ResourcePackManager.LayoutTabs): micro-duel settings and the start button.</summary>
    internal static class BtcDuelUI
    {
        public static bool TabActive;
        public static float ContentH = 600f;

        /// <summary>Adapter for the RPM tab chain (the Replay branch expects a bool "close menu" result).</summary>
        public static bool DrawTab(Event e, float w, float rowH, int fs, Color old)
        {
            Draw(e, w, rowH, fs, old);
            return false;
        }

        public static void Draw(Event e, float w, float rowH, int fs, Color old)
        {
            float y = 0f;
            Rect r = new Rect(0f, y, w, rowH);
            BtcUiStyle.HeaderLabel(r, BtcGameplay.T("Micro duel vs bot", "Микродуэль против бота"), fs); y += rowH;
            GUI.Label(new Rect(fs, y, w - fs * 2f, rowH * 1.9f),
                BtcGameplay.T("Train pure micro on a small arena: safe zones, neutral, mid. Win by kill & escape or by holding the mid for 5 s. Settings apply to the next match (Rematch is instant).",
                              "Тренировка микро на маленькой арене: сейфзоны, нейтраль, мид. Победа: kill & escape или удержание мида 5 с. Настройки применяются со следующего матча (реванш мгновенный)."),
                BtcUiStyle.Note(fs)); y += rowH * 2f;

            // difficulty
            GUI.Label(new Rect(fs, y, w * 0.3f, rowH), BtcGameplay.T("Bot difficulty", "Сложность бота"), BtcUiStyle.Label(fs));
            y += rowH;
            int n = Enum.GetValues(typeof(BtcDuelDifficulty)).Length;
            float bw = (w - fs * 2f) / n;
            for (int i = 0; i < n; i++)
            {
                bool on = (int)BtcDuelConfig.Difficulty == i;
                Rect b = new Rect(fs + bw * i, y, bw - 3f, rowH * 0.9f);
                if (GUI.Button(b, ((BtcDuelDifficulty)i).ToString(), on ? BtcUiStyle.TabActive(fs) : BtcUiStyle.Tab(fs))) { BtcDuelConfig.Difficulty = (BtcDuelDifficulty)i; BtcDuelConfig.Save(); }
            }
            y += rowH * 1.1f;
            string[] dsc = {
                BtcGameplay.T("Easy: attacks the nearest, no tricks.", "Easy: Р±СЊС‘С‚ Р±Р»РёР¶Р°Р№С€РµРіРѕ, Р±РµР· РїСЂРёС‘РјРѕРІ."),
                BtcGameplay.T("Medium: focus fire and escapes, slow reactions, no skips.", "Medium: С„РѕРєСѓСЃ Рё СѓС…РѕРґ, РјРµРґР»РµРЅРЅС‹Рµ СЂРµР°РєС†РёРё, Р±РµР· СЃРєРёРїРѕРІ."),
                BtcGameplay.T("Hard: slow imprecise skips, rotates wounded units.", "Hard: РјРµРґР»РµРЅРЅС‹Рµ РЅРµС‚РѕС‡РЅС‹Рµ СЃРєРёРїС‹, СЂРѕС‚Р°С†РёСЏ СЂР°РЅРµРЅС‹С…."),
                BtcGameplay.T("Extreme: near-perfect skips and reactions.", "Extreme: РїРѕС‡С‚Рё РёРґРµР°Р»СЊРЅС‹Рµ СЃРєРёРїС‹ Рё СЂРµР°РєС†РёРё."),
                BtcGameplay.T("Chosen: everything the bot can do.", "Chosen: РІСЃС‘, С‡С‚Рѕ СѓРјРµРµС‚ Р±РѕС‚."),
                BtcGameplay.T("Custom: same as Chosen for now.", "Custom: РїРѕРєР° РєР°Рє Chosen."),
                BtcGameplay.T("BreaktronZero (B0): neural network bot trained in the lab simulator (falls back to Chosen if no policy is available).", "BreaktronZero (B0): нейросетевой бот, обученный в симуляции (если нет весов, играет как Chosen).") };
            GUI.Label(new Rect(fs, y, w - fs * 2f, rowH), dsc[(int)BtcDuelConfig.Difficulty], BtcUiStyle.Note(fs));
            y += rowH;
            // toggles
            if (BtcUiStyle.CheckRow(new Rect(0f, y, w, rowH), BtcDuelConfig.SkipAnims, BtcGameplay.T("Allow animation skips (move-attack trick)", "Скипы анимации (мув-атак трюк)"), fs)) { BtcDuelConfig.SkipAnims = !BtcDuelConfig.SkipAnims; BtcDuelConfig.Save(); }
            y += rowH;
            if (BtcUiStyle.CheckRow(new Rect(0f, y, w, rowH), BtcDuelConfig.InfiniteHeal, BtcGameplay.T("Infinite heal (10 HP/s for everyone)", "Бесконечный хил (10 HP/с у всех)"), fs)) { BtcDuelConfig.InfiniteHeal = !BtcDuelConfig.InfiniteHeal; BtcDuelConfig.Save(); }
            y += rowH;
            if (BtcUiStyle.CheckRow(new Rect(0f, y, w, rowH), BtcDuelConfig.ArenaScale == 3, BtcGameplay.T("Large arena x3 (300x60)", "Р‘РѕР»СЊС€Р°СЏ Р°СЂРµРЅР° x3 (300x60)"), fs)) { BtcDuelConfig.ArenaScale = BtcDuelConfig.ArenaScale == 3 ? 1 : 3; BtcDuelConfig.Save(); }
            y += rowH * 1.2f;

            // unit table
            float c0 = fs, c1 = w * 0.42f, c2 = w * 0.72f, cw = w * 0.26f;
            GUI.Label(new Rect(c0, y, c1 - c0, rowH), BtcGameplay.T("Unit", "Юнит"), BtcUiStyle.Label(fs));
            GUI.Label(new Rect(c1, y, cw, rowH), BtcGameplay.T("You", "Ты") + " (" + BtcDuelConfig.Total(BtcDuelConfig.PlayerCount) + ")", BtcUiStyle.Label(fs));
            GUI.Label(new Rect(c2, y, cw, rowH), BtcGameplay.T("Bot", "Бот") + " (" + BtcDuelConfig.Total(BtcDuelConfig.BotCount) + ")", BtcUiStyle.Label(fs));
            y += rowH;
            for (int k = 0; k < BtcDuelConfig.UnitKinds; k++)
            {
                GUI.Label(new Rect(c0, y, c1 - c0, rowH), BtcDuelConfig.UnitNames[k], BtcUiStyle.Label(fs));
                Counter(new Rect(c1, y, cw, rowH * 0.9f), BtcDuelConfig.PlayerCount, k, fs, e);
                Counter(new Rect(c2, y, cw, rowH * 0.9f), BtcDuelConfig.BotCount, k, fs, e);
                y += rowH;
            }
            GUI.Label(new Rect(fs, y, w - fs * 2f, rowH), BtcGameplay.T("Shift = +/-5. Barrel worker: a worker carrying an explosive barrel (separate unit).", "Shift = +/-5. Barrel worker: работник с бочкой в руках (отдельный юнит)."), BtcUiStyle.Note(fs));
            y += rowH * 1.3f;

            // start
            bool active = BtcDuel.Active;
            bool en = GUI.enabled;
            GUI.enabled = en && !active;
            if (GUI.Button(new Rect(fs, y, w - fs * 2f, rowH * 1.3f), active ? BtcGameplay.T("Duel is running (use Rematch / Exit on screen)", "Дуэль идёт (Реванш / Выход на экране)") : BtcGameplay.T("Start duel", "Начать дуэль"), BtcUiStyle.Button(fs)))
                BtcDuel.Launch();
            GUI.enabled = en;
            y += rowH * 1.5f;
            if (active) GUI.Label(new Rect(fs, y, w - fs * 2f, rowH), BtcGameplay.T("Changes made now apply at the next Rematch.", "Изменения сработают со следующего реванша."), BtcUiStyle.Note(fs));
            y += rowH;
            ContentH = y + rowH * 0.5f;
        }

        private static void Counter(Rect r, int[] arr, int k, int fs, Event e)
        {
            float bw = r.height * 1.2f;
            int step = e.shift ? 5 : 1;
            if (GUI.Button(new Rect(r.x, r.y, bw, r.height), "-", BtcUiStyle.Button(fs))) { arr[k] = Mathf.Max(0, arr[k] - step); BtcDuelConfig.Save(); }
            GUI.Label(new Rect(r.x + bw, r.y, r.width - bw * 2f, r.height), arr[k].ToString(), BtcUiStyle.Label(fs));
            if (GUI.Button(new Rect(r.x + r.width - bw, r.y, bw, r.height), "+", BtcUiStyle.Button(fs))) { arr[k] = Mathf.Min(60, arr[k] + step); BtcDuelConfig.Save(); }
        }
    }
}
