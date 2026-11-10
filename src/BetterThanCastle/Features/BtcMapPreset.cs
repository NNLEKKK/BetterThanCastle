using System;
using System.IO;
using Brix.Assets;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Per-map preset of the map symmetry and the map scale: a small text file btc-map.txt in the map's own folder (it travels inside .btcs).
    /// When the host selects a map that has the file, the preset is applied by itself (a notice and Ctrl+Z undo). Maps without the file keep the
    /// current settings. Host only, BetterConnection ON; the values go through the normal symmetry / scale state (nothing new on the wire).
    /// </summary>
    internal static class BtcMapPreset
    {
        internal const string FileName = "btc-map.txt";
        private static string _lastPath;

        internal static bool Auto
        {
            get { return BtcMenuState.Bool("gameplay:map:auto", true); }
            set { BtcMenuState.SetBool("gameplay:map:auto", value); }
        }

        internal static string MapPath
        {
            get { Asset_Map m = GameParam.Map; return m != null ? m.path : null; }
        }

        internal static string MapName
        {
            get
            {
                Asset_Map m = GameParam.Map;
                if (m == null) return null;
                try { return string.IsNullOrEmpty(m.displayName) ? Path.GetFileName(m.path) : m.displayName; }
                catch (Exception) { return Path.GetFileName(m.path); }
            }
        }

        private static string FilePath(string dir) { return Path.Combine(dir, FileName); }

        internal static bool Has()
        {
            string p = MapPath;
            try { return p != null && File.Exists(FilePath(p)); } catch (Exception) { return false; }
        }

        private static bool Read(string dir, out string sym, out string scale)
        {
            sym = null; scale = null;
            try
            {
                string f = FilePath(dir);
                if (!File.Exists(f)) return false;
                string[] lines = File.ReadAllLines(f);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith("sym=")) sym = lines[i].Substring(4).Trim();
                    else if (lines[i].StartsWith("scale=")) scale = lines[i].Substring(6).Trim();
                }
                return sym != null || scale != null;
            }
            catch (Exception ex) { BtcLog.Warn("MapPreset", "read failed: " + ex.Message); return false; }
        }

        /// <summary>Writes the current symmetry and scale into the folder of the selected map.</summary>
        internal static bool Save()
        {
            string p = MapPath;
            if (p == null) return false;
            try
            {
                File.WriteAllText(FilePath(p), "# Better Than Castle map preset: symmetry and scale, applied when the host selects this map\r\nsym=" + BtcSymmetry.StateString() + "\r\nscale=" + BtcMapScale.StateString() + "\r\n", new System.Text.UTF8Encoding(false));
                return true;
            }
            catch (Exception ex) { BtcLog.Warn("MapPreset", "save failed: " + ex.Message); return false; }
        }

        internal static bool Delete()
        {
            string p = MapPath;
            if (p == null) return false;
            try { if (File.Exists(FilePath(p))) File.Delete(FilePath(p)); return true; }
            catch (Exception ex) { BtcLog.Warn("MapPreset", "delete failed: " + ex.Message); return false; }
        }

        /// <summary>Applies the preset of the selected map. Returns false when there is none or it cannot be applied now.</summary>
        internal static bool Apply(bool notice)
        {
            string p = MapPath;
            if (p == null || !BtcBc.BcEnabled || !BtcGameplay.CanEdit) return false;
            string sym, scale;
            if (!Read(p, out sym, out scale)) return false;
            string oldSym = BtcSymmetry.StateString(), oldScale = BtcMapScale.StateString();
            if (sym != null) BtcSymmetry.ApplyState(sym);
            if (scale != null) BtcMapScale.ApplyState(scale);
            if (BtcSymmetry.StateString() == oldSym && BtcMapScale.StateString() == oldScale) return true;
            BtcUndo.Push(BtcGameplayTab.Scope, BtcReplayUI.T("Map preset", "Пресет карты"), delegate { BtcSymmetry.ApplyState(oldSym); BtcMapScale.ApplyState(oldScale); });
            if (notice) BtcHud.Notice(BtcReplayUI.T("Map preset applied: ", "Применён пресет карты: ") + MapName, BtcReplayIcons.Map, BtcHud.Ok);
            BtcLog.Info("MapPreset", "applied for " + p);
            return true;
        }

        /// <summary>Call every frame (BtcGameplay.Tick): applies the preset once when the host picks another map.</summary>
        internal static void Tick()
        {
            try
            {
                if (!BtcBc.BcEnabled || !BtcGameplay.CanEdit) return;
                string p = MapPath;
                if (p == _lastPath) return;
                _lastPath = p;
                if (p != null && Auto) Apply(true);
            }
            catch (Exception ex) { BtcLog.Warn("MapPreset", "tick: " + ex.Message); }
        }
    }
}
