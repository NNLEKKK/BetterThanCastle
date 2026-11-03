using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Btc.RL;
using Btc.RL.Duel;
using Btc.Shared;

namespace Btc.Lab.Cli;

/// <summary>Real-duel commands on the simulator: train, eval, match, bench, view.</summary>
static partial class DuelCommands
{
    public const string Help =
        "  train --run NAME --opp prod4:3,prod2:1,hunter,self,pool:4 --units 1-3 --arena 1 --skips 0.5 --heal 0.3 [--fps 30-90 --minutes M --steps N --threads T --envs 64 --rollout 128 --hidden 128 --layers 2 --critic-hidden 128 --critic-layers 2 --shape 1 --shape-anneal STEPS --indiv 0.1 --order-cost 0.002 --time-limit 120 --print 5 --ckpt-every 25 --ckpt-keep 0 --snap 20 --pool-keep 10 --ent 0.01 --ent-end E --ent-anneal STEPS --dead-credit 1 --eval-every 0 --eval-n 40 --eval-opp prod4 --eval-argmax --eval-seed 777 --auto-cur 0 --cur-threshold 0.8 --init-policy P.btcpol --rollback 0.15 --rollback-n 2 --cur-earlier 0.2 --stage 0 --roles 0/1/2 --attack-prob 0.5 --passive 0.03 --passive-grace 4 --resume --<PpoConfig field> v]\n" +
        "  bench-train [--ckpt F | --run NAME --resume] [--updates 5 --warmup 1 --tile2 0/1 --split-critic 0/1 --batch-opp 0/1] + train flags: N updates, nothing saved\n" +
        "  train ... [--kl-adapt TARGET_KL --kl-adapt-min .1 --kl-adapt-max 10 --kl-adapt-critic 0/1]  adaptive actor lr by KL (0 = off); trailer in the checkpoint keeps the multiplier\n" +
        "  train --preset luminazero [--run luminazero ...]  LuminaZero (v2) env: units 4-15 arena 3, skips .5 heal .5, handicap p=.4 (+1..3 for the opponent), rewards v2 (--kill .4 --death .4 --dmg-dealt .3 --dmg-taken .3 --team-share .5, intermediate scale --inter-start 1 --inter-end .3 --inter-anneal STEPS), shape 0, indiv 0, hidden 256x2 / critic 256x2, no auto curriculum, 8-cell panel (skips/heal/handicap split)\n" +
        "  train --preset lumina [--run lumina --threads T --minutes M ...]  league+scripted mix " + PresetLuminaOpp + ", hidden 256x2 / critic 256x3, ent 0.02->0.003 over 100M, order cost 0.002, snap 20 keep 10, eval every 100 (n 40), auto curriculum 1v1 -> 1-3 -> 1-5 x1/x3 -> 1-10 x1/x3 at 0.8; --resume restores pool, stage, best\n" +
        "  eval --policy P.btcpol --opp prod4[,hunter] --n 500 --units 5 --arena 3 --skips 1 --heal 0 [--controller env|lumina --sample 1|0 --fps 60 --threads T --seed S --time-limit 300]\n" +
        "  match --a prod4 --b hunter --n 200 [same episode options as eval; side A = west; a/b may be a .btcpol or lumina:<file>.btcpol]\n" +
        "  bench [--units 10 --sec 4 --threads T]\n" +
        "  view --policy P.btcpol | --a NAME  --opp prod4 --seed 1 [--units 1 --arena 1 --skips 1 --heal 0]\n" +
        "  opponents: " + DuelOpponents.Help;

    static void LowPriority()
    {
        try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
    }

    static DuelCurriculum Curriculum(Args a, string units, string arena, double skips, double heal, string fps, double timeLimit, double dr = 0, double shape = 1.0, double indiv = 0.1, double handicap = 0, bool v2 = false)
    {
        var c = new DuelCurriculum();
        c.SetUnits(a.Str("units", units));
        c.SetArena(a.Str("arena", arena));
        c.SkipsProb = (float)a.Dbl("skips", skips);
        c.HealProb = (float)a.Dbl("heal", heal);
        c.SetFps(a.Str("fps", fps));
        c.RenderJitter = (float)a.Dbl("render-jitter", 0);
        c.DomainRand = (float)a.Dbl("dr", dr);
        c.TimeLimit = (float)a.Dbl("time-limit", timeLimit);
        c.DecisionSec = (float)a.Dbl("dec", 0.1);
        c.SkipDelay = (float)a.Dbl("skip-delay", 0.22);
        c.Shaping = (float)a.Dbl("shape", shape);
        c.Indiv = (float)a.Dbl("indiv", indiv);
        c.HandicapProb = (float)a.Dbl("handicap", handicap); c.HandicapMin = a.Int("handicap-min", 1); c.HandicapMax = a.Int("handicap-max", 3); if (a.Has("handicap-own")) c.HandicapOwnProb = (float)a.Dbl("handicap-own", 0);
        // rewards v2 terms are part of the luminazero recipe only; every other command keeps the old reward (all zero) unless asked
        c.KillReward = (float)a.Dbl("kill", v2 ? 0.4 : 0); c.DeathPenalty = (float)a.Dbl("death", v2 ? 0.4 : 0);
        c.DmgDealt = (float)a.Dbl("dmg-dealt", v2 ? 0.3 : 0); c.DmgTaken = (float)a.Dbl("dmg-taken", v2 ? 0.3 : 0);
        c.TeamShare = (float)a.Dbl("team-share", 0.5); c.InterScale = (float)a.Dbl("inter-start", 1.0);
        c.DrawReward = (float)a.Dbl("draw-reward", v2 ? -0.5 : 0);
        c.SafeWinReward = (float)a.Dbl("safe-win", v2 ? c.DrawReward : 1);   // v2: baiting the opponent into a safe-zone violation is worth no more than a draw
        // roles (attack / defence): the attacker loses at the time limit and pays --passive per second without progress
        c.Roles = a.Int("roles", 0); c.AttackProb = (float)a.Dbl("attack-prob", 0.5);
        c.PassivePen = (float)a.Dbl("passive", c.Roles > 0 ? 0.03 : 0); c.PassiveGrace = (float)a.Dbl("passive-grace", 4);
        return c;
    }

    // =====================================================================================
    /// <summary>
    /// --preset lumina: the LuminaZero recipe (league self-play + scripted mix, auto curriculum, periodic panel eval with the shipped
    /// controller, bigger nets, entropy anneal, order cost). Every value is still overridable by its own option.
    /// </summary>
    public const string PresetLuminaOpp = "prod4:3,prod2:1,v2:1,hunter:1,sweeper:1,baiter:1,clump:1,pool:4";

    const uint ExtraMagic = 0x474C5442; // "BTLG": session state inside the checkpoint extra blob

    /// <summary>`btclab bench-train [--ckpt F] [--updates N --warmup W] [--tile2 0/1 --split-critic 0/1 --batch-opp 0/1] + train flags`: N updates, nothing saved into runs/, prints the wall breakdown.</summary>
    public static int BenchTrain(Args a, string labRoot) => Train(a, labRoot, true);

