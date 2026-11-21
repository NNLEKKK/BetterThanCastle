#if DEV
using System;
using System.Diagnostics;
using System.Text;
using Brix.Game.AI;
using Brix.Game.AI.Nodes;
using Brix.Game.Components;
using Brix.Game.Semantique;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    // Diagnostic: who asks a worker to pick something up (stack of the call that builds the GoPickUp node).
    [HarmonyPatch(typeof(PickUpNode), "GoPickUp")]
    internal static class BtcDevPickTracePatch
    {
        private static int _n;

        private static void Prefix(Labor labor, GameComponent ressource)
        {
            if (_n++ > 300) return;
            try
            {
                StackTrace st = new StackTrace(2, false);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < st.FrameCount && i < 10; i++)
                {
                    var m = st.GetFrame(i).GetMethod();
                    if (m == null) continue;
                    sb.Append(m.DeclaringType != null ? m.DeclaringType.Name : "?").Append('.').Append(m.Name).Append(" < ");
                }
                BtcLog.Info("PickTrace", (labor != null ? labor.name : "null") + " -> " + (ressource != null ? ressource.name : "null") + " | " + sb);
            }
            catch (Exception) { }
        }
    }

    [HarmonyPatch(typeof(Transaction), "Take")]
    internal static class BtcDevTakeTracePatch
    {
        private static int _n;

        private static void Prefix(Transaction __instance, GameObject quoiObj)
        {
            if (_n++ > 300) return;
            try
            {
                StackTrace st = new StackTrace(2, false);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < st.FrameCount && i < 14; i++)
                {
                    var m = st.GetFrame(i).GetMethod();
                    if (m == null) continue;
                    sb.Append(m.DeclaringType != null ? m.DeclaringType.Name : "?").Append('.').Append(m.Name).Append(" < ");
                }
                BtcLog.Info("TakeTrace", __instance.name + " takes " + (quoiObj != null ? quoiObj.name : "null") + " | " + sb);
            }
            catch (Exception) { }
        }
    }

    [HarmonyPatch(typeof(Recepteur), "AddObject", new Type[] { typeof(GameObject), typeof(bool) })]
    internal static class BtcDevAddTracePatch
    {
        private static int _n;

        private static void Prefix(Recepteur __instance, GameObject item)
        {
            if (_n++ > 300) return;
            try
            {
                StackTrace st = new StackTrace(2, false);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < st.FrameCount && i < 14; i++)
                {
                    var m = st.GetFrame(i).GetMethod();
                    if (m == null) continue;
                    sb.Append(m.DeclaringType != null ? m.DeclaringType.Name : "?").Append('.').Append(m.Name).Append(" < ");
                }
                BtcLog.Info("AddTrace", __instance.name + " gets " + (item != null ? item.name : "null") + " | " + sb);
            }
            catch (Exception) { }
        }
    }
}
#endif
