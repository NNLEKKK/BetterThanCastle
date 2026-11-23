using System;
using System.Collections.Generic;
using Brix.Assets;
using Brix.UI.Builder.Menu;

namespace BetterThanCastle
{
    /// <summary>
    /// Map and game mode the host's new lobby starts with (vanilla picks the best-rated official map and its default mode).
    /// Local host choice, no net: the lobby then syncs the selection the vanilla way. Stored in BtcMenuState
    /// (lobby:map = map codename, lobby:mode = gamemode codename; empty = like vanilla). Applied from LobbyMenu.OpenSelf postfix.
    /// </summary>
    internal static class BtcLobbyDefaults
    {
        internal const string DefMap = "viper_pass_b7ef299e-a014-48b1-984f-4e645c943e15"; // Pioneer Pass
        internal const string DefMode = "conquest";
        private const AssetPlayableContext Ctx = AssetPlayableContext.Multiplayer;

        internal static string Map { get { return BtcMenuState.Get("lobby:map", DefMap); } set { BtcMenuState.Set("lobby:map", value ?? ""); } }
        internal static string Mode { get { return BtcMenuState.Get("lobby:mode", DefMode); } set { BtcMenuState.Set("lobby:mode", value ?? ""); } }

        /// <summary>Maps a lobby can start on (official + own + workshop), sorted by name.</summary>
        internal static List<Asset_Map> Maps()
        {
            List<Asset_Map> r = new List<Asset_Map>();
            try
            {
                List<Asset_Map> all = AssetDispensary.maps.SendQuery(AssetQueryParameters.AllSPSources, Ctx, false);
                for (int i = 0; i < all.Count; i++) if (all[i] != null && all[i].IsCurrentVersion()) r.Add(all[i]);
                r.Sort(delegate(Asset_Map a, Asset_Map b) { return string.Compare(Name(a), Name(b), StringComparison.OrdinalIgnoreCase); });
            }
            catch (Exception ex) { BtcLog.Warn("LobbyDef", "maps: " + ex.Message); }
            return r;
        }

        /// <summary>Game modes of a map in multiplayer (null map = none).</summary>
        internal static List<Asset_Gamemode> Modes(Asset_Map m)
        {
            try { if (m != null) { List<Asset_Gamemode> g = m.GetGamemodes(Ctx); if (g != null) return g; } }
            catch (Exception ex) { BtcLog.Warn("LobbyDef", "modes: " + ex.Message); }
            return new List<Asset_Gamemode>();
        }

        internal static Asset_Map FindMap(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            try { return AssetDispensary.maps.SendCodenameQuery(code); }
            catch (Exception) { return null; }
        }

        internal static Asset_Gamemode FindMode(Asset_Map m, string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            List<Asset_Gamemode> g = Modes(m);
            for (int i = 0; i < g.Count; i++) if (g[i] != null && g[i].codename == code) return g[i];
            return null;
        }

        internal static string Name(Asset_Map m)
        {
            if (m == null) return "";
            try { return string.IsNullOrEmpty(m.displayName) ? m.codename : m.displayName; }
            catch (Exception) { return m.codename; }
        }

        internal static string Name(Asset_Gamemode g)
        {
            if (g == null) return "";
            try { return string.IsNullOrEmpty(g.displayName) ? g.codename : g.displayName; }
            catch (Exception) { return g.codename; }
        }

        /// <summary>LobbyMenu.OpenSelf postfix (host only): replaces vanilla's pick with the saved map and mode.</summary>
        internal static void OnLobbyOpen()
        {
            if (!LobbyMenu.IsHost) return;
            Asset_Map map = FindMap(Map);
            if (map == null && !string.IsNullOrEmpty(Map)) BtcLog.Warn("LobbyDef", "map not found: " + Map);
            if (map != null)
            {
                BaseMapLibraryMenu.SetSelectedMap(map, Ctx, BaseAssetMenu.MenuGameContext.Normal, BaseAssetMenu.MenuTypeContext.New);
            }
            Asset_Gamemode mode = FindMode(GameParam.SelectedMap, Mode);
            if (mode != null && mode != GameParam.SelectedGamemode) BaseMapLibraryMenu.SetSelectedGamemode(GameParam.SelectedMap, mode, Ctx);
            if (map == null && mode == null) return;
            BaseMapLibraryMenu.SetSelectedAsLoaded(false);
            BtcLog.Info("LobbyDef", "lobby start map=" + (GameParam.Map != null ? GameParam.Map.codename : "null") + " mode=" + (GameParam.Gamemode != null ? GameParam.Gamemode.codename : "null"));
        }
    }
}
