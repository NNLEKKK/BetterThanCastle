#if DEV
using System;
using System.Collections.Generic;
using System.Text;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.AI.Damage;
using Brix.Game.Components;
using UnityEngine;

namespace BetterThanCastle.Dev
{
    /// <summary>
    /// DEV lab for phantoms (BtcPhantom). Scenario commands:
    /// phantom dump &lt;tag&gt;  - per player faction: unit count, spawn price, live phantoms (HP, stamina, vision, look), corpses on the map;
    /// phantom kill [n]     - kills n phantoms (default 1) through the vanilla damage receiver;
    /// phantom fist         - a phantom punches an own worker (HP must not change), then a worker hits the phantom (it must die).
    /// Every result is a LAB line (agent.log, tag Lab).
    /// </summary>
    internal static class BtcDevPhantom
    {
        internal static bool Cmd(string[] p)
        {
            string sub = p.Length > 1 ? p[1] : "dump";
            try
            {
                switch (sub)
                {
                    case "dump": Dump(p.Length > 2 ? p[2] : "-"); break;
                    case "kill": Kill(p.Length > 2 ? int.Parse(p[2]) : 1); break;
                    case "fist": Fist(); break;
                    case "fly": Fly(p); break;
                    case "flythru": FlyThru(); break;
                    case "shoot": Shoot(); break;
                    case "shotcheck": ShotCheck(); break;
                    case "energy": Energy(p.Length > 2 ? int.Parse(p[2]) : 50); break;
                    case "ghostprobe": GhostProbe(p.Length > 2 ? int.Parse(p[2]) : 24); break;
                    case "where": Where(p.Length > 2 ? p[2] : "-"); break;
                    case "forest": Forest(); break;
                    case "meet": Meet(); break;
                    case "pair": Pair(p.Length > 2 ? p[2] : "-"); break;
                    case "save": Save(p.Length > 2 ? p[2] : "phlab"); break;
                    case "load": Load(p.Length > 2 ? p[2] : "phlab"); break;
                    default: BtcDevLab.Lab("phantom: unknown " + sub); break;
                }
            }
            catch (Exception e) { BtcDevLab.Lab("phantom " + sub + " error: " + e); }
            return true;
        }

        private static void Dump(string tag)
        {
            List<GameObject> live = BtcPhantom.DevLive();
            StringBuilder sb = new StringBuilder("phantom dump " + tag + " live=" + BtcPhantom.LiveCount + " list=" + live.Count);
            int corpses = 0;
            foreach (Corpse c in UnityEngine.Object.FindObjectsOfType<Corpse>()) if (c.gameObject.activeInHierarchy) corpses++;
            sb.Append(" corpses=").Append(corpses);
            List<FireflyNest> nests = BtcMatchStats.NestList();
            if (nests != null)
                foreach (FireflyNest n in nests)
                {
                    if (n == null || !n.isHome || n.faction == null || n.faction.isAI) continue;
                    int ph = 0;
                    foreach (GameObject g in live) { Labor l = g.GetComponent<Labor>(); if (l != null && l.faction == n.faction) ph++; }
                    int real = 0;
                    foreach (Labor l in UnityEngine.Object.FindObjectsOfType<Labor>()) if (l.faction == n.faction && l.gameObject.activeInHierarchy) real++;
                    sb.Append(" | fac=").Append(n.faction.ID).Append(" unitCount=").Append(n.faction.UnitCount).Append(" laborsOnMap=").Append(real)
                      .Append(" phantoms=").Append(ph).Append(" price=").Append(n.NewFireflyRequiredEnergy).Append(" energy=").Append(n.GetAvailablePurifiedEnergy());
                }
            BtcDevLab.Lab(sb.ToString());
            foreach (GameObject g in live)
            {
                BricktronDamageReceiver dr = g.GetComponent<BricktronDamageReceiver>();
                LocalFactionVision v = g.GetComponent<LocalFactionVision>();
                Occupation o = g.GetComponent<Occupation>();
                BtcDevLab.Lab("  phantom " + g.name + " active=" + g.activeInHierarchy + " occ=" + (o != null ? o.CurrentOccupation.ToString() : "?")
                    + " hp=" + (dr != null ? dr.CurrentHP + "/" + dr.MaxHP : "?") + " stam=" + (dr != null ? dr.CurrentStamina + "/" + dr.MaxStamina : "?")
                    + " vis=" + (v != null ? v.LineOfSightRange.ToString() : "?") + " look=" + (g.GetComponent<BtcPhantomLook>() != null)
                    + " pos=" + g.transform.position);
            }
        }

