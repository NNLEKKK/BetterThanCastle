using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>Hold the "visual_compare" hotkey (Hotkeys tab, View) to see the vanilla look: the colored-HP master switch and the world overlays go off while the key is held and come back on release.</summary>
    internal static class BtcVisualCompare
    {
        private static bool _on, _hpWas, _ovWas;
        private static readonly KeyCode[] NoKeys = new KeyCode[0];

        internal static void Tick()
        {
            bool held = false;
            try
            {
                if (BtcVisualData.GetMaster != null && BtcVisualData.Ready != null && BtcVisualData.Ready())
                {
                    KeyCode[] k = BtcHotkeys.GetKeys("visual_compare", NoKeys);
                    held = k != null && k.Length > 0 && BtcHotkeys.AllHeld(k);
                }
            }
            catch (System.Exception) { }
            if (held == _on) return;
            _on = held;
            if (held)
            {
                _hpWas = BtcVisualData.GetMaster(); _ovWas = BtcOverlays.MasterOn;
                BtcVisualData.SetMaster(false);
                BtcOverlays.MasterOn = false;
            }
            else
            {
                BtcVisualData.SetMaster(_hpWas);
                BtcOverlays.MasterOn = _ovWas;
            }
        }
    }
}
