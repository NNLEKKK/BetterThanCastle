using System;
using Brix.Utils.UI;
using HarmonyLib;
using Rewired;

namespace BetterThanCastle.Patches
{
    /// <summary>
    /// The game registers a key-binding action only while its menu is active. Wrapping the registered delegate tells BtcApm that a bound action really ran
    /// (a key press without any such effect is not an APM action). Local only, no wire traffic.
    /// </summary>
    [HarmonyPatch(typeof(KeyBindingsUtility), "RegisterAction")]
    internal static class KeyBindingsRegisterPatch
    {
        private static void Prefix(ref Action<InputActionEventData> action)
        {
            try
            {
                Action<InputActionEventData> inner = action;
                if (inner == null) return;
                action = delegate (InputActionEventData d) { BtcApm.NoteEffect(); inner(d); };
            }
            catch (Exception e) { BtcLog.Error("Hook", "RegisterAction wrap", e); }
        }
    }
}
