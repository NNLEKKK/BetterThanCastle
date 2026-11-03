using Btc.Shared;

namespace Btc.RL.Duel;

/// <summary>One frozen self-play snapshot of the learner.</summary>
public sealed class LeagueSnap
{
    public int Id, Update;
    public MlpPolicy Policy;
    public byte[] Bytes;
    public int Games, WinsX2;     // learner results vs this snapshot (win = 2, draw = 1); halved when Games > Window
    public bool Best;             // protected from eviction (snapshot taken at the best periodic eval)
    public bool Exploiter;        // main-exploiter snapshot (AlphaStar league): own eviction quota, never the "latest main"

    /// <summary>Learner win rate vs this snapshot with a Beta(1,1) prior (unknown = 0.5).</summary>
    public double WinRate => (WinsX2 * 0.5 + 1.0) / (Games + 2.0);
    /// <summary>PFSP "hard" weight: opponents the learner beats rarely are played more often.</summary>
    public double Weight => Math.Max(1e-3, (1.0 - WinRate) * (1.0 - WinRate));
}

/// <summary>
/// Opponent mix + self-play league. Opponents = named entries with fixed weights (scripted bots, frozen files, "self") plus an
/// optional "pool" entry: the pool's share of episodes is spread over the frozen snapshots with PFSP weights w = (1 - wr)^2.
/// Snapshots: Add() every N updates; the newest Keep non-best snapshots stay (+ the best). Sampling weights are frozen at
/// BeginRollout (trainer thread) so a rollout is deterministic; results are counted with Interlocked/locks (order-independent).
/// </summary>
public sealed class DuelLeague : IOpponentSampler
{
    public const int Window = 400;

    public readonly string[] Names;
    public readonly float[] Weights;       // mutable contents: SetWeights switches the mix per curriculum stage (takes effect at the next BeginRollout)
    public readonly int PoolIndex;          // index of "pool" in Names, -1 = none
    public readonly int HPoolIndex;         // index of "hpool" (league snapshots driven through HumanOpponent), -1 = none
    public int Keep = 10;
    public int NextId = 1;
    public readonly List<LeagueSnap> Snaps = new();

    readonly LeagueSnap[] _envSnap;          // snapshot env e is playing (pool episodes only)
    LeagueSnap[] _frozen = Array.Empty<LeagueSnap>();
    double[] _snapCum = Array.Empty<double>();
    double[] _oppCum;
    readonly object _lock = new();

    public DuelLeague(string[] names, float[] weights, int numEnvs)
    {
        if (names.Length != weights.Length) throw new ArgumentException("names/weights");
        Names = names; Weights = weights;
        PoolIndex = Array.IndexOf(names, "pool");
        HPoolIndex = Array.IndexOf(names, "hpool");
        _envSnap = new LeagueSnap[numEnvs];
        _oppCum = new double[names.Length];
    }

    public int Count => Snaps.Count;

    public void SetWeights(float[] w)
    {
        if (w.Length != Weights.Length) throw new ArgumentException("weights");
        lock (_lock) { Array.Copy(w, Weights, w.Length); }
    }

    /// <summary>Policy env e is playing (the "pool" opponent's per-env lookup). Never null once a snapshot exists.</summary>
    public MlpPolicy PolicyFor(int e) => _envSnap[e]?.Policy ?? (_frozen.Length > 0 ? _frozen[^1].Policy : null);

    public LeagueSnap SnapOf(int e) => _envSnap[e];

    /// <summary>Adds a main-exploiter snapshot (update = the main agent's update count when it was taken).</summary>
    public LeagueSnap AddExploiter(MlpPolicy p, int update)
    {
        var s = new LeagueSnap { Id = NextId++, Update = update, Policy = p, Bytes = p.Save(), Exploiter = true };
        lock (_lock) { Snaps.Add(s); Evict(); }
        return s;
    }

    /// <summary>Thread-safe copy of the snapshot list (league exploiter / meta breaker sample from it while the main thread adds).</summary>
    public LeagueSnap[] Snapshot() { lock (_lock) return Snaps.ToArray(); }

