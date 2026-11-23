#if DEV
using System;
using Brix.Game.AI;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Dev
{
    /// <summary>DEV: timestamps of the order pipeline of every unit while "dueltrace 1" is on: OL do / cancel / path / follow (agent log).</summary>
    internal static class BtcDevOrderLag
    {
        private sealed class Watch { public Transform T; public Vector3 P; public float At; public string Task; }
        private static readonly System.Collections.Generic.Dictionary<int, Watch> Moves = new System.Collections.Generic.Dictionary<int, Watch>();
        private static BtcDevOrderLagRunner _runner;

        /// <summary>Per frame: a unit that got an order is logged once it moved 0.3 m from where it stood ("moved dt=").</summary>
        internal static void Tick()
        {
            if (Moves.Count == 0) return;
            System.Collections.Generic.List<int> done = null;
            foreach (System.Collections.Generic.KeyValuePair<int, Watch> kv in Moves)
            {
                Watch w = kv.Value;
                if (w.T == null) { (done ?? (done = new System.Collections.Generic.List<int>())).Add(kv.Key); continue; }
                float dt = Time.time - w.At;
                if ((w.T.position - w.P).sqrMagnitude > 0.09f || dt > 6f)
                {
                    BtcLog.Info("OrderLag", (dt > 6f ? "STUCK" : "moved") + " u" + kv.Key + " dt=" + dt.ToString("0.00") + " was=" + w.Task);
                    (done ?? (done = new System.Collections.Generic.List<int>())).Add(kv.Key);
                }
            }
            if (done != null) for (int i = 0; i < done.Count; i++) Moves.Remove(done[i]);
        }

        internal static void Mark(string what, Component c)
        {
            if (!BtcDevDuelTrace.On || c == null) return;
            if (what.StartsWith("do "))
            {
                BaseLabor bl = c as BaseLabor;
                Moves[c.gameObject.GetInstanceID()] = new Watch { T = c.transform, P = c.transform.position, At = Time.time, Task = bl != null && bl.CurrentTask != null ? bl.CurrentTask.Description + (bl.CurrentTask.CanInterrupt ? "" : "(noint)") : "-" };
                if (_runner == null) { GameObject g = new GameObject("BtcDevOrderLag"); UnityEngine.Object.DontDestroyOnLoad(g); _runner = g.AddComponent<BtcDevOrderLagRunner>(); }
            }
            Locomotion4 l = c.GetComponent<Locomotion4>();
            string st = l == null ? "" : " loc=" + (l.InPlace ? "P" : "") + (l.IsHalting ? "H" : "") + (l.Running ? "R" : "") + (l.Walking ? "W" : "") + (l.TurningOnSpot ? "T" : "");
            BtcLog.Info("OrderLag", what + " u" + c.gameObject.GetInstanceID() + " ft=" + Time.fixedTime.ToString("0.000") + " f=" + Time.frameCount + st);
        }
    }

    internal sealed class BtcDevOrderLagRunner : MonoBehaviour
    {
        private void Update() { try { BtcDevOrderLag.Tick(); } catch (Exception) { } }
    }

    [HarmonyPatch(typeof(BaseLabor), "Do")]
    internal static class BtcDevOrderLagDoPatch
    {
        private static void Prefix(BaseLabor __instance, Task work)
        {
            try { BtcDevOrderLag.Mark("do " + (work != null ? work.Description : "?") + " cur=" + (__instance.CurrentTask != null) + " cancel=" + (__instance.CurrentTask != null && __instance.CurrentTask.CanInterrupt), __instance); }
            catch (Exception) { }
        }
    }

    [HarmonyPatch(typeof(Navigation), "ProcessPathResult")]
    internal static class BtcDevOrderLagPathPatch
    {
        private static void Prefix(Navigation __instance) { try { BtcDevOrderLag.Mark("path", __instance); } catch (Exception) { } }
    }

    [HarmonyPatch(typeof(Navigation), "FollowNodeStart")]
    internal static class BtcDevOrderLagFollowPatch
    {
        private static void Prefix(Navigation __instance) { try { BtcDevOrderLag.Mark("follow", __instance); } catch (Exception) { } }
    }

    [HarmonyPatch(typeof(Task), "CleanUp")]
    internal static class BtcDevOrderLagCleanPatch
    {
        private static void Prefix(Task __instance) { try { BtcDevOrderLag.Mark("cleanup " + __instance.Description, __instance.Worker); } catch (Exception) { } }
    }

    [HarmonyPatch(typeof(Task), "OnFinally")]
    internal static class BtcDevOrderLagFinallyPatch
    {
        private static void Prefix(Task __instance) { try { BtcDevOrderLag.Mark("finally " + __instance.Description, __instance.Worker); } catch (Exception) { } }
    }

    [HarmonyPatch(typeof(Task), "CanInterruptTask")]
    internal static class BtcDevOrderLagCanIntPatch
    {
        private static void Prefix(Task __instance) { try { BtcDevOrderLag.Mark("caninterrupt " + __instance.Description, __instance.Worker); } catch (Exception) { } }
    }

    [HarmonyPatch(typeof(Navigation), "ClearFollowNode")]
    internal static class BtcDevOrderLagClearPatch
    {
        private static void Prefix(Navigation __instance) { try { BtcDevOrderLag.Mark("clearfollow", __instance); } catch (Exception) { } }
    }

    [HarmonyPatch(typeof(Labor), "GetReadyForNextTask")]
    internal static class BtcDevOrderLagReadyPatch
    {
        private static void Prefix(Labor __instance) { try { BtcDevOrderLag.Mark("ready", __instance); } catch (Exception) { } }
    }

    [HarmonyPatch(typeof(Navigation), "SearchPathOrTransferTo")]
    internal static class BtcDevOrderLagSearchPatch
    {
        private static void Prefix(Navigation __instance) { try { BtcDevOrderLag.Mark("search", __instance); } catch (Exception) { } }
    }
}
#endif
