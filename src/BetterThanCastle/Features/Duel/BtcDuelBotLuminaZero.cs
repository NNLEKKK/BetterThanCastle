using System;
using System.Collections.Generic;
using Btc.Shared;
using UnityEngine;

namespace BetterThanCastle
{
    /// <summary>
    /// BreaktronZero (B0, formerly LuminaZero): neural duel bot. Fills an engine-neutral DuelView from the match, encodes it with DuelObs (lab/Btc.Shared),
    /// runs the trained MlpPolicy and sends the decoded orders through BtcDuel (same three orders as every other bot).
    /// This file is compiled into the mod AND into the lab simulator (lab/Btc.Bots, shadow types), so the exact same controller
    /// is evaluated in the sim and in the real game. Only use members that the shadows provide (see lab/Btc.Bots/SimShadow).
    /// </summary>
    internal sealed class BtcDuelBotLuminaZero : IDuelBot
    {
        internal const float DecisionStep = 0.1f;
        internal const float SkipDelay = 0.22f;          // same as the Chosen bot default (SkipDelayMs 220)

        /// <summary>Loaded policy (null = none available; the match then falls back to the scripted bot).</summary>
#if SIM
        [System.ThreadStatic]
#endif
        internal static MlpPolicy Policy;
#if SIM
        [System.ThreadStatic]
#endif
        internal static string PolicySource;
        /// <summary>false (default): sample actions from the policy distribution (it is trained that way; argmax plays much weaker). true: argmax.</summary>
#if SIM
        [System.ThreadStatic]
#endif
        internal static bool Argmax;
        /// <summary>Diagnostics (sim-vs-game parity): once per fight second log the observation and the action of one own unit (LZOBS).</summary>
#if SIM
        [System.ThreadStatic]
#endif
        internal static bool ObsDump;
        private float _dumpAt = -10f;

        private readonly BtcDuelMatch _m;
        private readonly int _side;
        private readonly DuelView _v = new DuelView();
        private readonly BtcDuelUnitRef[] _refs = new BtcDuelUnitRef[DuelView.MaxUnits];
        private readonly float[] _obs = new float[DuelObs.Size];
        private readonly byte[] _mask = new byte[DuelActions.Count];
        private readonly int[] _slots = new int[DuelObs.SlotCount];
        private readonly int[] _idx = new int[DuelView.MaxUnits];
        private readonly float[] _d = new float[DuelView.MaxUnits];
        private readonly int[] _act = new int[1];
        private readonly float[] _u = new float[1];
        private readonly bool _sample;
        private float[] _out, _scratch;
        private float _acc;
        private readonly float _step = DecisionStep;     // the policy's own decision period (METADATA of the .btcpol), fallback 0.1 s
        private readonly MlpPolicy _pol;
        private int _start0 = -1, _start1 = -1;

        internal BtcDuelBotLuminaZero(BtcDuelMatch m, int side)
        {
            _m = m; _side = side; _pol = Policy; _sample = !Argmax;
            if (_pol != null && _pol.DecisionSec > 0f) _step = _pol.DecisionSec;
            BtcDuel.GuardOrders[side] = false;
            if (_pol != null) { _out = new float[_pol.OutputSize]; _scratch = new float[_pol.ScratchSize]; }
        }

        internal static bool Ready { get { return Policy != null; } }

        public void OnStrike(BtcDuelUnitRef u)
        {
            if (u.Side != _side || !BtcDuel.SkipAnims) return;
            u.SkipAt = Time.time + SkipDelay;
        }

        public void Tick(float dt)
        {
            try
            {
                if (_pol == null) return;
                SkipTick();
                _acc += dt;
                if (_acc < _step) return;
                _acc = 0f;
                Decide();
            }
            catch (Exception e) { BtcLog.Error("B0", "Tick", e); }
        }

        private void SkipTick()
        {
            List<BtcDuelUnitRef> us = _m.Units;
            float now = Time.time;
            for (int i = 0; i < us.Count; i++)
            {
                BtcDuelUnitRef u = us[i];
                if (u.Side != _side || u.SkipAt < 0f || now < u.SkipAt) continue;
                u.SkipAt = -1f;
                if (u.Dead || u.StrikeTarget == null) continue;
                BtcDuelUnitRef t;
                if (!BtcDuel.TryGet(u.StrikeTarget, out t) || t.Dead) continue;
                if (t.Zone == (_side == 0 ? BtcDuelZone.SafeB : BtcDuelZone.SafeA)) continue;
                // move-attack onto the ground (resets the strike recovery), then refocus the same target (Chosen bot skip)
                BtcDuel.MoveAttack(u, u.Tf.position, true);
                BtcDuel.Attack(u, u.StrikeTarget, true);
            }
        }

