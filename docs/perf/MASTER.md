# PERF MASTER PROMPT (persisted; read before any perf/profiler/optimization work)

## Goal
PvP gameplay must fly: no freezes/lag spikes, smart fast pathfinding, instant unit response, no AFK bricktrons. Graphics/visuals may be sacrificed. Replay load/seek as fast as is cheap.

## Rules
- Gameplay optimizations change simulation: apply ONLY when `BtcNetworkGate.Enabled` (BetterConnection ON), read at call time. OFF = vanilla. Graphics cuts are local (no wire), behind config toggles.
- Profiler and test automation are DEV only (`#if DEV`, `Dev/BtcDev*`). Never in Release.
- Test every new mod feature IN THE GAME via scenario + logs. NO screenshots (expensive). Logs are compact, fixed-format, agent-oriented; read them through subagents (`log-analyst`), never raw.
- Game is launched hidden (minimized/offscreen, muted, runInBackground). Never pop a window over the user. Never kill a game the user is playing.
- Cost guard: warn the user before anything likely >50% of the 5-hour limit and offer cheaper options.

## Pipeline
0. Persist docs/memory, hidden launch, replay retest, remove old Performance tab.
1. `Dev/BtcDevProf.cs` (sonnet): frame stats, targeted method timing, spike attribution, counters, idle-worker detector -> `BTClogs/prof.txt` (<=60 lines). `scripts/prof-diff.ps1 A B`.
2. Haiku swarm: `docs/perf/targets/<domain>.txt` (`Type:Method  # why`, <=80 each): pathfinding/nav; AI/labor/profession; world/blocks/physics/projectiles; net/replay/UI/Lua/loader.
3. `Dev/BtcDevCmd.cs` polls `BTClogs/cmd.txt` (replay, replay.speed, replay.seek, prof start|stop|dump, wait, timescale, quit; later newgame/host/join/spawn). `scripts/scenario.ps1 -Name X [-Clients 1]` with `tests/scenarios/*.txt`. Stress: recorded `.btcr` replays first, then host+client (2 windows, one PC).
4. Loop: log-analyst ranks culprits -> hook-porter (sonnet) / net-architect (opus, warn first) fix -> rerun scenario + prof-diff -> commit. Later: new Performance tab with checkboxes.

Full plan: `~/.claude/plans/robust-leaping-biscuit.md`. Memory: `project-perf-plan`.