        /// <summary>Manual save of the running game under a custom name (the path of the Save dialog).</summary>
        private static void Save(string name)
        {
            Brix.Assets.Asset_Map s = Brix.Assets.Asset_Map.CreateShellFrom(GameParam.Map, Brix.Assets.Asset_Map.MapType.Save);
            s.customName = name;
            s.saveType = Brix.Assets.Asset_Map.SaveType.Manualsave;
            s.Disk_WriteAsNew(Brix.Assets.AssetAuthorType.Official);
            BtcDevLab.Lab("phantom save " + name + " -> " + s.path);
        }

        /// <summary>From the main menu: starts the newest save with that custom name (sandbox), like Load game.</summary>
        private static void Load(string name)
        {
            List<Brix.Assets.Asset_Map> all = Brix.Assets.AssetDispensary.saves.SendQuery(Brix.Assets.AssetQueryParameters.AllSources | Brix.Assets.AssetQueryParameters.RefreshCache, Brix.Assets.AssetPlayableContext.Any, false);
            Brix.Assets.Asset_Map save = null;
            for (int i = 0; i < all.Count; i++) if (all[i].customName == name) save = all[i];
            if (save == null) { BtcDevLab.Lab("phantom load: no save " + name + " (of " + all.Count + ")"); return; }
            if (!BtcDevMatch.Pick("default", "sandbox", null, Brix.Assets.AssetPlayableContext.Singleplayer)) { BtcDevLab.Lab("phantom load: menu not ready"); return; }
            GameParam.Map = save;
            BtcDevLab.Lab("phantom load " + name + " <- " + save.path);
            GameParam.StartGame(null);
        }

        private static void Kill(int n)
        {
            List<GameObject> live = BtcPhantom.DevLive();
            int k = 0;
            for (int i = 0; i < live.Count && k < n; i++)
            {
                BricktronDamageReceiver dr = live[i].GetComponent<BricktronDamageReceiver>();
                if (dr == null) continue;
                dr.ReceiveAttack(5f, DamageType.Melee, DamageReaction.Twitch, live[i].transform.position, Vector3.zero, 0f, null);
                k++;
            }
            BtcDevLab.Lab("phantom kill " + k + "/" + n + " liveAfter=" + BtcPhantom.LiveCount);
        }

        private static Vector3 _flyTarget;

        private static GameObject _meetWorker;

        /// <summary>phantom forest: sends the first phantom to the nearest open forest cell (next to a tree, not a trunk) 8..40 blocks away.</summary>
        private static void Forest()
        {
            List<GameObject> live = BtcPhantom.DevLive();
            if (live.Count == 0) { BtcDevLab.Lab("phantom forest: no phantom"); return; }
            GameObject ph = live[0];
            _fly = ph;
            Brix.Engine.XYZ c = Brix.Engine.XYZ.FromVector3(ph.transform.position);
            float best = float.MaxValue; Brix.Engine.XYZ bp = c; int found = 0;
            for (int x = -40; x <= 40; x++) for (int z = -40; z <= 40; z++) for (int y = -8; y <= 10; y++)
            {
                Brix.Engine.XYZ p = new Brix.Engine.XYZ(c.x + x, c.y + y, c.z + z);
                if (!Brix.Engine.Voxel.IsOpen(p) || Brix.Engine.Voxel.IsOpen(p - Brix.Engine.XYZ.up)) continue;
                if (Brix.Engine.Nature.NatureRegistry.HasNatureBlockerAt(p) || !Brix.Legacy.TreeRegistry.Instance.IsNearATree(p)) continue;
                float d = x * x + z * z + y * y;
                if (d < 64f) continue;
                found++;
                if (d < best) { best = d; bp = p; }
            }
            if (found == 0) { BtcDevLab.Lab("phantom forest: no forest cell"); return; }
            _flyTarget = bp.ToVector3();
            Order.Move.RunToward.Send(ph.GetComponent<Labor>(), new SpaceLocation(_flyTarget, true), Labor.DecisionSource.User);
            BtcDevLab.Lab("phantom forest " + ph.name + " from=" + ph.transform.position + " to=" + _flyTarget + " cells=" + found + " dist=" + Mathf.Sqrt(best).ToString("F1"));
        }