        /// <summary>Fill the DuelView from the match (arena-local coordinates, fight-time clock).</summary>
        private void Fill()
        {
            DuelView v = _v;
            List<BtcDuelUnitRef> us = _m.Units;
            float now = Time.time, ft = _m.FightTime, t0 = now - ft;
            v.Len = BtcDuelMap.Len; v.Wid = BtcDuelMap.Wid; v.SafeLen = BtcDuelMap.SafeLen; v.NeutralLen = BtcDuelMap.NeutralLen; v.MidLen = BtcDuelMap.MidLen;
            v.Skips = BtcDuel.SkipAnims; v.InfiniteHeal = BtcDuelConfig.InfiniteHeal;
            v.Attacker = _side;     // B0 always plays the attacker role (lab roles: push, strike first)
            v.Time = ft;
            v.Escaping[0] = _m.Escaping[0]; v.Escaping[1] = _m.Escaping[1];
            v.Ctrl[0] = _m.Ctrl[0]; v.Ctrl[1] = _m.Ctrl[1];
            int n = Math.Min(us.Count, DuelView.MaxUnits), dead0 = 0, dead1 = 0, s0 = 0, s1 = 0;
            for (int i = 0; i < n; i++)
            {
                BtcDuelUnitRef u = us[i];
                _refs[i] = u;
                v.Side[i] = u.Side;
                if (u.Side == 0) s0++; else s1++;
                bool dead = u.Dead || u.Tf == null || u.Dr == null;
                v.Dead[i] = dead;
                if (dead) { if (u.Side == 0) dead0++; else dead1++; v.Hp[i] = 0f; v.TargetOf[i] = -1; continue; }
                Vector3 p = u.Tf.position, f = u.Tf.forward;
                v.X[i] = p.x - BtcDuelMap.X0; v.Z[i] = p.z - BtcDuelMap.Z0;
                v.VX[i] = u.Vel.x; v.VZ[i] = u.Vel.z;
                float fl = Mathf.Sqrt(f.x * f.x + f.z * f.z);
                v.FX[i] = fl > 1e-4f ? f.x / fl : 0f; v.FZ[i] = fl > 1e-4f ? f.z / fl : 0f;
                v.Hp[i] = u.Dr.CurrentHP; v.MaxHp[i] = u.MaxHp;
                v.StrikeAt[i] = u.StrikeAt - t0;
                v.HurtAt[i] = u.LastHurtAt - t0;
                v.OrdKind[i] = u.OrdKind;
                v.OrdAt[i] = u.OrdAt - t0;
                v.OrdHasDest[i] = u.OrdHasDest;
                v.OrdX[i] = u.OrdDest.x - BtcDuelMap.X0; v.OrdZ[i] = u.OrdDest.z - BtcDuelMap.Z0;
                v.Type[i] = (int)u.Kind;
                v.TargetOf[i] = -1;
                v.OrdTarget[i] = -1;
            }
            v.Count = n;
            if (_start0 < 0) { _start0 = s0; _start1 = s1; }
            v.StartUnits[0] = _start0; v.StartUnits[1] = _start1;
            v.Kills[0] = dead1; v.Kills[1] = dead0;
            // whom each unit attacks: the strike target if it struck within 1.3 s, else its current/pending task target
            for (int i = 0; i < n; i++)
            {
                if (v.Dead[i]) continue;
                BtcDuelUnitRef u = _refs[i];
                if (u.OrdKind == 2 && u.OrdTargetId >= 0)
                    for (int j = 0; j < n; j++) if (!v.Dead[j] && _refs[j].Id == u.OrdTargetId) { v.OrdTarget[i] = j; break; }
                GameObject tg = null;
                if (now - u.StrikeAt < 1.3f && u.StrikeTarget != null) tg = u.StrikeTarget;
                else if (u.Labor != null) { var task = u.Labor.GetCurrentOrPendingTask(); if (task != null) tg = task.Target as GameObject; }
                if (tg == null) continue;
                for (int j = 0; j < n; j++) if (!v.Dead[j] && (object)_refs[j].Go == (object)tg) { v.TargetOf[i] = j; break; }
            }
        }

