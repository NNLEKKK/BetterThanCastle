#if DEV
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Brix.Audio;
using Brix.Game;
using Brix.Game.AI;
using Brix.Game.AI.Damage;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterThanCastle.Dev
{
    /// <summary>
    /// DEV lab for unit sound sync (BtcSoundNet). Counts every SoundEngine play on this side (source anim/code, event, netId, job)
    /// and every animation sound event before the vanilla camera gate, so host and client logs can be compared. Commands:
    /// sndnet spawn &lt;n&gt;  - host: n units of mixed kinds for the host and for the first other player, side by side;
    /// sndnet swap         - host: every second unit of those two factions switches kit (worker &lt;-&gt; knight / archer);
    /// sndnet dump &lt;tag&gt;  - writes BTClogs/&lt;role&gt;/snd-&lt;tag&gt;.txt (counts, sites, jobs) and resets the counters.
    /// </summary>
    internal static class BtcDevSndNet
    {
        internal static bool InAnim;
        internal static bool Relayed { get { return BtcSoundNet.DevRelaying; } }
        private static readonly Dictionary<string, int> Counts = new Dictionary<string, int>();
        private static readonly Dictionary<string, string> Sites = new Dictionary<string, string>();
        private static readonly Dictionary<uint, string> Switch = new Dictionary<uint, string>();

        internal static bool Cmd(string[] p)
        {
            string sub = p.Length > 1 ? p[1] : "dump";
            try
            {
                switch (sub)
                {
                    case "spawn": Spawn(p.Length > 2 ? int.Parse(p[2]) : 7); break;
                    case "swap": Swap(); break;
                    case "dump": Dump(p.Length > 2 ? p[2] : "x"); break;
                    case "fog": Fog(p.Length > 2 ? p[2] : "x"); break;
                    case "mine": SpawnMine(p.Length > 2 ? int.Parse(p[2]) : 4); break;
                    case "kill": KillMine(p.Length > 2 ? int.Parse(p[2]) : 2); break;
                    default: BtcDevLab.Lab("sndnet: unknown " + sub); break;
                }
            }
            catch (Exception e) { BtcDevLab.Lab("sndnet " + sub + " error: " + e); }
            return true;
        }

        private static uint Nid(GameObject go)
        {
            if (go == null) return 0u;
            NetworkIdentity ni = go.GetComponentInParent<NetworkIdentity>();
            return ni != null ? ni.netId.Value : 0u;
        }

        private static string Job(GameObject go)
        {
            Occupation o = go != null ? go.GetComponentInParent<Occupation>() : null;
            return o != null ? o.CurrentJob.ToString() : "-";
        }

        internal static void Note(string src, string name, GameObject emitter)
        {
            if (string.IsNullOrEmpty(name)) return;
            uint nid = Nid(emitter);
            string key = src + " " + name + " " + nid + " " + Job(emitter);
            int c;
            Counts.TryGetValue(key, out c);
            Counts[key] = c + 1;
            string sk = src + " " + name;
            if (!Sites.ContainsKey(sk)) Sites[sk] = Caller();
        }

        internal static void NoteSwitch(string group, string data, GameObject emitter)
        {
            if (group != "Profession") return;
            uint nid = Nid(emitter);
            if (nid != 0u) Switch[nid] = data;
        }

        private static string Caller()
        {
            StringBuilder sb = new StringBuilder();
            System.Diagnostics.StackFrame[] fr = new System.Diagnostics.StackTrace(2, false).GetFrames();
            int n = 0;
            for (int i = 0; fr != null && i < fr.Length && n < 4; i++)
            {
                System.Reflection.MethodBase m = fr[i].GetMethod();
                if (m == null || m.DeclaringType == null) continue;
                string t = m.DeclaringType.Name;
                if (t == "SoundEngine" || t.StartsWith("BtcDev") || m.Name.Contains("Patch")) continue;
                if (n > 0) sb.Append(" < ");
                sb.Append(t).Append('.').Append(m.Name);
                n++;
            }
            return sb.ToString();
        }

        private static void Dump(string tag)
        {
            List<string> keys = new List<string>(Counts.Keys);
            keys.Sort(StringComparer.Ordinal);
            StringBuilder sb = new StringBuilder();
            sb.Append("# role=").Append(NetworkServer.active ? "host" : "client").Append(" tag=").Append(tag).Append('\n');
            foreach (string k in keys) sb.Append("C ").Append(k).Append(' ').Append(Counts[k]).Append('\n');
            foreach (KeyValuePair<string, string> kv in Sites) sb.Append("S ").Append(kv.Key).Append(" @ ").Append(kv.Value).Append('\n');
            foreach (Occupation o in UnityEngine.Object.FindObjectsOfType<Occupation>())
            {
                uint nid = Nid(o.gameObject);
                if (nid == 0u) continue;
                string sw;
                Switch.TryGetValue(nid, out sw);
                Labor l = o.GetComponent<Labor>();
                string set = "-";
                try { set = l != null && l.Sounds != null ? l.Sounds.Death : "-"; } catch (Exception) { }
                sb.Append("J ").Append(nid).Append(' ').Append(o.CurrentJob).Append(' ').Append(o.CurrentOccupation).Append(" sw=").Append(sw ?? "-").Append(" set=").Append(set).Append('\n');
            }
            string path = System.IO.Path.Combine(BtcDevPaths.LogDir, "snd-" + tag + ".txt");
            File.WriteAllText(path, sb.ToString());
            BtcDevLab.Lab("sndnet dump " + tag + " keys=" + keys.Count + " -> " + path);
            Counts.Clear();
        }

        // fog mute breakdown per unit: why BtcSoundNet.Audible says yes/no
        private static void Fog(string tag)
        {
            Faction me = User.LocalUser != null ? User.LocalUser.faction : null;
            StringBuilder sb = new StringBuilder();
            sb.Append("# fog tag=").Append(tag).Append(" fogOn=").Append(BtcSoundNet.FogOn).Append(" me=").Append(me != null ? me.ID.ToString() : "-").Append('\n');
            foreach (Labor l in UnityEngine.Object.FindObjectsOfType<Labor>())
            {
                if (l == null) continue;
                GameObject go = l.gameObject;
                Brix.Game.Components.GameComponent gc = go.GetComponent<Brix.Game.Components.GameComponent>();
                Brix.Game.Components.LocalFactionVision v = gc != null ? gc.vision : null;
                Brix.Game.Components.LocalFactionVision vc = go.GetComponent<Brix.Game.Components.LocalFactionVision>();
                Faction uf = Faction.GetFaction(go);
                sb.Append("F ").Append(Nid(go)).Append(" iid=").Append(go.GetInstanceID())
                  .Append(" fac=").Append(uf != null ? uf.ID.ToString() : "-").Append(uf == me ? "(me)" : "")
                  .Append(" aud=").Append(BtcSoundNet.Audible(go))
                  .Append(" gcVis=").Append(v == null ? "null" : (v == vc ? "own" : "OTHER"))
                  .Append(" comp=").Append(vc == null ? "null" : "ok");
                if (vc != null && me != null)
                    sb.Append(" vis=").Append(vc.IsVisibleForFaction(me, true)).Append(" seeMe=").Append(vc.FactionsSeeingMe.Contains(me.ID))
                      .Append(" ownedBy=").Append(Affiliation.IsOwnedBy(go, me.gameObject)).Append(" allied=").Append(Affiliation.IsAllied(go, me.gameObject))
                      .Append(" facGo=").Append(Faction.GetFaction(me.gameObject) != null ? Faction.GetFaction(me.gameObject).ID.ToString() : "-")
                      .Append(" en=").Append(vc.enabled).Append(" req=").Append(vc.Requested);
                sb.Append(" pos=").Append(go.transform.position);
                AkGameObj ak = go.GetComponent<AkGameObj>();
                sb.Append(" ak=").Append(ak == null ? "null" : (ak.enabled ? "on" : "OFF"));
                try
                {
                    using (AkTransform t = new AkTransform())
                    {
                        AKRESULT r = AkSoundEngine.GetPosition(go, t);
                        AkVector q = t.Position();
                        Vector3 wp = new Vector3(q.X, q.Y, q.Z);
                        sb.Append(" wwise=").Append(r).Append(' ').Append(wp).Append(" d=").Append((wp - go.transform.position).magnitude.ToString("0.0"));
                    }
                    uint sw;
                    sb.Append(" swRes=").Append(AkSoundEngine.GetSwitch("Profession", go, out sw)).Append(" sw=").Append(sw);
                    sb.Append(" active=").Append(AkSoundEngine.GetIsGameObjectActive(go));
                    if (tag.EndsWith("post")) sb.Append(" post=").Append(AkSoundEngine.PostEvent("Bricktron_Message_Nothing", go));
                }
                catch (Exception e) { sb.Append(" akErr=").Append(e.GetType().Name); }
                sb.Append('\n');
            }
            string path = System.IO.Path.Combine(BtcDevPaths.LogDir, "fog-" + tag + ".txt");
            File.WriteAllText(path, sb.ToString());
            BtcDevLab.Lab("sndnet fog " + tag + " -> " + path);
        }

        private static void SpawnMine(int n)
        {
            Faction mine = User.LocalUser != null ? User.LocalUser.faction : null;
            if (mine == null) { BtcDevLab.Lab("sndnet mine: no faction"); return; }
            Vector3 c = Vector3.zero; int k = 0;
            foreach (Labor l in UnityEngine.Object.FindObjectsOfType<Labor>()) if (l.faction == mine) { c += l.transform.position; k++; }
            if (k > 0) c /= k;
            int x = 0;
            for (int i = 0; i < n; i++)
            {
                Brix.Engine.XYZ q = Brix.Engine.Architecte.PigerUneCase(c, 6f, 30);
                Vector3 pos = q.Equals(Brix.Engine.XYZ.zero) ? c + Vector3.up : q.ToVector3() + Vector3.up * 0.5f;
                GameObject go = BtcDuel.SpawnUnit(Kinds[i % Kinds.Length], mine, pos, Quaternion.identity);
                if (go != null) { x++; BtcDevLab.Lab("sndnet mine spawned nid=" + Nid(go) + " iid=" + go.GetInstanceID()); }
            }
            BtcDevLab.Lab("sndnet mine units=" + x + " at=" + c);
        }

        private static void KillMine(int n)
        {
            Faction mine = User.LocalUser != null ? User.LocalUser.faction : null;
            int k = 0;
            foreach (Labor l in UnityEngine.Object.FindObjectsOfType<Labor>())
            {
                if (k >= n || l == null || l.faction != mine) continue;
                Brix.Game.AI.Damage.BricktronDamageReceiver dr = l.GetComponent<Brix.Game.AI.Damage.BricktronDamageReceiver>();
                if (dr == null) continue;
                BtcDevLab.Lab("sndnet kill nid=" + Nid(l.gameObject) + " iid=" + l.gameObject.GetInstanceID());
                dr.ReceiveAttack(9999f, DamageType.Melee, DamageReaction.Twitch, l.transform.position, Vector3.zero, 0f, null);
                k++;
            }
            BtcDevLab.Lab("sndnet kill " + k);
        }

        private static void Sides(out Faction mine, out Faction other)
        {
            mine = User.LocalUser != null ? User.LocalUser.faction : null;
            other = null;
            // the other side = the non-host faction with the most units (the client's; a User list also holds AI/neutral factions)
            Dictionary<Faction, int> n = new Dictionary<Faction, int>();
            foreach (Labor l in UnityEngine.Object.FindObjectsOfType<Labor>())
            {
                if (l == null || l.faction == null || l.faction == mine) continue;
                int c; n.TryGetValue(l.faction, out c); n[l.faction] = c + 1;
            }
            int best = 0;
            foreach (KeyValuePair<Faction, int> kv in n) if (kv.Value > best) { best = kv.Value; other = kv.Key; }
        }

        private static readonly BtcDuelUnit[] Kinds = { BtcDuelUnit.Worker, BtcDuelUnit.Knight, BtcDuelUnit.Archer, BtcDuelUnit.Halberdier, BtcDuelUnit.Arbalist, BtcDuelUnit.Alchemist, BtcDuelUnit.Artificer };

        private static void Spawn(int n)
        {
            Faction mine, other;
            Sides(out mine, out other);
            if (mine == null || other == null) { BtcDevLab.Lab("sndnet spawn: factions " + (mine != null) + "/" + (other != null)); return; }
            Vector3 c = Vector3.zero; int k = 0;
            // around the client's units: the client sees only its own area (fog)
            foreach (Labor l in UnityEngine.Object.FindObjectsOfType<Labor>()) if (l.faction == other) { c += l.transform.position; k++; }
            if (k > 0) c /= k;
            int x = 0;
            for (int s = 0; s < 2; s++)
            {
                Faction f = s == 0 ? mine : other;
                Vector3 at = c + new Vector3(s == 0 ? -6f : 6f, 0f, 0f);
                for (int i = 0; i < n; i++)
                {
                    Brix.Engine.XYZ q = Brix.Engine.Architecte.PigerUneCase(at, 6f, 30);
                    Vector3 pos = q.Equals(Brix.Engine.XYZ.zero) ? at + Vector3.up : q.ToVector3() + Vector3.up * 0.5f;
                    if (BtcDuel.SpawnUnit(Kinds[i % Kinds.Length], f, pos, Quaternion.identity) != null) x++;
                }
            }
            BtcDevLab.Lab("sndnet spawn units=" + x + " at=" + c);
        }

        private static void Swap()
        {
            Faction mine, other;
            Sides(out mine, out other);
            int i = 0, done = 0;
            foreach (Labor l in UnityEngine.Object.FindObjectsOfType<Labor>())
            {
                if (l == null || (l.faction != mine && l.faction != other)) continue;
                if ((i++ & 1) == 0) continue;
                Occupation o = l.GetComponent<Occupation>();
                if (o == null) continue;
                Occupation.Type to = o.CurrentJob == Occupation.Job.Builder ? ((done & 1) == 0 ? Occupation.Type.Knight : Occupation.Type.Archer) : Occupation.Type.Builder;
                Toolbag tb = l.GetComponent<Toolbag>();
                Slot[] slots = { Slot.LeftHand, Slot.RightHand, Slot.Head, Slot.Back };
                for (int s = 0; s < slots.Length; s++) if (tb.ToolAt(slots[s]) != null) tb.UnequipTool(slots[s], Tool.OnDropBehaviours.Drop);
                o.SetOccupation(to, true);
                done++;
            }
            BtcDevLab.Lab("sndnet swap units=" + done);
        }
    }
}

