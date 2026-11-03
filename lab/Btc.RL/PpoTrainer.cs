using System.Diagnostics;
using Btc.Shared;

namespace Btc.RL;

public sealed class UpdateStats
{
    public int Update;
    public long Step, Episodes;
    public int Samples;              // valid agent samples this update
    public string[] OppNames;
    public int[] OppEpisodes, OppWins, OppDraws, OppLosses;
    public double EpReward, EpLength; // means over episodes finished this update
    public int EpCount;
    public double Pg, Entropy, Kl, ClipFrac, VLoss, GradNorm, EntCoef;
    public double ExplVar, RetStd;   // critic explained variance 1 - Var(ret - v) / Var(ret) over valid samples (pre-update values); std of the returns
    public int MinibatchesDone;
    public double LrMult = 1;       // lr multiplier used by this update (KL-adaptive lr)
    public double RolloutSec, TrainSec;
    /// <summary>Rollout phase CPU seconds summed over workers: observe, policy inference, opponent act, env step; WorkerMax = slowest worker wall.</summary>
    public double TObs, TInfer, TOpp, TStep, WorkerMax, WorkerMin;
    public double TGae, TMinibatch, TAdam;
    public double[] OppSec;         // opponent Act CPU seconds per opponent index
    public double StepsPerSec => (RolloutSec + TrainSec) > 0 ? StepsThisUpdate / (RolloutSec + TrainSec) : 0;
    public double SamplesPerSec => (RolloutSec + TrainSec) > 0 ? Samples / (RolloutSec + TrainSec) : 0;
    public long StepsThisUpdate;
}

/// <summary>
/// PPO with a multi-head masked categorical actor and a separate centralized critic (input = obs ++ central features).
/// MAPPO-style parameter sharing: every active side-0 agent slot is one sample. Deterministic for a fixed thread count.
/// </summary>
public sealed class PpoTrainer
{
    public readonly PpoConfig Cfg;
    public readonly IEnvBatch Env;
    public readonly OpponentSpec[] Opps;
    public readonly Mlp Actor, Critic;
    public readonly Adam ActorOpt, CriticOpt;
    /// <summary>Optional per-episode opponent choice (null = env e always plays Opps[e % Opps.Length]).</summary>
    public readonly IOpponentSampler Sampler;
    public int UpdateCount;
    public long StepCount, EpisodeCount;
    /// <summary>Critic first layer as W_obs*obs + W_cen*central-per-team-group (identical math, ~25% fewer FLOPs). A/B switch for bench-train.</summary>
    public static bool SplitCritic = true;
    /// <summary>Opponents playing the same frozen policy snapshot within one rollout worker are evaluated as one batch.</summary>
    public static bool BatchOpp = true;
    /// <summary>Adaptive-LR multiplier (Cfg.KlAdapt > 0), applied to the actor lr (and the critic lr with Cfg.KlAdaptCritic).</summary>
    public float LrMult = 1f;

    readonly int N, T, M, O, C, L, H, CI;
    readonly int[] _heads;
    readonly Rng _rng;
    readonly int _threads;

    // rollout storage, slot = (t*N + e)*M + a
    readonly float[] _obs, _central, _logp, _value, _reward, _adv, _ret;
    readonly byte[] _mask;
    readonly int[] _act;
    readonly bool[] _active, _done; // _done[t*N+e]
    readonly float[] _lastValue; readonly bool[] _lastActive;
    int[] _valid; int _nValid;
    readonly int[] _mbRows;
    readonly int[] _envOpp;
    float _entCoef;

    readonly Worker[] _workers;
    readonly TrainShard[] _shards;
    readonly float[] _gA, _gC;

    public PpoTrainer(IEnvBatch env, OpponentSpec[] opps, PpoConfig cfg, IOpponentSampler sampler = null)
    {
        Cfg = cfg; Env = env; Opps = opps ?? Array.Empty<OpponentSpec>(); Sampler = sampler;
        if (sampler != null && env is not IOpponentSwitch) throw new ArgumentException("an opponent sampler needs an IOpponentSwitch env");
        if (env.HasOpponent && Opps.Length == 0) throw new ArgumentException("env needs an opponent");
        N = env.NumEnvs; T = cfg.Steps; M = env.MaxAgents; O = env.ObsSize; C = env.CentralSize;
        _heads = (int[])env.HeadSizes.Clone(); H = _heads.Length; L = 0; foreach (var h in _heads) L += h;
        CI = O + C;
        _threads = Math.Max(1, cfg.Threads);
        _rng = new Rng((ulong)cfg.Seed);

        Actor = new Mlp(O, cfg.HiddenSizes, L, cfg.HiddenAct);
        Critic = new Mlp(CI, cfg.CriticHiddenSizes, 1, cfg.HiddenAct);
        Actor.InitOrthogonal(_rng, MathF.Sqrt(2), 0.01f);
        Critic.InitOrthogonal(_rng, MathF.Sqrt(2), 1f);
        ActorOpt = new Adam(Actor.ParamCount); CriticOpt = new Adam(Critic.ParamCount);

        int S = T * N * M;
        _obs = new float[S * O]; _central = new float[Math.Max(1, S * C)]; _mask = new byte[S * L]; _act = new int[S * H];
        _logp = new float[S]; _value = new float[S]; _reward = new float[S]; _adv = new float[S]; _ret = new float[S];
        _active = new bool[S]; _done = new bool[T * N];
        _lastValue = new float[N * M]; _lastActive = new bool[N * M];
        _valid = new int[S]; _mbRows = new int[S];
        _envOpp = new int[N];

        // 2 workers per thread: the dynamic Parallel.For balances envs of uneven cost (opponent kind, army size)
        int W = Math.Min(N, _threads * 2);
        _workers = new Worker[W];
        for (int w = 0; w < W; w++)
        {
            int e0 = (int)((long)N * w / W), e1 = (int)((long)N * (w + 1) / W);
            _workers[w] = new Worker(this, e0, e1, Rng.Mix((ulong)cfg.Seed, (ulong)(1000 + w)));
        }
        int cap = (S + cfg.Minibatches - 1) / cfg.Minibatches / _threads + 2;
        _shards = new TrainShard[_threads];
        for (int i = 0; i < _threads; i++) _shards[i] = new TrainShard(this, cap);
        _gA = new float[Actor.ParamCount]; _gC = new float[Critic.ParamCount];

        Sampler?.BeginRollout();
        var r0 = new Rng(Rng.Mix((ulong)cfg.Seed, 4242));
        for (int e = 0; e < N; e++) ResetEnv(e, Rng.Mix((ulong)cfg.Seed, (ulong)(77 + e)), r0);
    }