    public static int Train(Args a, string labRoot, bool bench = false)
    {
        LowPriority();
        MlpOps.Tile2 = a.Int("tile2", 1) != 0; PpoTrainer.SplitCritic = a.Int("split-critic", 1) != 0; PpoTrainer.BatchOpp = a.Int("batch-opp", 1) != 0;
        int benchN = bench ? a.Int("updates", 5) : 0, benchWarm = bench ? a.Int("warmup", 1) : 0;
        string ckptArg = bench ? a.Str("ckpt", "") : "";
        string presetName = a.Str("preset", "");
        if (presetName == "lumina2") presetName = "luminazero";   // old name of the v2 preset
        if (presetName != "" && presetName != "lumina" && presetName != "luminazero") throw new ArgumentException("unknown --preset (known: luminazero, lumina)");
        bool preset = presetName != "", v2 = presetName == "luminazero";
        string P(string key, string presetVal, string plainVal) => a.Str(key, preset ? presetVal : plainVal);

        string run = a.Str("run", preset ? presetName : "duel");
        string dir = bench ? Path.Combine(Path.GetTempPath(), "btc-bench-" + Environment.ProcessId) : Path.Combine(labRoot, "runs", run);   // bench never writes into runs/
        Directory.CreateDirectory(dir);
        bool resume = a.Flag("resume");
        string last = null;
        if (bench && ckptArg != "") last = ckptArg;
        else if (resume)
        {
            last = Directory.GetFiles(bench ? Path.Combine(labRoot, "runs", run) : dir, "ckpt-*.bin").OrderBy(f => f, StringComparer.Ordinal).LastOrDefault();
            if (last == null) throw new FileNotFoundException("no checkpoint in " + dir);
        }
        PpoConfig cfg;
        if (last != null) cfg = PpoTrainer.ReadCheckpointConfig(last);
        else
        {
            cfg = new PpoConfig { NumEnvs = 64, Steps = 128, TotalSteps = 50_000_000 };
            if (preset)
            {
                cfg.TotalSteps = 300_000_000;
                cfg.Hidden = "256,256"; cfg.CriticHidden = v2 ? "256,256" : "256,256,256";
                cfg.EntCoef = v2 ? 0.005f : 0.02f; cfg.EntEnd = v2 ? 0.001f : 0.003f; cfg.EntAnneal = 100_000_000;   // v2: 0.02 washed out the continue prior before any reward signal (69 actions)
            }
        }
        cfg.NumEnvs = a.Int("envs", cfg.NumEnvs); cfg.Steps = a.Int("rollout", cfg.Steps);
        cfg.Threads = a.Int("threads", Environment.ProcessorCount);
        cfg.TotalSteps = a.Long("steps", cfg.TotalSteps); cfg.Seed = a.Long("seed", cfg.Seed);
        if (a.Has("hidden") || a.Has("layers"))
        {
            string h = a.Str("hidden", cfg.HiddenSizes.Length > 0 ? cfg.HiddenSizes[0].ToString(CultureInfo.InvariantCulture) : "128");
            cfg.Hidden = PpoConfig.Layers(h, a.Int("layers", 2));
            if (!a.Has("critic-hidden") && !a.Has("critic-layers")) cfg.CriticHidden = cfg.Hidden;
        }
        if (a.Has("critic-hidden") || a.Has("critic-layers"))
        {
            string h = a.Str("critic-hidden", cfg.CriticHiddenSizes.Length > 0 ? cfg.CriticHiddenSizes[0].ToString(CultureInfo.InvariantCulture) : "128");
            cfg.CriticHidden = PpoConfig.Layers(h, a.Int("critic-layers", 2));
        }
        if (a.Has("ent")) cfg.EntCoef = (float)a.Dbl("ent", cfg.EntCoef);
        if (a.Has("ent-start")) cfg.EntCoef = (float)a.Dbl("ent-start", cfg.EntCoef);
        if (a.Has("ent-end")) cfg.EntEnd = (float)a.Dbl("ent-end", cfg.EntEnd);
        if (a.Has("ent-anneal")) cfg.EntAnneal = a.Long("ent-anneal", cfg.EntAnneal);
        if (a.Has("dead-credit")) cfg.DeadCredit = a.Str("dead-credit", "1") is not ("0" or "false");
        if (a.Has("kl-adapt")) cfg.KlAdapt = (float)a.Dbl("kl-adapt", 0);                 // > 0: adaptive lr by KL (target); see PpoTrainer.AdaptLr
        if (a.Has("kl-adapt-min")) cfg.KlAdaptMin = (float)a.Dbl("kl-adapt-min", 0.1);
        if (a.Has("kl-adapt-max")) cfg.KlAdaptMax = (float)a.Dbl("kl-adapt-max", 10);
        if (a.Has("kl-adapt-critic")) cfg.KlAdaptCritic = a.Int("kl-adapt-critic", 0) != 0;

        string oppList = P("opp", v2 ? string.Join(",", DuelEval.ZNames.Select((n, i) => n + ":" + DuelEval.ZWeights[a.Int("stage", 0)][i].ToString(CultureInfo.InvariantCulture))) : PresetLuminaOpp, "prod2,hunter");
        int printEvery = a.Int("print", 5), ckptEvery = bench ? 0 : a.Int("ckpt-every", preset ? 50 : 25), ckptKeep = a.Int("ckpt-keep", preset ? 5 : 0);
        int snapEvery = a.Int("snap", 20), poolKeep = a.Int("pool-keep", v2 ? 25 : 10);
        bool resetBest = a.Flag("reset-best");                          // after a sim change: old eval scores are not comparable
        bool useGpu = a.Int("gpu", 0) != 0;                             // PPO update on the OpenCL GPU (lab/Btc.Gpu)
        int nX = a.Int("exploiters", v2 && !bench ? 1 : 0);                       // AlphaStar main exploiters (0 or 1)
        int xEnvs = a.Int("x-envs", Math.Max(8, cfg.NumEnvs / 2)), xTimeout = a.Int("x-timeout", 600);
        double xResetWr = a.Dbl("x-reset-wr", 0.7);
        // --league-x 1: AlphaStar league exploiter (PFSP vs the whole league with its own stats; snapshot joins the league when it beats
        // every member >= lx-wr or after lx-timeout updates; then resets to a copy of main with probability lx-reset)
        bool useLx = a.Int("league-x", 0) != 0;
        int lxEnvs = a.Int("lx-envs", Math.Max(8, cfg.NumEnvs / 2)), lxTimeout = a.Int("lx-timeout", 600);
        double lxWr = a.Dbl("lx-wr", 0.7), lxReset = a.Dbl("lx-reset", 0.25);
        // --meta-breaker 1: every mb-cycle updates (cooldown) a learner restarts from main or a random league snapshot with actor noise
        // and a higher entropy, plays the current main, and gets a terminal bonus for winning in ways that are RARE in the main's
        // current games (bonus * (1 - share of that ending)); when it beats main (>= mb-add) it joins the league and main must answer it
        bool useMb = a.Int("meta-breaker", 0) != 0;
        int mbEnvs = a.Int("mb-envs", Math.Max(8, cfg.NumEnvs / 2)), mbCycle = a.Int("mb-cycle", 300);
        double mbNoise = a.Dbl("mb-noise", 0.3), mbBonus = a.Dbl("mb-bonus", 0.5), mbEnt = a.Dbl("mb-ent", 0.005), mbAdd = a.Dbl("mb-add", 0.45), mbFromSnap = a.Dbl("mb-from-snap", 0.5);
        int keepX = a.Int("keep-exploiters", 8);
        int evalEvery = bench ? 0 : a.Int("eval-every", v2 ? 60 : preset ? 100 : 0), evalN = a.Int("eval-n", 40);   // v2: the bot panel through the shipped controller costs ~25 s
        string evalOpp = a.Str("eval-opp", "prod4");
        bool evalArgmax = a.Flag("eval-argmax");
        ulong evalSeed = (ulong)a.Long("eval-seed", 777);
        bool autoCur = a.Int("auto-cur", preset ? 1 : 0) != 0;
        bool zmix = v2 && !a.Has("opp");     // LuminaZero stage tables switch the opponent weights
        int lastStage = v2 ? DuelEval.ZLast : DuelEval.LastStage;
        double curThr = a.Dbl("cur-threshold", v2 ? 0.6 : 0.8);
        float curEarlier = (float)a.Dbl("cur-earlier", 0.2);
        int stage = a.Int("stage", 0);
        string initPol = a.Str("init-policy", "");                      // read before the a.Unused check
        string actorFrom = a.Str("actor-from", "");                    // read before the a.Unused check (applied after --resume)
        { var hr = a.Str("h-rate", "0.8-2.0").Split('-'); HumanOpponent.RateMin = float.Parse(hr[0], CultureInfo.InvariantCulture); HumanOpponent.RateMax = float.Parse(hr[^1], CultureInfo.InvariantCulture); }   // hpool: commands per second
        { var hr = a.Str("h-react", "0.2-0.45").Split('-'); HumanOpponent.ReactMin = float.Parse(hr[0], CultureInfo.InvariantCulture); HumanOpponent.ReactMax = float.Parse(hr[^1], CultureInfo.InvariantCulture); }   // hpool: reaction delay s
        int criticWarm = a.Int("critic-warmup", 0);                     // updates with the actor frozen (Lr 0) so a fresh critic catches up with a warm-started actor
        int botAnneal = a.Int("bot-anneal", 0), botFrom = a.Int("bot-anneal-from", -1);   // scripted opponents fade to weight 0 over botAnneal updates (from botFrom, default = the update the run starts at): a temporary push, then pure self-play
        var botKeep = new HashSet<string>(a.Str("bot-keep", "").Split(',', StringSplitOptions.RemoveEmptyEntries));   // scripted opponents that do not fade (e.g. presser: a known hole)
        // --rollback D: when the panel score stays below best - D for --rollback-n evals in a row, the main learner reloads the
        // weights + Adam state saved at the best eval (counters and league stay: the league keeps every newer snapshot)
        double rollD = a.Dbl("rollback", v2 ? 0.15 : 0); int rollN = a.Int("rollback-n", 2);
        double minutes = a.Dbl("minutes", 0);
        long shapeAnneal = a.Long("shape-anneal", v2 ? 50_000_000 : 0);   // v2: potential shaping (HP / alive / centre control) fades to 0 over 50M steps (cold-start help only)
        var cur = v2 ? Curriculum(a, "4-15", "3", 0.5, 0.5, "30-90", 120, 0.5, 1.0, 0.0, 0.4, true) : Curriculum(a, "1", "1", 0.5, 0.3, "30-90", 120, 0.5);
        cur.OrderCost = (float)a.Dbl("order-cost", v2 ? 0.0 : 0.002);
        float interEnd = (float)a.Dbl("inter-end", v2 ? 0.3 : 1.0); long interAnneal = a.Long("inter-anneal", v2 ? -1 : 0);
        float shape0 = cur.Shaping, inter0 = cur.InterScale;
        foreach (var kv in a.Unused.ToList())
            if (!cfg.Set(kv.Key, kv.Value)) throw new ArgumentException("unknown option --" + kv.Key);
        cur.Gamma = cfg.Gamma;
        if (autoCur && (stage < 0 || stage > lastStage)) throw new ArgumentException("--stage 0.." + lastStage);

        // ---- opponents: weighted mix, "pool" = league snapshots (PFSP), "self" = latest snapshot ----
        var (names, weights) = DuelOpponents.ParseMix(oppList);
        var league = new DuelLeague(names, weights, cfg.NumEnvs) { Keep = poolKeep };
        var selfRef = new PolicyRef(null);
        var defs = names.Select(n => n == "pool"
            ? new DuelOppDef { Name = "pool", Bot = null, Create = () => new PolicyOpponent(e => league.PolicyFor(e), true) }
            : n == "hpool"
            ? new DuelOppDef { Name = "hpool", Bot = null, Create = () => new HumanOpponent(new PolicyOpponent(e => league.PolicyFor(e), true), cur.DecisionSec) }
            : DuelOpponents.Get(n, selfRef)).ToArray();
        DuelCurriculum StageCur(int s)
        {
            if (!autoCur) return cur;
            var c = v2 ? DuelEval.StageZ(cur, s) : DuelEval.Stage(cur, s, curEarlier);
            if (a.Has("dec")) c.DecisionSec = cur.DecisionSec;          // explicit options beat the stage table (fine-tuning on an easier stage)
            if (a.Has("time-limit")) c.TimeLimit = cur.TimeLimit;
            if (a.Has("handicap")) { c.HandicapProb = cur.HandicapProb; c.HandicapMin = cur.HandicapMin; c.HandicapMax = cur.HandicapMax; c.HandicapOwnProb = cur.HandicapOwnProb; }
            return c;
        }
        var env = new SimDuelEnv(cfg.NumEnvs, StageCur(stage), defs);
        var opps = defs.Select(d => d.Spec).ToArray();
        var tr = new PpoTrainer(env, opps, cfg, league);
        Btc.Gpu.GpuPpo gpu = null, gpuX = null;
        if (useGpu) { gpu = new Btc.Gpu.GpuPpo(); tr.Gpu = gpu; Console.WriteLine("gpu: " + gpu.Name); }
        // Fresh v2 nets start "keep the current order" with probability --init-continue (default .9): with uniform logits over
        // 69 actions every 0.2 s a unit is re-ordered before any strike lands, so a random team deals ~no damage and gets no signal.
        double initCont = a.Dbl("init-continue", v2 ? 0.9 : 0);
        // --init-policy X.btcpol: warm start of a fresh run from an exported actor (critic, Adam, league start fresh)

        if (last == null && initPol != "")
        {
            var ip = MlpPolicy.Load(File.ReadAllBytes(initPol));
            var act = tr.Actor;
            if (ip.NumLayers != act.NumLayers || (ip.InputSize != act.Sizes[0] && ip.InputSize != act.Sizes[0] - 1)) throw new ArgumentException("--init-policy: net shape differs");
            if (ip.InputSize == act.Sizes[0] - 1)
            {
                // duelobs-v2 -> v3: v3 appends one input (the role flag, last global); its weights start at 0, so the warm actor is unchanged
                int nIn = ip.InputSize, nOut = act.Sizes[1];
                if (ip.Weights[0].Length != nIn * nOut) throw new ArgumentException("--init-policy: layer 0 size differs");
                var w = new float[(nIn + 1) * nOut];
                for (int o = 0; o < nOut; o++) Array.Copy(ip.Weights[0], o * nIn, w, o * (nIn + 1), nIn);
                ip.Weights[0] = w;
                Console.WriteLine("init: v2 actor migrated to v3 obs (+1 role input, zero weights)");
            }
            for (int l = 0; l < act.NumLayers; l++)
            {
                if (ip.Weights[l].Length != act.Sizes[l] * act.Sizes[l + 1] || ip.Biases[l].Length != act.Sizes[l + 1]) throw new ArgumentException("--init-policy: layer " + l + " size differs");
                ip.Weights[l].CopyTo(act.P, act.WOff[l]); ip.Biases[l].CopyTo(act.P, act.BOff[l]);
            }
            Console.WriteLine("init: actor from " + Path.GetFileName(initPol));
            initCont = 0;
        }
        if (last == null && initCont > 0 && initCont < 1)
        {
            int k = tr.Heads[0];
            tr.Actor.P[tr.Actor.BOff[^1] + DuelActions.Continue] += (float)Math.Log(initCont * (k - 1) / (1 - initCont));
            Console.WriteLine(FormattableString.Invariant($"init: P(continue) = {initCont:F2} (bias +{Math.Log(initCont * (k - 1) / (1 - initCont)):F2})"));
        }
        PpoTrainer trX = null; SimDuelEnv envX = null;   // main exploiter (set up below)
        PpoTrainer trL = null, trM = null; SimDuelEnv envL = null, envM = null; LeagueView lview = null;   // league exploiter, meta breaker
        var mbBonusArr = new float[SimDuelEnv.RoleCats.Length];
        DuelCurriculum MbCur(int s) { var c = StageCur(s).Clone(); c.WinBonus = mbBonusArr; return c; }
        void ApplyStage(int s) { env.Cur = StageCur(s); if (envX != null) envX.Cur = StageCur(s); if (envL != null) envL.Cur = StageCur(s); if (envM != null) envM.Cur = MbCur(s); if (zmix) league.SetWeights(DuelEval.ZWeights[s]); }
        EvalCell[] EvalPanel() => v2 ? DuelEval.PanelZ(autoCur ? stage : DuelEval.ZLast) : DuelEval.Panel;
        MlpPolicy Exp() { var p = tr.ExportPolicy(); p.DecisionSec = env.Cur.DecisionSec; return p; }   // the .btcpol carries the decision period of the stage it was trained in
        double bestScore = -1, lastScore = double.NaN; int bestUpdate = 0;
        string metrics = Path.Combine(dir, "metrics.csv");
        if (last != null)
        {
            var extra = tr.LoadCheckpoint(last);
            if (extra != null && extra.Length >= 4 && BitConverter.ToUInt32(extra, 0) == ExtraMagic)
            {
                using var r = new BinaryReader(new MemoryStream(extra));
                r.ReadUInt32(); r.ReadInt32();
                int st0 = r.ReadInt32(); bestScore = r.ReadDouble(); bestUpdate = r.ReadInt32(); lastScore = r.ReadDouble();
                int sl = r.ReadInt32(); if (sl > 0) selfRef.Policy = MlpPolicy.Load(r.ReadBytes(sl));
                league.Read(r);
                if (autoCur && !a.Has("stage")) stage = Math.Min(st0, lastStage);
                if (resetBest) { bestScore = -1; bestUpdate = 0; }
            }
            else if (extra != null) selfRef.Policy = MlpPolicy.Load(extra);
            ApplyStage(stage);
            tr.ResetAll(0xC0FFEE);
            Console.WriteLine($"resumed {Path.GetFileName(last)} update={tr.UpdateCount} step={tr.StepCount} stage={stage} best={bestScore:F3}@u{bestUpdate} {league.Describe()}");
        }
        // --actor-from X.btcpol on --resume: the main actor is replaced (league, critic, counters stay; actor Adam restarts), e.g. to
        // continue the league from an earlier, better snapshot
        if (last != null && actorFrom != "")
        {
            var ap = MlpPolicy.Load(File.ReadAllBytes(actorFrom));
            var act = tr.Actor;
            if (ap.NumLayers != act.NumLayers || ap.InputSize != act.Sizes[0]) throw new ArgumentException("--actor-from: net shape differs");
            for (int l = 0; l < act.NumLayers; l++) { ap.Weights[l].CopyTo(act.P, act.WOff[l]); ap.Biases[l].CopyTo(act.P, act.BOff[l]); }
            Array.Clear(tr.ActorOpt.M); Array.Clear(tr.ActorOpt.V); tr.ActorOpt.T = 0;
            Console.WriteLine("resume: actor replaced from " + Path.GetFileName(actorFrom));
        }
        if (selfRef.Policy == null) selfRef.Policy = Exp();
        if (league.PoolIndex >= 0 && league.Count == 0) { league.Add(Exp(), tr.UpdateCount); tr.ResetAll(0xF00D); }

        // ---- AlphaStar main exploiter: a second learner that only plays the CURRENT main policy. When it beats main (wr >= x-reset-wr
        // over >= 200 episodes, draws = half) or after x-timeout updates, its snapshot joins the league (if it got anywhere) and it
        // restarts as a copy of main. No outside knowledge enters training: exploiters are self-play too.
        const int xWindow = 200;
        var mainRef = new PolicyRef(null);
        int xSince = 0, xResets = 0, xEp = 0, xWinX2 = 0, xAdded = 0;
        string xck = Path.Combine(dir, "mx-ckpt.bin");
        if (nX > 0)
        {
            var cfgX = PpoConfig.FromText(cfg.ToText()); cfgX.NumEnvs = xEnvs; cfgX.Seed = cfg.Seed + 7919;
            var defX = new DuelOppDef { Name = "main", Bot = null, Create = () => new PolicyOpponent(mainRef, true) };
            envX = new SimDuelEnv(xEnvs, StageCur(stage), new[] { defX });
            trX = new PpoTrainer(envX, new[] { defX.Spec }, cfgX);
            if (useGpu) { gpuX = new Btc.Gpu.GpuPpo(); trX.Gpu = gpuX; }
            trX.CopyWeightsFrom(tr);
            mainRef.Policy = Exp();
            if (last != null && File.Exists(xck)) trX.LoadCheckpoint(xck);
        }
        MlpPolicy ExpX() { var p = trX.ExportPolicy(); p.DecisionSec = envX.Cur.DecisionSec; return p; }
        league.KeepExploiters = keepX;

        // ---- league exploiter (lx) and meta breaker (mb): more self-play learners, own checkpoints ----
        int lxSince = 0, lxAdds = 0, lxResets = 0, mbSince = 0, mbCycles = 0, mbAdds = 0, mbEp = 0, mbWinX2 = 0;
        string lck = Path.Combine(dir, "lx-ckpt.bin"), mck = Path.Combine(dir, "mb-ckpt.bin");
        var metaF = new double[SimDuelEnv.RoleCats.Length]; bool metaInit = false;
        var mbRng = new Rng((ulong)cfg.Seed * 31 + 17);
        string mbFrom = "main";
        if (useLx)
        {
            var cfgL = PpoConfig.FromText(cfg.ToText()); cfgL.NumEnvs = lxEnvs; cfgL.Seed = cfg.Seed + 104729;
            lview = new LeagueView(league, lxEnvs);
            var defL = new DuelOppDef { Name = "league", Bot = null, Create = () => new PolicyOpponent(e => lview.PolicyFor(e), true) };
            envL = new SimDuelEnv(lxEnvs, StageCur(stage), new[] { defL });
            trL = new PpoTrainer(envL, new[] { defL.Spec }, cfgL, lview);
            trL.CopyWeightsFrom(tr);
            if (last != null && File.Exists(lck)) trL.LoadCheckpoint(lck);
        }
        void LoadActor(PpoTrainer t, MlpPolicy ip)
        {
            var act = t.Actor;
            for (int l = 0; l < act.NumLayers; l++) { ip.Weights[l].CopyTo(act.P, act.WOff[l]); ip.Biases[l].CopyTo(act.P, act.BOff[l]); }
        }
        void MbReset()
        {
            trM.CopyWeightsFrom(tr);    // critic (and actor) from main
            mbFrom = "main";
            var snaps = league.Snapshot();
            if (snaps.Length > 1 && mbRng.NextDouble() < mbFromSnap)
            {
                var s = snaps[mbRng.Next(snaps.Length)];
                if (s.Policy.NumLayers == trM.Actor.NumLayers && s.Policy.InputSize == trM.Actor.Sizes[0]) { LoadActor(trM, s.Policy); mbFrom = (s.Exploiter ? "x" : "#") + s.Id; }
            }
            // actor noise: per weight matrix, gaussian with sd = mb-noise * that matrix's own sd (the output layer gets half)
            var act = trM.Actor;
            for (int l = 0; l < act.NumLayers; l++)
            {
                int o = act.WOff[l], n = act.Sizes[l] * act.Sizes[l + 1];
                double m = 0, v = 0; for (int i = 0; i < n; i++) m += act.P[o + i]; m /= n;
                for (int i = 0; i < n; i++) { double d = act.P[o + i] - m; v += d * d; }
                double sd = Math.Sqrt(v / n) * mbNoise * (l == act.NumLayers - 1 ? 0.5 : 1.0);
                for (int i = 0; i < n; i++) act.P[o + i] += (float)(sd * mbRng.Gaussian());
            }
            mbSince = 0; mbEp = 0; mbWinX2 = 0;
        }
        if (useMb)
        {
            var cfgM = PpoConfig.FromText(cfg.ToText()); cfgM.NumEnvs = mbEnvs; cfgM.Seed = cfg.Seed + 130363;
            cfgM.EntCoef = (float)mbEnt; cfgM.EntEnd = (float)mbEnt;
            var defM = new DuelOppDef { Name = "main", Bot = null, Create = () => new PolicyOpponent(mainRef, true) };
            envM = new SimDuelEnv(mbEnvs, MbCur(stage), new[] { defM });
            trM = new PpoTrainer(envM, new[] { defM.Spec }, cfgM);
            if (last != null && File.Exists(mck)) trM.LoadCheckpoint(mck); else MbReset();
            if (mainRef.Policy == null) mainRef.Policy = Exp();
        }
        MlpPolicy ExpOf(PpoTrainer t, SimDuelEnv e) { var p = t.ExportPolicy(); p.DecisionSec = e.Cur.DecisionSec; return p; }

        string header = $"train run={run} preset={(preset ? presetName : "-")} opp={oppList} {env.Cur.Describe()} envs={cfg.NumEnvs}x{cfg.Steps} threads={cfg.Threads} hidden={cfg.Hidden}/{cfg.CriticHidden} steps={cfg.TotalSteps} gamma={cfg.Gamma.ToString(CultureInfo.InvariantCulture)}" +
            FormattableString.Invariant($" ent={cfg.EntCoef}->{cfg.EntEnd}@{cfg.EntAnneal} deadcredit={cfg.DeadCredit} snap={snapEvery} poolkeep={poolKeep} eval={evalEvery}x{evalN} vs {evalOpp} ({(evalArgmax ? "argmax" : "sample")}) autocur={autoCur} stage={stage} thr={curThr} earlier={curEarlier} ckptkeep={ckptKeep}");
        if (last == null || !File.Exists(metrics))
        {
            var mh = new StringBuilder();
            mh.Append("# ").Append(header).Append('\n');
            foreach (var l in cfg.ToText().Split('\n', StringSplitOptions.RemoveEmptyEntries)) mh.Append("# cfg ").Append(l).Append('\n');
            mh.Append("update,step,episodes,stage,pool,").Append(string.Join(",", opps.Select(o => "wr_" + o.Name)))
              .Append(",ep_reward,ep_len,entropy,ent_coef,kl,clipfrac,vloss,gradnorm,shape,samples_per_s,steps_per_s,score\n");
            File.WriteAllText(metrics, mh.ToString());
        }
        else File.AppendAllText(metrics, "# resumed " + DateTime.Now.ToString("s") + " " + header + "\n");
        File.AppendAllText(Path.Combine(dir, "train.txt"), DateTime.Now.ToString("s") + " " + header + "\n" + cfg.ToText() + "\n");
        Console.WriteLine(header);

        byte[] Extra()
        {
            using var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms))
            {
                w.Write(ExtraMagic); w.Write(1);
                w.Write(stage); w.Write(bestScore); w.Write(bestUpdate); w.Write(lastScore);
                var sb = selfRef.Policy?.Save(); w.Write(sb?.Length ?? 0); if (sb != null) w.Write(sb);
                league.Write(w);
            }
            return ms.ToArray();
        }
        string Save()
        {
            string p = Path.Combine(dir, $"ckpt-{tr.UpdateCount:D6}.bin");
            tr.SaveCheckpoint(p, Extra());
            trX?.SaveCheckpoint(xck);
            trL?.SaveCheckpoint(lck);
            trM?.SaveCheckpoint(mck);
            if (ckptKeep > 0)
                foreach (var old in Directory.GetFiles(dir, "ckpt-*.bin").OrderByDescending(f => f, StringComparer.Ordinal).Skip(ckptKeep))
                    try { File.Delete(old); } catch { }
            return p;
        }
        // The bot panel runs in the background on a third of the threads (it plays through the shipped scalar controller and took ~40% of
        // the wall time inline); its result is applied (best.btcpol, league best, curriculum stage) on the training thread when ready.
        Task<(MlpPolicy pol, EvalCell[] panel, MatchStats[] res, StringBuilder rep, int upd, Stopwatch sw)> evalTask = null;
        void StartEval()
        {
            var sw = Stopwatch.StartNew();
            var pol = Exp();
            if (rollD > 0) tr.SaveCheckpoint(Path.Combine(dir, "eval-ckpt.bin"));   // the state this eval measures (rollback target if it becomes best)
            File.WriteAllBytes(Path.Combine(dir, "policy.btcpol"), pol.Save());
            var rep = new StringBuilder();
            int upd = tr.UpdateCount, evStage = stage;
            rep.AppendLine(FormattableString.Invariant($"eval update={tr.UpdateCount} step={tr.StepCount} stage={stage} controller=lumina({(evalArgmax ? "argmax" : "sample")}) vs {evalOpp} n={evalN}/cell seed={evalSeed}"));
            var panel = EvalPanel();
            int evThreads = Math.Max(2, cfg.Threads / 3);
            evalTask = Task.Run(() => (pol, panel, DuelEval.RunPanel(pol, evalArgmax, evalOpp, evalN, evThreads, evalSeed, rep, panel, v2 ? DuelEval.ZTimeLimit[evStage] : 300f), rep, upd, sw));
        }
        int rollBad = 0, rollbacks = 0;
        double FinishEval()
        {
            var (pol, panel, res, rep, upd, sw) = evalTask.Result; evalTask = null;
            double score = DuelEval.Mean(res), stageScore = v2 ? score : DuelEval.Mean(res, DuelEval.StageCells[stage]);
            rep.AppendLine(FormattableString.Invariant($"score {score:F4} (mean wr over {res.Length} cells)  stage{stage} cells {stageScore:F4}"));
            string note = "";
            if (score > bestScore)
            {
                bestScore = score; bestUpdate = upd;
                string ec = Path.Combine(dir, "eval-ckpt.bin");
                if (rollD > 0 && File.Exists(ec)) File.Copy(ec, Path.Combine(dir, "best-ckpt.bin"), true);
                rollBad = 0;
                File.WriteAllBytes(Path.Combine(dir, "best.btcpol"), pol.Save());
                if (league.PoolIndex >= 0) league.Add(pol, upd, true);
                note += " NEW BEST";
            }
            if (autoCur && stage < lastStage && stageScore > curThr)
            {
                if (v2 && File.Exists(Path.Combine(dir, "best.btcpol"))) File.Copy(Path.Combine(dir, "best.btcpol"), Path.Combine(dir, $"best-stage{stage}.btcpol"), true);
                stage++;
                if (v2) { bestScore = -1; bestUpdate = tr.UpdateCount; }        // scores of different stages are not comparable: best.btcpol = best of the current stage
                ApplyStage(stage);
                note += v2 ? $" -> stage {stage} [{env.Cur.Describe()}]" : $" -> stage {stage} ({DuelEval.StageUnits[stage]} units, arena {DuelEval.StageArena[stage]})";
            }
            else if (rollD > 0 && bestScore >= 0 && score < bestScore - rollD && ++rollBad >= rollN && File.Exists(Path.Combine(dir, "best-ckpt.bin")))
            {
                int u0 = tr.UpdateCount; long s0 = tr.StepCount, e0 = tr.EpisodeCount;
                tr.LoadCheckpoint(Path.Combine(dir, "best-ckpt.bin"));
                tr.UpdateCount = u0; tr.StepCount = s0; tr.EpisodeCount = e0;
                tr.ResetAll(0xB0B0u + (ulong)(++rollbacks));
                rollBad = 0;
                note += FormattableString.Invariant($" ROLLBACK #{rollbacks} to u{bestUpdate} (score < best - {rollD:F2} {rollN}x)");
            }
            else if (score >= bestScore - rollD) rollBad = 0;
            rep.AppendLine("best " + bestScore.ToString("F4", CultureInfo.InvariantCulture) + " @u" + bestUpdate + note);
            File.WriteAllText(Path.Combine(dir, $"eval-{upd:D6}.txt"), rep.ToString());
            Console.WriteLine(FormattableString.Invariant($"  eval u{upd}: ") + string.Join(" ", panel.Select((c, i) => FormattableString.Invariant($"{c.Name} {res[i].HonestWinRate:F2}{(res[i].WinBy.GetValueOrDefault("safe") > 0 ? "(+" + res[i].WinBy["safe"] + "safe)" : "")}"))) +
                FormattableString.Invariant($" | score {score:F3} st{stage}-cells {stageScore:F3} best {bestScore:F3}@u{bestUpdate}{note} ({sw.Elapsed.TotalSeconds:F0}s)"));
            lastScore = score;
            return score;
        }

        var wall = Stopwatch.StartNew();
        int benchStart = tr.UpdateCount; double bRo = 0, bTr = 0, bObs = 0, bInf = 0, bOpp = 0, bStep = 0, bMb = 0, bAdam = 0, bGae = 0, bSps = 0, bKl = 0; int bN = 0;
        int K = opps.Length;
        int[] wEp = new int[K], wWin = new int[K], wDraw = new int[K];
        double wRew = 0, wLen = 0, wSps = 0; int wEpAll = 0, wN = 0;
        string reason = "steps";
        while (tr.StepCount < cfg.TotalSteps)
        {
            if (shapeAnneal > 0) { cur.Shaping = shape0 * (float)Math.Max(0.0, 1.0 - (double)tr.StepCount / shapeAnneal); env.Cur.Shaping = cur.Shaping; }
            { long ia = interAnneal < 0 ? cfg.TotalSteps : interAnneal; if (ia > 0) { float fr = (float)Math.Min(1.0, (double)tr.StepCount / ia); cur.InterScale = inter0 + (interEnd - inter0) * fr; env.Cur.InterScale = cur.InterScale; } }
            // the exploiter update runs alongside the main one (one rollouts on the CPU while the other trains on the GPU); it plays
            // the main policy of the previous update
            Task<UpdateStats> xTask = null;
            if (trX != null)
            {
                if (mainRef.Policy == null) mainRef.Policy = Exp();
                envX.Cur.Shaping = cur.Shaping; envX.Cur.InterScale = cur.InterScale;
                xTask = Task.Run(() => trX.Update());
            }
            Task<UpdateStats> lTask = null, mTask = null;
            if (trL != null) { envL.Cur.Shaping = cur.Shaping; envL.Cur.InterScale = cur.InterScale; lTask = Task.Run(() => trL.Update()); }
            if (trM != null) { if (mainRef.Policy == null) mainRef.Policy = Exp(); envM.Cur.Shaping = cur.Shaping; envM.Cur.InterScale = cur.InterScale; mTask = Task.Run(() => trM.Update()); }
            if (botAnneal > 0 && !zmix)
            {
                if (botFrom < 0) botFrom = tr.UpdateCount;
                float fr = Math.Max(0f, 1f - (float)(tr.UpdateCount - botFrom) / botAnneal);
                var bw = new float[weights.Length];
                for (int i = 0; i < bw.Length; i++) bw[i] = names[i] is "pool" or "hpool" || botKeep.Contains(names[i]) ? weights[i] : weights[i] * fr;
                league.SetWeights(bw);
            }
            float lrKeep = cfg.Lr; if (tr.UpdateCount < criticWarm) cfg.Lr = 0f;
            var st = tr.Update();
            cfg.Lr = lrKeep;
            if (snapEvery > 0 && st.Update % snapEvery == 0)
            {
                selfRef.Policy = Exp();
                if (league.PoolIndex >= 0) league.Add(selfRef.Policy, st.Update);
            }
            var sl = lTask?.Result; var sm = mTask?.Result;   // both done before mainRef changes
            if (trX != null)
            {
                var sx = xTask.Result;
                mainRef.Policy = Exp();      // the exploiter plays the current main (from its next update on)
                xSince++; xEp += sx.OppEpisodes[0]; xWinX2 += 2 * sx.OppWins[0] + sx.OppDraws[0];
                if (xEp > 2 * xWindow) { xEp /= 2; xWinX2 /= 2; }
                double xwr = xEp > 0 ? xWinX2 / (2.0 * xEp) : 0;
                bool beat = xEp >= xWindow && xwr >= xResetWr, timeout = xSince >= xTimeout;
                if (beat || timeout)
                {
                    bool add = beat || xwr >= 0.4;
                    if (add) { league.AddExploiter(ExpX(), st.Update); xAdded++; }
                    Console.WriteLine(FormattableString.Invariant($"  mx reset #{xResets + 1} at u{st.Update}: wr vs main {xwr:F2} over {xEp} after {xSince} updates ({(beat ? "beat main" : "timeout")}){(add ? ", added to league" : "")}"));
                    trX.CopyWeightsFrom(tr); xSince = 0; xEp = 0; xWinX2 = 0; xResets++;
                }
            }
            if (trM != null && trX == null) mainRef.Policy = Exp();
            if (sl != null)
            {
                lxSince++;
                var (mn, nq, tot) = lview.MinWinRate(20);
                bool beatAll = nq == tot && tot > 0 && mn >= lxWr, lto = lxSince >= lxTimeout;
                if (beatAll || lto)
                {
                    league.AddExploiter(ExpOf(trL, envL), st.Update); lxAdds++;
                    bool rs = mbRng.NextDouble() < lxReset;
                    if (rs) { trL.CopyWeightsFrom(tr); lxResets++; }
                    lview.Clear();
                    Console.WriteLine(FormattableString.Invariant($"  lx snapshot #{lxAdds} at u{st.Update}: min wr vs league {mn:F2} ({nq}/{tot} rated) after {lxSince} updates ({(beatAll ? "beat all" : "timeout")}), added to league{(rs ? ", reset to main" : "")}"));
                    lxSince = 0;
                }
            }
            if (sm != null)
            {
                mbSince++; mbEp += sm.OppEpisodes[0]; mbWinX2 += 2 * sm.OppWins[0] + sm.OppDraws[0];
                double mwr = mbEp > 0 ? mbWinX2 / (2.0 * mbEp) : 0;
                bool early = mbEp >= xWindow && mwr >= Math.Max(mbAdd, 0.65);
                if (early || mbSince >= mbCycle)
                {
                    bool add = mbEp >= 50 && mwr >= mbAdd;
                    if (add) { league.AddExploiter(ExpOf(trM, envM), st.Update); mbAdds++; }
                    Console.WriteLine(FormattableString.Invariant($"  mb cycle #{mbCycles + 1} at u{st.Update} (from {mbFrom}): wr vs main {mwr:F2} over {mbEp} after {mbSince} updates{(add ? ", added to league" : "")}; bonus {string.Join(" ", SimDuelEnv.RoleCats.Select((c, i) => c + " " + mbBonusArr[i].ToString("F2", CultureInfo.InvariantCulture)))}"));
                    mbCycles++; MbReset();
                }
            }
            string scoreCol = "";
            if (evalTask != null && evalTask.IsCompleted) scoreCol = FinishEval().ToString("F4", CultureInfo.InvariantCulture);
            if (evalEvery > 0 && st.Update % evalEvery == 0 && evalTask == null) StartEval();
            var line = new StringBuilder();
            line.Append(st.Update).Append(',').Append(st.Step).Append(',').Append(st.Episodes).Append(',').Append(stage).Append(',').Append(league.Count);
            for (int k = 0; k < K; k++)
            {
                line.Append(',').Append(st.OppEpisodes[k] > 0 ? ((double)st.OppWins[k] / st.OppEpisodes[k]).ToString("F4", CultureInfo.InvariantCulture) : "");
                wEp[k] += st.OppEpisodes[k]; wWin[k] += st.OppWins[k]; wDraw[k] += st.OppDraws[k];
            }
            line.Append(FormattableString.Invariant($",{st.EpReward:F4},{st.EpLength:F1},{st.Entropy:F4},{st.EntCoef:F5},{st.Kl:F5},{st.ClipFrac:F4},{st.VLoss:F5},{st.GradNorm:F4},{env.Cur.Shaping:F3},{st.SamplesPerSec:F0},{st.StepsPerSec:F0},{scoreCol}\n"));
            File.AppendAllText(metrics, line.ToString());
            wRew += st.EpReward * st.EpCount; wLen += st.EpLength * st.EpCount; wEpAll += st.EpCount; wSps += st.StepsPerSec; wN++;

            if (ckptEvery > 0 && st.Update % ckptEvery == 0) Save();
            bool timeUp = minutes > 0 && wall.Elapsed.TotalMinutes >= minutes;
            if (st.Update % printEvery == 0 || timeUp)
            {
                var sb = new StringBuilder();
                sb.Append(FormattableString.Invariant($"u {st.Update} step {st.Step} eps {st.Episodes} t {wall.Elapsed.TotalMinutes:F1}m st {stage} pool {league.Count}"));
                for (int k = 0; k < K; k++)
                {
                    if (wEp[k] == 0) continue;
                    double wr = (double)wWin[k] / wEp[k], dr = (double)wDraw[k] / wEp[k];
                    sb.Append(FormattableString.Invariant($" wr[{opps[k].Name}] {wr:F2}/{dr:F2}d n{wEp[k]}"));
                }
                if (cur.Roles > 0)
                {
                    // learner as attacker / defender since the last print: win rate, episodes, deciding reasons
                    var rs = env.TakeRoleStats();
                    // meta = how the main's games end (both roles); the meta breaker is paid for the rare endings
                    {
                        long tot = 0; for (int ci = 0; ci < metaF.Length; ci++) tot += rs[2 + ci] + rs[8 + 2 + ci];
                        if (tot > 0)
                        {
                            for (int ci = 0; ci < metaF.Length; ci++) { double f = (double)(rs[2 + ci] + rs[8 + 2 + ci]) / tot; metaF[ci] = metaInit ? 0.8 * metaF[ci] + 0.2 * f : f; }
                            metaInit = true;
                            for (int ci = 0; ci < metaF.Length; ci++) mbBonusArr[ci] = SimDuelEnv.RoleCats[ci] is "safe" or "other" ? 0f : (float)(mbBonus * (1 - metaF[ci]));
                        }
                    }
                    for (int r = 0; r < 2; r++)
                    {
                        long n = rs[r * 8]; if (n == 0) continue;
                        sb.Append(FormattableString.Invariant($" {(r == 0 ? "att" : "def")} {(double)rs[r * 8 + 1] / n:F2} n{n} ["));
                        for (int ci = 0; ci < SimDuelEnv.RoleCats.Length; ci++) if (rs[r * 8 + 2 + ci] > 0) sb.Append(FormattableString.Invariant($"{SimDuelEnv.RoleCats[ci]} {rs[r * 8 + 2 + ci]} "));
                        sb.Length--; sb.Append(']');
                    }
                }
                sb.Append(FormattableString.Invariant($" rew {(wEpAll > 0 ? wRew / wEpAll : 0):F2} len {(wEpAll > 0 ? wLen / wEpAll * cur.DecisionSec : 0):F0}s ent {st.Entropy:F2}/{st.EntCoef:F4} kl {st.Kl:F4} vl {st.VLoss:F3} ev {st.ExplVar:F2} rsd {st.RetStd:F2} inter {env.Cur.InterScale:F2} sps {wSps / wN:F0}"));
                if (cfg.KlAdapt > 0) sb.Append(FormattableString.Invariant($" lrm {st.LrMult:F2}"));
                sb.Append(FormattableString.Invariant($" | ro {st.RolloutSec:F2}s (w {st.WorkerMin:F2}-{st.WorkerMax:F2}; cpu obs {st.TObs:F2} inf {st.TInfer:F2} opp {st.TOpp:F2} step {st.TStep:F2}) tr {st.TrainSec:F2}s (gae {st.TGae:F2} mb {st.TMinibatch:F2} adam {st.TAdam:F2}) opp"));
                for (int k = 0; k < opps.Length && k < st.OppSec.Length; k++) sb.Append(FormattableString.Invariant($" {opps[k].Name}:{st.OppSec[k]:F2}"));
                if (gpu != null) { sb.Append(" | gpu/" + printEvery + "u " + gpu.Prof() + " fails " + tr.GpuFailures + (trX != null ? "/" + trX.GpuFailures : "")); gpu.TUp = gpu.TFwd = gpu.TBwd = gpu.TDown = gpu.TBegin = gpu.TW = 0; }
                if (trX != null) sb.Append(FormattableString.Invariant($" | mx wr {(xEp > 0 ? xWinX2 / (2.0 * xEp) : 0):F2} n{xEp} age {xSince} resets {xResets} added {xAdded}"));
                if (trL != null) { var (mn, nq, tot) = lview.MinWinRate(20); sb.Append(FormattableString.Invariant($" | lx minwr {mn:F2} {nq}/{tot} age {lxSince} snaps {lxAdds} resets {lxResets}")); }
                if (trM != null) { var ms = envM.TakeRoleStats(); long w = 0, n = 0; var cats = new StringBuilder(); for (int r = 0; r < 2; r++) { n += ms[r * 8]; w += ms[r * 8 + 1]; } for (int ci = 0; ci < SimDuelEnv.RoleCats.Length; ci++) { long c = ms[2 + ci] + ms[8 + 2 + ci]; if (c > 0) cats.Append(SimDuelEnv.RoleCats[ci]).Append(' ').Append(c).Append(' '); } sb.Append(FormattableString.Invariant($" | mb wr {(mbEp > 0 ? mbWinX2 / (2.0 * mbEp) : 0):F2} n{mbEp} age {mbSince}/{mbCycle} cycles {mbCycles} added {mbAdds} [{cats.ToString().TrimEnd()}]")); }
                if (!double.IsNaN(lastScore)) sb.Append(FormattableString.Invariant($" score {lastScore:F3} best {bestScore:F3}"));
                Console.WriteLine(sb.ToString());
                Array.Clear(wEp); Array.Clear(wWin); Array.Clear(wDraw); wRew = wLen = wSps = 0; wEpAll = wN = 0;
            }
            if (timeUp) { reason = "minutes"; break; }
            if (bench)
            {
                int bu = st.Update - benchStart;
                Console.WriteLine(FormattableString.Invariant($"bu {bu}{(bu <= benchWarm ? " (warmup)" : "")} ro {st.RolloutSec:F3}s (w {st.WorkerMin:F2}-{st.WorkerMax:F2}; cpu obs {st.TObs:F2} inf {st.TInfer:F2} opp {st.TOpp:F2} step {st.TStep:F2}) tr {st.TrainSec:F3}s (gae {st.TGae:F3} mb {st.TMinibatch:F3} adam {st.TAdam:F3}) total {st.RolloutSec + st.TrainSec:F3}s sps {st.StepsPerSec:F0} kl {st.Kl:F5} vl {st.VLoss:F4} ent {st.Entropy:F3} lrm {st.LrMult:F2}"));
                if (bu > benchWarm) { bRo += st.RolloutSec; bTr += st.TrainSec; bObs += st.TObs; bInf += st.TInfer; bOpp += st.TOpp; bStep += st.TStep; bMb += st.TMinibatch; bAdam += st.TAdam; bGae += st.TGae; bSps += st.StepsPerSec; bKl += st.Kl; bN++; }
                if (bu >= benchWarm + benchN) { reason = "bench"; break; }
            }
        }
        if (bench)
        {
            double d = Math.Max(1, bN);
            Console.WriteLine(FormattableString.Invariant($"BENCH mean of {bN} updates (envs {cfg.NumEnvs}x{cfg.Steps}, threads {cfg.Threads}, tile2 {MlpOps.Tile2}, split-critic {PpoTrainer.SplitCritic}, batch-opp {PpoTrainer.BatchOpp}, kl-adapt {cfg.KlAdapt}): ro {bRo / d:F3}s (cpu obs {bObs / d:F2} inf {bInf / d:F2} opp {bOpp / d:F2} step {bStep / d:F2}) tr {bTr / d:F3}s (gae {bGae / d:F3} mb {bMb / d:F3} adam {bAdam / d:F3}) total {(bRo + bTr) / d:F3}s sps {bSps / d:F0} kl {bKl / d:F5}"));
            try { Directory.Delete(dir, true); } catch { }
            return 0;
        }
        string ck = Save();
        string pol = Path.Combine(dir, "policy.btcpol");
        File.WriteAllBytes(pol, Exp().Save());
        Console.WriteLine($"done ({reason}) update={tr.UpdateCount} step={tr.StepCount} wall={wall.Elapsed.TotalMinutes:F1}m stage={stage} best={bestScore:F3}@u{bestUpdate} ckpt={ck} policy={pol} {league.Describe()}");
        return 0;
    }

    // =====================================================================================
    static (double lo, double hi) Wilson(int k, int n, double z = 1.96)
    {
        if (n == 0) return (0, 1);
        double p = (double)k / n, d = 1 + z * z / n, c = p + z * z / (2 * n), m = z * Math.Sqrt(p * (1 - p) / n + z * z / (4.0 * n * n));
        return ((c - m) / d, (c + m) / d);
    }

    static void PrintStats(string aName, string bName, DuelCurriculum cur, MatchStats s)
    {
        int n = Math.Max(1, s.N);
        var (lo, hi) = Wilson(s.W, s.N);
        Console.WriteLine(FormattableString.Invariant(
            $"{aName} vs {bName} [{cur.Describe()}] n={s.N}: W {s.W} D {s.D} L {s.L}  wr {(double)s.W / n:F3} [95% {lo:F3}-{hi:F3}]  len {s.Len / n:F1}s  kills {(double)s.KillsA / n:F2} losses {(double)s.KillsB / n:F2} per match  ({s.N / Math.Max(1e-9, s.Sec):F1} matches/s)"));
        Console.WriteLine(FormattableString.Invariant($"  honest wr {s.HonestWinRate:F3}  {s.ByText()}"));
        Console.WriteLine("  end reasons: " + string.Join("; ", s.Reasons.OrderByDescending(kv => kv.Value).Take(5).Select(kv => kv.Value + "x " + kv.Key)));
    }

    public static int Eval(Args a)
    {
        LowPriority();
        string polPath = a.Str("policy", "");
        // --sample 1 (default) samples from the policy (how it is trained and how the mod plays); --sample 0 = argmax. A bare --sample = 1.
        bool sample = a.Str("sample", "1") is not ("0" or "false");
        var me = new PolicyRef(DuelOpponents.LoadPolicy(polPath));
        string ctl = a.Str("controller", "env");
        DuelOppDef aDef;
        string tag = Path.GetFileNameWithoutExtension(polPath) + (sample ? ",sample" : ",argmax");
        if (ctl == "lumina")
            // the shipped in-game controller (BtcDuelBotLuminaZero) running inside the host: the parity check. Its [ThreadStatic] Argmax is
            // set per worker thread by the spec factory at every episode reset.
            aDef = DuelOpponents.Lumina(me.Policy, !sample, "lumina(" + tag + ")");
        else if (ctl == "env") aDef = new DuelOppDef { Name = "env(" + tag + ")", Bot = null, Create = () => new PolicyOpponent(me, sample) };
        else throw new ArgumentException("--controller env|lumina");
        return Matches(a, aDef, a.Str("opp", "prod4"));
    }

    public static int Match(Args a)
    {
        LowPriority();
        var aDef = DuelOpponents.Get(a.Str("a", "prod4"));
        return Matches(a, aDef, a.Str("b", "hunter"));
    }

    static int Matches(Args a, DuelOppDef aDef, string bList)
    {
        int n = a.Int("n", 200), threads = a.Int("threads", Environment.ProcessorCount);
        ulong seed = (ulong)a.Long("seed", 12345);
        var cur = Curriculum(a, "5", "1", 1, 0, "60", 300);
        foreach (var kv in a.Unused) throw new ArgumentException("unknown option --" + kv.Key);
        foreach (var bName in bList.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var bDef = DuelOpponents.Get(bName.Trim(), new PolicyRef(null));
            var s = DuelMatchRunner.Run(aDef, bDef, cur, n, threads, seed);
            PrintStats(aDef.Name, bDef.Name, cur, s);
        }
        return 0;
    }

    // =====================================================================================
    public static int Bench(Args a)
    {
        LowPriority();
        int units = a.Int("units", 10), threads = a.Int("threads", Environment.ProcessorCount);
        double sec = a.Dbl("sec", 4);
        var cur = Curriculum(a, units.ToString(CultureInfo.InvariantCulture), "1", 1, 0, "60", 120);
        Console.WriteLine($"bench units={units}v{units} {sec}s per case, threads 1 and {threads}");
        foreach (var (label, aName, bName) in new[] { ("bots prod4 vs prod4", "prod4", "prod4"), ("no bots (External, Continue only)", "ext", "ext") })
            foreach (int th in new[] { 1, threads }.Distinct())
            {
                var aDef = DuelOpponents.Get(aName); var bDef = DuelOpponents.Get(bName);
                var env = new SimDuelEnv(th, cur, new[] { bDef }, aDef.External ? null : aDef);
                long steps = 0, eps = 0, frames = 0;
                var sw = Stopwatch.StartNew();
                Parallel.For(0, th, new ParallelOptions { MaxDegreeOfParallelism = th }, t =>
                {
                    int M = env.MaxAgents; var a0 = new int[M]; var a1 = new int[M]; var rew = new float[M];
                    long s = 0, e = 0, f = 0; ulong k = 0;
                    env.Reset(t, Rng.Mix(99, (ulong)t));
                    while (sw.Elapsed.TotalSeconds < sec)
                    {
                        for (int i = 0; i < 20; i++)
                        {
                            env.Step(t, a0, a1, rew, 0, out bool done, out _); s++;
                            if (done) { e++; f += env.Host(t).Sim.Frame; env.Reset(t, Rng.Mix(100 + (ulong)t, ++k)); }
                        }
                    }
                    Interlocked.Add(ref steps, s); Interlocked.Add(ref eps, e); Interlocked.Add(ref frames, f);
                });
                double el = sw.Elapsed.TotalSeconds;
                Console.WriteLine(FormattableString.Invariant(
                    $"  {label,-36} threads {th,2}: {steps / el,9:F0} decision steps/s  {eps / el,7:F2} matches/s  ({frames / Math.Max(1, eps) * 0.02:F0}s sim per match, {steps / el * 0.1:F0}x realtime)"));
            }
        return 0;
    }

    // =====================================================================================
    public static int View(Args a)
    {
        LowPriority();
        DuelOppDef aDef;
        if (a.Has("policy"))
        {
            var me = new PolicyRef(DuelOpponents.LoadPolicy(a.Str("policy", "")));
            bool sample = a.Flag("sample");
            aDef = new DuelOppDef { Name = "policy", Bot = null, Create = () => new PolicyOpponent(me, sample) };
        }
        else if (a.Has("script"))
        {
            // movement diagnostics: every live unit gets the same action every decision (e.g. 34 = Run12 towards the enemy, 50 = AttackSlot 0)
            int act = a.Int("script", DuelActions.Run12);
            aDef = new DuelOppDef { Name = "script" + act, Bot = null, Create = () => new ConstActionOpponent(act) };
        }
        else aDef = DuelOpponents.Get(a.Str("a", "prod4"));
        var bDef = DuelOpponents.Get(a.Str("opp", "prod4"), new PolicyRef(null));
        ulong seed = (ulong)a.Long("seed", 1);
        var cur = Curriculum(a, "1", "1", 1, 0, "60", 300);
        if (a.Flag("lzdump"))
        {
            // print the LuminaZero controller observation dumps (LZOBS, same code as in the game) of this match
            Btc.Bots.SimBotSetup.GlobalLogSink = (t, tag, msg) => { if (tag == "LZOBS") Console.WriteLine(FormattableString.Invariant($"{t:F2}|LZOBS|{msg}")); };
            var lz = typeof(Btc.Bots.SimBotHost).Assembly.GetTypes().First(x => x.Name == "BtcDuelBotLuminaZero");
            lz.GetField("ObsDump", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, true);
        }
        foreach (var kv in a.Unused) throw new ArgumentException("unknown option --" + kv.Key);
        var env = new SimDuelEnv(1, cur, new[] { bDef }, aDef.External ? null : aDef);
        var oa = aDef.Create(); var ob = bDef.Create(); var rng = new Rng(seed);
        int M = env.MaxAgents; var a0 = new int[M]; var a1 = new int[M]; var rew = new float[M];
        env.Reset(0, Rng.Mix(seed, 100000));
        var i0 = env.Info(0);
        Console.WriteLine(FormattableString.Invariant($"view {aDef.Name} (west) vs {bDef.Name}: {i0.Units0}v{i0.Units1} arena x{i0.Arena} skips={i0.Skips} heal={i0.Heal} fps={i0.Fps:F0}"));
        Console.WriteLine("    t | alive A/B |  hp A / B   | mean x A / B  | min gap | kills A/B | ctrl A/B | orders | rew A");
        bool done; int outcome; double rsum = 0; int lastSec = -1;
        do
        {
            if (aDef.External) oa.Act(env, 0, 0, rng, a0); else Array.Clear(a0);
            ob.Act(env, 0, 1, rng, a1);
            env.Step(0, a0, a1, rew, 0, out done, out outcome);
            for (int k = 0; k < M; k++) rsum += rew[k];
            var h = env.Host(0); var s = h.Sim;
            int sec = (int)h.FightTime;
            if (sec != lastSec || done)
            {
                lastSec = sec;
                int al0 = 0, al1 = 0; float hp0 = 0, hp1 = 0, x0 = 0, x1 = 0, gap = float.MaxValue;
                for (int u = 0; u < s.Count; u++)
                {
                    if (s.Dead[u]) continue;
                    if (s.Side[u] == 0) { al0++; hp0 += s.Hp[u]; x0 += s.X[u]; } else { al1++; hp1 += s.Hp[u]; x1 += s.X[u]; }
                    for (int v = 0; v < s.Count; v++)
                    {
                        if (s.Dead[v] || s.Side[v] == s.Side[u] || s.Side[u] != 0) continue;
                        float dx = s.X[u] - s.X[v], dz = s.Z[u] - s.Z[v]; gap = Math.Min(gap, MathF.Sqrt(dx * dx + dz * dz));
                    }
                }
                Console.WriteLine(FormattableString.Invariant(
                    $"{h.FightTime,5:F1} | {al0,4}/{al1,-4} | {hp0,5:F0}/{hp1,-5:F0} | {(al0 > 0 ? x0 / al0 : 0),5:F1}/{(al1 > 0 ? x1 / al1 : 0),-6:F1} | {(gap < 1e6 ? gap : 0),7:F1} | {s.Kills[0],4}/{s.Kills[1],-4} | {s.Ctrl[0],3:F0}/{s.Ctrl[1],-4:F0} | {h.OrderCount,6} | {rsum,6:F2}"));
            }
        } while (!done);
        var inf = env.Info(0);
        Console.WriteLine($"end: winner={inf.Winner} ({(outcome > 0 ? "A" : outcome < 0 ? "B" : "draw")}) reason='{inf.Reason}' t={inf.FightTime:F1}s decisions={inf.Decisions} orders={inf.Orders}");
        return 0;
    }
}

/// <summary>Diagnostics opponent: the same action for every live unit on every decision (masked actions fall back to Continue).</summary>
sealed class ConstActionOpponent : IOpponent
{
    readonly int _act;
    float[] _obs, _cen; byte[] _mask; bool[] _active;
    public ConstActionOpponent(int act) { _act = act; }
    public void Act(IEnvBatch env, int e, int side, Rng rng, int[] actionsOut)
    {
        int M = env.MaxAgents, L = 0; foreach (var h in env.HeadSizes) L += h;
        if (_obs == null) { _obs = new float[M * env.ObsSize]; _cen = new float[Math.Max(1, M * env.CentralSize)]; _mask = new byte[M * L]; _active = new bool[M]; }
        env.Observe(e, side, _obs, 0, _mask, 0, _cen, 0, _active, 0);
        for (int a = 0; a < M; a++) actionsOut[a] = _active[a] && _mask[a * L + _act] != 0 ? _act : DuelActions.Continue;
    }
}

