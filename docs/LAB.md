# LAB: headless duel simulator + RL (LuminaZero) + btc MCP server
Read this before any lab/RL/sim work. Plan of record: C:\Users\LKFM\.claude\plans\mcp-synthetic-stonebraker.md.
Nothing in `lab/` is shipped; `dist/` stays one DLL. Only `lab/Btc.Shared/*.cs` and `lab/policies/lumina.btcpol` enter the mod (linked / embedded).

## Map (lab/Btc.Lab.sln, net10.0, zero NuGet)
| project | what |
|---|---|
| Btc.Shared | net35-safe code compiled into BOTH mod and lab: DuelView (engine-neutral snapshot), DuelObs (policy observation v2, Schema string), DuelActions (69 actions, masks, decode), SkipExecutor, MlpPolicy (.btcpol inference, schema hash) |
| Btc.Sim | DuelSim engine: voxel-cell world, 20 ms AI tick + render-frame movement, orders Move/Attack/MoveAttack, AttackInfo state machine, rules (safe, kill&escape, space control), LogReplay (rules parity, open-loop replay vs real logs), SimSelfTest |
| Btc.Bots | the mod's UNCHANGED bot sources (BtcDuelBot*, Hunter, Path, Lumina) compiled with shadow types (SimShadow/: UnityEngine, Brix, mod statics) + SimBotHost (reproduces BtcDuelMatch/DuelPatches events). Kinds = `duelplayerbot` kinds: 0 V1, 1 V2, 2 production, 3-7 Hunter family, 8 X, 9 LuminaZero |
| Btc.RL | batched MLP (AVX2, manual backprop), Adam, PPO/MAPPO trainer, checkpoints, Toy env, Duel/SimDuelEnv (real env) |
| Btc.Lab.Cli | `btclab` (see below) |
| Btc.Lab.Tests | console test runner, exit code = failures |
| Btc.Mcp | `btc` MCP server (docs/MCP.md) |

## Invariants
- Btc.Shared: no Span/System.Numerics/LINQ/ValueTuple; must compile in the mod (`scripts/errors.ps1`). Changing DuelObs/DuelActions layout => bump `DuelObs.Schema` (old policies are rejected by hash).
- Mod bot files may only get `#if SIM` lines (ThreadStatic); mod IL must stay identical.
- Sim Step: deterministic per seed, allocation-free. Every mechanics change => rerun SimSelfTest + replay-check and record numbers in docs/sim/fidelity.md.
- LuminaZero in game = `src/BetterThanCastle/Features/Duel/BtcDuelBotLuminaZero.cs`, the SAME class runs in the sim (SimBotHost kind 9). Evaluate exported policies with it, not only with the training env.

## Commands
- Build: `dotnet build lab/Btc.Lab.Cli/Btc.Lab.Cli.csproj -c Release -v q -nologo` (MCP: `lab_build`). Tests: `dotnet lab/Btc.Lab.Tests/bin/Release/net10.0/Btc.Lab.Tests.dll`.
- `btclab gradcheck | bench-mlp | toy-train | toy-eval | export` (toy env), `btclab train | eval | match | bench | view` (real sim). MCP: `lab {args, background, lowPriority}` + `job_wait`.
- Runs: `lab/runs/<name>/` (gitignored): metrics.csv, ckpt-*.bin, policy.btcpol. Ship a policy: copy to `lab/policies/lumina.btcpol` (embedded on next mod build) or for local tests to `game/csmod/BetterThanCastle/policies/lumina.btcpol` (override, no rebuild).
- Real game: difficulty "LuminaZero" in the Duel tab; DEV `duelplayerbot 1 9 4` puts LuminaZero on the human side (bot-vs-bot in hidden games via scripts/duel-par.ps1 / MCP duel_par).

## Docs
docs/sim/mech-*.md (vanilla mechanics with refs), docs/sim/measured.md (real-game lab measurements), docs/sim/fidelity.md (sim vs real gates), docs/MCP.md.

