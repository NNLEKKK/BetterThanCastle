using System;
using System.Collections.Generic;
using Brix.External.Factories;
using Brix.Engine;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.AI.Damage;
using Brix.Lifecycle.Pooling;
using Brix.Lua;
using Brix.Transactions;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    /// <summary>Global duel state, read by the patches (all of them gated by Active) and by the UI.</summary>
    internal static class BtcDuel
    {
        /// <summary>True from the moment the player pressed Start until the game is left. Every duel hook starts with this check.</summary>
        internal static bool Active;
        internal static BtcDuelMatch Match;
        /// <summary>Snapshot of the SkipAnims setting for the running match (read by the patches every call, no allocation).</summary>
        internal static bool SkipAnims = true;
        /// <summary>DEV/self-play: when >= 0 a second bot (this level) drives the human side.</summary>
        internal static int AutoPlayerLevel = -1;
        /// <summary>Self-play opponent: 0 = old v1 behaviours (V2 class, flag off), 1 = bot V2, 2 = current bot.</summary>
        internal static int AutoPlayerKind;
        /// <summary>Self-play opponent difficulty tier (0 Easy .. 3 Extreme, 4 full): human-like opponents for measuring dominance.</summary>
        internal static int AutoPlayerTier = 4;
        /// <summary>Lab: the target of every strike steps away this many ms after the strike started (0 = off).</summary>
        internal static float LabDodgeMs;
        internal const float VanillaCycleMs = 1500f;

        internal static readonly List<GameObject> Barrels = new List<GameObject>();
        internal static Dictionary<int, BtcDuelUnitRef> ById = new Dictionary<int, BtcDuelUnitRef>();

        internal static bool TryGet(GameObject go, out BtcDuelUnitRef u)
        {
            u = null;
            if (go == null) return false;
            return ById.TryGetValue(go.GetInstanceID(), out u);
        }

        // ---------------- launching ----------------
        /// <summary>Menu button: pick sandbox on the default map and start a local game; the driver then builds the arena.</summary>
        internal static bool Launch()
        {
            if (Active) return false;
            BtcDuelConfig.Save();
            if (!BtcDuelLaunch.PickSandbox()) return false;
            Active = true;
            EnsureDriver();
            Match.BeginLoad();
            GameParam.StartGame(null);
            return true;
        }

        internal static void EnsureDriver()
        {
            if (Match != null) return;
            GameObject go = new GameObject("BtcDuel");
            UnityEngine.Object.DontDestroyOnLoad(go);
            Match = go.AddComponent<BtcDuelMatch>();
        }

        // ---------------- spawning ----------------
        private static Factory.AssetKey _bricktron;

        private static Factory.AssetKey BricktronKey()
        {
            if (_bricktron != null) return _bricktron;
            LuaStaticDriver sd;
            if (LuaDriverHQ.AllStatics.TryGetValue("fy_Bricktron", out sd) && sd != null) _bricktron = sd.AssetKey;
            return _bricktron;
        }

        internal static GameObject SpawnUnit(BtcDuelUnit kind, Faction f, Vector3 pos, Quaternion rot)
        {
            Factory.AssetKey key = BricktronKey();
            if (key == null) { BtcLog.Error("Duel", "no fy_Bricktron key"); return null; }
            GameObject go = Transactor.SpawnUNet(key, f, pos, rot, null, true);
            if (go == null) return null;
            Occupation occ = go.GetComponent<Occupation>();
            Toolbag tb = go.GetComponent<Toolbag>();
            switch (kind)
            {
                case BtcDuelUnit.Worker: occ.SetOccupation(Occupation.Type.Builder, true); break;
                case BtcDuelUnit.BarrelWorker:
                    occ.SetOccupation(Occupation.Type.Builder, true);
                    break;
                case BtcDuelUnit.Knight: occ.SetOccupation(Occupation.Type.Knight, true); break;
                case BtcDuelUnit.Archer: occ.SetOccupation(Occupation.Type.Archer, true); break;
                case BtcDuelUnit.Halberdier:
                    occ.SetOccupation(Occupation.Type.Archer, false);
                    tb.EquipTool(Accessories.Poleaxe); tb.EquipTool(Accessories.KettleHat);
                    break;
                case BtcDuelUnit.Arbalist:
                    occ.SetOccupation(Occupation.Type.Archer, false);
                    tb.EquipTool(Accessories.Crossbow); tb.EquipTool(Accessories.Pouch); tb.EquipTool(Accessories.Kepi);
                    break;
                case BtcDuelUnit.Alchemist:
                    occ.SetOccupation(Occupation.Type.Archer, false);
                    tb.EquipTool(Accessories.Backpack); tb.EquipTool(Accessories.Bonnet);
                    break;
                case BtcDuelUnit.Geomancer:
                    occ.SetOccupation(Occupation.Type.Archer, false);
                    tb.EquipTool(Accessories.Staff); tb.EquipTool(Accessories.Sachel); tb.EquipTool(Accessories.ArtificerHat);
                    break;
            }
            return go;
        }

        /// <summary>Barrel worker: spawn an explosive barrel next to the worker and order the vanilla pick-up (same order as CmdPickup).</summary>
        internal static void GiveBarrel(GameObject worker, Faction f)
        {
            try
            {
                Vector3 p = worker.transform.position + worker.transform.forward * 1.2f;
                GameObject barrel = Transactor.SpawnUNet(ObjetsDynamiques.Baril, f, p, Quaternion.identity, null, true);
                if (barrel == null) { BtcLog.Warn("Duel", "barrel spawn failed"); return; }
                Barrels.Add(barrel);
                Order.Transfer.GoPickUpFrom.Send(worker.GetComponent<Labor>(), barrel, Labor.DecisionSource.User);
            }
            catch (Exception e) { BtcLog.Warn("Duel", "barrel: " + e.Message); }
        }

        internal static void Despawn(GameObject go)
        {
            if (go == null) return;
            // the vanilla NetworkAsymmetricDestroy only releases objects owned by the local user (bot units are not): release directly like the editor cleanup does
            try { ObjectPoolSingleton.Release(go); }
            catch (Exception e) { BtcLog.Warn("Duel", "despawn: " + e.Message); try { UnityEngine.Object.Destroy(go); } catch (Exception) { } }
        }

        // ---------------- orders (same objects the client commands send, minus the ownership check) ----------------
        /// <summary>Per side: drop non-forced bot orders while a unit is inside its strike window (any order before impact+~100 ms cancels or delays the hit).</summary>
        internal static readonly bool[] GuardOrders = new bool[2];
        internal const float StrikeWindow = 0.56f;

        private static bool Guarded(BtcDuelUnitRef u, bool force)
        {
            if (force || !GuardOrders[u.Side]) return false;
            if (Time.time - u.StrikeAt >= StrikeWindow) return false;
            u.TargetId = -9;                 // the caller thinks the order went out: make it re-issue next tick
            return true;
        }

        internal static void MoveAttack(BtcDuelUnitRef u, Vector3 pos, bool force = false)
        {
            if (Guarded(u, force)) return;
            u.LastOrderAt = Time.time; u.LastDest = pos; u.PathDest = Vector3.zero;
            if (BtcDuelLog.BotOrders) BtcDuelLog.Line("BORD", "u" + u.Id + " MoveAttack to=" + pos.x.ToString("0.0") + "," + pos.z.ToString("0.0"));
            try { Order.Combat.MoveAttack.Send(u.Labor, new SpaceLocation(pos, true), Labor.DecisionSource.User); }
            catch (Exception e) { BtcLog.Error("Duel", "MoveAttack order", e); }
        }

        internal static void Attack(BtcDuelUnitRef u, GameObject target, bool force = false)
        {
            if (Guarded(u, force)) return;
            u.LastOrderAt = Time.time; u.PathDest = Vector3.zero;
            if (BtcDuelLog.BotOrders) { BtcDuelUnitRef bt; BtcDuel.TryGet(target, out bt); BtcDuelLog.Line("BORD", "u" + u.Id + " Attack >" + (bt != null ? "u" + bt.Id : "?")); }
            try { Order.Combat.GoAttackDirectly.Send(u.Labor, target, Labor.DecisionSource.User); }
            catch (Exception e) { BtcLog.Error("Duel", "Attack order", e); }
        }

        internal static void Move(BtcDuelUnitRef u, Vector3 pos, bool force = false)
        {
            if (Guarded(u, force)) return;
            u.LastOrderAt = Time.time; u.LastDest = pos; u.PathDest = Vector3.zero;
            if (BtcDuelLog.BotOrders) BtcDuelLog.Line("BORD", "u" + u.Id + " Move to=" + pos.x.ToString("0.0") + "," + pos.z.ToString("0.0"));
            try { Order.Move.RunToward.Send(u.Labor, new SpaceLocation(pos, true), Labor.DecisionSource.User); }
            catch (Exception e) { BtcLog.Error("Duel", "Move order", e); }
        }
    }

    /// <summary>Bridge into the DEV-only BtcDevMatch.Pick logic without depending on DEV code: picks sandbox + default map.</summary>
    internal static class BtcDuelLaunch
    {
        private const string DefMap = "PlateauMont-Royal_34885478-77e1-4597-ac79-de48e3f5f9a1";

        internal static bool PickSandbox()
        {
            try
            {
                if (GameParam.instance == null) { BtcLog.Warn("Duel", "GameParam.instance == null (not in main menu)"); return false; }
                Brix.Assets.Asset_Map map = Brix.Assets.AssetDispensary.maps.SendCodenameQuery(DefMap);
                if (map == null)
                {
                    List<Brix.Assets.Asset_Map> all = Brix.Assets.AssetDispensary.maps.SendQuery(Brix.Assets.AssetQueryParameters.AllSources, Brix.Assets.AssetPlayableContext.Singleplayer, false);
                    if (all.Count > 0) map = all[0];
                }
                Brix.Assets.Asset_Gamemode mode = Brix.Assets.AssetDispensary.gamemodes.SendCodenameQuery("sandbox", Brix.Assets.AssetQueryParameters.AllSources, Brix.Assets.AssetPlayableContext.Any);
                if (map == null || mode == null) { BtcLog.Error("Duel", "map or sandbox gamemode not found"); return false; }
                GameParam.Map = map;
                GameParam.Gamemode = mode;
                GameParam.Preset = mode.GetDefaultPreset(Brix.Assets.AssetQueryParameters.AllSources, Brix.Assets.AssetPlayableContext.Any);
                BtcLog.Info("Duel", "launch map=" + map.codename + " mode=" + mode.codename);
                return true;
            }
            catch (Exception e) { BtcLog.Error("Duel", "pick failed", e); return false; }
        }
    }
}
