using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>Facade over the private pack/hat/dump state of ResourcePackManager for the Packs tab (UI/Packs/BtcPacksTab.cs).</summary>
    public partial class ResourcePackManager
    {
        internal static bool PacksReady { get { return instance != null; } }
        internal static string PackRoot { get { return RootDir; } }
        internal static string PackCurrent() { return instance == null ? null : instance.currentPack; }

        /// <summary>Pack folder names (includes the "Vanilla" folder); the default textures are not in the list.</summary>
        internal static List<string> PackNames()
        {
            if (instance != null && instance.menuPacks != null) return instance.menuPacks;
            return ListPacks(true);
        }

        internal static void PackRescan()
        {
            if (instance != null) instance.menuPacks = ListPacks(true);
        }

        /// <summary>Selects a pack (null = default game textures) and shows the in-game message.</summary>
        internal static void PackSet(string name)
        {
            if (instance == null) return;
            if (string.Equals(name, instance.currentPack, StringComparison.OrdinalIgnoreCase)) return;
            instance.SetPack(name, true);
        }

        internal static int PackFileCount()
        {
            if (instance == null || instance.index == null) return 0;
            List<string> files = IndexFiles(instance.index);
            int n = 0;
            for (int i = 0; i < files.Count; i++) if (!IsThumb(files[i])) n++;
            return n;
        }

        /// <summary>thumbnail.png in a pack folder is the menu preview, not a texture.</summary>
        private static bool IsThumb(string file) { return string.Equals(Path.GetFileNameWithoutExtension(file), "thumbnail", StringComparison.OrdinalIgnoreCase); }

        internal static int PackApplied()
        {
            if (instance == null) return 0;
            int n = 0;
            foreach (KeyValuePair<int, Entry> kv in instance.entries) if (kv.Value.applied) n++;
            return n;
        }

        /// <summary>Files of the current pack that matched no game texture (names without extension, sorted).</summary>
        internal static List<string> PackUnmatched()
        {
            List<string> res = new List<string>();
            if (instance == null || instance.index == null) return res;
            List<string> files = IndexFiles(instance.index);
            for (int i = 0; i < files.Count; i++)
                if (!IsThumb(files[i]) && !instance.matchedFiles.Contains(files[i])) res.Add(Path.GetFileNameWithoutExtension(files[i]));
            res.Sort(StringComparer.OrdinalIgnoreCase);
            return res;
        }

        // ---- Halloween hats
        internal static int HatCount { get { return HatOrder.Count; } }
        internal static string HatLabel(int i) { return HatOrder[i].label; }
        internal static string HatHandle(int i) { return HatOrder[i].handle; }
        internal static bool HatOn(int i) { return instance != null && !instance.hatsOff.Contains(HatOrder[i].handle); }

        internal static void UiHatSet(string handle, bool on)
        {
            if (instance == null) return;
            instance.SetTypeHat(handle, on);
            instance.SaveHatSettings();
        }

        // ---- dumps (modder tools)
        internal static void UiDumpTextures() { if (instance != null) instance.dbgDumpTexPending = true; }
        internal static void UiDumpBuildings() { if (instance != null) instance.dbgDumpBldPending = true; }
        internal static string DumpDir { get { return Path.Combine(BtcPaths.Debug, "dump"); } }
    }
}