    LeagueSnap LatestMain()
    {
        for (int i = _frozen.Length - 1; i >= 0; i--) if (!_frozen[i].Exploiter) return _frozen[i];
        return _frozen[^1];
    }

    public LeagueSnap Add(MlpPolicy p, int update, bool best = false)
    {
        if (Snaps.Count > 0 && Snaps[^1].Update == update && !Snaps[^1].Exploiter)
        {
            // same update = same weights (periodic snapshot + best eval in one update): just flag it
            var l = Snaps[^1];
            if (best) { foreach (var o in Snaps) o.Best = false; l.Best = true; Evict(); }
            return l;
        }
        var s = new LeagueSnap { Id = NextId++, Update = update, Policy = p, Bytes = p.Save(), Best = best };
        lock (_lock)
        {
            if (best) foreach (var o in Snaps) o.Best = false;
            Snaps.Add(s);
            Evict();
        }
        return s;
    }

    void Evict()
    {
        int normal = Snaps.Count(x => !x.Best && !x.Exploiter);
        while (normal > Keep)
        {
            int i = Snaps.FindIndex(x => !x.Best && !x.Exploiter);
            Snaps.RemoveAt(i); normal--;
        }
        int ex = Snaps.Count(x => x.Exploiter);
        while (ex > KeepExploiters) { Snaps.RemoveAt(Snaps.FindIndex(x => x.Exploiter)); ex--; }
    }

    public void BeginRollout()
    {
        lock (_lock)
        {
            _frozen = Snaps.ToArray();
            _snapCum = new double[_frozen.Length];
            double acc = 0;
            for (int i = 0; i < _frozen.Length; i++) { acc += _frozen[i].Weight; _snapCum[i] = acc; }
            acc = 0;
            for (int k = 0; k < Names.Length; k++)
            {
                double w = (k == PoolIndex || k == HPoolIndex) && _frozen.Length == 0 ? 0 : Weights[k];
                acc += w; _oppCum[k] = acc;
            }
            if (acc <= 0) // only an empty pool: play it anyway (PolicyOpponent idles on a null policy) until the first snapshot exists
                for (int k = 0; k < Names.Length; k++) _oppCum[k] = k + 1;
        }
    }

    static int PickCum(double[] cum, double u)
    {
        double r = u * cum[^1];
        for (int i = 0; i < cum.Length; i++) if (r < cum[i]) return i;
        return cum.Length - 1;
    }

    /// <summary>Pool episodes: share vs the newest snapshot, share vs a uniformly drawn snapshot (anti-forgetting), rest PFSP.</summary>
    public double LatestShare = 0.35, UniformShare = 0.15;   // AlphaStar main agent: 35% self-play, 50% PFSP, 15% vs older players
    public int KeepExploiters = 8;

    public int Pick(int env, Rng rng)
    {
        int k = PickCum(_oppCum, rng.NextDouble());
        LeagueSnap s = null;
        if ((k == PoolIndex || k == HPoolIndex) && _frozen.Length > 0)
        {
            double u = rng.NextDouble();
            if (u < LatestShare) s = LatestMain();
            else if (u < LatestShare + UniformShare) s = _frozen[Math.Min(_frozen.Length - 1, (int)(rng.NextDouble() * _frozen.Length))];
            else s = _frozen[PickCum(_snapCum, rng.NextDouble())];
        }
        _envSnap[env] = s;
        return k;
    }

    public void Report(int env, int opp, int outcome)
    {
        var s = _envSnap[env];
        if (opp != PoolIndex || s == null) return;
        lock (_lock)
        {
            s.Games++; s.WinsX2 += outcome > 0 ? 2 : outcome == 0 ? 1 : 0;
            if (s.Games > Window) { s.Games /= 2; s.WinsX2 /= 2; }
        }
    }

