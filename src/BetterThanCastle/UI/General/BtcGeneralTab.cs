using BepInEx.Configuration;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// General tab (menu tab 15, header corner next to the UI editor): settings of the game window and of the mod itself that belong to no feature:
    /// window mode, intro, loading curtain, language, update check, Discord, menu keys. One page of folds, every change has an undo step (scope "general").
    /// The UI sound level lives in the UI editor (Menu look page).
    /// </summary>
    internal sealed class BtcGeneralTab : BtcMenuTab
    {
        internal static readonly BtcGeneralTab Instance = new BtcGeneralTab();
        internal const int TabId = 15;
        private const string Scope = "general";
        private Vector2 _scroll;
        private float _h = 100f;

        internal override int Id { get { return TabId; } }
        internal override bool IsActive { get { return BtcMenu.CurrentId == TabId; } }
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

        /// <summary>Link jump (tab:15:&lt;config key or fold&gt;): opens the fold of the row and pulses it.</summary>
        internal void Jump(string key)
        {
            string fold = "gen:window";
            if (key == "lang" || key == "langgame") fold = "gen:lang";
            else if (key == "CheckOnStart") fold = "gen:update";
            else if (key == "discord") fold = "gen:apps";
            else if (key == "keys") fold = "gen:keys";
            BtcUi.SetOpen(fold, true);
            BtcJump.Target(Scope, key);
        }

        internal override void Draw(Rect area, int fs)
        {
            BtcVisualRows.Styles(fs);
            ResourcePackManager.EnsureVisualBuilt();   // fills BtcVisualData.GetDiscord / SetDiscord
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

            if (BtcUi.Fold(ref y, w, fs, "gen:window", Tt("Game window", "Окно игры"), 3))
            {
                int cur = BtcDisplay.Mode - 1;
                string[] tips = { Tt("Fullscreen", "Полный экран"), Tt("Windowed", "Окно"), Tt("Borderless", "Без рамки") };
                float y0 = y;
                int c = BtcVisualRows.Choice(ref y, w, fs, 0, Tt("Window mode", "Режим окна"), cur, null, new[] { BtcReplayIcons.Monitor, BtcReplayIcons.Layout, BtcReplayIcons.Grid }, tips);
                BtcJump.Row(Scope, "WindowMode", new Rect(fs * 0.5f, y0, w - fs, y - y0));
                if (c >= 0 && c != cur)
                {
                    int oldMode = BtcDisplay.Mode;
                    BtcDisplay.Apply(c + 1, 1);
                    BtcUndo.Push(Scope, Tt("Window mode", "Режим окна"), delegate { BtcDisplay.Apply(oldMode, 1); });
                }
                Row(ref y, w, fs, BtcConfig.SkipIntro, "Skip intro (logo screens on startup)", "Пропускать интро (заставки при запуске)", null, null);
                Row(ref y, w, fs, BtcConfig.NoLoadCurtain, "Load without the curtain (watch the world load)", "Загрузка без шторки (видно, как грузится мир)", null, null);
                y += fs * 0.5f;
            }
            if (BtcUi.Fold(ref y, w, fs, "gen:lang", Tt("Language", "Язык"), 1))
            {
                int cur = BtcLang.Russian ? 1 : 0;
                float y0 = y;
                int c = BtcVisualRows.Choice(ref y, w, fs, 0, Tt("Language of the mod", "Язык мода"), cur, new[] { "English", "Русский" }, null, null);
                BtcJump.Row(Scope, "lang", new Rect(fs * 0.5f, y0, w - fs, y - y0));
                if (c >= 0 && c != cur)
                {
                    bool wasRu = cur == 1;
                    BtcLang.SetRussian(c == 1);
                    BtcUndo.Push(Scope, Tt("Language", "Язык"), delegate { BtcLang.SetRussian(wasRu); });
                }
                bool gameRu = BtcMenuState.Bool("lang:game", true);
                float yg = y;
                if (BtcVisualRows.Switch(ref y, w, fs, 0, Tt("Translate mod elements in the game", "Переводить элементы мода в игре"), gameRu,
                    Tt("Rack filters, chopping blocks, terraform task. Off = they stay in English even with the Russian menu.", "Фильтры на стойках, пеньки, задание терраформинга. Выкл = остаются на английском даже при русском меню.")))
                {
                    BtcMenuState.SetBool("lang:game", !gameRu);
                    BtcUndo.Push(Scope, Tt("Translate mod elements in the game", "Переводить элементы мода в игре"), delegate { BtcMenuState.SetBool("lang:game", gameRu); });
                }
                BtcJump.Row(Scope, "langgame", new Rect(fs * 0.5f, yg, w - fs, y - yg));
                y += fs * 0.5f;
            }
            if (BtcUi.Fold(ref y, w, fs, "gen:update", Tt("Updates", "Обновления"), 1))
            {
                Row(ref y, w, fs, BtcConfig.UpdateCheck, "Check for a new mod version at game start", "Проверять новую версию мода при запуске игры",
                    "Asks GitHub Releases once per start and offers the update in a card.", "Один раз за запуск спрашивает GitHub Releases и предлагает обновление в карточке.");
                string skipped = BtcConfig.UpdateSkipped != null ? BtcConfig.UpdateSkipped.Value : "";
                if (!string.IsNullOrEmpty(skipped) && BtcUi.Action(ref y, w, fs, BtcReplayIcons.Undo, Tt("Offer the skipped version again: ", "Снова предлагать пропущенную версию: ") + skipped, ""))
                {
                    string was = skipped;
                    BtcConfig.UpdateSkipped.Value = "";
                    BtcUndo.Push(Scope, Tt("Skipped version", "Пропущенная версия"), delegate { BtcConfig.UpdateSkipped.Value = was; });
                }
                BtcUi.Note(ref y, w, fs, Tt("Installed: v", "Установлена: v") + BtcPlugin.Version);
                y += fs * 0.5f;
            }
            if (BtcUi.Fold(ref y, w, fs, "gen:apps", Tt("Other programs", "Другие программы"), 1))
            {
                if (BtcVisualData.GetDiscord != null)
                {
                    bool dis = BtcVisualData.GetDiscord();
                    float y0 = y;
                    if (BtcVisualRows.Switch(ref y, w, fs, 0, "Discord Rich Presence", dis, Tt("Shows your match in your Discord status.", "Показывает ваш матч в статусе Discord.")))
                    {
                        BtcVisualData.SetDiscord(!dis);
                        BtcUndo.Push(Scope, "Discord Rich Presence", delegate { BtcVisualData.SetDiscord(dis); });
                    }
                    BtcJump.Row(Scope, "discord", new Rect(fs * 0.5f, y0, w - fs, y - y0));
                }
                y += fs * 0.5f;
            }
            if (BtcUi.Fold(ref y, w, fs, "gen:keys", Tt("Menu keys", "Клавиши меню"), 2))
            {
                if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Keys, Tt("Key that opens this menu", "Клавиша, открывающая это меню"), Tt("Advanced Hotkeys", "Advanced Hotkeys"))) BtcChangelog.Go("tab:3:menu_toggle");
                if (BtcUi.Action(ref y, w, fs, BtcReplayIcons.Keys, Tt("Key of the UI editor", "Клавиша UI редактора"), Tt("Advanced Hotkeys", "Advanced Hotkeys"))) BtcChangelog.Go("tab:3:ui_editor");
                y += fs * 0.5f;
            }
            BtcUi.Note(ref y, w, fs, Tt("UI sounds, menu size, colours and screen layout: UI editor.", "Звуки интерфейса, размер меню, цвета и расположение на экране: UI редактор."));
            _h = y + fs;
            GUI.EndScrollView();
            BtcReplayIcons.DrawTip(fs, area.xMax, false);
        }
    }
}