namespace BetterThanCastle.Patches
{
    [HarmonyPatch(typeof(SoundEngine), "Play", new Type[] { typeof(string), typeof(GameObject) })]
    internal static class DevSndPlayPatch
    {
        // runs after the BtcSoundNet prefix: muted plays (fog, doubles) are counted as "mute"
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(string name, GameObject emitter, bool __runOriginal)
        {
            try { BetterThanCastle.Dev.BtcDevSndNet.Note(!__runOriginal ? "mute" : BetterThanCastle.Dev.BtcDevSndNet.InAnim ? "anim" : BetterThanCastle.Dev.BtcDevSndNet.Relayed ? "relay" : "code", name, emitter); } catch (Exception) { }
        }
    }

    [HarmonyPatch(typeof(SoundEngine), "Play", new Type[] { typeof(SoundEngine.PlayInfo) })]
    internal static class DevSndPlayInfoPatch
    {
        private static void Prefix(SoundEngine.PlayInfo info) { try { BetterThanCastle.Dev.BtcDevSndNet.Note("info", info.soundName, info.emitter); } catch (Exception) { } }
    }

    [HarmonyPatch(typeof(SoundEngine), "SetSwitch", new Type[] { typeof(string), typeof(string), typeof(GameObject) })]
    internal static class DevSndSwitchPatch
    {
        private static void Prefix(string group, string data, GameObject emitter) { try { BetterThanCastle.Dev.BtcDevSndNet.NoteSwitch(group, data, emitter); } catch (Exception) { } }
    }

    [HarmonyPatch(typeof(Locomotion4), "HandleEvent")]
    internal static class DevSndAnimEventPatch
    {
        private static void Prefix(Locomotion4 __instance, Brix.Game.AI.Behaviors.AnimationEvent ev)
        {
            BetterThanCastle.Dev.BtcDevSndNet.InAnim = true;
            try
            {
                if (ev == null || string.IsNullOrEmpty(ev.SoundEvent)) return;
                string name = ev.SoundEvent;
                if (ev.IsBricktronSoundEffect) name = __instance.Sounds.SoundsByName[ev.SoundEvent];
                BetterThanCastle.Dev.BtcDevSndNet.Note("animev", name, __instance.gameObject);
            }
            catch (Exception) { }
        }
        private static void Finalizer() { BetterThanCastle.Dev.BtcDevSndNet.InAnim = false; }
    }
}
#endif
