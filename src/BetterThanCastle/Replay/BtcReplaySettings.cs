using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>Recording / playback settings of the Replay tab (the panel under the gear), in the new switch-row style (BtcUi).</summary>
    internal static class BtcReplaySettings
    {
        private static string Tt(string en, string ru) { return BtcReplayUI.T(en, ru); }

        internal static float Draw(Event e, float y, float w, int fs)
        {
            if (BtcUi.Fold(ref y, w, fs, "replay:rec", Tt("Recording", "Запись"), 0)) {
            BtcUi.Note(ref y, w, fs, Tt("Records the full network stream the host sends to clients (everything a spectator sees: spawns, movement, animations) into a compressed file. During the game recording only copies packets into a buffer, compression and disk writes run on a background thread. Replays are stored in BetterThanCastle/Replays.", "Записывает полный сетевой поток, который хост отправляет клиентам (всё, что видит зритель: спавн, движение, анимации), в сжатый файл. Во время игры запись лишь копирует пакеты в буфер, сжатие и запись на диск идут в фоновом потоке. Реплеи лежат в BetterThanCastle/Replays."));
            BtcUi.ToggleUndo(ref y, w, fs, "replay", Tt("Record replays automatically at match start (when you host)", "Автозапись реплея при старте матча (когда вы хост)"), BtcReplay.GetRecordEnabled(), null, v => BtcReplay.SetRecordEnabled(v));
            BtcUi.ToggleUndo(ref y, w, fs, "replay", Tt("Also record singleplayer games", "Записывать и одиночные игры"), BtcReplay.GetRecordSingleplayer(), null, v => BtcReplay.SetRecordSingleplayer(v));
            BtcUi.ToggleUndo(ref y, w, fs, "replay", Tt("Show REC indicator with timer and size", "Показывать индикатор REC с таймером и размером"), BtcReplay.GetShowIndicator(), null, v => BtcReplay.SetShowIndicator(v));
            BtcUi.ToggleUndo(ref y, w, fs, "replay", Tt("Download the replay from the host after a match (when you are a client)", "Загружать реплей у хоста после матча (когда вы клиент)"), BtcConfig.ReplayDownloadFromHost.Value, null, v => BtcConfig.ReplayDownloadFromHost.Value = v);
            BtcUi.ToggleUndo(ref y, w, fs, "replay", Tt("Send the replay to all clients after a match (when you host)", "Отправлять реплей всем клиентам после матча (когда вы хост)"), BtcConfig.ReplayAutoShare.Value, null, v => BtcConfig.ReplayAutoShare.Value = v);
            }
            y += fs * 0.5f;
            if (BtcUi.Fold(ref y, w, fs, "replay:play", Tt("Playback", "Просмотр"), 0)) {
            BtcUi.ToggleUndo(ref y, w, fs, "replay", Tt("Auto-skip pauses and host freezes when playing a replay", "Автоматически перематывать паузы и фризы хоста в реплее"), BtcReplayKeys.SkipPauses, null, v => BtcReplayKeys.SkipPauses = v);
            }
            y += fs * 0.3f;
            if (BtcReplay.Mode == BtcReplay.State.Recording)
                BtcUi.Status(ref y, w, fs, "REC  " + BtcReplayUI.Clock(BtcReplay.RecSeconds) + "  -  " + BtcReplayUI.Size(BtcReplay.RecBytes) + "  -  " + BtcReplay.RecKeyframes + " " + Tt("keyframes", "ключ. кадров") + "  -  " + BtcReplay.RecFile, BtcReplayUI.Red);
            else BtcUi.Status(ref y, w, fs, Tt("Not recording.", "Запись не идёт."), new Color(1f, 1f, 1f, 0.6f));
            if (!string.IsNullOrEmpty(BtcReplay.Message) && Time.unscaledTime < BtcReplay.MessageUntil) BtcUi.Note(ref y, w, fs, BtcReplay.Message);
            y += fs * 0.5f;
            return y;
        }
    }
}