    public string Describe()
    {
        if (Snaps.Count == 0) return "pool empty";
        return "pool " + string.Join(" ", Snaps.Select(s => FormattableString.Invariant($"{(s.Exploiter ? "x" : "#")}{s.Id}@u{s.Update}{(s.Best ? "*" : "")}:{s.WinRate:F2}/{s.Games}")));
    }

    // ---------------- persistence (checkpoint extra blob) ----------------
    const int FlagV2 = 0x40000000;   // set in the Keep slot: every snapshot carries an Exploiter flag

    public void Write(BinaryWriter w)
    {
        w.Write(Keep | FlagV2); w.Write(NextId); w.Write(Snaps.Count);
        foreach (var s in Snaps)
        {
            w.Write(s.Id); w.Write(s.Update); w.Write(s.Games); w.Write(s.WinsX2); w.Write(s.Best); w.Write(s.Exploiter);
            w.Write(s.Bytes.Length); w.Write(s.Bytes);
        }
    }

    public void Read(BinaryReader r)
    {
        bool v2 = (r.ReadInt32() & FlagV2) != 0; // Keep itself is a run option: the current value wins
        NextId = r.ReadInt32();
        int n = r.ReadInt32();
        Snaps.Clear();
        for (int i = 0; i < n; i++)
        {
            var s = new LeagueSnap { Id = r.ReadInt32(), Update = r.ReadInt32(), Games = r.ReadInt32(), WinsX2 = r.ReadInt32(), Best = r.ReadBoolean() };
            if (v2) s.Exploiter = r.ReadBoolean();
            s.Bytes = r.ReadBytes(r.ReadInt32());
            s.Policy = MlpPolicy.Load(s.Bytes);
            Snaps.Add(s);
        }
        Evict();
    }
}

/// <summary>
/// League exploiter's opponent sampler (AlphaStar): PFSP over every snapshot of the main league (main snapshots and all exploiters)
/// with its OWN win statistics, so it hunts the weaknesses of the whole league rather than of the newest main only.
/// </summary>
public sealed class LeagueView : IOpponentSampler
{
    readonly DuelLeague _l;
    readonly Dictionary<int, (int g, int w2)> _st = new();
    readonly LeagueSnap[] _env;
    readonly object _lock = new();
    LeagueSnap[] _frozen = Array.Empty<LeagueSnap>();
    double[] _cum = Array.Empty<double>();
    public int Window = 200;

    public LeagueView(DuelLeague l, int numEnvs) { _l = l; _env = new LeagueSnap[numEnvs]; }

    public MlpPolicy PolicyFor(int e) => _env[e]?.Policy ?? (_frozen.Length > 0 ? _frozen[^1].Policy : null);

    (int g, int w2) Stat(int id) { lock (_lock) return _st.TryGetValue(id, out var x) ? x : (0, 0); }
    public double WinRate(LeagueSnap s) { var (g, w2) = Stat(s.Id); return (w2 * 0.5 + 1.0) / (g + 2.0); }

    public void BeginRollout()
    {
        _frozen = _l.Snapshot();
        _cum = new double[_frozen.Length];
        double acc = 0;
        for (int i = 0; i < _frozen.Length; i++) { double wr = WinRate(_frozen[i]); acc += Math.Max(1e-3, (1 - wr) * (1 - wr)); _cum[i] = acc; }
    }

    public int Pick(int env, Rng rng)
    {
        LeagueSnap s = null;
        if (_frozen.Length > 0)
        {
            double r = rng.NextDouble() * _cum[^1];
            int i = 0; while (i < _cum.Length - 1 && r >= _cum[i]) i++;
            s = _frozen[i];
        }
        _env[env] = s;
        return 0;
    }

    public void Report(int env, int opp, int outcome)
    {
        var s = _env[env]; if (s == null) return;
        lock (_lock)
        {
            var (g, w2) = _st.TryGetValue(s.Id, out var x) ? x : (0, 0);
            g++; w2 += outcome > 0 ? 2 : outcome == 0 ? 1 : 0;
            if (g > Window) { g /= 2; w2 /= 2; }
            _st[s.Id] = (g, w2);
        }
    }

    /// <summary>Lowest win rate over the current league members with at least minGames games (1 if none qualify) and how many qualify.</summary>
    public (double min, int n, int total) MinWinRate(int minGames)
    {
        var snaps = _l.Snapshot();
        double m = 1; int n = 0;
        foreach (var s in snaps) { if (Stat(s.Id).g < minGames) continue; n++; m = Math.Min(m, WinRate(s)); }
        return (m, n, snaps.Length);
    }

    public void Clear() { lock (_lock) _st.Clear(); }
}
