using System;
using System.Text;
using BepInEx.Configuration;

namespace BetterThanCastle
{
    internal enum BtcDuelUnit { Worker = 0, BarrelWorker, Knight, Archer, Halberdier, Arbalist, Alchemist, Artificer }

    internal enum BtcDuelDifficulty { Easy = 0, Medium, Hard, Extreme, Chosen, Custom, BreaktronZero }

    /// <summary>Micro-duel settings (menu tab "Duel"). Changes apply at the start of the next match (rematch reads them).</summary>
    internal static class BtcDuelConfig
    {
        internal static readonly int UnitKinds = Enum.GetValues(typeof(BtcDuelUnit)).Length;
        internal static readonly string[] UnitNames = { "Worker", "Barrel worker", "Knight", "Archer", "Halberdier", "Arbalist", "Alchemist", "Artificer" };

        internal static int[] PlayerCount = new int[UnitKinds];
        internal static int[] BotCount = new int[UnitKinds];
        internal static BtcDuelDifficulty Difficulty = BtcDuelDifficulty.Chosen;
        internal static bool SkipAnims = true;
        internal static bool InfiniteHeal = false;
        /// <summary>1 = 100x20 arena, 3 = 300x60.</summary>
        internal static int ArenaScale = 1;

        private static ConfigEntry<string> _cPlayer, _cBot, _cDiff;
        private static ConfigEntry<bool> _cSkip, _cHeal;
        private static ConfigEntry<int> _cArena;

        internal static void Init(ConfigFile cfg)
        {
            _cPlayer = cfg.Bind("Duel", "PlayerUnits", "0,0,0,0,5,0,0,0", "Player start units, order: Worker,BarrelWorker,Knight,Archer,Halberdier,Arbalist,Alchemist,Artificer.");
            _cBot = cfg.Bind("Duel", "BotUnits", "0,0,0,0,5,0,0,0", "Bot start units (same order).");
            _cDiff = cfg.Bind("Duel", "Difficulty", "Chosen", "Easy, Medium, Hard, Extreme, Chosen, Custom, BreaktronZero (B0; old names LuminaZero/LuminaAI are read as BreaktronZero).");
            _cSkip = cfg.Bind("Duel", "SkipAnims", true, "ON: the vanilla animation-skip trick (move-attack right before a hit) works. OFF: every melee unit keeps the full strike cycle.");
            _cHeal = cfg.Bind("Duel", "InfiniteHeal", false, "ON: every duel unit heals 10 HP/s all the time.");
            _cArena = cfg.Bind("Duel", "ArenaScale", 1, "1 = 100x20 arena, 3 = three times larger (300x60).");
            ArenaScale = _cArena.Value == 3 ? 3 : 1;
            Parse(_cPlayer.Value, PlayerCount);
            Parse(_cBot.Value, BotCount);
            try { string dv = _cDiff.Value == "LuminaAI" || _cDiff.Value == "LuminaZero" ? "BreaktronZero" : _cDiff.Value; Difficulty = (BtcDuelDifficulty)Enum.Parse(typeof(BtcDuelDifficulty), dv, true); } catch (Exception) { Difficulty = BtcDuelDifficulty.Chosen; }
            SkipAnims = _cSkip.Value;
            InfiniteHeal = _cHeal.Value;
        }

        internal static void Save()
        {
            if (_cPlayer == null) return;
            _cPlayer.Value = Join(PlayerCount);
            _cBot.Value = Join(BotCount);
            _cDiff.Value = Difficulty.ToString();
            _cSkip.Value = SkipAnims;
            _cHeal.Value = InfiniteHeal;
            _cArena.Value = ArenaScale;
        }

        internal static int Total(int[] c) { int n = 0; for (int i = 0; i < c.Length; i++) n += c[i]; return n; }

        private static string Join(int[] c)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < c.Length; i++) { if (i > 0) sb.Append(','); sb.Append(c[i]); }
            return sb.ToString();
        }

        private static void Parse(string s, int[] into)
        {
            try
            {
                string[] p = (s ?? "").Split(',');
                for (int i = 0; i < into.Length && i < p.Length; i++)
                {
                    int v; if (int.TryParse(p[i].Trim(), out v)) into[i] = Math.Max(0, Math.Min(60, v));
                }
            }
            catch (Exception) { }
        }
    }
}
