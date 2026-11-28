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
        private static readonly BtcVoidMap.Spec _spec = new BtcVoidMap.Spec
        {
            Folder = "BtcAimTraining", Codename = Codename, Version = 2, Tag = "Aim", Display = "BTC Aim training",
            Description = "Empty map used by the Better Than Castle aim trainer.", Context = 1,
            // home crystal spot: far from the middle of the world, where the floor goes
            Marks = new[] { new UnityEngine.Vector3(120f, 120f, 120f) }, IdBase = "go:b7c0a1e2-0000-4aa0-9d2a-a1m0000000",
        };

        /// <summary>Writes the map when missing or outdated and refreshes the map list. False = could not (caller falls back to the default map).</summary>
        internal static bool Ensure() { return BtcVoidMap.Ensure(_spec); }
    }

    /// <summary>Empty user maps (stock Void heightmap + spawn markers) for the practice modes (aim trainer, micro duel).</summary>
    internal static class BtcVoidMap
    {
        internal sealed class Spec
        {
            internal string Folder, Codename, Tag, Display, Description, IdBase;
            internal int Version, Context;
            internal UnityEngine.Vector3[] Marks;
        }

        internal static bool Ensure(Spec sp)
        {
            try
            {
                string dir = Path.Combine(Path.Combine(Path.Combine(Paths.GameRootPath, "Info"), Path.Combine("UGC", "Maps")), sp.Folder);
                string stamp = Path.Combine(dir, "btc-version.txt");
                bool fresh = File.Exists(stamp) && File.ReadAllText(stamp).Trim() == sp.Version.ToString();
                if (!fresh)
                {
                    string src = Path.Combine(Path.Combine(Path.Combine(Paths.GameRootPath, "Info"), "Maps"), "Void");
                    if (!File.Exists(Path.Combine(src, "Heightmap.png"))) { BtcLog.Warn(sp.Tag, "no Void map to copy"); return false; }
                    Directory.CreateDirectory(dir);
                    File.Copy(Path.Combine(src, "Heightmap.png"), Path.Combine(dir, "Heightmap.png"), true);
                    if (File.Exists(Path.Combine(src, "thumbnail.png"))) File.Copy(Path.Combine(src, "thumbnail.png"), Path.Combine(dir, "thumbnail.png"), true);
                    UTF8Encoding u = new UTF8Encoding(false);
                    File.WriteAllText(Path.Combine(dir, "meta.json"), Meta(sp), u);
                    File.WriteAllText(Path.Combine(dir, "LuaMarkersCount.json"), Markers(sp), u);
                    File.WriteAllText(Path.Combine(dir, "gameobjects.json"), GameObjects(sp), u);
                    File.WriteAllText(Path.Combine(dir, "trees.json"), "[]", u);
                    File.WriteAllText(stamp, sp.Version.ToString(), u);
                    BtcLog.Info(sp.Tag, "empty map written to " + dir);
                }
                if (Brix.Assets.AssetDispensary.maps.SendCodenameQuery(sp.Codename) == null) Brix.Assets.AssetDispensary.maps.RefreshCache();
                bool ok = Brix.Assets.AssetDispensary.maps.SendCodenameQuery(sp.Codename) != null;
                if (!ok) BtcLog.Warn(sp.Tag, "empty map not in the map list after refresh");
                return ok;
            }
            catch (Exception e) { BtcLog.Warn(sp.Tag, "empty map: " + e.Message); return false; }
        }

        private static string Meta(Spec sp)
        {
            return "{\n" +
                   "  \"__CODENAME\": \"" + sp.Codename + "\",\n" +
                   "  \"__DISPLAYNAME\": \"" + sp.Display + "\",\n" +
                   "  \"__CUSTOMNAME\": \"\",\n" +
                   "  \"__DESCRIPTION\": \"" + sp.Description + "\",\n" +
                   "  \"__MAPTYPE\": \"Map\",\n" +
                   "  \"__SAVETYPE\": \"Map\",\n" +
                   "  \"__ASSETVERSION\": 25,\n" +
                   "  \"__MAPSIZE\": \"Medium\",\n" +
                   "  \"__SUPPORTED_GAMEMODES\": {\n    \"$type\": \"System.String[], mscorlib\",\n    \"$values\": []\n  },\n" +
                   "  \"__LASTPLAYED_GAMEMODE\": \"\",\n" +
                   "  \"__SPAWNCOUNT\": " + sp.Marks.Length + ",\n" +
                   "  \"__PLAYABLECONTEXT\": " + sp.Context + ",\n" +
                   "  \"__RATING\": 0.5,\n" +
                   "  \"__BIOMETYPE\": \"Green\"\n" +
                   "}\n";
        }

        private static string Vec(UnityEngine.Vector3 m)
        {
            return "{\n      \"$type\": \"UnityEngine.Vector3, UnityEngine\",\n      \"x\": " + F(m.x) + ",\n      \"y\": " + F(m.y) + ",\n      \"z\": " + F(m.z) + "\n    }";
        }

        private static string Markers(Spec sp)
        {
            string q = "{\n      \"$type\": \"UnityEngine.Quaternion, UnityEngine\",\n      \"w\": 1.0\n    }";
            StringBuilder s = new StringBuilder("[\n");
            for (int i = 0; i < sp.Marks.Length; i++)
            {
                string v = Vec(sp.Marks[i]);
                if (i > 0) s.Append(",\n");
                s.Append("  {\n    \"markerTag\": \"unset\",\n    \"markerGroups\": {\n      \"$type\": \"System.Collections.Generic.List`1[[System.String, mscorlib]], mscorlib\",\n")
                 .Append("      \"$values\": [\n        \"Player Starting Point\"\n      ]\n    },\n")
                 .Append("    \"position\": ").Append(v).Append(",\n    \"localPosition\": ").Append(v).Append(",\n    \"rotation\": ").Append(q).Append(",\n    \"localRotation\": ").Append(q).Append("\n  }");
            }
            return s.Append("\n]\n").ToString();
        }

        /// <summary>One world editor marker per spawn in group "Player Starting Point" (same shape as the stock maps write it).</summary>
        private static string GameObjects(Spec sp)
        {
            StringBuilder s = new StringBuilder("[\n");
            for (int i = 0; i < sp.Marks.Length; i++)
            {
                UnityEngine.Vector3 m = sp.Marks[i];
                string id = sp.IdBase + (i * 2 + 1).ToString("00"), beacon = sp.IdBase + (i * 2 + 2).ToString("00");
                string nm = "Crystal - Player Spawn ( " + (i + 1) + " )";
                int x = (int)m.x, y = (int)m.y, z = (int)m.z;
                if (i > 0) s.Append(",\n");
                s.Append("  {\n    \"$id\": \"").Append(id).Append("\",\n    \"$type\": \"UnityEngine.GameObject, UnityEngine\",\n    \"$assetKey\": \"Balises.WorldEditorMarker\",\n");
                s.Append("    \"$path\": \"/\",\n    \"name\": \"WorldEditorMarker (").Append(i).Append(")\",\n    \"components\": [\n");
                s.Append("      {\n        \"$type\": \"UnityEngine.Transform, UnityEngine\",\n        \"position\": {\n          \"$type\": \"UnityEngine.Vector3, UnityEngine\",\n");
                s.Append("          \"x\": ").Append(F(m.x)).Append(",\n          \"y\": ").Append(F(m.y)).Append(",\n          \"z\": ").Append(F(m.z)).Append("\n        }\n      },\n");
                s.Append("      {\n        \"$type\": \"Brix.Game.Components.Blueprint, Assembly-CSharp-firstpass\",\n        \"_isAnti\": false,\n        \"commited\": false\n      },\n");
                s.Append("      {\n        \"$type\": \"Brix.Game.Beacons.BeaconDriver, Assembly-CSharp-firstpass\",\n        \"nameOverride\": \"").Append(nm).Append("\",\n        \"Beacon\": {\n");
                s.Append("          \"$component\": \"Brix.Legacy.Beacon, Assembly-CSharp-firstpass\",\n          \"$id\": \"").Append(beacon).Append("\",\n");
                s.Append("          \"$type\": \"UnityEngine.GameObject, UnityEngine\",\n          \"$assetKey\": \"Balises.MarkerBeacon\",\n          \"$parent\": \"").Append(id).Append("\",\n");
                s.Append("          \"$path\": \"/").Append(id).Append("\",\n          \"name\": \"").Append(nm).Append("\",\n          \"components\": [\n");
                s.Append("            {\n              \"$type\": \"Brix.Legacy.Beacon, Assembly-CSharp-firstpass\",\n              \"VolumeComponent\": null,\n              \"radius\": 0.0,\n");
                s.Append("              \"position\": {\n                \"$type\": \"Brix.Engine.XYZ, Brix.Core\",\n                \"x\": ").Append(x).Append(",\n                \"y\": ").Append(y).Append(",\n                \"z\": ").Append(z).Append("\n              }\n            }\n          ]\n        }\n      },\n");
                s.Append("      {\n        \"$type\": \"Brix.Lua.LuaMarker, Assembly-CSharp-firstpass\",\n        \"markerTag\": \"unset\",\n        \"markerGroups\": [\n          \"Player Starting Point\"\n        ],\n");
                s.Append("        \"DisplayName\": \"").Append(nm).Append("\",\n        \"Placed\": true\n      }\n    ]\n  }");
            }
            for (int i = 0; i < sp.Marks.Length; i++) s.Append(",\n  {\n    \"$ref\": \"").Append(sp.IdBase + (i * 2 + 2).ToString("00")).Append("\"\n  }");
            return s.Append("\n]\n").ToString();
        }

        private static string F(float f) { return f.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture); }
    }
}
