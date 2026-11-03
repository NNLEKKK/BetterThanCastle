using System;
using System.Collections.Generic;
using Brix.Audio;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.Components;
using Brix.Lua;
using HarmonyLib;
using MoonSharp.Interpreter;
using UnityEngine;

namespace BetterThanCastle.Patches
{
    /// <summary>
    /// "Call to arms" (hotkey command_CallToArms / button): GameMenu.lua resolves `userCrystal` ONCE at menu load by
    /// `localUser.alliedFaction == crystal.faction`. For extended factions (colours 5-8) that lookup can be nil, and
    /// lua_CallToArms then throws in AsGameObject(0, allowNil=false) so no unit is ordered. Fix: when the Lua argument is not
    /// a valid crystal, resolve the local player's crystal lazily and run the vanilla body.
    /// Applies for any faction (vanilla body otherwise identical), no wire change (same CmdAddCallToArmsGoal).
    /// </summary>
    [HarmonyPatch(typeof(UIGameObserver), "lua_CallToArms")]
    internal static class UIGameObserverCallToArmsPatch
    {
        private static bool Prefix(CallbackArguments args, ref DynValue __result)
        {
            try
            {
                GameObject go = null;
                try { go = args.AsGameObject(0, "CallToArms", false); }
                catch (Exception) { go = null; }
                string src = go != null ? "lua" : "fallback";
                User lu = User.LocalUser;
                Faction lf = lu != null ? lu.faction : null;
                // Lua's userCrystal is resolved once at menu load; it may be stale or belong to another faction.
                if (go == null || (lf != null && !Affiliation.BelongsToMe(go)))
                {
                    GameObject own = FindOwnCrystal();
                    if (own != null) { go = own; src = go == null ? src : "fallback"; }
                }
                if (go == null)
                {
                    BtcLog.Warn("CallToArms", "no own crystal found; faction=" + (lf != null ? lf.name : "null") + "; vanilla path (Lua error)");
                    return true; // vanilla throws the same way as before: no regression
                }
                SoundEngine.Play("Call_To_Arm");
                int total = 0, builders = 0, owned = 0;
                foreach (Labor labor in UIGameObserver.bricktrons.CheckCreation())
                {
                    total++;
                    if (labor.Profession.OccupationType == Occupation.Type.Builder)
                    {
                        builders++;
                        if (lu != null && Affiliation.IsOwnedBy(labor.gameObject, lu)) owned++;
                        DirectOrderProject.AddCallToArmsGoal(labor, go);
                    }
                }
                AffiliationComponent ac = go.GetComponent<AffiliationComponent>();
                BtcLog.Info("CallToArms", "crystal=" + src + " crystalFaction=" + (ac != null && ac.faction != null ? ac.faction.name : "null")
                    + " localFaction=" + (lf != null ? lf.name : "null") + " bricktrons=" + total + " builders=" + builders
                    + " ownedByMe(server check proxy)=" + owned + " pos=" + go.transform.position);
                __result = DynValue.Void;
                return false;
            }
            catch (Exception e)
            {
                BtcLog.Error("Hook", "UIGameObserver.lua_CallToArms: " + e);
                return true;
            }
        }

        private static GameObject FindOwnCrystal()
        {
            User u = User.LocalUser;
            if (u == null) return null;
            LuaStaticDriver drv = LuaDriverHQ.GetStaticDriver("fy_Crystal");
            if (drv == null) return null;
            Faction f = u.faction;
            Vector3 from = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            GameObject best = null;
            float bd = float.MaxValue;
            if (f != null)
            {
                List<LuaDriver> own = drv.GetAllOfFaction(f);
                if (own != null)
                {
                    for (int i = 0; i < own.Count; i++)
                    {
                        if (own[i] == null) continue;
                        float d = (own[i].transform.position - from).sqrMagnitude;
                        if (d < bd) { bd = d; best = own[i].gameObject; }
                    }
                }
            }
            if (best != null) return best;
            List<LuaDriver> all = drv.GetAll();
            if (all == null) return null;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] == null) continue;
                GameObject g = all[i].gameObject;
                if (!Affiliation.BelongsToMe(g)) continue;
                float d = (g.transform.position - from).sqrMagnitude;
                if (d < bd) { bd = d; best = g; }
            }
            return best;
        }
    }
}