    /// <summary>Chooses the opponent of env e for its next episode (sampler or the fixed e % K rule) and resets it.</summary>
    void ResetEnv(int e, ulong seed, Rng rng)
    {
        int oi = Opps.Length > 0 ? e % Opps.Length : 0;
        if (Sampler != null) { oi = Sampler.Pick(e, rng); ((IOpponentSwitch)Env).SetOpponent(e, oi); }
        _envOpp[e] = oi;
        Env.Reset(e, seed);
    }

    /// <summary>Restarts every env (e.g. after the curriculum stage was restored on resume).</summary>
    public void ResetAll(ulong salt)
    {
        Sampler?.BeginRollout();
        var r0 = new Rng(Rng.Mix((ulong)Cfg.Seed, salt));
        for (int e = 0; e < N; e++) ResetEnv(e, Rng.Mix(salt, (ulong)(UpdateCount * 7919L + e)), r0);
    }

    /// <summary>Opponent index env e is currently playing.</summary>
    public int EnvOpponent(int e) => _envOpp[e];

    public int[] Heads => _heads;

    /// <summary>Copies actor/critic weights from another trainer of the same shape and restarts both optimizers (exploiter reset).</summary>
    public void CopyWeightsFrom(PpoTrainer o)
    {
        Array.Copy(o.Actor.P, Actor.P, Actor.P.Length); Array.Copy(o.Critic.P, Critic.P, Critic.P.Length);
        foreach (var opt in new[] { ActorOpt, CriticOpt }) { Array.Clear(opt.M); Array.Clear(opt.V); opt.T = 0; }
    }

    public MlpPolicy ExportPolicy() => Actor.ToPolicy(_heads, false, MlpPolicy.Fnv1a(Env.Schema));

    ParallelOptions Par => new ParallelOptions { MaxDegreeOfParallelism = _threads };

    float CurrentLr(float baseLr)
    {
        if (!Cfg.LrAnneal || Cfg.TotalSteps <= 0) return baseLr;
        float frac = 1f - (float)((double)StepCount / Cfg.TotalSteps);
        return baseLr * Math.Max(frac, Cfg.LrFloor);
    }

    public UpdateStats Update()
    {
        var st = new UpdateStats
        {
            OppNames = Opps.Select(o => o.Name).ToArray(),
            OppEpisodes = new int[Math.Max(1, Opps.Length)], OppWins = new int[Math.Max(1, Opps.Length)],
            OppDraws = new int[Math.Max(1, Opps.Length)], OppLosses = new int[Math.Max(1, Opps.Length)],
            OppSec = new double[Math.Max(1, Opps.Length)],
        };
        var sw = Stopwatch.StartNew();
        Sampler?.BeginRollout();
        Parallel.For(0, _workers.Length, Par, w => _workers[w].Rollout());
        double epRew = 0, epLen = 0;
        foreach (var w in _workers)
        {
            for (int k = 0; k < st.OppEpisodes.Length; k++)
            {
                st.OppEpisodes[k] += w.EpN[k]; st.OppWins[k] += w.Wins[k]; st.OppDraws[k] += w.Draws[k]; st.OppLosses[k] += w.Losses[k];
            }
            epRew += w.EpRewSum; epLen += w.EpLenSum; st.EpCount += w.EpCount;
            double f = 1.0 / Stopwatch.Frequency;
            for (int k = 0; k < st.OppSec.Length; k++) st.OppSec[k] += w.OppTicks[k] * f;
            st.TObs += w.TObs * f; st.TInfer += w.TInfer * f; st.TOpp += w.TOpp * f; st.TStep += w.TStep * f;
            double wall = w.TWall * f;
            st.WorkerMax = Math.Max(st.WorkerMax, wall); st.WorkerMin = st.WorkerMin == 0 ? wall : Math.Min(st.WorkerMin, wall);
        }
        st.EpReward = st.EpCount > 0 ? epRew / st.EpCount : 0;
        st.EpLength = st.EpCount > 0 ? epLen / st.EpCount : 0;
        st.RolloutSec = sw.Elapsed.TotalSeconds;
        StepCount += (long)N * T; EpisodeCount += st.EpCount; st.StepsThisUpdate = (long)N * T;

        sw.Restart();
        ComputeGae();
        (st.ExplVar, st.RetStd) = ExplainedVariance();
        st.TGae = sw.Elapsed.TotalSeconds;
        if (Gpu != null) TrainGpu(st); else Train(st);
        st.TrainSec = sw.Elapsed.TotalSeconds;
        st.LrMult = LrMult;
        AdaptLr(st);
        UpdateCount++;
        st.Update = UpdateCount; st.Step = StepCount; st.Episodes = EpisodeCount; st.Samples = _nValid;
        return st;
    }

