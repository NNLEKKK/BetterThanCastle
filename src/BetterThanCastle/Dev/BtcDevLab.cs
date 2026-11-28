#if DEV
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Brix.Game.AI;
using Brix.Game.AI.Nodes;
using UnityEngine;

namespace BetterThanCastle.Dev
{
    /// <summary>
    /// DEV lab for measuring duel mechanics (sim calibration). Commands (arena-local metres: x from the west wall, z from the south wall):
    /// duelo &lt;side&gt; &lt;slot|all&gt; move|attack|moveattack|stop|turn &lt;x&gt; &lt;z&gt; | &lt;targetSide&gt; &lt;targetSlot&gt;;
    /// duelplace &lt;side&gt; &lt;slot&gt; &lt;x&gt; &lt;z&gt; [facingDeg, 0 = +z, 90 = +x]; duelnobot 0|1 (no bot ticks); duelnorules 0|1 (no space control / escape win);
    /// duelcancel &lt;K|-1&gt; [attack|skip|moveattack|move] (side-0 strike re-ordered K sim frames after the strike started); dueldump (prefab numbers -> lab-dump.txt).
    /// Every action writes a LAB line into the duel log.
    /// </summary>
    internal sealed class BtcDevLab : MonoBehaviour
    {
        internal static bool NoBot, NoRules;
        internal static int CancelK = -1, CancelMode;
        private static BtcDevLab _inst;
        private static FieldInfo _fBot, _fBotP;
        private readonly Dictionary<int, float> _seen = new Dictionary<int, float>();