        /// <summary>phantom meet: an own normal worker and the first phantom both run to one cell between them.</summary>
        private static void Meet()
        {
            List<GameObject> live = BtcPhantom.DevLive();
            if (live.Count == 0) { BtcDevLab.Lab("phantom meet: no phantom"); return; }
            GameObject ph = live[0];
            _fly = ph;
            Labor pl = ph.GetComponent<Labor>();
            _meetWorker = null;
            foreach (Labor l in UnityEngine.Object.FindObjectsOfType<Labor>())
                if (l.faction == pl.faction && l.gameObject.activeInHierarchy && !BtcPhantom.IsPhantom(l.gameObject)) { _meetWorker = l.gameObject; break; }
            if (_meetWorker == null) { BtcDevLab.Lab("phantom meet: no worker"); return; }
            _flyTarget = Brix.Engine.XYZ.FromVector3((ph.transform.position + _meetWorker.transform.position) * 0.5f).ToVector3();
            Order.Move.RunToward.Send(pl, new SpaceLocation(_flyTarget, true), Labor.DecisionSource.User);
            Order.Move.RunToward.Send(_meetWorker.GetComponent<Labor>(), new SpaceLocation(_flyTarget, true), Labor.DecisionSource.User);
            BtcDevLab.Lab("phantom meet worker=" + _meetWorker.name + " from=" + _meetWorker.transform.position + " common cell=" + _flyTarget + " body=" + BtcPhantomBody.On);
        }

        private static void Pair(string tag)
        {
            if (_meetWorker == null || _fly == null) { BtcDevLab.Lab("phantom pair " + tag + ": no pair"); return; }
            Brix.Engine.XYZ w = Brix.Engine.XYZ.FromVector3(_meetWorker.transform.position), p = Brix.Engine.XYZ.FromVector3(_fly.transform.position);
            BtcDevLab.Lab("phantom pair " + tag + " worker=" + _meetWorker.transform.position.ToString("F2") + " phantom=" + _fly.transform.position.ToString("F2")
                + " sameCell=" + (w == p) + " phantomToCell=" + Vector3.Distance(_fly.transform.position, _flyTarget).ToString("F2") + " workerToCell=" + Vector3.Distance(_meetWorker.transform.position, _flyTarget).ToString("F2") + " body=" + BtcPhantomBody.On);
        }
        private static GameObject _fly;