    /// <summary>KL-adaptive lr (rl_games style): KL > 2*target -> lr /= 1.5, KL < target/2 -> lr *= 1.5, clamped to [KlAdaptMin, KlAdaptMax]. Applies from the next update.</summary>
    void AdaptLr(UpdateStats st)
    {
        if (Cfg.KlAdapt <= 0f || st.MinibatchesDone == 0) return;
        if (st.Kl > 2.0 * Cfg.KlAdapt) LrMult /= 1.5f;
        else if (st.Kl < 0.5 * Cfg.KlAdapt) LrMult *= 1.5f;
        LrMult = Math.Clamp(LrMult, Cfg.KlAdaptMin, Cfg.KlAdaptMax);
    }

    /// <summary>
    /// Dead-agent credit (MAPPO with a shared team outcome). Without it an agent that dies mid-episode ends its trajectory on the
    /// death transition: it never sees the team's terminal +1/-1 (nor the team shaping after its death), so dying early in a won match
    /// looks the same as dying in a lost one. The env keeps writing the team reward into the slot of a dead agent (SimDuelEnv: terminal
    /// + team potential shaping; the individual term is 0 since a dead unit deals/takes nothing), so here every reward the dead slot
    /// receives after death is folded, discounted, into the agent's LAST live transition: r_death += sum_k gamma^k * r_(death+k).
    /// That transition stays terminal for GAE (next slot inactive), i.e. its target is the Monte-Carlo team return after death.
    /// If the rollout ends before the episode does, the tail is bootstrapped with the mean critic value of the still-live teammates
    /// (the centralized critic estimates the same team return). Episode ends (done) close the credit window; slots are untouched otherwise.
    /// </summary>
    public static void CreditDeadAgents(float[] reward, bool[] active, bool[] done, bool[] lastActive, float[] lastValue, int T, int N, int M, float gamma)
    {
        for (int e = 0; e < N; e++)
            for (int a = 0; a < M; a++)
            {
                int last = -1; bool open = false; float disc = 0f;
                for (int t = 0; t < T; t++)
                {
                    int s = (t * N + e) * M + a; bool d = done[t * N + e];
                    if (active[s]) { open = false; last = d ? -1 : s; continue; }
                    if (last >= 0)
                    {
                        if (!open) { open = true; disc = gamma; }
                        reward[last] += disc * reward[s]; disc *= gamma;
                    }
                    if (d) { open = false; last = -1; }
                }
                if (last >= 0 && !lastActive[e * M + a])
                {
                    if (!open) disc = gamma;
                    float sum = 0f; int k = 0;
                    for (int b = 0; b < M; b++) if (lastActive[e * M + b]) { sum += lastValue[e * M + b]; k++; }
                    if (k > 0) reward[last] += disc * sum / k;
                }
            }
    }

    /// <summary>GAE(lambda) per agent slot; an inactive next slot or done ends the trajectory.</summary>
    public static void Gae(float[] reward, float[] value, bool[] active, bool[] done, float[] lastValue, bool[] lastActive,
        float[] adv, float[] ret, int T, int N, int M, float gamma, float lambda)
    {
        float g = gamma, gl = gamma * lambda;
        for (int e = 0; e < N; e++)
            for (int a = 0; a < M; a++)
            {
                float nextV = lastValue[e * M + a], nextAdv = 0; bool nextActive = lastActive[e * M + a];
                for (int t = T - 1; t >= 0; t--)
                {
                    int s = (t * N + e) * M + a;
                    if (!active[s]) { nextActive = false; nextAdv = 0; nextV = 0; continue; }
                    bool cont = !done[t * N + e] && nextActive;
                    float nv = cont ? nextV : 0f, na = cont ? nextAdv : 0f;
                    float delta = reward[s] + g * nv - value[s];
                    float ad = delta + gl * na;
                    adv[s] = ad; ret[s] = ad + value[s];
                    nextV = value[s]; nextAdv = ad; nextActive = true;
                }
            }
    }

    void ComputeGae()
    {
        if (Cfg.DeadCredit) CreditDeadAgents(_reward, _active, _done, _lastActive, _lastValue, T, N, M, Cfg.Gamma);
        Gae(_reward, _value, _active, _done, _lastValue, _lastActive, _adv, _ret, T, N, M, Cfg.Gamma, Cfg.Lambda);
        _nValid = 0;
        int S = T * N * M;
        for (int s = 0; s < S; s++) if (_active[s]) _valid[_nValid++] = s;
        if (Cfg.NormAdv && _nValid > 1)
        {
            double mean = 0, var = 0;
            for (int i = 0; i < _nValid; i++) mean += _adv[_valid[i]];
            mean /= _nValid;
            for (int i = 0; i < _nValid; i++) { double d = _adv[_valid[i]] - mean; var += d * d; }
            float sd = (float)Math.Sqrt(var / _nValid) + 1e-8f;
            for (int i = 0; i < _nValid; i++) _adv[_valid[i]] = (float)((_adv[_valid[i]] - mean) / sd);
        }
    }

    /// <summary>Explained variance of the critic on this rollout: 1 - Var(ret - v) / Var(ret) (1 = perfect, 0 = no better than the
    /// mean, &lt; 0 = worse), plus the std of the GAE returns (the value-loss scale).</summary>
    public (double ev, double retStd) ExplainedVariance()
    {
        if (_nValid < 2) return (0, 0);
        double mr = 0, md = 0;
        for (int i = 0; i < _nValid; i++) { int s = _valid[i]; mr += _ret[s]; md += _ret[s] - _value[s]; }
        mr /= _nValid; md /= _nValid;
        double vr = 0, vd = 0;
        for (int i = 0; i < _nValid; i++) { int s = _valid[i]; double a = _ret[s] - mr, b = _ret[s] - _value[s] - md; vr += a * a; vd += b * b; }
        vr /= _nValid; vd /= _nValid;
        return (vr > 1e-12 ? 1 - vd / vr : 0, Math.Sqrt(vr));
    }

