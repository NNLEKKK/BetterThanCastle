using System;
using System.Collections.Generic;
using System.Reflection;
using Brix.Engine;
using Brix.External.Signals;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.AI.Nodes;
using Brix.Game.Components;
using Brix.Lifecycle.Pooling;
using Brix.Network;
using HarmonyLib;
using Motus.Behavior;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle
{
    /// <summary>
    /// Work highlights (yellow dig voxel, yellow repair/transform target, red siege/detonate target) are made by the worker AI,
    /// which runs on the host only: vanilla host saw them for EVERY faction (enemies too), clients saw none, not even their own.
    /// Fix: the vanilla wrapper delegates (static `&lt;&gt;f__mg$cacheN` fields, filled lazily by vanilla when null) are preset with ours;
    /// ours draw on the host only for the local faction (spectators see all) and, BC ON, send add/remove to the connections of the
    /// worker's own faction (msg 133, reliable). BC OFF: local filter only, nothing on the wire.
    /// </summary>
    internal static class BtcWorkHighlight
    {
        internal const short Msg = 133;
        private const byte KAddPos = 1, KAddObj = 2, KRemove = 3, KBorne = 4;
        private static readonly Color Yellow = new Color(1f, 0.77f, 0.04f);

        private sealed class Req
        {
            internal Labor Labor;
            internal XYZ Pos;
            internal GameObject Go;    // null = voxel highlight at Pos
            internal Color Col;
            internal int Id;
            internal readonly List<int> Conns = new List<int>();
        }

        private static readonly Action<Req, Signal<GameObject>> StartA = Start;
        private static readonly Action<Signal<GameObject>, Req> StopA = Stop;
        private static int _nextId, _logged;
        private static bool _installed;
        internal static int LocalShown, LocalHidden, SentAdds, GotAdds, SentBornes, GotBornes;   // counters for the DEV lab dump

        // ------------------------------------------------------------------ install (lazy, before vanilla fills its caches)
        internal static void Install()
        {
            if (_installed) return;
            _installed = true;
            int n = 0;
            n += Set(typeof(DigGoal), "<>f__mg$cache1", "Dig");
            n += Set(typeof(RepairGoal), "<>f__mg$cache0", "Repair");
            n += Set(typeof(TransformGoal), "<>f__mg$cache1", "Transform");
            n += Set(typeof(CombatNodes), "<>f__mg$cache2", "TargetRed");
            n += Set(typeof(WorkNode), "<>f__mg$cache1", "TargetRed");
            BtcLog.Info("WorkHl", "installed " + n + "/5 work highlight wrappers");
        }

        private static int Set(Type owner, string field, string method)
        {
            try
            {
                FieldInfo f = AccessTools.Field(owner, field);
                MethodInfo m = AccessTools.Method(typeof(BtcWorkHighlight), method);
                if (f == null || m == null || !f.IsStatic) { BtcLog.Error("WorkHl", owner.Name + "." + field + " not found"); return 0; }
                Delegate cur = f.GetValue(null) as Delegate;
                if (cur != null && !cur.Method.Name.StartsWith("Highlight")) { BtcLog.Error("WorkHl", owner.Name + "." + field + " holds " + cur.Method.Name); return 0; }
                f.SetValue(null, Delegate.CreateDelegate(f.FieldType, m));
                return 1;
            }
            catch (Exception e) { BtcLog.Error("WorkHl", owner.Name + "." + field + ": " + e.Message); return 0; }
        }

        // ------------------------------------------------------------------ wrappers (same signatures as WorkNodesWrappers.Highlight*)
        private static Node Dig(Node node, DigGoal goal, Labor labor) { return Wrap(node, labor, goal.voxel.position, null, Yellow); }
        private static Node Repair(Node node, RepairGoal goal, Labor labor) { return Wrap(node, labor, goal.voxel.position, goal.gameObject, Yellow); }
        private static Node Transform(Node node, TransformGoal goal, Labor labor) { return Wrap(node, labor, goal.voxel.position, goal.gameObject, Yellow); }
        private static Node TargetRed(Node node, GameComponent target, Labor labor) { return Wrap(node, labor, target.voxel.position, target.gameObject, Color.red); }

        private static Node Wrap(Node node, Labor labor, XYZ pos, GameObject go, Color col)
        {
            Req r = new Req { Labor = labor, Pos = pos, Go = go, Col = col };
            Signal<GameObject> s = new Signal<GameObject>();
            return Node.Sequence.Label("HighlightTarget Sequence").Do(StartA, r, s).Label("HighlightAt").Do(node as StaticNode).Finally(StopA, s, r);
        }

        private static void Start(Req r, Signal<GameObject> s)
        {
            try
            {
                Faction f = r.Labor != null ? r.Labor.faction : null;
                bool sees = LocalSees(f);
                if (sees)
                {
                    if (r.Go != null) Highlighter.highlight.Object(r.Go, r.Col, s);
                    else Highlighter.highlight.At(r.Pos, r.Col, s);
                    LocalShown++;
                }
                else LocalHidden++;
                r.Conns.Clear();
                if (NetworkServer.active && BtcNetworkGate.Enabled && f != null) SendAdd(r, f);
                SentAdds += r.Conns.Count;
                if (_logged < 12) { _logged++; BtcLog.Info("WorkHl", "start " + (f != null ? f.name : "?") + " at " + r.Pos + (r.Go != null ? " obj" : "") + " local=" + sees + " sent=" + r.Conns.Count); }
            }
            catch (Exception e) { BtcLog.Error("WorkHl", "start: " + e.Message); }
        }

        private static void Stop(Signal<GameObject> s, Req r)
        {
            s.Invoke(null);
            if (r.Conns.Count == 0) return;
            try
            {
                HlMsg m = new HlMsg { Kind = KRemove, Id = r.Id };
                for (int i = 0; i < r.Conns.Count; i++)
                {
                    NetworkConnection c = Conn(r.Conns[i]);
                    if (c != null) c.Send(Msg, m);
                }
                r.Conns.Clear();
            }
            catch (Exception e) { BtcLog.Error("WorkHl", "stop: " + e.Message); }
        }

        private static bool LocalSees(Faction f)
        {
            User u = User.LocalUser;
            Faction v = u != null ? u.faction : null;
            if (v == null || v.IsSpectator) return true;
            return v == f;
        }

        // ------------------------------------------------------------------ host -> own faction's clients (BC ON)
        private static void SendAdd(Req r, Faction f)
        {
            if (UNetManager.instance == null) return;
            HlMsg m = null;
            for (int i = 0; i < NetworkServer.connections.Count; i++)
            {
                NetworkConnection c = NetworkServer.connections[i];
                if (c == null || !c.isConnected || BtcNetManager.IsLocalConnection(c) || BtcReplay.IsRec(c)) continue;
                User u = UNetManager.instance.GetUserFromConnection(c);
                if (u == null || u.faction != f) continue;
                if (m == null)
                {
                    r.Id = ++_nextId;
                    NetworkIdentity ni = r.Go != null ? r.Go.GetComponent<NetworkIdentity>() : null;
                    m = new HlMsg { Kind = ni != null ? KAddObj : KAddPos, Id = r.Id, Net = ni != null ? ni.netId.Value : 0u, Pos = r.Pos, Col = r.Col };
                }
                c.Send(Msg, m);
                r.Conns.Add(c.connectionId);
            }
        }

        /// <summary>Navigation.NavigateToOrAway prefix (host): vanilla spawns the move marker (UI.Borne: arrow + dot over the goal
        /// block) only for the host's own faction; send it to the clients of the walking unit's faction so they spawn it too.</summary>
        internal static void OnHostNavigate(Navigation nav, Location location)
        {
            if (!NetworkServer.active || !BtcNetworkGate.Enabled || location == null || location is CharacterLocation || UNetManager.instance == null) return;
            Faction f = nav.faction;
            User me = User.LocalUser;
            if (f == null || (me != null && me.faction == f) || nav.Labor == null || !nav.Labor.Autonomy.ShowMoveBorne) return;
            HlMsg m = null;
            for (int i = 0; i < NetworkServer.connections.Count; i++)
            {
                NetworkConnection c = NetworkServer.connections[i];
                if (c == null || !c.isConnected || BtcNetManager.IsLocalConnection(c) || BtcReplay.IsRec(c)) continue;
                User u = UNetManager.instance.GetUserFromConnection(c);
                if (u == null || u.faction != f) continue;
                if (m == null) m = new HlMsg { Kind = KBorne, Point = Architecte.SurfaceRaycast(location.Position, 1, false) };
                c.Send(Msg, m);
                SentBornes++;
            }
        }

        private static NetworkConnection Conn(int id)
        {
            for (int i = 0; i < NetworkServer.connections.Count; i++)
            {
                NetworkConnection c = NetworkServer.connections[i];
                if (c != null && c.connectionId == id) return c.isConnected ? c : null;
            }
            return null;
        }

        // ------------------------------------------------------------------ client
        private static readonly Dictionary<int, Signal<GameObject>> Shown = new Dictionary<int, Signal<GameObject>>();

        internal static void OnClientConnected(UNetManager m)
        {
            if (NetworkServer.active || !BtcNetworkGate.Enabled) return;
            try
            {
                ClearClient();
                NetworkClient c = m != null ? m.LocalNetworkClient : null;
                if (c == null) return;
                if (c.handlers.ContainsKey(Msg)) c.UnregisterHandler(Msg);
                c.RegisterHandler(Msg, OnMsg);
            }
            catch (Exception e) { BtcLog.Error("WorkHl", "client: " + e.Message); }
        }

        private static void ClearClient()
        {
            foreach (KeyValuePair<int, Signal<GameObject>> p in Shown) { try { p.Value.Invoke(null); } catch { } }
            Shown.Clear();
        }

        private static void OnMsg(NetworkMessage nm)
        {
            try
            {
                HlMsg m = nm.ReadMessage<HlMsg>();
                if (m.Kind == KBorne)
                {
                    ObjectPoolSingleton.Request_Unsafe(Brix.External.Factories.UI.Borne, m.Point, Quaternion.Euler(270f, 0f, 0f));
                    GotBornes++;
                    return;
                }
                Signal<GameObject> s;
                if (Shown.TryGetValue(m.Id, out s)) { Shown.Remove(m.Id); s.Invoke(null); }
                if (m.Kind == KRemove || Highlighter.highlight == null) return;
                if (Shown.Count > 2000) ClearClient();
                s = new Signal<GameObject>();
                GameObject go = m.Kind == KAddObj ? ClientScene.FindLocalObject(new NetworkInstanceId(m.Net)) : null;
                if (go != null) Highlighter.highlight.Object(go, m.Col, s);
                else Highlighter.highlight.At(m.Pos, m.Col, s);
                Shown[m.Id] = s;
                GotAdds++;
                if (_logged < 12) { _logged++; BtcLog.Info("WorkHl", "got " + m.Id + " at " + m.Pos + (go != null ? " obj" : "")); }
            }
            catch (Exception e) { BtcLog.Error("WorkHl", "msg: " + e.Message); }
        }

        private sealed class HlMsg : MessageBase
        {
            internal byte Kind;
            internal int Id;
            internal uint Net;
            internal XYZ Pos;
            internal Color Col;
            internal Vector3 Point;

            public override void Serialize(NetworkWriter w)
            {
                w.Write(Kind);
                if (Kind == KBorne) { w.Write(Point); return; }
                w.WritePackedUInt32((uint)Id);
                if (Kind == KRemove) return;
                w.WritePackedUInt32(Net);
                w.Write(Pos.x); w.Write(Pos.y); w.Write(Pos.z);
                w.Write((byte)Mathf.RoundToInt(Col.r * 255f)); w.Write((byte)Mathf.RoundToInt(Col.g * 255f)); w.Write((byte)Mathf.RoundToInt(Col.b * 255f));
            }

            public override void Deserialize(NetworkReader r)
            {
                Kind = r.ReadByte();
                if (Kind == KBorne) { Point = r.ReadVector3(); return; }
                Id = (int)r.ReadPackedUInt32();
                if (Kind == KRemove) return;
                Net = r.ReadPackedUInt32();
                int x = r.ReadInt32(), y = r.ReadInt32(), z = r.ReadInt32();
                Pos = new XYZ(x, y, z);
                Col = new Color(r.ReadByte() / 255f, r.ReadByte() / 255f, r.ReadByte() / 255f);
            }
        }
    }

    [HarmonyPatch(typeof(Navigation), "NavigateToOrAway")]
    internal static class WorkHighlightBornePatch
    {
        static void Prefix(Navigation __instance, Location location)
        {
            try { BtcWorkHighlight.OnHostNavigate(__instance, location); }
            catch (Exception e) { BtcLog.Error("WorkHl", "borne: " + e.Message); }
        }
    }

    /// <summary>Installs the BtcWorkHighlight wrappers before vanilla fills its wrapper caches (first SetupBuilder / node build).</summary>
    [HarmonyPatch]
    internal static class WorkHighlightInstallPatch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(DigGoal), "SetupBuilder");
            yield return AccessTools.Method(typeof(RepairGoal), "SetupBuilder");
            yield return AccessTools.Method(typeof(TransformGoal), "SetupBuilder");
            yield return AccessTools.Method(typeof(CombatNodes), "BreakObject");
            yield return AccessTools.Method(typeof(WorkNode), "Detonate");
        }
        static void Prefix()
        {
            try { BtcWorkHighlight.Install(); }
            catch (Exception e) { BtcLog.Error("WorkHl", "install: " + e.Message); }
        }
    }
}