        /// <summary>phantom fly &lt;dx&gt; &lt;dy&gt; &lt;dz&gt; [walk]: the first phantom gets a user move order to its position + offset
        /// (with phantom.fly on it must end there, hovering; dy &gt; 0 = in the air, a far dx/dz through a hill = through terrain).</summary>
        private static void Fly(string[] p)
        {
            List<GameObject> live = BtcPhantom.DevLive();
            if (live.Count == 0) { BtcDevLab.Lab("phantom fly: no phantom"); return; }
            GameObject ph = live[0];
            if (p.Length > 5 && p[5] == "other")   // a phantom of another player (MP: the client's one, so the client can see it)
            {
                Faction mine = User.LocalUser != null ? User.LocalUser.faction : null;
                for (int i = 0; i < live.Count; i++) { Labor li = live[i].GetComponent<Labor>(); if (li != null && li.faction != mine) { ph = live[i]; break; } }
            }
            _fly = ph;
            Vector3 off = new Vector3(p.Length > 2 ? float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture) : 0f,
                                      p.Length > 3 ? float.Parse(p[3], System.Globalization.CultureInfo.InvariantCulture) : 0f,
                                      p.Length > 4 ? float.Parse(p[4], System.Globalization.CultureInfo.InvariantCulture) : 0f);
            Vector3 from = ph.transform.position;
            _flyTarget = new Vector3(Mathf.Round(from.x + off.x), Mathf.Round(from.y + off.y), Mathf.Round(from.z + off.z));
            Labor l = ph.GetComponent<Labor>();
            Order.Move.RunToward.Send(l, new SpaceLocation(_flyTarget, true), Labor.DecisionSource.User);
            BtcDevLab.Lab("phantom fly " + ph.name + " id=" + ph.GetInstanceID() + " from=" + from + " to=" + _flyTarget + " flyRule=" + BtcPhantom.Fly);
        }