    void Train(UpdateStats st)
    {
        int nv = _nValid;
        if (nv == 0) return;
        int mbs = Math.Max(1, Cfg.Minibatches);
        int mbSize = (nv + mbs - 1) / mbs;
        double pg = 0, ent = 0, kl = 0, cf = 0, vl = 0, gn = 0; int n = 0, mbDone = 0;
        float lrA = CurrentLr(Cfg.Lr) * LrMult, lrC = CurrentLr(Cfg.CriticLr) * (Cfg.KlAdaptCritic ? LrMult : 1f);
        _entCoef = Cfg.EntAt(StepCount); st.EntCoef = _entCoef;
        bool stop = false;
        for (int ep = 0; ep < Cfg.Epochs && !stop; ep++)
        {
            for (int i = nv - 1; i > 0; i--) { int j = _rng.Next(i + 1); (_valid[i], _valid[j]) = (_valid[j], _valid[i]); }
            for (int m0 = 0; m0 < nv; m0 += mbSize)
            {
                int rows = Math.Min(mbSize, nv - m0);
                float scale = 1f / rows;
                int sh = Math.Min(_threads, rows);
                long tm0 = Stopwatch.GetTimestamp();
                // split critic: rows sorted by slot so the rows of one team group are adjacent and a shard encodes each group once (same row set, only the summation order changes)
                int[] src = _valid; int sOff = m0;
                if (SplitCritic && C > 0) { Array.Copy(_valid, m0, _mbRows, 0, rows); Array.Sort(_mbRows, 0, rows); src = _mbRows; sOff = 0; }
                Parallel.For(0, sh, Par, k =>
                {
                    int r0 = sOff + (int)((long)rows * k / sh), r1 = sOff + (int)((long)rows * (k + 1) / sh);
                    _shards[k].Run(src, r0, r1, scale);
                });
                long tm1 = Stopwatch.GetTimestamp();
                st.TMinibatch += (tm1 - tm0) / (double)Stopwatch.Frequency;
                var a = new ActorStats(); var c = new CriticStats();
                for (int k = 0; k < sh; k++)
                {
                    var s = _shards[k];
                    a.Pg += s.A.Pg; a.Ent += s.A.Ent; a.Kl += s.A.Kl; a.ClipFrac += s.A.ClipFrac; a.N += s.A.N; c.VLoss += s.C.VLoss;
                }
                double mbKl = a.Kl / Math.Max(1, a.N);
                if (Cfg.TargetKl > 0 && mbKl > 1.5 * Cfg.TargetKl) { stop = true; kl += mbKl * a.N; n += a.N; break; }
                Reduce(_gA, sh, true); Reduce(_gC, sh, false);
                gn += ActorOpt.Step(Actor.P, _gA, lrA, Cfg.MaxGradNorm);
                CriticOpt.Step(Critic.P, _gC, lrC, Cfg.MaxGradNorm);
                st.TAdam += (Stopwatch.GetTimestamp() - tm1) / (double)Stopwatch.Frequency;
                pg += a.Pg; ent += a.Ent; kl += a.Kl; cf += a.ClipFrac; vl += c.VLoss; n += a.N; mbDone++;
            }
        }
        int nn = Math.Max(1, n);
        st.Pg = pg / nn; st.Entropy = ent / nn; st.Kl = kl / nn; st.ClipFrac = cf / nn; st.VLoss = vl / nn;
        st.GradNorm = gn / Math.Max(1, mbDone); st.MinibatchesDone = mbDone;
    }

    // ---------------- GPU update (same algorithm as Train; network passes on the IPpoGpu backend) ----------------
    public IPpoGpu Gpu;
    /// <summary>Valid samples of the current update in their pre-shuffle order (GPU row ordinals).</summary>
    public int[] ValidOrig => _validOrig;
    public int ValidCount => _nValid;
    public float[] ObsBuffer => _obs;
    public float[] CentralBuffer => _central;
    public int ObsSize => O;
    public int CentralSize => C;
    public int Agents => M;
    int[] _validOrig, _ordOf, _mbOrd, _mbS; float[] _gLogits, _gValues, _gDz, _gDv;

    void TrainGpu(UpdateStats st)
    {
        int nv = _nValid;
        if (nv == 0) return;
        int S = T * N * M;
        if (_ordOf == null) { _ordOf = new int[S]; _validOrig = new int[S]; }
        Array.Copy(_valid, _validOrig, nv);
        for (int j = 0; j < nv; j++) _ordOf[_valid[j]] = j;
        // A driver reset (TDR, e.g. a game using the GPU) can leave the OpenCL context silently returning garbage: then every
        // update stopped at the first minibatch on KL ~1.4 and the run froze for hours (2026-11-01). Probe the GPU forward
        // against the CPU on a few rows every update; on a mismatch or an exception recreate the backend, else use the CPU path.
        for (int attempt = 0; ; attempt++)
        {
            string why;
            try { Gpu.BeginUpdate(this); why = GpuProbe(); }
            catch (Exception ex) { why = ex.GetType().Name + ": " + ex.Message; }
            if (why == null) break;
            GpuFailures++;
            Console.WriteLine($"gpu: {why} -> reset backend (attempt {attempt + 1})");
            if (attempt >= 2) { Console.WriteLine("gpu: still failing, this update runs on the CPU"); Train(st); return; }
            try { Gpu.Reset(); } catch (Exception ex) { Console.WriteLine("gpu: reset failed: " + ex.Message); Train(st); return; }
        }
        try { TrainGpuCore(st, nv); }
        catch (Exception ex) { GpuFailures++; Console.WriteLine("gpu: update failed (" + ex.GetType().Name + ": " + ex.Message + "), resetting the backend"); try { Gpu.Reset(); } catch { } }
    }