## LuminaZero v2 (obs/actions/rewards; schema `duelobs-v2|self43|e8x36|a6x32|g26|c552|act69`)
- Env (`--preset luminazero`, alias lumina2): learner units 4-15 uniform (SimDuelEnv.MaxUnitsPerSide 18), arena 3, skips .5, heal .5, handicap p .4 (opponent +1..3 units, `HandicapMin/Max`, side 1 only), domain randomisation kept. Skips are automatic (SkipCtl/SkipExecutor/cancel guard), the agent never learns them. Infinite heal = +10 hp per 1 s (SimParams.HealStep/HealPerStep).
- Obs (549 floats): self 43, 8 enemy x 36, 6 ally x 32 (nearest first), globals 26. Every unit: order kind, order dest (has, dx, dz rel to observer /20, dist of dest from that unit), order target (targets me / my ally / my enemy + rel pos), time since last order (1/(1+t)), strike in progress, hp, vel, attackers on it, type one-hot 8; enemies also "team focus" flag; globals add heal flag, army sizes/handicap, team centroids. Everything bounded (<= 6) and mirror-consistent (test v2-obs-features-mirror). `Encode` slotOut is `DuelObs.SlotCount` = 10: 8 enemy slots, [8] team focus (enemy most targeted by our units, nearest non-protected fallback), [9] enemy attacking me.
- Actions (69): 0 continue, 1 stop, run 2/5/12 m x 16 dirs (2..49), attack slot k<8 (50..57), attack focus (58), attack attacker (59, masked when none), attack-move 8 m x 8 dirs (60..67), retreat (68). Mask: cancel guard, enemy-safe destinations, attacking a unit in its own safe zone, empty slots.
- Central critic (MAPPO): critic input = obs (549) + central (552): 18 own + 18 enemy units x 15 (alive, pos, vel, hp, order kind/dest, has target, strike/hurt recency) + 12 globals; PpoTrainer concatenates per agent (central is encoded once per team and copied).
- Rewards (DuelCurriculum, normalised by army size): terminal +-1 (0 draw); kill +KillReward(.4)/N_enemy team-shared; death -DeathPenalty(.4)/N_own; damage dealt +DmgDealt(.3)*dmg/(N_enemy*MaxHp), taken -DmgTaken(.3)*dmg/(N_own*MaxHp); TeamShare(.5) mixes team-shared with per-unit (x N_own); InterScale multiplies all intermediate terms (`--inter-start 1 --inter-end .3 --inter-anneal STEPS`, default anneal over TotalSteps in the preset). Shaping 0, Indiv 0, OrderCost 0 in the preset. Plain `train`/`eval`/`match` keep the v1 reward (kill/death/damage knobs default 0 there; `--kill --death --dmg-dealt --dmg-taken --team-share`).
- Eval panel for luminazero (`DuelEval.Panel2`, 8 cells: 4v4, 10v10, 15v15, no skips, heal, 6v8, 8v11, 15v18) + a "split wr" line (skips/noskips, heal/noheal, even/handicap).

## Speed tools (2026-11-02, plan C:\Users\LKFM\.claude\plans\reactive-mapping-naur.md)
- `btclab bench-train [--ckpt F | --run X --resume] [--updates 5 --warmup 1]` + any train flags: N updates, nothing saved, per-update wall breakdown + `BENCH mean`. A/B switches (also in train, default ON): `--tile2`, `--split-critic` (critic central part once per team; CPU only, GPU keeps concat), `--batch-opp` (same-snapshot opponents batched per worker).
- `--kl-adapt T` (default 0 = off): actor lr x1.5 when KL < T/2, /1.5 when KL > 2T, clamp `--kl-adapt-min .1 --kl-adapt-max 10`, `--kl-adapt-critic 1` scales critic too; multiplier saved as optional ckpt trailer, log shows `lrm`.
- Measured: split critic -7..12% mb, -15% learner inference; batch-opp ~0 (pool of 26 snapshots, 2-3 envs per worker). Train (~80% of wall) remains the bottleneck: GPU and fewer samples (decision period, normalisation) are the real levers.
