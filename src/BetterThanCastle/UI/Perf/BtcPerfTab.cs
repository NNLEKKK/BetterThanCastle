using BepInEx.Configuration;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Performance tab (menu tab 14, bolt icon): every speed / memory / loading setting of the mod in one place. One page of folds;
    /// each row is a config switch with an undo entry. Most of them are read once at start or at the next world load (the hint says so).
    /// </summary>
    internal sealed class BtcPerfTab : BtcMenuTab
    {
        internal static readonly BtcPerfTab Instance = new BtcPerfTab();
        private const string Scope = "perf";
        private Vector2 _scroll;
        private float _h = 100f;

        internal override int Id { get { return 14; } }
        internal override bool IsActive { get { return BtcMenu.CurrentId == 14; } }
        internal override string UndoScope { get { return Scope; } }

        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }

        private static void Row(ref float y, float w, int fs, ConfigEntry<bool> e, string en, string ru, string hintEn, string hintRu)
        {
            if (e == null) return;
            bool on = e.Value;
            float y0 = y;
            bool click = BtcVisualRows.Switch(ref y, w, fs, 0, Tt(en, ru), on, hintEn == null ? null : Tt(hintEn, hintRu));
            BtcJump.Row(Scope, e.Definition.Key, new Rect(fs * 0.5f, y0, w - fs, y - y0));
            if (!click) return;
            e.Value = !on;
            BtcUndo.Push(Scope, Tt(en, ru), delegate { e.Value = on; });
        }

        /// <summary>Changelog link jump: opens the section of the setting (config key, e.g. "MemoryFixes"), scrolls to it and pulses it.</summary>
        internal void Jump(string key)
        {
            string fold = "perf:frame";
            ConfigEntry<bool>[] vis = { BtcConfig.PauseHiddenAnims, BtcConfig.HideNameTags, BtcConfig.StaticCrystals, BtcConfig.SimpleFireflies, BtcConfig.CrystalLights, BtcConfig.ShortShadows,
                BtcConfig.NoSunShadows, BtcConfig.NoAmbientOcclusion, BtcConfig.NoBloom, BtcConfig.WarmShaders, BtcConfig.UnitNightLights, BtcConfig.FireflyLights, BtcConfig.CrystalShadows };
            ConfigEntry<bool>[] ui = { BtcConfig.CsGameMenu, BtcConfig.ReplayNoGameHud };
            ConfigEntry<bool>[] load = { BtcConfig.FastLoad, BtcConfig.ThreadedTerrain, BtcConfig.SkipModeIntro, BtcConfig.LoadTimer, BtcConfig.LoadCancel, BtcConfig.ReuseMenuPanels, BtcConfig.EarlyClientUi };
            if (Has(vis, key)) fold = "perf:visual";
            else if (Has(ui, key)) fold = "perf:ui";
            else if (Has(load, key)) fold = "perf:load";
            BtcUi.SetOpen(fold, true);
            BtcJump.Target(Scope, key);
        }

        private static bool Has(ConfigEntry<bool>[] list, string key)
        {
            for (int i = 0; i < list.Length; i++) if (list[i] != null && list[i].Definition.Key == key) return true;
            return false;
        }

        // one switch for every visual cut below: on = all decorations off; undo puts each back as it was
        private static void MaxRow(ref float y, float w, int fs)
        {
            ConfigEntry<bool>[] keep = { BtcConfig.UnitNightLights, BtcConfig.FireflyLights, BtcConfig.CrystalShadows, BtcConfig.CrystalLights };
            ConfigEntry<bool>[] cut = { BtcConfig.PauseHiddenAnims, BtcConfig.HideNameTags, BtcConfig.StaticCrystals, BtcConfig.SimpleFireflies, BtcConfig.ShortShadows, BtcConfig.NoAmbientOcclusion, BtcConfig.NoBloom, BtcConfig.NoSunShadows };
            bool on = true;
            foreach (ConfigEntry<bool> e in keep) if (e != null && e.Value) on = false;
            foreach (ConfigEntry<bool> e in cut) if (e != null && !e.Value) on = false;
            string en = "Maximum performance", ru = "Максимальная производительность";
            if (!BtcVisualRows.Switch(ref y, w, fs, 0, Tt(en, ru), on, Tt("Turns off every decoration in this section at once.", "Выключает все украшения этого раздела разом."))) return;
            bool[] oldKeep = new bool[keep.Length], oldCut = new bool[cut.Length];
            for (int i = 0; i < keep.Length; i++) if (keep[i] != null) { oldKeep[i] = keep[i].Value; keep[i].Value = on; }
            for (int i = 0; i < cut.Length; i++) if (cut[i] != null) { oldCut[i] = cut[i].Value; cut[i].Value = !on || i == 0; }
            BtcUndo.Push(Scope, Tt(en, ru), delegate
            {
                for (int i = 0; i < keep.Length; i++) if (keep[i] != null) keep[i].Value = oldKeep[i];
                for (int i = 0; i < cut.Length; i++) if (cut[i] != null) cut[i].Value = oldCut[i];
            });
        }

        internal override void Draw(Rect area, int fs)
        {
            BtcVisualRows.Styles(fs);
            Event ev = Event.current;
            if (ev.type == EventType.KeyDown && ev.keyCode == BtcMenu.MenuKeyMain)
            {
                BtcMenu.PgDownFrame = Time.frameCount;
                ResourcePackManager.CloseMenu();
                ev.Use();
                return;
            }
            Rect view = new Rect(area.x, area.y + fs * 0.4f, area.width, area.height - fs * 0.4f);
            float w = view.width - (_h > view.height ? 14f : 0f);
            BtcJump.Scroll(ref _scroll, view.height);
            _scroll = GUI.BeginScrollView(view, _scroll, new Rect(0f, 0f, w, _h));
            float y = fs * 0.3f;
            const string after = "Applies after a game restart.", afterRu = "Действует после перезапуска игры.";
            const string next = "Applies from the next world load.", nextRu = "Действует со следующей загрузки мира.";

            if (BtcUi.Fold(ref y, w, fs, "perf:frame", Tt("Frame and memory", "Кадр и память"), 3))
            {
                Row(ref y, w, fs, BtcConfig.MemoryFixes, "Memory optimisation (fewer and shorter GC freezes)", "Оптимизация памяти (меньше и короче фризы GC)", after, afterRu);
                Row(ref y, w, fs, BtcConfig.FrameSavers, "Frame optimisation (less CPU per frame)", "Оптимизация кадра (меньше нагрузка на CPU)", after, afterRu);
                Row(ref y, w, fs, BtcConfig.AnimatorCulling, "Skip animations of units off screen", "Не анимировать юнитов вне экрана",
                    "Needs BetterConnection. Measured: almost no gain.", "Нужен BetterConnection. По замерам почти не даёт прироста.");
                y += fs * 0.5f;
            }
            if (BtcUi.Fold(ref y, w, fs, "perf:visual", Tt("Visuals", "Визуал"), 14))
            {
                MaxRow(ref y, w, fs);
                Row(ref y, w, fs, BtcConfig.PauseHiddenAnims, "Pause hidden animations", "Не анимировать скрытое",
                    "Icons and name tags over units animate only while shown, crystal shards only on screen; spawn point handles (world editor only) are off in a normal game. Looks the same.",
                    "Значки и таблички над юнитами анимируются только когда видны, осколки кристаллов только на экране; ручки точек спавна (нужны только в редакторе мира) в обычной игре выключены. Выглядит так же.");
                Row(ref y, w, fs, BtcConfig.HideNameTags, "Hide name tags", "Скрыть таблички с именами",
                    "No names over units and beacons, even on hover; the unit mode icon stays.", "Нет имён над юнитами и маяками, даже при наведении; значок режима юнита остаётся.");
                Row(ref y, w, fs, BtcConfig.StaticCrystals, "Static crystals", "Статичные кристаллы",
                    "No breathing, shaking or capture fill; the colour changes only when a capture completes. The capture progress bar stays.",
                    "Без дыхания, тряски и заливки при захвате; цвет меняется, только когда захват завершён. Полоска прогресса захвата остаётся.");
                Row(ref y, w, fs, BtcConfig.SimpleFireflies, "Simple fireflies", "Простые светлячки",
                    "No particles and trail on fireflies (dozens at every home crystal); the glowing ball stays.",
                    "Без частиц и хвоста у светлячков (их десятки у каждого домашнего кристалла); светящийся шарик остаётся.");
                Row(ref y, w, fs, BtcConfig.CrystalLights, "Light from crystals", "Свет от кристаллов",
                    "The light crystals cast on the ground around them. Off: crystals still glow themselves.",
                    "Свет, который кристаллы бросают на землю вокруг. Выкл.: сами кристаллы всё так же светятся.");
                Row(ref y, w, fs, BtcConfig.ShortShadows, "Short shadows", "Короткие тени",
                    "Sun and moon shadows up to 120 m from the camera instead of 400 m: far fewer shadow renders, sharper shadows nearby.",
                    "Тени солнца и луны до 120 м от камеры вместо 400 м: намного меньше отрисовок теней, вблизи тени чётче.");
                Row(ref y, w, fs, BtcConfig.NoSunShadows, "No sun shadows", "Без теней от солнца",
                    "No sun and moon shadows at all, whatever the game graphics option says: a day then costs as much as a night.",
                    "Совсем без теней от солнца и луны, что бы ни стояло в графике игры: день тогда нагружает как ночь.");
                Row(ref y, w, fs, BtcConfig.NoAmbientOcclusion, "No ambient occlusion", "Без затенения (AO)",
                    "Soft contact shadows off, whatever the game graphics option says.", "Мягкие контактные тени выключены, что бы ни стояло в графике игры.");
                Row(ref y, w, fs, BtcConfig.NoBloom, "No bloom", "Без свечения (bloom)",
                    "Glow around bright things off, whatever the game graphics option says.", "Свечение вокруг ярких мест выключено, что бы ни стояло в графике игры.");
                Row(ref y, w, fs, BtcConfig.WarmShaders, "Prepare shaders at game start", "Готовить шейдеры при запуске игры",
                    "Off by default. Once per game start: about 2 s pause in the main menu and 1.6 s longer first world load; in return the first night or effects no longer freeze a match for up to 0.5 s. " + after,
                    "По умолчанию выключено. Один раз за запуск: пауза около 2 с в главном меню и первая загрузка мира дольше на 1.6 с; зато первая ночь и эффекты не фризят матч до 0.5 с. " + afterRu);
                Row(ref y, w, fs, BtcConfig.UnitNightLights, "Unit lanterns at night", "Фонари юнитов ночью",
                    "One light per unit. Off: darker units at night, less load.", "Свет на каждом юните. Выкл.: юниты ночью темнее, нагрузка меньше.");
                Row(ref y, w, fs, BtcConfig.FireflyLights, "Firefly glow lights", "Свет светлячков",
                    "Dozens of small lights around crystals at night. Off: the fireflies stay visible.", "Десятки огоньков у кристаллов ночью. Выкл.: светлячки остаются видны.");
                Row(ref y, w, fs, BtcConfig.CrystalShadows, "Shadows from crystal lights", "Тени от света кристаллов",
                    "Six extra shadow renders per crystal, day and night. Off: crystals still glow.", "Шесть лишних отрисовок теней на кристалл, днём и ночью. Выкл.: кристаллы всё так же светятся.");
                y += fs * 0.5f;
            }
            if (BtcUi.Fold(ref y, w, fs, "perf:ui", Tt("Interface", "Интерфейс"), 2))
            {
                Row(ref y, w, fs, BtcConfig.CsGameMenu, "Fast game interface (C# instead of the Lua menu)", "Быстрый игровой интерфейс (C# вместо Lua-меню)",
                    "Faster world load, much less memory. " + next, "Быстрее загрузка мира, намного меньше памяти. " + nextRu);
                Row(ref y, w, fs, BtcConfig.ReplayNoGameHud, "Replays without the game interface", "Реплеи без игрового интерфейса",
                    "Faster replay load, less memory; the mini map stays.", "Быстрее загрузка реплея, меньше памяти; миникарта остаётся.");
                y += fs * 0.5f;
            }
            if (BtcUi.Fold(ref y, w, fs, "perf:load", Tt("Loading", "Загрузка"), 7))
            {
                Row(ref y, w, fs, BtcConfig.FastLoad, "Faster world load", "Ускоренная загрузка мира",
                    "Terrain drawn in a few frames, no idle waits, faster curtain, no freeze at match start. " + after,
                    "Рельеф строится за несколько кадров, без пустых ожиданий, шторка быстрее, нет фриза в начале матча. " + afterRu);
                Row(ref y, w, fs, BtcConfig.ThreadedTerrain, "Build the terrain on all CPU cores", "Строить рельеф на всех ядрах процессора",
                    "The heaviest terrain passes run on several threads, same result. About 0.7 s faster load.",
                    "Самые тяжёлые проходы рельефа идут в нескольких потоках, результат тот же. Загрузка быстрее примерно на 0.7 с.");
                Row(ref y, w, fs, BtcConfig.SkipModeIntro, "Skip the game mode intro window", "Пропускать вступительное окно режима",
                    "Sandbox, conquest and invasion start at once, as if its button was pressed (the tutorial keeps it).",
                    "Песочница, завоевание и вторжение начинаются сразу, как будто нажата кнопка окна (в обучении окно остаётся).");
                Row(ref y, w, fs, BtcConfig.LoadTimer, "Load timer", "Таймер загрузки", null, null);
                Row(ref y, w, fs, BtcConfig.LoadCancel, "Cancel button while loading", "Кнопка отмены во время загрузки", null, null);
                Row(ref y, w, fs, BtcConfig.ReuseMenuPanels, "Reuse menu panels while loading (experimental)", "Переиспользовать панели меню при загрузке (эксперимент)",
                    "Was slower in tests, off by default.", "В тестах было медленнее, по умолчанию выключено.");
                Row(ref y, w, fs, BtcConfig.EarlyClientUi, "Client builds its interface while the host loads (experimental)", "Клиент строит интерфейс, пока грузится хост (эксперимент)",
                    "Multiplayer client only.", "Только для клиента в мультиплеере.");
                y += fs * 0.5f;
            }
            BtcUi.Note(ref y, w, fs, Tt("Everything here is local: nothing changes on the wire, other players are not affected.",
                "Всё здесь локально: по сети ничего не меняется, на других игроков не влияет."));
            _h = y + fs;
            GUI.EndScrollView();
            BtcReplayIcons.DrawTip(fs, area.xMax, false);
        }
    }
}