    /// <summary>GPU update failures recovered so far (probe mismatch / exceptions).</summary>
    public int GpuFailures;
    MlpWorkspace _probeA, _probeC;

    /// <summary>null = the GPU forward matches the CPU on the first rows of this update, else the reason.</summary>
    string GpuProbe()
    {
        int rows = Math.Min(32, _nValid);
        if (rows == 0) return null;
        if (_probeA == null) { _probeA = new MlpWorkspace(Actor, 32); _probeC = new MlpWorkspace(Critic, 32); }
        var ord = new int[rows]; var lg = new float[rows * L]; var vg = new float[rows];
        for (int j = 0; j < rows; j++) ord[j] = j;
        Gpu.Forward(ord, rows, lg, vg);
        for (int j = 0; j < rows; j++)
        {
            int s = _validOrig[j];
            Array.Copy(_obs, (long)s * O, _probeA.Input, j * O, O);
            Array.Copy(_obs, (long)s * O, _probeC.Input, j * CI, O);
            if (C > 0) Array.Copy(_central, (long)s * C, _probeC.Input, j * CI + O, C);
        }
        MlpOps.Forward(Actor, _probeA, rows); MlpOps.Forward(Critic, _probeC, rows);
        double dl = 0, dv = 0;
        for (int i = 0; i < rows * L; i++) { double d = Math.Abs(lg[i] - _probeA.Output[i]); if (!(d <= dl)) dl = double.IsNaN(d) ? double.PositiveInfinity : d; }
        for (int j = 0; j < rows; j++) { double d = Math.Abs(vg[j] - _probeC.Output[j]); if (!(d <= dv)) dv = double.IsNaN(d) ? double.PositiveInfinity : d; }
        return dl <= 2e-2 * (1 + MaxAbs(_probeA.Output, rows * L)) && dv <= 2e-2 * (1 + MaxAbs(_probeC.Output, rows)) ? null : FormattableString.Invariant($"probe mismatch (max |dlogit| {dl:E1}, |dvalue| {dv:E1})");
    }

    static double MaxAbs(float[] a, int n) { double m = 0; for (int i = 0; i < n; i++) m = Math.Max(m, Math.Abs(a[i])); return m; }

    void TrainGpuCore(UpdateStats st, int nv)
    {
        int mbs = Math.Max(1, Cfg.Minibatches);
        int mbSize = (nv + mbs - 1) / mbs;
        if (_mbOrd == null || _mbOrd.Length < mbSize)
        {
            _mbOrd = new int[mbSize]; _mbS = new int[mbSize];
            _gLogits = new float[mbSize * L]; _gDz = new float[mbSize * L]; _gValues = new float[mbSize]; _gDv = new float[mbSize];
        }
        double pg = 0, ent = 0, kl = 0, cf = 0, vl = 0, gn = 0; int n = 0, mbDone = 0;
        float lrA = CurrentLr(Cfg.Lr) * LrMult, lrC = CurrentLr(Cfg.CriticLr) * (Cfg.KlAdaptCritic ? LrMult : 1f);
        _entCoef = Cfg.EntAt(StepCount); st.EntCoef = _entCoef;
        bool stop = false;
        int chunks = _threads * 2;
        var aSt = new ActorStats[chunks]; var cSt = new CriticStats[chunks];
        for (int ep = 0; ep < Cfg.Epochs && !stop; ep++)
        {
            for (int i = nv - 1; i > 0; i--) { int j = _rng.Next(i + 1); (_valid[i], _valid[j]) = (_valid[j], _valid[i]); }
            for (int m0 = 0; m0 < nv; m0 += mbSize)
            {
                int rows = Math.Min(mbSize, nv - m0);
                float scale = 1f / rows;
                long tm0 = Stopwatch.GetTimestamp();
                for (int k = 0; k < rows; k++) { int s = _valid[m0 + k]; _mbS[k] = s; _mbOrd[k] = _ordOf[s]; }
                Gpu.Forward(_mbOrd, rows, _gLogits, _gValues);
                Array.Clear(aSt); Array.Clear(cSt);
                Parallel.For(0, chunks, Par, ci =>
                {
                    int r0 = (int)((long)rows * ci / chunks), r1 = (int)((long)rows * (ci + 1) / chunks);
                    PpoLoss.ActorGrad(_gLogits, _gDz, r0, r1, L, _heads, _mbS, _mask, _act, _logp, _adv, Cfg.Clip, _entCoef, scale, ref aSt[ci]);
                    PpoLoss.CriticGrad(_gValues, _gDv, r0, r1, _mbS, _ret, _value, Cfg.VClip, Cfg.VfCoef, scale, ref cSt[ci]);
                });
                var a = new ActorStats(); var c = new CriticStats();
                for (int k = 0; k < chunks; k++)
                {
                    a.Pg += aSt[k].Pg; a.Ent += aSt[k].Ent; a.Kl += aSt[k].Kl; a.ClipFrac += aSt[k].ClipFrac; a.N += aSt[k].N; c.VLoss += cSt[k].VLoss;
                }
                double mbKl = a.Kl / Math.Max(1, a.N);
                if (Cfg.TargetKl > 0 && mbKl > 1.5 * Cfg.TargetKl) { stop = true; kl += mbKl * a.N; n += a.N; st.TMinibatch += (Stopwatch.GetTimestamp() - tm0) / (double)Stopwatch.Frequency; break; }
                Gpu.Backward(rows, _gDz, _gDv, _gA, _gC);
                long tm1 = Stopwatch.GetTimestamp();
                st.TMinibatch += (tm1 - tm0) / (double)Stopwatch.Frequency;
                gn += ActorOpt.Step(Actor.P, _gA, lrA, Cfg.MaxGradNorm);
                CriticOpt.Step(Critic.P, _gC, lrC, Cfg.MaxGradNorm);
                Gpu.WeightsChanged(this);
                st.TAdam += (Stopwatch.GetTimestamp() - tm1) / (double)Stopwatch.Frequency;
                pg += a.Pg; ent += a.Ent; kl += a.Kl; cf += a.ClipFrac; vl += c.VLoss; n += a.N; mbDone++;
            }
        }
        int nn = Math.Max(1, n);
        st.Pg = pg / nn; st.Entropy = ent / nn; st.Kl = kl / nn; st.ClipFrac = cf / nn; st.VLoss = vl / nn;
        st.GradNorm = gn / Math.Max(1, mbDone); st.MinibatchesDone = mbDone;
    }

