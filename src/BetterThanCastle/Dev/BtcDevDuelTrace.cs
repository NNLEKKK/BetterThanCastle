#if DEV
using System.Collections.Generic;
using System.Text;
using Brix.Game.AI;
using Brix.Game.AI.Nodes;
using UnityEngine;

namespace BetterThanCastle.Dev
{
    /// <summary>
    /// DEV: per-FixedUpdate trace of every duel unit into the duel log (command "dueltrace 0|1").
    /// Line: f=frameCount ft=fixedTime u&lt;id&gt;:x,z:fx,fz:vx,vz:hp:task=..:af=CurrentFrame|-1:naf=NextAttackFrame:strk=0|1 (several units separated by a space).
    /// The unit's AttackInfo is learned from the AttackInfo.NextFrame postfix (DuelPatches) via Note().
    /// </summary>
    internal sealed class BtcDevDuelTrace : MonoBehaviour
    {
        internal static bool On;
        private static BtcDevDuelTrace _inst;
        private sealed class Ai { public AttackInfo A; public float At; }
        private static readonly Dictionary<int, Ai> Map = new Dictionary<int, Ai>();
        private readonly StringBuilder _sb = new StringBuilder(2048);

        internal static void Set(bool on)
        {
            On = on;
            if (on && _inst == null)
            {
                GameObject go = new GameObject("BtcDevDuelTrace");
                DontDestroyOnLoad(go);
                _inst = go.AddComponent<BtcDevDuelTrace>();
            }
        }

        /// <summary>Called from the AttackInfo.NextFrame postfix for duel units.</summary>
        internal static void Note(int laborGoId, AttackInfo ai)
        {
            if (!On) return;
            Ai e;
            if (!Map.TryGetValue(laborGoId, out e)) { e = new Ai(); Map[laborGoId] = e; }
            e.A = ai; e.At = Time.time;
        }

        private void FixedUpdate()
        {
            if (!On || !BtcDuel.Active || BtcDuel.Match == null || !BtcDuelLog.Open) return;
            List<BtcDuelUnitRef> us = BtcDuel.Match.Units;
            _sb.Length = 0;
            _sb.Append("f=").Append(Time.frameCount).Append(" ft=").Append(Time.fixedTime.ToString("0.000"));
            for (int i = 0; i < us.Count; i++)
            {
                BtcDuelUnitRef u = us[i];
                if (u.Dead || u.Tf == null || u.Dr == null) continue;
                Vector3 p = u.Tf.position, f = u.Tf.forward;
                string task = "-";
                if (u.Labor != null && u.Labor.CurrentTask != null && u.Labor.CurrentTask.Description != null) task = u.Labor.CurrentTask.Description;
                int af = -1, naf = -1, strk = 0;
                Ai e;
                if (Map.TryGetValue(u.Go.GetInstanceID(), out e) && e.A != null && Time.time - e.At < 0.15f && e.A.Attacker == u.Labor)
                { af = e.A.CurrentFrame; naf = e.A.NextAttackFrame; strk = e.A.Striking ? 1 : 0; }
                _sb.Append(" u").Append(u.Id).Append(':').Append(p.x.ToString("0.000")).Append(',').Append(p.z.ToString("0.000"))
                   .Append(':').Append(f.x.ToString("0.000")).Append(',').Append(f.z.ToString("0.000"))
                   .Append(':').Append(u.Vel.x.ToString("0.000")).Append(',').Append(u.Vel.z.ToString("0.000"))
                   .Append(':').Append((int)u.Dr.CurrentHP).Append(":task=").Append(task)
                   .Append(":af=").Append(af).Append(":naf=").Append(naf).Append(":strk=").Append(strk);
                // lab extension: target id, want flags, locomotion flags (R run W walk H halt K stance Y ready C crank S swing X react T turn O turn-on-spot A air)
                int tg = -1; char ws = '-';
                if (af >= 0)
                {
                    BtcDuelUnitRef tu;
                    if (e.A.MeleeTarget != null && BtcDuel.TryGet(e.A.MeleeTarget, out tu)) tg = tu.Id;
                    ws = e.A.WantStrike ? 'S' : (e.A.WantStep ? 'P' : '-');
                }
                _sb.Append(":tg=").Append(tg).Append(":w=").Append(ws).Append(":loc=");
                Locomotion4 l = u.Go.GetComponent<Locomotion4>();
                if (l != null)
                {
                    int n0 = _sb.Length;
                    if (l.Running) _sb.Append('R'); if (l.Walking) _sb.Append('W'); if (l.IsHalting) _sb.Append('H');
                    if (l.InCombatStance) _sb.Append('K'); if (l.IsReadyToStrike) _sb.Append('Y');
                    Brix.Game.Locomotion.Striking st = l.Fighting != null ? l.Fighting.Striking : null;
                    if (st != null) { if (st.IsCranking) _sb.Append('C'); if (st.IsSwinging) _sb.Append('S'); if (st.IsReacting) _sb.Append('X'); }
                    if (l.Fighting != null && l.Fighting.TurningTo != null && l.Fighting.TurningTo.IsTurning) _sb.Append('T');
                    if (l.TurningOnSpot) _sb.Append('O'); if (l.IsAirborne) _sb.Append('A');
                    if (_sb.Length == n0) _sb.Append('-');
                }
                _sb.Append(":y=").Append(p.y.ToString("0.00"));
            }
            BtcDuelLog.Line("TRACE", _sb.ToString());
        }
    }
}
#endif
