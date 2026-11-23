using System;
using System.Reflection;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Game.Semantique;
using Brix.Input.ContextualOrders;
using HarmonyLib;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    // Shift+RMB (move-attack) on a loose item/resource/tool: vanilla sends the unit to pick/inspect it (no attack on the way).
    // Treat the object as plain ground instead, so the order is MoveAttack to its position like on terrain.
    [HarmonyPatch(typeof(ContextualActor), "Prepare")]
    internal static class ContextualActorPreparePatch
    {
        private static readonly FieldInfo ActionTypeField = AccessTools.Field(typeof(ContextualActor), "actionType");
        private static readonly FieldInfo SubjectField = AccessTools.Field(typeof(ContextualActor), "subject");

        private static void Prefix(ContextualActor __instance, ref object target)
        {
            try
            {
                GameObject go = target as GameObject;
                if (go == null || ActionTypeField == null) return;
                if ((ActionType)ActionTypeField.GetValue(__instance) != ActionType.Ternary) return;
                if (go.GetComponent<Labor>() != null) return;
                Labor subj = SubjectField != null ? SubjectField.GetValue(__instance) as Labor : null;
                if (subj != null && go.GetComponent<GameComponent>() != null && Affiliation.IsEnemy(subj.gameObject, go)) return;
                if (go.GetComponent<Tool>() != null || go.GetComponent<Recepteur>() != null || Apparence.Is<Ressource>(go))
                {
                    target = (object)(Vector3?)go.transform.position;
                }
            }
            catch (Exception e) { BtcLog.Error("Hook", "ContextualActor.Prepare: " + e.Message); }
        }
    }
}