    void Reduce(float[] dst, int sh, bool actor)
    {
        int len = dst.Length, chunk = 4096;
        int nChunks = (len + chunk - 1) / chunk;
        Parallel.For(0, nChunks, Par, ci =>
        {
            int i0 = ci * chunk, i1 = Math.Min(len, i0 + chunk);
            Array.Copy(actor ? _shards[0].WA.Grad : _shards[0].WC.Grad, i0, dst, i0, i1 - i0);
            for (int k = 1; k < sh; k++)
            {
                var g = actor ? _shards[k].WA.Grad : _shards[k].WC.Grad;
                for (int i = i0; i < i1; i++) dst[i] += g[i];
            }
        });
    }

    // ---------------- rollout worker ----------------
    sealed class Worker
    {
        readonly PpoTrainer _t;
        public readonly int E0, E1;
        public readonly Rng Rng;
        readonly MlpWorkspace _wa, _wc;
        readonly IOpponent[] _opp;
        readonly OppBatcher _batch; readonly int[] _bEnv; readonly MlpPolicy[] _bPol; readonly bool[] _bSample, _bDone; readonly int[] _oAct;
        readonly int[] _a0, _a1;
        readonly float[] _rew, _epRew; readonly int[] _epLen;
        readonly float[] _bObs, _bCen; readonly byte[] _bMask; readonly bool[] _bAct;
        public int[] EpN, Wins, Draws, Losses;
        public double EpRewSum, EpLenSum; public int EpCount;
        public long TObs, TInfer, TOpp, TStep, TWall;   // Stopwatch ticks of the last rollout
        public long[] OppTicks;

        public Worker(PpoTrainer t, int e0, int e1, ulong seed)
        {
            _t = t; E0 = e0; E1 = e1; Rng = new Rng(seed);
            int rows = (e1 - e0) * t.M;
            _wa = new MlpWorkspace(t.Actor, rows); _wc = new MlpWorkspace(t.Critic, rows); if (SplitCritic && t.C > 0) _wc.EnableSplit(t.O, t.C); _live = new int[rows];
            _opp = new IOpponent[t.Opps.Length];
            for (int i = 0; i < _opp.Length; i++) _opp[i] = t.Opps[i].Create();
            int ne = e1 - e0;
            if (BatchOpp && t.Env.HasOpponent)
            {
                _batch = new OppBatcher(t.Env, ne); _bEnv = new int[ne]; _bPol = new MlpPolicy[ne]; _bSample = new bool[ne]; _bDone = new bool[ne]; _oAct = new int[ne * t.M * t.H];
            }
            _a0 = new int[t.M * t.H]; _a1 = new int[t.M * t.H]; _rew = new float[t.M];
            _epRew = new float[e1 - e0]; _epLen = new int[e1 - e0];
            _bObs = new float[rows * t.O]; _bCen = new float[Math.Max(1, rows * t.C)]; _bMask = new byte[rows * t.L]; _bAct = new bool[rows];
            int k = Math.Max(1, t.Opps.Length);
            EpN = new int[k]; Wins = new int[k]; Draws = new int[k]; Losses = new int[k]; OppTicks = new long[k];
        }

        /// <summary>Forward of the live rows only (dead/padding slots are most of the 18 per side): row r of the block is packed
        /// to workspace row j = _live[j] order; outputs are read back through _live.</summary>
        int Infer(float[] obs, float[] cen, bool[] active, int baseRow, int rows, bool actor)
        {
            var t = _t;
            int n = 0;
            for (int r = 0; r < rows; r++) if (active[baseRow + r]) _live[n++] = r;
            if (n == 0) return 0;
            float[] ai = _wa.Input, ci = _wc.Input;
            var cb = _wc.Cb; int ng = 0, lastG = -1;
            for (int j = 0; j < n; j++)
            {
                int r = baseRow + _live[j];
                if (actor) Array.Copy(obs, r * t.O, ai, j * t.O, t.O);
                if (cb != null)
                {
                    Array.Copy(obs, r * t.O, ci, j * t.O, t.O);
                    int g = _live[j] / t.M;   // team group = env of this worker
                    if (g != lastG) { lastG = g; Array.Copy(cen, r * t.C, cb.Cen, ng * t.C, t.C); ng++; }
                    cb.Grp[j] = ng - 1;
                }
                else
                {
                    Array.Copy(obs, r * t.O, ci, j * t.CI, t.O);
                    if (t.C > 0) Array.Copy(cen, r * t.C, ci, j * t.CI + t.O, t.C);
                }
            }
            if (cb != null) cb.G = ng;
            if (actor) MlpOps.Forward(t.Actor, _wa, n);
            MlpOps.Forward(t.Critic, _wc, n);
            return n;
        }
        readonly int[] _live;

