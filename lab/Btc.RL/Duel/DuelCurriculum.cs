using System.Globalization;
using System.Text;

namespace Btc.RL.Duel;

/// <summary>
/// Episode distribution + env knobs of SimDuelEnv. Every Reset draws units / arena / skips / heal / render fps from here.
/// Discrete choices use "v:w,v:w" (weighted), "a-b" (uniform integer range) or "a,b,c" (uniform) strings.
/// </summary>
public sealed class DuelCurriculum
{
    public int[] Units = { 1 }; public float[] UnitW = { 1f };
    public int[] Arena = { 1 }; public float[] ArenaW = { 1f };
    public float SkipsProb = 0.5f;      // P(rule "skips on")
    public float HealProb = 0.3f;       // P(rule "infinite heal")
    public float FpsMin = 30f, FpsMax = 90f;
    public float RenderJitter = 0f;     // SimParams.RenderJitter (per-frame dt noise)
    public float DomainRand = 0.5f;     // SimParams.Jittered scale per episode (latency, nav start, stance, strike timing, speed); render fps comes from FpsMin..FpsMax
    public float TimeLimit = 120f;      // sim draw after this many fight seconds (episodes always terminate)
    public float DecisionSec = 0.1f;    // LuminaZero decision period (BtcDuelBotLuminaZero.DecisionStep): render-time accumulator, reset to 0
    public float SkipDelay = 0.22f;     // External-side skip: SkipAt = strike time + this (BtcDuelBotLuminaZero.SkipDelay)
    public float Shaping = 1f;          // coefficient of the potential-based shaping (mutable between updates: anneal)
    public float Indiv = 0.1f;          // individual term: (dealt - taken) / MaxHp * Indiv
    public float Gamma = 0.997f;        // must equal PpoConfig.Gamma (potential-based shaping)
    public float OrderCost = 0f;        // reward penalty per order the learner issues (any action except Continue) for a live unit
    /// <summary>Handicap: with probability HandicapProb the opponent side (1) gets uniform HandicapMin..HandicapMax extra units (always in its favour; capped at SimDuelEnv.MaxUnitsPerSide).</summary>
    public float HandicapProb = 0f; public int HandicapMin = 1, HandicapMax = 3;
    /// <summary>Probability that a handicap favours the learner (side 0) instead of the opponent; 0 = always the opponent (eval cells).</summary>
    public float HandicapOwnProb = 0f;
    // ---- rewards v2 (all terms normalised by army size; intermediate terms are multiplied by InterScale, the trainer may anneal it) ----
    public float KillReward = 0.4f;     // +KillReward / N_enemyStart per enemy death, shared by the whole team
    public float DeathPenalty = 0.4f;   // -DeathPenalty / N_ownStart per own death (TeamShare mix: shared vs the unit that died)
    public float DmgDealt = 0.3f;       // +DmgDealt * dmg / (N_enemyStart * MaxHp)
    public float DmgTaken = 0.3f;       // -DmgTaken * dmg / (N_ownStart * MaxHp)
    public float TeamShare = 0.5f;      // damage/death terms: TeamShare * team-shared + (1 - TeamShare) * per-unit (the unit that dealt / took / died)
    public float InterScale = 1f;       // multiplier of every intermediate term (kill/death/damage; not terminal, not Shaping/Indiv)
    public float DrawReward = 0f;       // terminal reward of the learner for a draw (time limit); the reported outcome stays 0
    public float SafeWinReward = 1f;    // terminal reward for a win handed over by the opponent's safe-zone violation (entered / hit into the learner's safe zone); the reported outcome stays +1
    public float[] WinBonus;            // meta breaker: extra terminal reward for a learner win, indexed like SimDuelEnv.RoleCats (null = off)
    public int Roles = 0;               // 0 no roles; 1 random attacker side per episode (the attacker loses at the time limit); 2 side 0 (the learner) always attacks
    public float AttackProb = 0.5f;     // Roles 1: probability that the learner (side 0) is the attacker
    public float PassivePen = 0f;       // learner-attacker penalty per fight second without progress (no damage dealt, no closing of the gap to the enemy) after PassiveGrace
    public float PassiveGrace = 4f;     // seconds of no progress before the passive penalty starts
    /// <summary>Earlier curriculum stages: with probability EarlierProb an episode draws its units/arena from one of them (uniform).</summary>
    public DuelCurriculum[] Earlier;
    public float EarlierProb = 0f;