        private void Decide()
        {
            Fill();
            DuelView v = _v;
            MlpPolicy pol = _pol;
            if (_out == null || _out.Length != pol.OutputSize) { _out = new float[pol.OutputSize]; _scratch = new float[pol.ScratchSize]; }
            for (int i = 0; i < v.Count; i++)
            {
                if (v.Dead[i] || v.Side[i] != _side) continue;
                DuelObs.Encode(v, i, _obs, 0, _slots, _idx, _d);
                DuelActions.Mask(v, i, _slots, _mask, 0);
                pol.Forward(_obs, _out, _scratch);
                if (_sample) { _u[0] = UnityEngine.Random.value; if (_u[0] >= 1f) _u[0] = 0.9999999f; pol.SampleWithRng(_out, _mask, _u, _act); }
                else pol.ArgmaxPerHead(_out, _mask, _act);
                int kind, target; float x, z;
                if (ObsDump && v.Time - _dumpAt >= 1f) { _dumpAt = v.Time; DumpObs(v, i, _act[0]); }
                DuelActions.Decode(v, i, _act[0], _slots, out kind, out x, out z, out target);
                if (DuelActions.IsRepeat(v, i, kind, x, z, target)) continue;
                BtcDuelUnitRef u = _refs[i];
                Vector3 dest = new Vector3(x + BtcDuelMap.X0, BtcDuelMap.Floor, z + BtcDuelMap.Z0);
                switch (kind)
                {
                    case DuelActions.OrdMove: BtcDuel.Move(u, dest); u.OrdKind = 3; break;
                    case DuelActions.OrdMoveAttack: BtcDuel.MoveAttack(u, dest); u.OrdKind = 1; break;
                    case DuelActions.OrdAttack: if (target >= 0) { BtcDuel.Attack(u, _refs[target].Go); u.OrdKind = 2; } break;
                }
            }
        }

        private void DumpObs(DuelView v, int i, int act)
        {
            var sb = new System.Text.StringBuilder(1200);
            sb.Append("u").Append(_refs[i].Id).Append(" side=").Append(_side).Append(" a=").Append(act).Append(" obs=");
            for (int k = 0; k < DuelObs.Size; k++)
            {
                if (k > 0) sb.Append(',');
                float o = _obs[k];
                sb.Append(o == 0f ? "0" : o.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            }
            BtcDuelLog.Line("LZOBS", sb.ToString());
        }

        // ---------------- policy loading (mod only) ----------------
#if !SIM
        /// <summary>Loads the policy once: user override &lt;game&gt;/BetterThanCastle/policies/breaktronzero.btcpol (old name luminazero.btcpol), else the embedded one.</summary>
        internal static bool EnsureLoaded()
        {
            if (Policy != null) return true;
            try
            {
                byte[] data = null;
                string dir = System.IO.Path.Combine(System.IO.Path.Combine(BepInEx.Paths.GameRootPath, "BetterThanCastle"), "policies"), file = System.IO.Path.Combine(dir, "breaktronzero.btcpol");
                if (!System.IO.File.Exists(file)) file = System.IO.Path.Combine(dir, "luminazero.btcpol");     // pre-rename name
                if (System.IO.File.Exists(file)) { data = System.IO.File.ReadAllBytes(file); PolicySource = file; }
                else
                {
                    using (System.IO.Stream s = typeof(BtcDuelBotLuminaZero).Assembly.GetManifestResourceStream("BetterThanCastle.luminazero.btcpol"))
                    {
                        if (s != null) { data = new byte[s.Length]; int r = 0; while (r < data.Length) { int k = s.Read(data, r, data.Length - r); if (k <= 0) break; r += k; } PolicySource = "embedded"; }
                    }
                }
                if (data == null) { BtcLog.Warn("B0", "no policy (embedded or policies/breaktronzero.btcpol)"); return false; }
                MlpPolicy p = MlpPolicy.Load(data);
                uint want = MlpPolicy.Fnv1a(DuelObs.Schema);
                if (p.SchemaHash != want) { BtcLog.Error("B0", "policy schema mismatch: " + p.SchemaHash + " != " + want + " (" + PolicySource + ")"); return false; }
                if (p.InputSize != DuelObs.Size || p.LogitCount != DuelActions.Count) { BtcLog.Error("B0", "policy shape mismatch in=" + p.InputSize + " logits=" + p.LogitCount); return false; }
                Policy = p;
                BtcLog.Info("B0", "policy loaded from " + PolicySource + " layers=" + p.NumLayers);
                return true;
            }
            catch (Exception e) { BtcLog.Error("B0", "load", e); return false; }
        }
#endif
    }
}