        public void Rollout()
        {
            var t = _t; var env = t.Env;
            int M = t.M, H = t.H, L = t.L, N = t.N;
            Array.Clear(EpN); Array.Clear(Wins); Array.Clear(Draws); Array.Clear(Losses);
            EpRewSum = 0; EpLenSum = 0; EpCount = 0;
            int rows = (E1 - E0) * M;
            TObs = TInfer = TOpp = TStep = 0; Array.Clear(OppTicks);
            long w0 = Stopwatch.GetTimestamp(), c0, c1;
            for (int step = 0; step < t.T; step++)
            {
                int baseRow = (step * N + E0) * M;
                c0 = Stopwatch.GetTimestamp();
                for (int e = E0; e < E1; e++)
                {
                    int s = (step * N + e) * M;
                    env.Observe(e, 0, t._obs, s * t.O, t._mask, s * L, t._central, s * t.C, t._active, s);
                }
                c1 = Stopwatch.GetTimestamp(); TObs += c1 - c0;
                int nl = Infer(t._obs, t._central, t._active, baseRow, rows, true);
                for (int r = 0; r < rows; r++)
                {
                    int s = baseRow + r;
                    if (t._active[s]) continue;
                    for (int h = 0; h < H; h++) t._act[s * H + h] = 0;
                    t._logp[s] = 0; t._value[s] = 0;
                }
                for (int j = 0; j < nl; j++)
                {
                    int s = baseRow + _live[j];
                    t._logp[s] = Categorical.Sample(_wa.Output, j * L, t._heads, t._mask, s * L, Rng, t._act, s * H);
                    t._value[s] = _wc.Output[j];
                }
                c0 = Stopwatch.GetTimestamp(); TInfer += c0 - c1;
                if (_batch != null)
                {
                    long b0 = Stopwatch.GetTimestamp();
                    int nbo = 0;
                    for (int e = E0; e < E1; e++)
                    {
                        _bDone[e - E0] = false;
                        if (_opp[t._envOpp[e]] is PolicyOpponent po)
                        {
                            var pol = po.PolicyFor(e);
                            if (pol == null) continue;
                            _bEnv[nbo] = e; _bPol[nbo] = pol; _bSample[nbo] = po.Sample; _bDone[e - E0] = true; nbo++;
                        }
                    }
                    if (nbo > 0)
                    {
                        _batch.Run(env, nbo, _bEnv, _bPol, _bSample, Rng, _oAct);
                        long bt = Stopwatch.GetTimestamp() - b0; TOpp += bt;
                        for (int i = 0; i < nbo; i++) OppTicks[t._envOpp[_bEnv[i]]] += bt / nbo;
                    }
                }
                int bi = 0;
                for (int e = E0; e < E1; e++)
                {
                    int s = (step * N + e) * M;
                    Array.Copy(t._act, s * H, _a0, 0, M * H);
                    int oi = t._envOpp[e];
                    long o0 = Stopwatch.GetTimestamp();
                    if (env.HasOpponent)
                    {
                        if (_batch != null && _bDone[e - E0]) { Array.Copy(_oAct, bi * M * H, _a1, 0, M * H); bi++; }
                        else _opp[oi].Act(env, e, 1, Rng, _a1);
                    }
                    long o1 = Stopwatch.GetTimestamp(); TOpp += o1 - o0; OppTicks[oi] += o1 - o0;
                    env.Step(e, _a0, env.HasOpponent ? _a1 : null, _rew, 0, out bool done, out int outcome);
                    TStep += Stopwatch.GetTimestamp() - o1;
                    float rs = 0; int na = 0;
                    for (int a = 0; a < M; a++)
                    {
                        t._reward[s + a] = _rew[a];
                        if (t._active[s + a]) { rs += _rew[a]; na++; }
                    }
                    int li = e - E0;
                    _epRew[li] += na > 0 ? rs / na : 0; _epLen[li]++;
                    t._done[step * N + e] = done;
                    if (done)
                    {
                        EpN[oi]++;
                        if (outcome > 0) Wins[oi]++; else if (outcome < 0) Losses[oi]++; else Draws[oi]++;
                        EpRewSum += _epRew[li]; EpLenSum += _epLen[li]; EpCount++;
                        _epRew[li] = 0; _epLen[li] = 0;
                        t.Sampler?.Report(e, oi, outcome);
                        t.ResetEnv(e, Rng.NextU64(), Rng);
                    }
                }
            }
            // bootstrap values for the state after the last step
            for (int e = E0; e < E1; e++)
            {
                int r = (e - E0) * M;
                env.Observe(e, 0, _bObs, r * t.O, _bMask, r * L, _bCen, r * t.C, _bAct, r);
            }
            int nb = Infer(_bObs, _bCen, _bAct, 0, rows, false);
            for (int r = 0; r < rows; r++) { int i = E0 * M + r; t._lastActive[i] = _bAct[r]; t._lastValue[i] = 0f; }
            for (int j = 0; j < nb; j++) t._lastValue[E0 * M + _live[j]] = _wc.Output[j];
            TWall = Stopwatch.GetTimestamp() - w0;
        }

        public void Write(BinaryWriter w) { w.Write(Rng.State); }
        public void Read(BinaryReader r) { Rng.State = r.ReadUInt64(); }
    }

    // ---------------- training shard ----------------
    sealed class TrainShard
    {
        readonly PpoTrainer _t;
        public readonly MlpWorkspace WA, WC;
        readonly byte[] _mask; readonly int[] _act; readonly float[] _oldLogp, _adv, _ret, _oldV;
        public ActorStats A; public CriticStats C;
        readonly int[] _gmap, _gids;   // team group (= slot / M) -> index into the split critic central batch