    public DuelCurriculum Clone() { var c = (DuelCurriculum)MemberwiseClone(); c.Earlier = null; c.EarlierProb = 0f; return c; }

    public static (int[] v, float[] w) ParseDist(string s)
    {
        var vs = new List<int>(); var ws = new List<float>();
        foreach (var part in s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int c = part.IndexOf(':'), d = part.IndexOf('-');
            if (c > 0) { vs.Add(int.Parse(part[..c], CultureInfo.InvariantCulture)); ws.Add(float.Parse(part[(c + 1)..], CultureInfo.InvariantCulture)); }
            else if (d > 0)
            {
                int a = int.Parse(part[..d], CultureInfo.InvariantCulture), b = int.Parse(part[(d + 1)..], CultureInfo.InvariantCulture);
                for (int k = a; k <= b; k++) { vs.Add(k); ws.Add(1f); }
            }
            else { vs.Add(int.Parse(part, CultureInfo.InvariantCulture)); ws.Add(1f); }
        }
        if (vs.Count == 0) throw new ArgumentException("empty distribution: " + s);
        return (vs.ToArray(), ws.ToArray());
    }

    public void SetUnits(string s) { (Units, UnitW) = ParseDist(s); foreach (var u in Units) if (u < 1 || u > SimDuelEnv.MaxUnitsPerSide) throw new ArgumentException("units 1.." + SimDuelEnv.MaxUnitsPerSide); }
    public void SetArena(string s) { (Arena, ArenaW) = ParseDist(s); foreach (var a in Arena) if (a != 1 && a != 3) throw new ArgumentException("arena 1 or 3"); }
    public void SetFps(string s)
    {
        int d = s.IndexOf('-');
        if (d > 0) { FpsMin = float.Parse(s[..d], CultureInfo.InvariantCulture); FpsMax = float.Parse(s[(d + 1)..], CultureInfo.InvariantCulture); }
        else FpsMin = FpsMax = float.Parse(s, CultureInfo.InvariantCulture);
    }

    public static int Pick(int[] v, float[] w, float u)
    {
        float tot = 0f; foreach (var x in w) tot += x;
        float r = u * tot;
        for (int i = 0; i < v.Length; i++) { r -= w[i]; if (r < 0f) return v[i]; }
        return v[^1];
    }

    public int MaxUnits { get { int m = 1; foreach (var u in Units) m = Math.Max(m, u); return m; } }

    public string Describe()
    {
        var sb = new StringBuilder();
        sb.Append("units=").Append(string.Join(",", Units.Select((u, i) => u + ":" + UnitW[i].ToString("0.##", CultureInfo.InvariantCulture))));
        sb.Append(" arena=").Append(string.Join(",", Arena.Select((u, i) => u + ":" + ArenaW[i].ToString("0.##", CultureInfo.InvariantCulture))));
        sb.Append(FormattableString.Invariant($" skips={SkipsProb:0.##} heal={HealProb:0.##} fps={FpsMin:0}-{FpsMax:0} tl={TimeLimit:0}s dr={DomainRand:0.##} dec={DecisionSec * 1000:0}ms shape={Shaping:0.##} indiv={Indiv:0.##} ordercost={OrderCost:0.####} handicap={HandicapProb:0.##}/+{HandicapMin}..{HandicapMax}/own{HandicapOwnProb:0.##} rew=kill{KillReward:0.##}/death{DeathPenalty:0.##}/dealt{DmgDealt:0.##}/taken{DmgTaken:0.##}/team{TeamShare:0.##}/inter{InterScale:0.##} draw{DrawReward:0.##} safewin{SafeWinReward:0.##} roles{Roles}/att{AttackProb:0.##}/passive{PassivePen:0.###}@{PassiveGrace:0}s"));
        if (Earlier != null && Earlier.Length > 0 && EarlierProb > 0f) sb.Append(FormattableString.Invariant($" earlier={Earlier.Length}x{EarlierProb:0.##}"));
        return sb.ToString();
    }
}
