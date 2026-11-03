using System.Globalization;

namespace Btc.Sim;

/// <summary>
/// Parser + replays of real duel logs (BtcDuelLog format `t|TAG|msg`, one match per file).
/// RulesParity: logged positions/hits/kills drive the sim rules only, END must match (winner, reason, time +-0.3 s).
/// OpenLoop: from each SNAP state, inject logged orders (ORD/BORD with destinations) and compare predicted positions/hp with later SNAPs.
/// </summary>
public static class LogReplay
{
    public sealed class Snap { public float T; public float[] X, Z, Hp; public bool[] Alive; }
    public enum EvKind : byte { Hit, Kill, Order }
    public sealed class Ev { public float T; public EvKind Kind; public int A = -1, B = -1; public float Dealt, Hp, X, Z; public SimOrder Order; public bool Bot; }

    public sealed class DuelLog
    {
        public string Path;
        public int X0, Z0, Scale = 1, N0, N1;
        public bool Skips, Heal, HasDest;
        public readonly List<int> Ids = new();            // log unit id per sim index
        public readonly Dictionary<int, int> Idx = new(); // log id -> sim index
        public readonly List<Snap> Snaps = new();
        public readonly List<Ev> Events = new();
        public float EndT = -1f; public int EndWinner = -3; public string EndReason = "";
    }