        public TrainShard(PpoTrainer t, int cap)
        {
            _t = t;
            WA = new MlpWorkspace(t.Actor, cap); WC = new MlpWorkspace(t.Critic, cap);
            if (SplitCritic && t.C > 0) { WC.EnableSplit(t.O, t.C); _gmap = new int[t.T * t.N]; Array.Fill(_gmap, -1); _gids = new int[cap]; }
            _mask = new byte[cap * t.L]; _act = new int[cap * t.H];
            _oldLogp = new float[cap]; _adv = new float[cap]; _ret = new float[cap]; _oldV = new float[cap];
        }

        public void Run(int[] src, int r0, int r1, float scale)
        {
            var t = _t; int rows = r1 - r0;
            Array.Clear(WA.Grad); Array.Clear(WC.Grad);
            A = default; C = default;
            float[] xa = WA.Input, xc = WC.Input;
            var cb = WC.Cb; int ng = 0;
            for (int j = 0; j < rows; j++)
            {
                int s = src[r0 + j];
                Array.Copy(t._obs, s * t.O, xa, j * t.O, t.O);
                if (cb != null)
                {
                    Array.Copy(t._obs, s * t.O, xc, j * t.O, t.O);
                    int gid = s / t.M, gi = _gmap[gid];
                    if (gi < 0) { gi = ng++; _gmap[gid] = gi; _gids[gi] = gid; Array.Copy(t._central, s * t.C, cb.Cen, gi * t.C, t.C); }
                    cb.Grp[j] = gi;
                }
                else
                {
                    Array.Copy(t._obs, s * t.O, xc, j * t.CI, t.O);
                    if (t.C > 0) Array.Copy(t._central, s * t.C, xc, j * t.CI + t.O, t.C);
                }
                Array.Copy(t._mask, s * t.L, _mask, j * t.L, t.L);
                Array.Copy(t._act, s * t.H, _act, j * t.H, t.H);
                _oldLogp[j] = t._logp[s]; _adv[j] = t._adv[s]; _ret[j] = t._ret[s]; _oldV[j] = t._value[s];
            }
            PpoLoss.Actor(t.Actor, WA, rows, t._heads, _mask, _act, _oldLogp, _adv, t.Cfg.Clip, t._entCoef, scale, true, ref A);
            if (cb != null) cb.G = ng;
            PpoLoss.Critic(t.Critic, WC, rows, _ret, _oldV, t.Cfg.VClip, t.Cfg.VfCoef, scale, true, ref C);
            if (cb != null) for (int k = 0; k < ng; k++) _gmap[_gids[k]] = -1;
        }
    }

    // ---------------- checkpoints ----------------
    const uint CkptMagic = 0x4B435442; // "BTCK"
    const uint LrMagic = 0x4D52544C;   // "LTRM": optional trailer with the adaptive-lr multiplier

    public void SaveCheckpoint(string path, byte[] extra = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string tmp = path + ".tmp";
        using (var fs = File.Create(tmp))
        using (var w = new BinaryWriter(fs))
        {
            w.Write(CkptMagic); w.Write(1);
            w.Write(Cfg.ToText()); w.Write(Env.Schema);
            w.Write(UpdateCount); w.Write(StepCount); w.Write(EpisodeCount);
            w.Write(_rng.State);
            w.Write(_workers.Length); foreach (var wk in _workers) wk.Write(w);
            WriteFloats(w, Actor.P); ActorOpt.Write(w);
            WriteFloats(w, Critic.P); CriticOpt.Write(w);
            w.Write(extra?.Length ?? 0); if (extra != null) w.Write(extra);
            if (Cfg.KlAdapt > 0f) { w.Write(LrMagic); w.Write(LrMult); }   // optional trailer: old readers stop after the extra blob
        }
        File.Move(tmp, path, true);
    }

    /// <summary>Restores weights, optimizer and RNG state. Returns the "extra" blob (e.g. self-play snapshot) or null.</summary>
    public byte[] LoadCheckpoint(string path)
    {
        using var r = new BinaryReader(File.OpenRead(path));
        if (r.ReadUInt32() != CkptMagic) throw new InvalidDataException("not a checkpoint");
        r.ReadInt32(); r.ReadString();
        string schema = r.ReadString();
        if (schema != Env.Schema) throw new InvalidDataException($"schema mismatch: {schema} vs {Env.Schema}");
        UpdateCount = r.ReadInt32(); StepCount = r.ReadInt64(); EpisodeCount = r.ReadInt64();
        _rng.State = r.ReadUInt64();
        int nw = r.ReadInt32();
        for (int i = 0; i < nw; i++) { ulong s = r.ReadUInt64(); if (i < _workers.Length) _workers[i].Rng.State = s; }
        ReadFloats(r, Actor.P); ActorOpt.Read(r);
        ReadFloats(r, Critic.P); CriticOpt.Read(r);
        int ex = r.ReadInt32();
        // environments restart fresh episodes (their mid-episode state is not checkpointed)
        ResetAll(0x5EED);
        var blob = ex > 0 ? r.ReadBytes(ex) : null;
        LrMult = 1f;
        if (r.BaseStream.Length - r.BaseStream.Position >= 8 && r.ReadUInt32() == LrMagic) LrMult = r.ReadSingle();
        return blob;
    }

    /// <summary>Reads only the config text stored in a checkpoint.</summary>
    public static PpoConfig ReadCheckpointConfig(string path)
    {
        using var r = new BinaryReader(File.OpenRead(path));
        if (r.ReadUInt32() != CkptMagic) throw new InvalidDataException("not a checkpoint");
        r.ReadInt32();
        return PpoConfig.FromText(r.ReadString());
    }

    static void WriteFloats(BinaryWriter w, float[] a) { w.Write(a.Length); foreach (var x in a) w.Write(x); }
    static void ReadFloats(BinaryReader r, float[] a)
    {
        int n = r.ReadInt32();
        if (n != a.Length) throw new InvalidDataException("param size mismatch");
        for (int i = 0; i < n; i++) a[i] = r.ReadSingle();
    }
}