        private static void Ensure()
        {
            if (_inst != null) return;
            GameObject go = new GameObject("BtcDevLab");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            _inst = go.AddComponent<BtcDevLab>();
            _fBot = typeof(BtcDuelMatch).GetField("_bot", BindingFlags.Instance | BindingFlags.NonPublic);
            _fBotP = typeof(BtcDuelMatch).GetField("_botP", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private static float F(string[] p, int i, float def)
        {
            float v;
            return p.Length > i && float.TryParse(p[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) ? v : def;
        }

        private static Vector3 W(float x, float z) { return new Vector3(BtcDuelMap.X0 + x, BtcDuelMap.Floor, BtcDuelMap.Z0 + z); }

        private static List<BtcDuelUnitRef> Pick(int side, string slot)
        {
            List<BtcDuelUnitRef> r = new List<BtcDuelUnitRef>();
            if (BtcDuel.Match == null) return r;
            int k = 0, want = slot == "all" || slot.IndexOf(',') >= 0 ? -1 : (int)F(new[] { slot }, 0, 0f);
            string[] list = slot.IndexOf(',') >= 0 ? slot.Split(',') : null;
            foreach (BtcDuelUnitRef u in BtcDuel.Match.Units)
            {
                if (u.Side != side) continue;
                if ((list != null ? Array.IndexOf(list, k.ToString()) >= 0 : (want < 0 || k == want)) && !u.Dead && u.Go != null && u.Labor != null) r.Add(u);
                k++;
            }
            return r;
        }

        internal static void Lab(string msg) { BtcDuelLog.Line("LAB", msg); BtcLog.Info("Lab", msg); }

        private static string Loc(BtcDuelUnitRef u)
        {
            Vector3 p = u.Tf.position;
            return (p.x - BtcDuelMap.X0).ToString("0.00") + "," + (p.z - BtcDuelMap.Z0).ToString("0.00");
        }

        /// <summary>Returns true if the command was a lab command.</summary>
        internal static bool Cmd(string c, string[] p)
        {
            switch (c)
            {
                case "duelnobot": Ensure(); NoBot = F(p, 1, 1f) > 0.5f; return true;
                case "duelnorules": Ensure(); NoRules = F(p, 1, 1f) > 0.5f; return true;
                case "duelcancel":
                    Ensure(); CancelK = (int)F(p, 1, -1f);
                    { string m = p.Length > 2 ? p[2] : "attack"; CancelMode = m == "skip" ? 1 : m == "moveattack" ? 2 : m == "move" ? 3 : 0; }
                    Lab("cancel K=" + CancelK + " mode=" + CancelMode);
                    return true;
                case "dueldump": Dump(); return true;
                case "sendmap":
                    {
                        // sendmap: host sends the current map download to every client (map transfer speed lab)
                        Brix.Network.UNetManager m = Brix.Network.UNetManager.instance;
                        if (m == null || !UnityEngine.Networking.NetworkServer.active) { Lab("sendmap: not host"); return true; }
                        foreach (UnityEngine.Networking.NetworkConnection nc in UnityEngine.Networking.NetworkServer.connections)
                            if (nc != null && nc.connectionId != 0) { m.SendFiles(nc); Lab("sendmap conn " + nc.connectionId); }
                        return true;
                    }
                case "phantom": return BtcDevPhantom.Cmd(p);
                case "chain": return BtcDevChain.Cmd(p);
                case "animnet": return BtcDevAnimNet.Cmd(p);
                case "sndnet": return BtcDevSndNet.Cmd(p);
                case "wormward": return BtcDevWormWard.Cmd(p);
                case "shieldbar":
                    {
                        // shieldbar <side> <x> <z>: a base-shield barrier owned by that side's faction at an arena point (y of the side's first unit)
                        Ensure();
                        List<BtcDuelUnitRef> us = Pick((int)F(p, 1, 0f), "0");
                        if (us.Count == 0) { Lab("shieldbar: no unit"); return true; }
                        Vector3 w = W(F(p, 2, 50f), F(p, 3, 10f)); w.y = us[0].Tf.position.y;
                        Brix.Game.Faction f = us[0].Labor.faction;
                        BtcShield.DevAddBar(f != null ? f.ID : -1, w);
                        Lab("shieldbar side=" + F(p, 1, 0f) + " fac=" + (f != null ? f.ID : -1) + " at=" + w);
                        return true;
                    }
                case "shieldbox":
                    {
                        // shieldbox <side> <x> <z> <hx> <hy> <hz> [ox oz]: like shieldbar, plus a map box part (half sizes in blocks, centre offset ox/oz)
                        // bound to that point; a second call with the same point adds another part (the barrier is their union)
                        Ensure();
                        List<BtcDuelUnitRef> us = Pick((int)F(p, 1, 0f), "0");
                        if (us.Count == 0) { Lab("shieldbox: no unit"); return true; }
                        Vector3 w = W(F(p, 2, 50f), F(p, 3, 10f)); w.y = us[0].Tf.position.y;
                        int hx = (int)F(p, 4, 10f), hy = (int)F(p, 5, 10f), hz = (int)F(p, 6, 10f), ox = (int)F(p, 7, 0f), oz = (int)F(p, 8, 0f);
                        Brix.Engine.XYZ vc = Brix.Engine.XYZ.FromVector3(w);
                        BtcShieldBox.Box first = BtcShieldBox.For(w);
                        Vector3 key = first != null ? first.Crystal : w;
                        BtcShieldBox.Boxes.Add(new BtcShieldBox.Box { Crystal = key, X0 = vc.x + ox - hx, X1 = vc.x + ox + hx, Y0 = vc.y - hy, Y1 = vc.y + hy, Z0 = vc.z + oz - hz, Z1 = vc.z + oz + hz });
                        Brix.Game.Faction f = us[0].Labor.faction;
                        if (first == null) BtcShield.DevAddBar(f != null ? f.ID : -1, w);
                        Lab("shieldbox side=" + F(p, 1, 0f) + " at=" + w + " half=" + hx + "," + hy + "," + hz);
                        return true;
                    }
                case "duellift":
                    {
                        // duellift <side> <idx> <dy>: put units dy above where they stand (they fall)
                        Ensure();
                        foreach (BtcDuelUnitRef u in Pick((int)F(p, 1, 0f), p.Length > 2 ? p[2] : "0"))
                        {
                            Locomotion4 l = u.Go.GetComponent<Locomotion4>();
                            Vector3 w = u.Tf.position + Vector3.up * F(p, 3, 10f);
                            if (l != null) l.SetPosition(w, true); else u.Tf.position = w;
                            Lab("lift u" + u.Id + " y=" + u.Tf.position.y.ToString("0.0"));
                        }
                        return true;
                    }
                case "shielddump": Lab("shield " + (p.Length > 1 ? p[1] : "-") + " " + BtcShield.DevDump()); return true;
                case "duellog": Lab("mark " + (p.Length > 1 ? p[1] : "-")); return true;
                case "duelplace":
                    {
                        Ensure();
                        foreach (BtcDuelUnitRef u in Pick((int)F(p, 1, 0f), p.Length > 2 ? p[2] : "0"))
                        {
                            Vector3 w = W(F(p, 3, 50f), F(p, 4, 10f)); w.y = u.Tf.position.y;
                            try { Order.Move.HaltWhatever.Send(u.Labor, Labor.DecisionSource.User); } catch (Exception e) { BtcLog.Warn("Lab", "halt: " + e.Message); }
                            Locomotion4 l = u.Go.GetComponent<Locomotion4>();
                            if (p.Length > 5) u.Tf.rotation = Quaternion.Euler(0f, F(p, 5, 0f), 0f);
                            if (l != null) l.SetPosition(w, true); else u.Tf.position = w;
                            u.PrevPos = u.Tf.position;
                            Lab("place u" + u.Id + " to=" + F(p, 3, 50f).ToString("0.00") + "," + F(p, 4, 10f).ToString("0.00") + " now=" + Loc(u) + " y=" + u.Tf.position.y.ToString("0.000"));
                        }
                        return true;
                    }
                case "duelwhere":
                    {
                        // duelwhere <side> <slots> [minX]: positions; with minX also how many units are past x = minX
                        List<BtcDuelUnitRef> us = Pick((int)F(p, 1, 0f), p.Length > 2 ? p[2] : "all");
                        float minX = F(p, 3, -1f);
                        int past = 0;
                        StringBuilder sb = new StringBuilder();
                        foreach (BtcDuelUnitRef u in us)
                        {
                            sb.Append(" ").Append(Loc(u)).Append("y").Append(u.Tf.position.y.ToString("0.0"));
                            if (u.Tf.position.x - BtcDuelMap.X0 >= minX) past++;
                        }
                        Lab("where past=" + past + "/" + us.Count + sb + " detour checks=" + BtcEarlyDetour.Checks + " found=" + BtcEarlyDetour.Found + " ok=" + BtcEarlyDetour.Detoured + " fail=" + BtcEarlyDetour.Failed);
                        return true;
                    }
                case "duelwall":
                    {
                        // duelwall x0 z0 x1 z1 [height=2] [0 = remove]: stone cells on the arena floor (arena coordinates)
                        int x0 = Mathf.RoundToInt(BtcDuelMap.X0 + F(p, 1, 0f)), z0 = Mathf.RoundToInt(BtcDuelMap.Z0 + F(p, 2, 0f));
                        int x1 = Mathf.RoundToInt(BtcDuelMap.X0 + F(p, 3, 0f)), z1 = Mathf.RoundToInt(BtcDuelMap.Z0 + F(p, 4, 0f));
                        int h = (int)F(p, 5, 2f), y0 = Mathf.RoundToInt(BtcDuelMap.Floor);
                        List<Brix.Engine.XYZ> cells = new List<Brix.Engine.XYZ>();
                        for (int x = Mathf.Min(x0, x1); x <= Mathf.Max(x0, x1); x++)
                            for (int z = Mathf.Min(z0, z1); z <= Mathf.Max(z0, z1); z++)
                            {
                                int g = y0 + 2;
                                while (g > y0 - 8 && !Brix.Engine.Voxel.IsFull(new Brix.Engine.XYZ(x, g, z))) g--;
                                for (int y = g + 1; y <= g + h; y++) cells.Add(new Brix.Engine.XYZ(x, y, z));
                            }
                        BtcCarve.SetCells(cells, F(p, 6, 1f) > 0.5f ? (int)Brix.Engine.VoxelTerrainType.Stone : 0);
                        foreach (Brix.Engine.XYZ wc in cells) Brix.Engine.GameSignals.Invoke<Brix.Engine.XYZ>(Brix.Engine.GameSignals.TerrainChanged, wc);
                        int full = 0; foreach (Brix.Engine.XYZ wc in cells) if (Brix.Engine.Voxel.IsFull(wc)) full++;
                        Brix.Engine.XYZ pa = new Brix.Engine.XYZ(Mathf.Min(x0, x1) - 1, y0 - 1, (z0 + z1) / 2), pb = new Brix.Engine.XYZ(Mathf.Min(x0, x1), y0 - 1, (z0 + z1) / 2);
                        string col = ""; for (int yy = y0 - 4; yy <= y0 + 3; yy++) col += Brix.Engine.Voxel.IsFull(new Brix.Engine.XYZ(pa.x, yy, pa.z)) ? "#" : "."; Lab("col " + (y0 - 4) + ":" + col + " floor=" + BtcDuelMap.Floor.ToString("0.00"));
                        Lab("wall " + cells.Count + " cells y0=" + y0 + " full=" + full + " link=" + Brix.Game.AI.Pathfinding.PathNodeRegistry.PermissionVers(pa, pb) + " below=" + Brix.Engine.Voxel.IsFull(new Brix.Engine.XYZ(pa.x, pa.y - 1, pa.z)) + " at=" + Brix.Engine.Voxel.IsFull(pa));
                        return true;
                    }
                case "duelfire":
                    {
                        // duelfire <side> <slots> <x> <z>: manual fire at the floor block under arena x,z (like CmdFireAt at a voxel)
                        Ensure();
                        Brix.Engine.XYZ v = Brix.Engine.XYZ.FromVector3(W(F(p, 3, 50f), F(p, 4, 10f)));
                        v = new Brix.Engine.XYZ(v.x, v.y - 1, v.z);
                        foreach (BtcDuelUnitRef u in Pick((int)F(p, 1, 0f), p.Length > 2 ? p[2] : "all"))
                        {
                            try { Order.Combat.ShootAt.Send(u.Labor, Location.Of(new Vector3(v.x, v.y, v.z)), Labor.DecisionSource.User); }
                            catch (Exception e) { BtcLog.Warn("Lab", "fire: " + e.Message); }
                            Lab("fire u" + u.Id + " at " + v.x + "," + v.y + "," + v.z + " full=" + Brix.Engine.Voxel.IsFull(v));
                        }
                        return true;
                    }
                case "duelhurt":
                    {
                        // duelhurt <side> <slots> <hp>: plain damage without attacker (no stun, no smash)
                        foreach (BtcDuelUnitRef u in Pick((int)F(p, 1, 0f), p.Length > 2 ? p[2] : "all"))
                        {
                            Brix.Game.AI.Damage.BricktronDamageReceiver dr = u.Go.GetComponent<Brix.Game.AI.Damage.BricktronDamageReceiver>();
                            if (dr != null) dr.ApplyDamage(F(p, 3, 5f), null);
                            Lab("hurt u" + u.Id + " hp=" + (dr != null ? dr.CurrentHP.ToString("0") : "?"));
                        }
                        return true;
                    }
                case "dueltask":
                    {
                        // dueltask <side> <slots> <tag>: current task of each unit (+ shield path refusals)
                        StringBuilder sb = new StringBuilder();
                        foreach (BtcDuelUnitRef u in Pick((int)F(p, 1, 0f), p.Length > 2 ? p[2] : "all"))
                        {
                            Task t = u.Labor.CurrentTask;
                            sb.Append(" u").Append(u.Id).Append("=").Append(t != null ? t.ToString() + "/" + t.Status : "none").Append(" ").Append(Loc(u));
                        }
                        Lab("task " + (p.Length > 3 ? p[3] : "") + sb + " shieldRefused=" + BtcShield.PathRefused);
                        return true;
                    }
                case "duelo":
                    {
                        Ensure();
                        int side = (int)F(p, 1, 0f);
                        string kind = p.Length > 3 ? p[3].ToLowerInvariant() : "stop";
                        BtcDuelUnitRef tgt = null;
                        if (kind == "attack") { List<BtcDuelUnitRef> t = Pick((int)F(p, 4, 1f), p.Length > 5 ? p[5] : "0"); tgt = t.Count > 0 ? t[0] : null; }
                        Vector3 w = W(F(p, 4, 50f), F(p, 5, 10f));
                        foreach (BtcDuelUnitRef u in Pick(side, p.Length > 2 ? p[2] : "all"))
                        {
                            string what = kind;
                            switch (kind)
                            {
                                case "move": BtcDuel.Move(u, w, true); what += " " + F(p, 4, 0f) + "," + F(p, 5, 0f); break;
                                case "moveattack": BtcDuel.MoveAttack(u, w, true); what += " " + F(p, 4, 0f) + "," + F(p, 5, 0f); break;
                                case "turn": try { Order.Move.TurnTo.Send(u.Labor, new SpaceLocation(w, true), Labor.DecisionSource.User); } catch (Exception e) { BtcLog.Warn("Lab", "turn: " + e.Message); } break;
                                case "attack": if (tgt != null) { BtcDuel.Attack(u, tgt.Go, true); what += " u" + tgt.Id; } break;
                                default: try { Order.Move.HaltWhatever.Send(u.Labor, Labor.DecisionSource.User); } catch (Exception e) { BtcLog.Warn("Lab", "halt: " + e.Message); } break;
                            }
                            Lab("ord u" + u.Id + " " + what + " at=" + Loc(u));
                        }
                        return true;
                    }
            }
            return false;
        }

        private void Update()
        {
            BtcDuelMatch m = BtcDuel.Match;
            if (m == null || !BtcDuel.Active || m.State != BtcDuelState.Fight) return;
            if (NoBot)
            {
                if (_fBot != null && _fBot.GetValue(m) != null) { _fBot.SetValue(m, null); Lab("bot off"); }
                if (_fBotP != null && _fBotP.GetValue(m) != null) { _fBotP.SetValue(m, null); Lab("auto player off"); }
            }
            if (NoRules) { m.Ctrl[0] = m.Ctrl[1] = 0f; m.Escaping[0] = m.Escaping[1] = false; }
        }

        private void FixedUpdate()
        {
            BtcDuelMatch m = BtcDuel.Match;
            if (CancelK < 0 || m == null || !BtcDuel.Active || m.State != BtcDuelState.Fight) return;
            for (int i = 0; i < m.Units.Count; i++)
            {
                BtcDuelUnitRef u = m.Units[i];
                if (u.Side != 0 || u.Dead) continue;
                AttackInfo ai = u.StrikeAI as AttackInfo;
                if (ai == null || ai.Attacker != u.Labor) continue;
                float h;
                if (_seen.TryGetValue(u.Id, out h) && h == u.StrikeAt) continue;
                int d = ai.CurrentFrame - u.StrikeFrame;
                if (d < 0) { _seen[u.Id] = u.StrikeAt; continue; }   // AttackInfo was recycled by another order
                if (d < CancelK) continue;
                _seen[u.Id] = u.StrikeAt;
                if (u.StrikeTarget == null) continue;
                BtcDuelUnitRef t; BtcDuel.TryGet(u.StrikeTarget, out t);
                Lab("cancelorder u" + u.Id + " K=" + CancelK + " d=" + d + " ms=" + (int)((Time.time - u.StrikeAt) * 1000f) + " mode=" + CancelMode + " tgt=u" + (t != null ? t.Id : -1));
                switch (CancelMode)
                {
                    case 1: BtcDuel.MoveAttack(u, u.Tf.position, true); BtcDuel.Attack(u, u.StrikeTarget, true); break;
                    case 2: BtcDuel.MoveAttack(u, u.Tf.position, true); break;
                    case 3: BtcDuel.Move(u, u.Tf.position, true); break;
                    default: BtcDuel.Attack(u, u.StrikeTarget, true); break;
                }
            }
        }

        // ---------------- prefab numbers ----------------
        private static void Dump()
        {
            BtcDuelMatch m = BtcDuel.Match;
            if (m == null || m.Units.Count == 0) { BtcLog.Warn("Lab", "dump: no units"); return; }
            StringBuilder sb = new StringBuilder(64 * 1024); Visited.Clear();
            BtcDuelUnitRef u = m.Units[0];
            sb.Append("unit u").Append(u.Id).Append(" kind=").Append(u.Kind).Append(" fixedDt=").Append(Time.fixedDeltaTime).Append('\n');
            foreach (Component c in u.Go.GetComponents<Component>())
            {
                if (c == null) continue;
                string tn = c.GetType().FullName;
                if (tn.StartsWith("UnityEngine.") && !(c is Animator)) continue;
                sb.Append("== ").Append(tn).Append('\n');
                DumpObj(sb, c, "  ", 0);
                Animator an = c as Animator;
                if (an != null && an.runtimeAnimatorController != null)
                    foreach (AnimationClip cl in an.runtimeAnimatorController.animationClips)
                        if (cl != null) sb.Append("  clip ").Append(cl.name).Append(" len=").Append(cl.length.ToString("0.000")).Append(" fr=").Append(cl.frameRate).Append('\n');
            }
            string path = System.IO.Path.Combine(BtcDevPaths.LogDir, "lab-dump.txt");
            File.WriteAllText(path, sb.ToString());
            Lab("dump -> " + path + " bytes=" + sb.Length);
        }

        private static readonly List<object> Visited = new List<object>();
        private static void DumpObj(StringBuilder sb, object o, string ind, int depth)
        {
            if (o == null || depth > 4) return;
            for (int i = 0; i < Visited.Count; i++) if (ReferenceEquals(Visited[i], o)) return;
            Visited.Add(o);
            for (Type t = o.GetType(); t != null && t != typeof(MonoBehaviour) && t != typeof(object) && t != typeof(Component) && t != typeof(Behaviour); t = t.BaseType)
            {
                foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    Type ft = f.FieldType;
                    object v;
                    try { v = f.GetValue(o); } catch (Exception) { continue; }
                    if (ft.IsPrimitive || ft.IsEnum || ft == typeof(string) || ft == typeof(Vector3) || ft == typeof(Vector2))
                    {
                        sb.Append(ind).Append(t.Name).Append('.').Append(f.Name).Append('=').Append(v == null ? "null" : v.ToString()).Append('\n');
                    }
                    else if (v != null && !ft.IsArray && !typeof(UnityEngine.Object).IsAssignableFrom(ft) && !typeof(IEnumerable).IsAssignableFrom(ft) && !typeof(Delegate).IsAssignableFrom(ft)
                        && ft.Namespace != null && ft.Namespace.StartsWith("Brix") && depth < 4 && ft.IsClass)
                    {
                        sb.Append(ind).Append(t.Name).Append('.').Append(f.Name).Append(" : ").Append(ft.Name).Append('\n');
                        DumpObj(sb, v, ind + "  ", depth + 1);
                    }
                }
            }
        }
    }
}
#endif