    static float F(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
    static string After(string msg, string key) { int i = msg.IndexOf(key, StringComparison.Ordinal); if (i < 0) return null; i += key.Length; int j = msg.IndexOf(' ', i); return j < 0 ? msg.Substring(i) : msg.Substring(i, j - i); }
    static int UId(string tok) => tok != null && tok.Length > 1 && tok[0] == 'u' && int.TryParse(tok.AsSpan(1), out int v) ? v : -1;

    public static DuelLog Parse(string path)
    {
        var L = new DuelLog { Path = path };
        var raw = new List<(int id, int side)>();
        string[] lines;
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var sr = new StreamReader(fs)) lines = sr.ReadToEnd().Replace("\r", "").Split('\n');
        bool started = false;
        foreach (string ln in lines)
        {
            int a = ln.IndexOf('|'); if (a < 0) continue;
            int b = ln.IndexOf('|', a + 1); if (b < 0) continue;
            string tag = ln.Substring(a + 1, b - a - 1), msg = ln.Substring(b + 1);
            if (!float.TryParse(ln.AsSpan(0, a), NumberStyles.Float, CultureInfo.InvariantCulture, out float t)) continue;
            switch (tag)
            {
                case "CFG":
                    L.X0 = int.Parse(After(msg, "arenaX0=") ?? "0", CultureInfo.InvariantCulture);
                    L.Z0 = int.Parse(After(msg, "Z0=") ?? "0", CultureInfo.InvariantCulture);
                    L.Skips = (After(msg, "skip=") ?? "True") == "True";
                    L.Heal = (After(msg, "heal=") ?? "False") == "True";
                    break;
                case "UNIT":
                {
                    string[] w = msg.Split(' ');
                    raw.Add((UId(w[0]), w[1] == "s1" ? 1 : 0));
                    break;
                }
                case "START":
                    started = true;
                    foreach (var r in raw.Where(r => r.side == 0)) { L.Idx[r.id] = L.Ids.Count; L.Ids.Add(r.id); }
                    L.N0 = L.Ids.Count;
                    foreach (var r in raw.Where(r => r.side == 1)) { L.Idx[r.id] = L.Ids.Count; L.Ids.Add(r.id); }
                    L.N1 = L.Ids.Count - L.N0;
                    break;
                case "SNAP":
                {
                    if (!started) break;
                    int n = L.Ids.Count;
                    var s = new Snap { T = t, X = new float[n], Z = new float[n], Hp = new float[n], Alive = new bool[n] };
                    foreach (string tok in msg.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    {
                        string[] p = tok.Split(':'); if (p.Length < 4) continue;
                        if (!L.Idx.TryGetValue(int.Parse(p[0], CultureInfo.InvariantCulture), out int ix)) continue;
                        string[] xz = p[2].Split(',');
                        s.X[ix] = F(xz[0]) - L.X0; s.Z[ix] = F(xz[1]) - L.Z0; s.Hp[ix] = F(p[3]); s.Alive[ix] = true;
                    }
                    L.Snaps.Add(s);
                    break;
                }
                case "HIT":
                {
                    string[] w = msg.Split(' ');
                    int gt = w[0].IndexOf('>');
                    var e = new Ev { T = t, Kind = EvKind.Hit, A = Map(L, UId(w[0].Substring(0, gt))), B = Map(L, UId(w[0].Substring(gt + 1))), Dealt = F(After(msg, "dmg=")), Hp = F(After(msg, "hp=")) };
                    if (e.B >= 0) L.Events.Add(e);
                    break;
                }
                case "KILL":
                {
                    string[] w = msg.Split(' ');
                    var e = new Ev { T = t, Kind = EvKind.Kill, A = Map(L, UId(w.Length > 3 ? w[3] : null)), B = Map(L, UId(w[0])) };
                    if (e.B >= 0) L.Events.Add(e);
                    break;
                }
                case "ORD":
                case "BORD":
                {
                    string[] w = msg.Split(' ');
                    int u = Map(L, UId(w[0])); if (u < 0 || w.Length < 2) break;
                    var e = new Ev { T = t, Kind = EvKind.Order, A = u, Bot = tag == "BORD" };
                    string to = After(msg, "to="), tgt = After(msg, ">u");
                    string d = w[1];
                    if (tgt != null && !d.StartsWith("HoldPosition", StringComparison.Ordinal)) { e.Order = SimOrder.Attack; e.B = Map(L, int.Parse(tgt, CultureInfo.InvariantCulture)); if (e.B < 0) break; }
                    else if (to != null && (d.StartsWith("RunToward", StringComparison.Ordinal) || d == "Move")) e.Order = SimOrder.Move;
                    else if (to != null && d.StartsWith("MoveAttack", StringComparison.Ordinal)) e.Order = SimOrder.MoveAttack;
                    else break;   // instinct packages / orders without destination: the sim produces or cannot replay them
                    if (to != null) { string[] xz = to.Split(','); e.X = F(xz[0]) - L.X0; e.Z = F(xz[1]) - L.Z0; L.HasDest = true; }
                    L.Events.Add(e);
                    break;
                }
                case "END":
                    if (msg.StartsWith("deactivate", StringComparison.Ordinal)) break;
                    string wn = After(msg, "winner=");
                    L.EndWinner = wn == "player" ? 0 : wn == "bot" ? 1 : -1;
                    int ri = msg.IndexOf("reason=", StringComparison.Ordinal), ti = msg.IndexOf(" t=", StringComparison.Ordinal);
                    L.EndReason = ri >= 0 && ti > ri ? msg.Substring(ri + 7, ti - ri - 7) : "";
                    L.EndT = F(After(msg, " t="));
                    break;
            }
        }
        if (L.Snaps.Count > 0)
        {
            float mx = 0f; var s0 = L.Snaps[0];
            for (int i = L.N0; i < L.Ids.Count; i++) if (s0.Alive[i]) mx = Math.Max(mx, s0.X[i]);
            for (int i = 0; i < L.N0; i++) if (s0.Alive[i]) mx = Math.Max(mx, s0.X[i] + 95f);
            L.Scale = Math.Max(1, (int)MathF.Round((mx + 2.5f) / 100f));
        }
        L.Events.Sort((p, q) => p.T.CompareTo(q.T));
        return L;
    }

    static int Map(DuelLog L, int id) => id >= 0 && L.Idx.TryGetValue(id, out int v) ? v : -1;

    public static List<string> FindLogs(string root)
    {
        var r = new List<string>();
        foreach (string dir in new[] { Path.Combine(root, "duel-logs"), Path.Combine(root, "duel-logs-kirusha") })
            if (Directory.Exists(dir)) r.AddRange(Directory.GetFiles(dir, "*.txt", SearchOption.AllDirectories));
        r.Sort(StringComparer.Ordinal);
        return r;
    }

    /// <summary>Repo-relative default: game/csmod/BetterThanCastle (searched upward from the current directory and the assembly).</summary>
    public static string DefaultLogRoot()
    {
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var d = new DirectoryInfo(start);
            while (d != null)
            {
                string p = Path.Combine(d.FullName, "game", "csmod", "BetterThanCastle");
                if (Directory.Exists(p)) return p;
                d = d.Parent;
            }
        }
        return null;
    }

    static DuelSim NewSim(DuelLog L, SimParams p, bool mechanics)
    {
        var sim = new DuelSim { P = p ?? SimParams.Default(), MechanicsEnabled = mechanics };
        var rules = new SimRules { UnitsPerSide = L.N0, UnitsSide1 = L.N1, ArenaScale = L.Scale, Skips = L.Skips, InfiniteHeal = L.Heal && mechanics, TimeLimit = 100000f, OriginX = L.X0, OriginZ = L.Z0 };
        sim.Reset(rules, 1);
        return sim;
    }

