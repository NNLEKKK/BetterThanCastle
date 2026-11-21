using System;
using System.IO;
using System.Text;
using BepInEx;

namespace BetterThanCastle
{
    /// <summary>
    /// The aim trainer's own empty map: written once into the user map folder (Info/UGC/Maps/BtcAimTraining) from the stock Void map
    /// (empty terrain, nothing else) plus the one "Player Starting Point" marker sandbox needs. Loads far faster than a real map.
    /// Game files are only read (Void heightmap/thumbnail); the new folder is user content like any custom map.
    /// </summary>
    internal static class BtcAimMap
    {
        internal const string Codename = "BtcAimTraining_3b9d1c52-6f0e-4a7e-9d2a-aim000000001";
        private const int Version = 2;
        /// <summary>Home crystal spot: far from the middle of the world, where the floor goes.</summary>
        private const float MarkX = 120f, MarkY = 120f, MarkZ = 120f;

        private static string Dir { get { return Path.Combine(Path.Combine(Path.Combine(Paths.GameRootPath, "Info"), Path.Combine("UGC", "Maps")), "BtcAimTraining"); } }

        /// <summary>Writes the map when missing or outdated and refreshes the map list. False = could not (caller falls back to the default map).</summary>
        internal static bool Ensure()
        {
            try
            {
                string dir = Dir, stamp = Path.Combine(dir, "btc-version.txt");
                bool fresh = File.Exists(stamp) && File.ReadAllText(stamp).Trim() == Version.ToString();
                if (!fresh)
                {
                    string src = Path.Combine(Path.Combine(Path.Combine(Paths.GameRootPath, "Info"), "Maps"), "Void");
                    if (!File.Exists(Path.Combine(src, "Heightmap.png"))) { BtcLog.Warn("Aim", "no Void map to copy"); return false; }
                    Directory.CreateDirectory(dir);
                    File.Copy(Path.Combine(src, "Heightmap.png"), Path.Combine(dir, "Heightmap.png"), true);
                    if (File.Exists(Path.Combine(src, "thumbnail.png"))) File.Copy(Path.Combine(src, "thumbnail.png"), Path.Combine(dir, "thumbnail.png"), true);
                    UTF8Encoding u = new UTF8Encoding(false);
                    File.WriteAllText(Path.Combine(dir, "meta.json"), Meta(), u);
                    File.WriteAllText(Path.Combine(dir, "LuaMarkersCount.json"), Markers(), u);
                    File.WriteAllText(Path.Combine(dir, "gameobjects.json"), GameObjects(), u);
                    File.WriteAllText(Path.Combine(dir, "trees.json"), "[]", u);
                    File.WriteAllText(stamp, Version.ToString(), u);
                    BtcLog.Info("Aim", "empty map written to " + dir);
                }
                if (Brix.Assets.AssetDispensary.maps.SendCodenameQuery(Codename) == null) Brix.Assets.AssetDispensary.maps.RefreshCache();
                bool ok = Brix.Assets.AssetDispensary.maps.SendCodenameQuery(Codename) != null;
                if (!ok) BtcLog.Warn("Aim", "empty map not in the map list after refresh");
                return ok;
            }
            catch (Exception e) { BtcLog.Warn("Aim", "empty map: " + e.Message); return false; }
        }

        private static string Meta()
        {
            return "{\n" +
                   "  \"__CODENAME\": \"" + Codename + "\",\n" +
                   "  \"__DISPLAYNAME\": \"BTC Aim training\",\n" +
                   "  \"__CUSTOMNAME\": \"\",\n" +
                   "  \"__DESCRIPTION\": \"Empty map used by the Better Than Castle aim trainer.\",\n" +
                   "  \"__MAPTYPE\": \"Map\",\n" +
                   "  \"__SAVETYPE\": \"Map\",\n" +
                   "  \"__ASSETVERSION\": 25,\n" +
                   "  \"__MAPSIZE\": \"Medium\",\n" +
                   "  \"__SUPPORTED_GAMEMODES\": {\n    \"$type\": \"System.String[], mscorlib\",\n    \"$values\": []\n  },\n" +
                   "  \"__LASTPLAYED_GAMEMODE\": \"\",\n" +
                   "  \"__SPAWNCOUNT\": 1,\n" +
                   "  \"__PLAYABLECONTEXT\": 1,\n" +
                   "  \"__RATING\": 0.5,\n" +
                   "  \"__BIOMETYPE\": \"Green\"\n" +
                   "}\n";
        }

