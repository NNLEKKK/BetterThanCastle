using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// Draws the mod menu from its own OnGUI, separate from ResourcePackManager's overlays (timer Ready panel, HUD bits,
    /// numbers). Separate script = own IMGUI control ID sequence, so overlays that appear only in a match can no longer shift
    /// the ids of the menu's text fields (typing into a search field was lost in game). GUI.depth -1500 puts it in front of
    /// every mod overlay (BtcHud -1100, launcher -1200): it gets each event first and ResourcePackManager.MenuGUI swallows
    /// mouse/keyboard events while the full-screen menu is open. Updater (-2000) and replay loading (-10000) stay in front.
    /// </summary>
    internal sealed class BtcMenuHost : MonoBehaviour
    {
        private void Awake() { useGUILayout = false; }

        private void OnGUI()
        {
            if (!ResourcePackManager.MenuIsOpen) return;
            GUI.depth = -1500;
            try { ResourcePackManager.HostMenuGUI(); }
            catch (System.Exception ex) { BtcLog.Error("Menu", "menu draw failed: " + ex.Message); }
        }
    }
}