    static void Interp(DuelLog L, int k, float t, int u, out float x, out float z, out bool alive)
    {
        Snap a = L.Snaps[k];
        alive = a.Alive[u];
        x = a.X[u]; z = a.Z[u];
        if (k + 1 < L.Snaps.Count)
        {
            Snap b = L.Snaps[k + 1];
            if (b.Alive[u] && a.Alive[u] && b.T > a.T)
            {
                float f = Math.Clamp((t - a.T) / (b.T - a.T), 0f, 1f);
                x += (b.X[u] - x) * f; z += (b.Z[u] - z) * f;
            }
        }
        else if (k > 0 && a.Alive[u] && L.Snaps[k - 1].Alive[u] && t > a.T)
        {
            // after the last SNAP (the END usually comes 0-0.2 s later): extrapolate the last velocity for up to 0.4 s
            Snap p = L.Snaps[k - 1];
            float dt = a.T - p.T, f = Math.Min(t - a.T, 0.4f);
            if (dt > 0.01f) { x += (a.X[u] - p.X[u]) / dt * f; z += (a.Z[u] - p.Z[u]) / dt * f; }
        }
    }

    public sealed class ParityResult { public string File; public bool Usable, Pass; public int LogWinner, SimWinner; public string LogReason, SimReason; public float LogT, SimT; }

    static string Category(string r)
    {
        if (r == null) return "";
        if (r.Contains("entered the safe zone")) return "safe-entry";
        if (r.Contains("attacked a unit in the safe zone")) return "safe-hit";
        if (r.Contains("kill and escape")) return "kill-escape";
        if (r.Contains("space control")) return "space";
        if (r.Contains("all enemy units dead")) return "all-dead";
        if (r.Contains("everyone dead")) return "draw-dead";
        return r;
    }

    /// <summary>Rules-only replay: positions interpolated from SNAPs, HIT/KILL events through the rule hooks.</summary>
    public static ParityResult RulesParity(DuelLog L, SimParams p = null)
    {
        var res = new ParityResult { File = Path.GetFileName(L.Path), LogWinner = L.EndWinner, LogReason = L.EndReason, LogT = L.EndT };
        if (L.EndT < 0f || L.Snaps.Count < 2 || L.Ids.Count == 0) return res;
        res.Usable = true;
        var sim = NewSim(L, p, false);
        int k = 0, ev = 0, n = L.Ids.Count;
        float limit = L.EndT + 3f;
        while (!sim.Over && sim.Time < limit)
        {
            float t = (sim.Frame + 1) * DuelSim.FixedDt;
            while (k + 1 < L.Snaps.Count && L.Snaps[k + 1].T <= t) k++;
            for (int u = 0; u < n; u++)
            {
                if (sim.Dead[u]) continue;
                if (t < L.Snaps[0].T) continue;
                Interp(L, k, t, u, out float x, out float z, out bool alive);
                if (alive) sim.SetPosition(u, x, z);
            }
            while (ev < L.Events.Count && L.Events[ev].T <= t)
            {
                Ev e = L.Events[ev++];
                if (e.Kind == EvKind.Hit) sim.ExternalHit(e.A, e.B, e.Dealt, e.Hp);
                else if (e.Kind == EvKind.Kill) sim.ExternalKill(e.B, e.A);
            }
            sim.Step();
        }
        res.SimWinner = sim.Over ? sim.Winner : -3; res.SimReason = sim.Reason; res.SimT = sim.FightTime;
        res.Pass = sim.Over && sim.Winner == L.EndWinner && Category(sim.Reason) == Category(L.EndReason) && MathF.Abs(sim.FightTime - L.EndT) <= 0.3f;
        return res;
    }

    static bool HasTeleport(DuelLog L, int k, float tEnd)
    {
        for (int j = Math.Max(1, k - 1); j < L.Snaps.Count && L.Snaps[j - 1].T <= tEnd; j++)
        {
            Snap a = L.Snaps[j - 1], b = L.Snaps[j];
            float dt = Math.Max(0.05f, b.T - a.T);
            for (int u = 0; u < a.X.Length; u++)
            {
                if (a.Alive[u] != b.Alive[u] && b.Alive[u]) return true;   // respawn / revive
                if (!a.Alive[u] || !b.Alive[u]) continue;
                float dx = b.X[u] - a.X[u], dz = b.Z[u] - a.Z[u];
                if (dx * dx + dz * dz > 100f * dt * dt) return true;      // > 10 m/s
            }
        }
        return false;
    }

    public sealed class OpenLoopResult { public int Starts, Samples; public double PosRmse, PosRmseAtHorizon, HpRmse, StillRmseAtHorizon; public int HorizonSamples; }