        private static string Markers()
        {
            string v = "{\n      \"$type\": \"UnityEngine.Vector3, UnityEngine\",\n      \"x\": " + F(MarkX) + ",\n      \"y\": " + F(MarkY) + ",\n      \"z\": " + F(MarkZ) + "\n    }";
            string q = "{\n      \"$type\": \"UnityEngine.Quaternion, UnityEngine\",\n      \"w\": 1.0\n    }";
            return "[\n  {\n    \"markerTag\": \"unset\",\n    \"markerGroups\": {\n      \"$type\": \"System.Collections.Generic.List`1[[System.String, mscorlib]], mscorlib\",\n" +
                   "      \"$values\": [\n        \"Player Starting Point\"\n      ]\n    },\n" +
                   "    \"position\": " + v + ",\n    \"localPosition\": " + v + ",\n    \"rotation\": " + q + ",\n    \"localRotation\": " + q + "\n  }\n]\n";
        }

        /// <summary>One world editor marker in group "Player Starting Point" (same shape as the stock maps write it).</summary>
        private static string GameObjects()
        {
            const string id = "go:b7c0a1e2-0000-4aa0-9d2a-a1m000000001", beacon = "go:b7c0a1e2-0000-4aa0-9d2a-a1m000000002";
            int x = (int)MarkX, y = (int)MarkY, z = (int)MarkZ;
            StringBuilder s = new StringBuilder();
            s.Append("[\n  {\n    \"$id\": \"").Append(id).Append("\",\n    \"$type\": \"UnityEngine.GameObject, UnityEngine\",\n    \"$assetKey\": \"Balises.WorldEditorMarker\",\n");
            s.Append("    \"$path\": \"/\",\n    \"name\": \"WorldEditorMarker (0)\",\n    \"components\": [\n");
            s.Append("      {\n        \"$type\": \"UnityEngine.Transform, UnityEngine\",\n        \"position\": {\n          \"$type\": \"UnityEngine.Vector3, UnityEngine\",\n");
            s.Append("          \"x\": ").Append(F(MarkX)).Append(",\n          \"y\": ").Append(F(MarkY)).Append(",\n          \"z\": ").Append(F(MarkZ)).Append("\n        }\n      },\n");
            s.Append("      {\n        \"$type\": \"Brix.Game.Components.Blueprint, Assembly-CSharp-firstpass\",\n        \"_isAnti\": false,\n        \"commited\": false\n      },\n");
            s.Append("      {\n        \"$type\": \"Brix.Game.Beacons.BeaconDriver, Assembly-CSharp-firstpass\",\n        \"nameOverride\": \"Crystal - Player Spawn ( 1 )\",\n        \"Beacon\": {\n");
            s.Append("          \"$component\": \"Brix.Legacy.Beacon, Assembly-CSharp-firstpass\",\n          \"$id\": \"").Append(beacon).Append("\",\n");
            s.Append("          \"$type\": \"UnityEngine.GameObject, UnityEngine\",\n          \"$assetKey\": \"Balises.MarkerBeacon\",\n          \"$parent\": \"").Append(id).Append("\",\n");
            s.Append("          \"$path\": \"/").Append(id).Append("\",\n          \"name\": \"Crystal - Player Spawn ( 1 )\",\n          \"components\": [\n");
            s.Append("            {\n              \"$type\": \"Brix.Legacy.Beacon, Assembly-CSharp-firstpass\",\n              \"VolumeComponent\": null,\n              \"radius\": 0.0,\n");
            s.Append("              \"position\": {\n                \"$type\": \"Brix.Engine.XYZ, Brix.Core\",\n                \"x\": ").Append(x).Append(",\n                \"y\": ").Append(y).Append(",\n                \"z\": ").Append(z).Append("\n              }\n            }\n          ]\n        }\n      },\n");
            s.Append("      {\n        \"$type\": \"Brix.Lua.LuaMarker, Assembly-CSharp-firstpass\",\n        \"markerTag\": \"unset\",\n        \"markerGroups\": [\n          \"Player Starting Point\"\n        ],\n");
            s.Append("        \"DisplayName\": \"Crystal - Player Spawn ( 1 )\",\n        \"Placed\": true\n      }\n    ]\n  },\n  {\n    \"$ref\": \"").Append(beacon).Append("\"\n  }\n]\n");
            return s.ToString();
        }

        private static string F(float f) { return f.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture); }
    }
}