        /// <summary>phantom ghostprobe [r]: in a cube of radius r (default 24) round the first phantom, counts the cells the path search
        /// treats as blocked for an own normal worker vs for the phantom (SearchPathRequest.HasBlockerAt) and the occupied cells
        /// (FloorPicker.IsOccupied), lists the blocking object names for the worker, and the forest permission of both.</summary>
        private static void GhostProbe(int r)
        {
            List<GameObject> live = BtcPhantom.DevLive();
            if (live.Count == 0) { BtcDevLab.Lab("phantom ghostprobe: no phantom"); return; }
            GameObject ph = live[0];
            Labor pl = ph.GetComponent<Labor>();
            GameObject wk = null;
            foreach (Labor l in UnityEngine.Object.FindObjectsOfType<Labor>())
                if (l.faction == pl.faction && l.gameObject.activeInHierarchy && !BtcPhantom.IsPhantom(l.gameObject)) { wk = l.gameObject; break; }
            if (wk == null) { BtcDevLab.Lab("phantom ghostprobe: no worker"); return; }
            Brix.Engine.XYZ c = Brix.Engine.XYZ.FromVector3(ph.transform.position);
            int wB = 0, pB = 0, wO = 0, pO = 0, both = 0;
            Dictionary<string, int> names = new Dictionary<string, int>();
            for (int x = -r; x <= r; x++) for (int y = -6; y <= 8; y++) for (int z = -r; z <= r; z++)
            {
                Brix.Engine.XYZ p = new Brix.Engine.XYZ(c.x + x, c.y + y, c.z + z);
                bool wb = SearchPathRequest.HasBlockerAt(wk, p), pb = SearchPathRequest.HasBlockerAt(ph, p);
                if (wb) { wB++; Brix.Game.Components.Volumes.IVolume v = Brix.Game.Components.Volumes.VolumeRegistry.VolumeWithPropertyAt(p, Brix.Game.Components.Volumes.VProperty.obstacle);
                    string n = v == null ? "?" : (v.GameObject != null ? v.GameObject.name : v.GetType().Name + ":" + v.Name); int k; names.TryGetValue(n, out k); names[n] = k + 1; }
                if (pb) pB++;
                if (wb && pb) both++;
                if (FloorPicker.IsOccupied(wk, p, false)) wO++;
                if (FloorPicker.IsOccupied(ph, p, false)) pO++;
            }
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<string, int> kv in names) sb.Append(kv.Key).Append('=').Append(kv.Value).Append(' ');
            BtcDevLab.Lab("phantom ghostprobe r=" + r + " ghost=" + BtcPhantom.Ghost + " searchBlocked worker=" + wB + " phantom=" + pB + " both=" + both
                + " occupied worker=" + wO + " phantom=" + pO + " forest worker=" + wk.GetComponent<Labor>().Profession.CanWalkInForest() + " phantom=" + pl.Profession.CanWalkInForest()
                + " | worker blockers: " + sb);
        }

        private static GameObject _shotWorker, _shotPhantom, _shooter;
        private static float _shotHp;
        private static int _shotThrough0;

        /// <summary>phantom shoot: an own worker is put 3 blocks behind the first phantom and an arrow from a factionless shooter (= enemy)
        /// 8 blocks in front is fired at the worker through the phantom. shotcheck (2 s later): the phantom must be dead and the worker hit.</summary>
        private static void Shoot()
        {
            List<GameObject> live = BtcPhantom.DevLive();
            if (live.Count == 0) { BtcDevLab.Lab("phantom shoot: no phantom"); return; }
            GameObject ph = live[0];
            Labor pl = ph.GetComponent<Labor>();
            GameObject wk = null;
            foreach (Labor l in UnityEngine.Object.FindObjectsOfType<Labor>())
                if (l.faction == pl.faction && l.gameObject.activeInHierarchy && !BtcPhantom.IsPhantom(l.gameObject)) { wk = l.gameObject; break; }
            if (wk == null) { BtcDevLab.Lab("phantom shoot: no worker"); return; }
            Vector3 p = ph.transform.position;
            wk.transform.position = p + Vector3.right * 3f;
            if (_shooter == null) _shooter = new GameObject("BtcLabShooter");
            _shooter.transform.position = p - Vector3.right * 8f + Vector3.up * 0.8f;
            _shooter.transform.rotation = Quaternion.LookRotation(Vector3.right);
            BricktronDamageReceiver dr = wk.GetComponent<BricktronDamageReceiver>();
            _shotWorker = wk; _shotPhantom = ph; _shotHp = dr != null ? dr.CurrentHP : -1f; _shotThrough0 = BtcPhantom.ShotThrough;
            Vector3 aim = wk.transform.position + Vector3.up * 0.8f;
            KinematicProjectile.FireAt(Brix.External.Factories.Test.Arrow, _shooter, _shooter.transform.position, _shooter.transform.position, Location.Of(aim), aim, 40f, 0f, 50);
            BtcDevLab.Lab("phantom shoot phantom=" + ph.GetInstanceID() + " at " + p + " worker=" + wk.GetInstanceID() + " hp=" + _shotHp + " live=" + BtcPhantom.LiveCount);
        }

        /// <summary>phantom energy [n]: puts n energy (one unnamed firefly) into every player home crystal.</summary>
        private static void Energy(int n)
        {
            System.Reflection.MethodInfo mk = HarmonyLib.AccessTools.Method(typeof(FireflyNest), "CreateUnnamedFirefly");
            List<FireflyNest> nests = BtcMatchStats.NestList();
            int k = 0;
            if (nests != null && mk != null)
                foreach (FireflyNest nest in nests)
                {
                    if (nest == null || !nest.isHome || nest.faction == null || nest.faction.isAI) continue;
                    mk.Invoke(nest, new object[] { n });
                    k++;
                }
            BtcDevLab.Lab("phantom energy +" + n + " homes=" + k + " method=" + (mk != null));
        }

        private static void ShotCheck()
        {
            BricktronDamageReceiver dr = _shotWorker != null ? _shotWorker.GetComponent<BricktronDamageReceiver>() : null;
            float hp = dr != null ? dr.CurrentHP : -1f;
            bool phDead = _shotPhantom == null || !BtcPhantom.IsPhantom(_shotPhantom);
            bool through = BtcPhantom.ShotThrough > _shotThrough0;
            BtcDevLab.Lab("phantom shotcheck " + (phDead && through && hp < _shotHp ? "OK" : "FAIL") + " phantomDead=" + phDead + " passedThrough=" + through
                + " workerHp " + _shotHp + " -> " + hp + " live=" + BtcPhantom.LiveCount);
        }

        /// <summary>phantom flythru:the first phantom flies through its home crystal (a building) to the mirrored point on the other side.</summary>
        private static void FlyThru()
        {
            List<GameObject> live = BtcPhantom.DevLive();
            if (live.Count == 0) { BtcDevLab.Lab("phantom flythru: no phantom"); return; }
            GameObject ph = _fly != null && BtcPhantom.IsPhantom(_fly) ? _fly : live[0];
            _fly = ph;
            Labor l = ph.GetComponent<Labor>();
            FireflyNest home = null;
            List<FireflyNest> nests = BtcMatchStats.NestList();
            if (nests != null) foreach (FireflyNest n in nests) if (n != null && n.isHome && n.faction == l.faction) home = n;
            if (home == null) { BtcDevLab.Lab("phantom flythru: no home crystal"); return; }
            Vector3 from = ph.transform.position, c = home.transform.position;
            Vector3 d = c - from; d.y = 0f;
            Vector3 t = c + d.normalized * Mathf.Max(4f, d.magnitude);
            _flyTarget = new Vector3(Mathf.Round(t.x), Mathf.Round(from.y + 1f), Mathf.Round(t.z));
            Order.Move.RunToward.Send(l, new SpaceLocation(_flyTarget, true), Labor.DecisionSource.User);
            BtcDevLab.Lab("phantom flythru " + ph.name + " id=" + ph.GetInstanceID() + " from=" + from + " crystal=" + c + " to=" + _flyTarget);
        }

        private static void Where(string tag)
        {
            List<GameObject> live = BtcPhantom.DevLive();
            if (live.Count == 0) { BtcDevLab.Lab("phantom where " + tag + ": no phantom"); return; }
            GameObject ph = _fly != null && BtcPhantom.IsPhantom(_fly) ? _fly : live[0];
            Locomotion4 lo = ph.GetComponent<Locomotion4>();
            Vector3 pos = ph.transform.position;
            BtcDevLab.Lab("phantom where " + tag + " " + ph.name + " pos=" + pos.ToString("F2") + " target=" + _flyTarget + " dist=" + Vector3.Distance(pos, _flyTarget).ToString("F2")
                + " grounded=" + (lo != null && lo.IsInGroundedState()) + " flights=" + BtcPhantomFly.Flights + " arrived=" + BtcPhantomFly.Arrived + " unfindable=" + BtcPhantomFly.Unfindable + " jumps=" + BtcPhantomFly.Jumps
                + " live=" + BtcPhantom.LiveCount + " hp=" + ph.GetComponent<BricktronDamageReceiver>().CurrentHP);
        }

        private static void Fist()
        {
            List<GameObject> live = BtcPhantom.DevLive();
            if (live.Count == 0) { BtcDevLab.Lab("phantom fist: no phantom"); return; }
            GameObject ph = live[0];
            Labor pl = ph.GetComponent<Labor>();
            GameObject worker = null;
            foreach (Labor l in UnityEngine.Object.FindObjectsOfType<Labor>())
                if (l.faction == pl.faction && l.gameObject.activeInHierarchy && !BtcPhantom.IsPhantom(l.gameObject)) { worker = l.gameObject; break; }
            if (worker == null) { BtcDevLab.Lab("phantom fist: no worker"); return; }
            BricktronDamageReceiver wdr = worker.GetComponent<BricktronDamageReceiver>();
            float before = wdr.CurrentHP;
            DamageReaction r = wdr.ReceiveAttack(20f, DamageType.Melee, DamageReaction.Twitch, worker.transform.position, Vector3.zero, 0f, ph);
            BtcDevLab.Lab("phantom fist: worker hp " + before + " -> " + wdr.CurrentHP + " reaction=" + r + " (expect unchanged, Ignored)");
            BricktronDamageReceiver pdr = ph.GetComponent<BricktronDamageReceiver>();
            pdr.ReceiveAttack(1f, DamageType.Melee, DamageReaction.Twitch, ph.transform.position, Vector3.zero, 0f, worker);
            BtcDevLab.Lab("phantom fist: phantom hit by worker -> isPhantomStill=" + BtcPhantom.IsPhantom(ph) + " active=" + ph.activeInHierarchy + " hp=" + pdr.CurrentHP);
        }
    }
}
#endif