    /// <summary>Sim trajectory of one logged unit when every logged order is replayed from SNAP `fromSnap` on (rules off), next to the
    /// logged positions: (t, game x, game z, sim x, sim z). Shows where order handling differs (e.g. order streams every 0.1 s).</summary>
    public static List<(float t, float gx, float gz, float sx, float sz)> TraceUnit(DuelLog L, int logId, float sec, int fromSnap = 0, SimParams p = null)
    {
        var res = new List<(float, float, float, float, float)>();
        if (!L.Idx.TryGetValue(logId, out int u) || L.Snaps.Count <= fromSnap) return res;
        var orders = L.Events.Where(e => e.Kind == EvKind.Order).ToList();
        Snap s0 = L.Snaps[fromSnap];
        var sim = NewSim(L, p, true);
        sim.RulesEnabled = false;
        for (int i = 0; i < L.Ids.Count; i++) sim.SetUnitState(i, s0.X[i], s0.Z[i], s0.Hp[i], !s0.Alive[i]);
        int oi = orders.FindIndex(o => o.T > s0.T); if (oi < 0) oi = orders.Count;
        float t0 = s0.T;
        for (int sk = fromSnap; sk < L.Snaps.Count && L.Snaps[sk].T <= t0 + sec; sk++)
        {
            float tn = L.Snaps[sk].T;
            while (t0 + sim.Time < tn)
            {
                while (oi < orders.Count && orders[oi].T <= t0 + sim.Time) { var o = orders[oi++]; sim.Order(o.A, o.Order, o.X, o.Z, o.B); }
                sim.Step();
            }
            res.Add((tn, L.Snaps[sk].X[u], L.Snaps[sk].Z[u], sim.X[u], sim.Z[u]));
        }
        return res;
    }

    /// <summary>Open-loop prediction from logged states with logged orders (needs logs with ORD/BORD destinations).</summary>
    public static OpenLoopResult OpenLoop(DuelLog L, float horizonSec, SimParams p = null, int strideSnaps = 5)
    {
        var r = new OpenLoopResult();
        if (!L.HasDest || L.Snaps.Count < 3) return r;
        double se = 0, seH = 0, seHp = 0, seS = 0;
        int n = L.Ids.Count;
        var orders = L.Events.Where(e => e.Kind == EvKind.Order).ToList();
        for (int k = 0; k < L.Snaps.Count - 1; k += Math.Max(1, strideSnaps))
        {
            Snap s0 = L.Snaps[k];
            if (L.EndT > 0 && s0.T + horizonSec > L.EndT) break;
            if (HasTeleport(L, k, s0.T + horizonSec)) continue;   // DEV lab placements are not logged as orders
            var sim = NewSim(L, p, true);
            sim.RulesEnabled = false;
            for (int u = 0; u < n; u++) sim.SetUnitState(u, s0.X[u], s0.Z[u], s0.Hp[u], !s0.Alive[u]);
            // re-issue every unit's last order before the start (state in flight is otherwise lost)
            for (int u = 0; u < n; u++)
            {
                if (!s0.Alive[u]) continue;
                Ev last = null;
                foreach (var o in orders) { if (o.T > s0.T) break; if (o.A == u && o.T > s0.T - 30f) last = o; }
                if (last != null) sim.Order(u, last.Order, last.X, last.Z, last.B);
            }
            int oi = orders.FindIndex(o => o.T > s0.T); if (oi < 0) oi = orders.Count;
            int sk = k + 1;
            r.Starts++;
            float t0 = s0.T;
            while (sk < L.Snaps.Count && L.Snaps[sk].T <= t0 + horizonSec + 1e-3f)
            {
                float tn = L.Snaps[sk].T;
                while (t0 + sim.Time < tn)
                {
                    while (oi < orders.Count && orders[oi].T <= t0 + sim.Time) { var o = orders[oi++]; sim.Order(o.A, o.Order, o.X, o.Z, o.B); }
                    sim.Step();
                }
                Snap sn = L.Snaps[sk];
                bool atH = L.Snaps.Count > sk + 1 && L.Snaps[sk + 1].T > t0 + horizonSec + 1e-3f;
                for (int u = 0; u < n; u++)
                {
                    if (!sn.Alive[u] || sim.Dead[u]) continue;
                    double dx = sim.X[u] - sn.X[u], dz = sim.Z[u] - sn.Z[u], e2 = dx * dx + dz * dz;
                    se += e2; r.Samples++;
                    double dh = sim.Hp[u] - sn.Hp[u]; seHp += dh * dh;
                    if (atH) { seH += e2; r.HorizonSamples++; double sx = s0.X[u] - sn.X[u], sz = s0.Z[u] - sn.Z[u]; seS += sx * sx + sz * sz; }
                }
                sk++;
            }
        }
        if (r.Samples > 0) { r.PosRmse = Math.Sqrt(se / r.Samples); r.HpRmse = Math.Sqrt(seHp / r.Samples); }
        if (r.HorizonSamples > 0) { r.PosRmseAtHorizon = Math.Sqrt(seH / r.HorizonSamples); r.StillRmseAtHorizon = Math.Sqrt(seS / r.HorizonSamples); }
        return r;
    }
}
