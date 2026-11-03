# LuminaBuild (LB): RL agent for the fastest economy rush (8 halberdier kits + 2 capture wards)

## Context
The user wants a second RL model next to LuminaZero (combat). Task: starting from 6 bricktrons on a cropped training map (the user supplies it), produce 8 halberdier kits (halberd + kettle hat lying on weapon racks) and 2 crafted capture wards **as fast as possible**. The agent gets the full player toolset (per-unit orders, tasks, blueprints, stockpile filters, crafting queues, priorities, autonomy). It sees the whole map incl. voxel types. Rewards are one-shot milestones. A run ends as a loss after 60 s game time without useful progress. Train in a fast sim that matches the real game, on the user's mod settings. Training starts only on the user's command.

User decisions (2026-11-02): strictly 6 workers (no spawning); stall = 60 s without a new maximum of any useful counter; wards only crafted (not placed); the user will play 3-5 demo runs for calibration and imitation.

Facts found:
- The existing lab (`lab/`, .NET 10, custom AVX MLP + PPO/MAPPO + league, `Btc.Shared` net35 shared with the mod) is a flat 2D melee duel sim. **It has no economy, voxels, items or buildings.** Reusable: PpoTrainer, Adam, MlpKernels, checkpoint/.btcpol formats, CLI/MCP job tooling, the fidelity method (measured params, log replay, closed-loop gates).
- Game mechanics are all C# (`ref/vanilla`): Labor/AutonomyStatus/Profession, `Order.*`, Projects (Build/Harvest/Mine/Tunnel/Landscape/Cleanup/Repair) with `ProjectPriority` weights + `Bias` {Low 0, Medium 4, High 8, Locked -10}, Blueprint/Architecte, Recepteur (filters) + ResourceConverter (full pile of Stones -> StoneBlock), CraftingStation/Recipe/CookBook, column-based VoxelEngine, TreeRegistry.
- Recipes: Planks = 1 log -> 3 planks (work 3); IronIngot = 10 raw iron (heat 2); Poleaxe = 1 plank (work 10); KettleHat = 1 iron (work 10); CaptureWard = 40 orange crystal + 1 iron (work 10).
  Minimum for the goal: 8 planks (3 logs), 10 ingots = 100 raw iron + 20 crystal heat, 80 crystal for wards -> about 100 raw iron, about 100 orange crystal, plus building costs (prefab data, to be dumped).
- Every player action already goes through hooked UNet commands (101 replay prefixes in `Patches/UNet*Cmd*Patches.cs`). That gives one command grammar for the sim, the real-game executor and the user's demo recording.
- The user's mod settings (`game/csmod/BetterThanCastle/configs/gameplay.txt`): `mov.simple=1` (items fall straight down), `mov.items=1` (walk through free blocks), `mov.retry=1`, `mov.cancel=1`, `bai.delayOn=1` with delay 0 (no hourglass), `bai.wait=1` (work search every tick), `bai.build=1` (sane build assignment), `bai.hold=5`, `eco.stable=1`. The sim implements exactly these and refuses to run if the settings snapshot differs.

## Architecture

### 1. Command grammar (shared, `lab/Btc.Shared/Eco/EcoCommands.cs`, net35)
One enum per player-visible command, 1:1 with the UNet Cmds, with parameter slots (actor, target, param):
- **Unit:** move, chop tree, dig voxel, harvest nature, pick up (from item/container), drop at, store into, smart transfer, transform log, work at station, repair, break object, stop, set autonomy (wait/hold/free), change project, equip/unequip tool, equip from rack / store into rack, clean up.
- **Projects:** spawn harvest (types), mine/quarry (bounds, stair), tunnel (dir, type), landscape, cleanup; toggle/set resource; raise/lower priority; add/remove capacity; delete project; commit / cancel.
- **Blueprints:** place (key, pos, rot), anti-blueprint, destroy, send to new project.
- **Stockpile/converter:** accept all / accept only / keep empty, add/remove exclusion, manual conversion, set auto-convert.
- **Crafting:** queue recipe (id, qty), remove, loop, produce, do nothing, assign worker, add/remove resources.
- Everything the player can do is expressible. Masks forbid invalid commands, and spawning is masked off.

### 2. Data capture in the real game (mod, DEV only: `src/BetterThanCastle/Dev/BtcDevEco*.cs`)
- `ecodump`: exports the loaded map to `BTClogs/eco-map.bin`. It reads every column through `VoxelEngine.GetColumnData`/`Voxel` (terrain type per voxel), plus trees (TreeRegistry), nature objects, items, buildings, stockpiles, units, blueprint catalogue with costs and footprints, rack capacity, stockpile `BaseCapacity`, and the CookBook id table.
- `ecotrace`: an event log in the duel-log format (`t|TAG|msg`) built on the existing Cmd prefixes. Tags: CMD (every command), TASK (goal start/end), INV (unit carry), STOCK (container contents), CRAFT (recipe work/heat ticks), BLD (block placed), MILE (milestone), SNAP (positions every 0.2 s). The user's demo runs and hidden scripted runs both produce it.
- `ecoplay <file>`: executes a command script in the real game, so sim action logs can be replayed for the closed-loop check.
- DEV scenarios `eco-walk`, `eco-dig`, `eco-chop`, `eco-carry`, `eco-build`, `eco-craft` measure timings into `docs/eco/measured.md`: walk/climb/jump speeds, dig time per terrain type, chop, pick-up/drop, carry capacity per resource, block placement, work-hit rate, heat rate, conversion. Each value is tagged [measured]/[code]/[guess] like the duel SimParams.

### 3. Simulator `lab/Btc.EcoSim/` (new project, deterministic, allocation-free Step)
- **World:** a dense voxel grid sized to the cropped map (uint8 terrain type, block id), column tops cached, TreeRegistry clone, item entities (simple physics: vertical fall to the ground, units walk through items).
- **Navigation:** a voxel walk graph (step-up/climb/jump/drop rules taken from measurement). BFS distance fields per target are cached and invalidated when terrain changes locally. This is the CPU hot spot.
- **Units:** an action state machine per order (walk -> act -> carry), carry inventory, autonomy states, mod behaviour (`mov.retry`, `mov.cancel`, hourglass 0, work search every tick, hold 5 s).
- **Projects:** goal lists and the `ProjectPriority` scoring formula with the vanilla weight presets plus `Bias`. The worker pick follows Labor.AssignToIdle. This part has the biggest fidelity risk, so manual orders get exact fidelity first.
- **Buildings:** blueprint validity, delivery of block resources, build time, then the station spawns. CraftingStation/Recipe (work/heat/ingredients, queue, loop, AutoStore), Recepteur with filters and capacity, ResourceConverter (full pile of stones -> bricks), weapon rack slots.
- **Speed:** a 30 Hz AI tick plus event-driven time skipping when every unit is in a long action. Target ≥1000x realtime per thread, so a 15-minute run takes about 1 s and 15 threads give about 50k runs per hour.
- **Gates:** `EcoSelfTest` asserts every [measured] value. `StateHash` checks determinism.

### 4. Fidelity (the "everything must match" requirement)
- **(a)** Micro timings: self-test against `measured.md`.
- **(b)** Open-loop: replay the user's demo command streams in the sim. Each milestone time must fall within ±10% (or ±5 s) of the real one.
- **(c)** Closed-loop: replay sim action logs of the scripted expert and of policies in the real game (`ecoplay`, hidden, timeScale up) and compare milestone timelines. A checkpoint goes to the user only after (c) passes.

### 5. RL env `lab/Btc.RL/Eco/`
- **Decisions are event-driven:** a decision happens when any unit becomes idle, finishes a goal or a project/station changes state, plus a heartbeat every 2 s. Each decision issues 0..k commands (an autoregressive "another command / done" head).
- **Observation:**
  - (a) A spatial map of the whole cropped map as 2D column channels: top height, top terrain type one-hot (dirt/grass/stone/iron/orange crystal/blue crystal/crust...), ore counts in depth bands, trees, items by type, blueprints/buildings, unit positions, reachability distance from the base.
  - (b) An entity list: 6 units, stations, stockpiles, racks, projects, blueprints. Each has type, state, inventory, queue, filters and priority.
  - (c) Globals: inventory totals, milestone bits, time, time since the stall timer reset.
- **Action heads (autoregressive):** command type -> actor (pointer over entities) -> target (pointer over entities, OR a spatial cell from the map head, with the voxel picked by a rule plus a depth sub-head for dig/tunnel/quarry) -> parameters (resource type, recipe, qty, autonomy, rotation, size). Validity masks apply at every head.
- **Network:** new layers in Btc.RL: a small conv / patch encoder for the map, an entity transformer (this also serves LuminaZero's planned entity attention), and pointer heads. A CPU AVX implementation like the existing MlpKernels, with inference duplicated in `Btc.Shared` (net35) for the real game.
- **Rewards (each paid once per episode):**
  - Milestones from the brief: first chopped tree, stump/chopping block, first planks, stockpile, first bricks (80 stones on one stockpile with auto-convert on), furnace, iron ingot production started, weapon rack, workbench, first halberd, first kettle hat, altar, first capture ward.
  - Intermediate correct steps, also one-shot: first log delivered, first raw iron/crystal mined, first ore in the furnace, first finished ingot.
  - Goal counts, one-shot per new maximum: kit k=1..8 complete on a rack, ward 2.
  - Terminal win: +big × (1 − T/Tmax), plus a small per-second time penalty to drive speed.
- **Stall rule:** a timer resets on a new maximum of any useful counter (logs, planks, raw iron, crystal, ingots, blocks placed on required buildings, craft work/heat ticks, items on the rack). 60 s game time without a reset = loss (−1). Hard cap Tmax, for example 40 minutes game time.
- **Curriculum:** start states from mid-game snapshots (stations already built) so the late stages are learned early; then full runs.

### 6. Training pipeline
1. A scripted expert `EcoScript` (hand-written manual-control build order). It is the baseline and the first fidelity probe.
2. Behaviour cloning from the user's demos plus EcoScript traces.
3. PPO fine-tune (single agent, centralised value) with the existing PpoTrainer, checkpoints and eval (median and p90 time to goal, stall rate).
4. Start **only on the user's command**: `btclab eco-train --run lb1`.

### 7. Real-game executor
`BtcEcoAgent` (DEV first) loads the `.btcpol`, builds the same observation from the live game and issues the same Cmds. It is used for gate (c) and for the user to watch.

## Scale and cost (rough)
| Phase | Content | Cost (% of a 5-hour limit) |
|---|---|---|
| P0 | Grammar, ecodump, ecotrace, ecoplay, measurement scenarios | ~15% |
| P1 | EcoSim world, navigation, units, items, stockpiles, crafting, blueprints, projects | ~45% (largest, ~6-8k lines) |
| P2 | Fidelity gates (a)-(c) on demos and EcoScript | ~15% |
| P3 | Env, obs/action heads, conv + attention layers, rewards, BC + PPO wiring | ~30% |
| P4 | Real-game executor | ~10% |

Total: about 1.2 limit windows of agent work, spread over several sessions. Heavy work goes to the sim-engineer (sonnet) agent; summaries come from cheap agents. This does not collide with the running LuminaZero session: only new projects/files, plus shared `Btc.RL` layers added without changing existing ones.

## Files (new unless noted)
- `lab/Btc.EcoSim/*` (World, Nav, Units, Items, Projects, Crafting, Blueprints, EcoParams, EcoSelfTest, EcoLogReplay)
- `lab/Btc.Shared/Eco/{EcoCommands,EcoObs,EcoView}.cs` (net35)
- `lab/Btc.RL/Eco/{EcoEnv,EcoReward,EcoScript,EcoBc}.cs`, plus `lab/Btc.RL/Nn/{Conv,Attention,Pointer}.cs`
- `lab/Btc.Lab.Cli/EcoCommands.cs` (eco-bench, eco-replay, eco-fidelity, eco-train, eco-eval), and `Btc.Lab.sln` (edit)
- `src/BetterThanCastle/Dev/BtcDevEco{Dump,Trace,Play}.cs`, `tests/scenarios/eco-*.txt`
- `docs/eco/{DESIGN,measured}.md`, `docs/decisions.md` (append)

## Verification
- `lab_build` with 0 errors; `btclab eco-selftest` all asserts pass; determinism test (same seed gives the same StateHash).
- `btclab eco-bench`: at least 1000x realtime per thread.
- EcoScript reaches the goal in the sim; `ecoplay` of its log in a hidden real game matches the milestone times within ±10%.
- The user's demo replays (gate b) are within tolerance.
- Mod DEV build: `errors.ps1`, then `smoke.ps1` SMOKE OK; the Release build plus `verify-dist` show no `BtcDev*` (all eco capture is in `Dev/`).
- No training run before the user's explicit command.

## Inputs needed from the user
- The cropped map (save or map file).
- 3-5 demo runs with `ecotrace` on (after P0).

## File formats (P0 contract between mod DEV capture and lab)

### eco-map.bin (written by DEV `ecodump`, read by `Btc.EcoSim.EcoMapLoader`)
Little-endian. Y is up. All voxel coords are integer world voxel coords.
1. magic `BTCE` (4 bytes), u16 version = 1.
2. i32 originX, originY, originZ (world voxel coords of grid cell 0,0,0); u16 sizeX, sizeY, sizeZ.
3. u8 terrain[sizeX*sizeY*sizeZ], index = (x*sizeZ + z)*sizeY + y (column-major, matches VoxelEngine columns). Value = VoxelTerrainType byte (0 empty; Earth 6, Stone 8, Iron 54, OrangeCrystal 44, BlueCrystal 38, crusts 55/57/59, ...).
4. u16 block[same size]: 0 = no placed block, else 1-based index into the block-key table (section META `blk|i|key`).
5. i32 metaBytes, then UTF-8 text, one record per line, `|` separated:
   - `gp|<BtcGameplay encoded own settings>` (sim refuses to run on a different snapshot)
   - `blk|<i>|<block key>|<resource key>`
   - `tree|x|y|z|treeType` ; `nature|type|x|y|z` (plant/boulder/iron/pyrite)
   - `ent|<netId>|<kind>|<key>|px|py|pz|rotY|<k=v;...>` kinds: unit, item, stockpile, station, rack, building, blueprint, project, crystal
   - `bp|<blueprint key>|<factory>|<cost: res*qty,...>|<footprint dx,dy,dz,...>|<k=v;...>`
   - `recipe|<id>|<name>|<inputs res*qty,...>|work|heat|<creates>|qty`
   - `cap|<container key>|<BaseCapacity>`
   - `param|<name>|<value>` (any engine constant read from code at dump time)

### eco trace (DEV `ecotrace 1`, file BTClogs/eco-<stamp>.txt; also <game>/BetterThanCastle/eco-logs/ when the user plays demos)
`t|TAG|msg`, t = game seconds since run start (3 decimals). Tags:
- `CFG|map=<name>;gp=<settings>` at start; `END|reason`
- `CMD|<CmdName>|<arg1>|<arg2>...` every player UNet command (args: netId for GameObjects/components, voxel `x,y,z`, floats 3 decimals)
- `TASK|<unitNetId>|start|end|fail|<goal kind>|<target>`
- `INV|<unitNetId>|<carried res>*<qty>` on change
- `STOCK|<containerNetId>|<res>*<qty>,...` on change
- `CRAFT|<stationNetId>|<recipe>|work=<n>|heat=<n>|done`
- `BLD|<blueprintNetId>|<blocks placed>/<total>|done`
- `VOX|x,y,z|<old>|<new>` terrain change
- `MILE|<milestone id>` (ids in docs/eco/DESIGN.md rewards)
- `SNAP|<netId>:x,y,z;...` every 0.2 s (units only)

## Change 2026-11-02 (user): no automatic worker AI
- Units never pick work by themselves: no idle job search (Labor.AssignToIdle), no ProjectPriority scoring, freeAgent behaves like waitForOrder.
- The action set is unchanged. Project/task commands stay; a worker works on a project only after an explicit change-project order from the model and then takes that project's goals by a simple rule (nearest open goal, [guess], to be measured).
- Real-game runs (ecoplay, BtcEcoAgent, demos for calibration) put every unit into waitForOrder at start so vanilla auto-assignment does not kick in; the executor re-asserts it when the game flips autonomy.
- Removes the biggest fidelity risk (section 3 "Projects").

## Change 2026-11-02 (user, after the first dump)
- Training map: Info/UGC/Maps/OrosiValley_L0 ("Orosi Valley For L0"), sandbox; dump 64x280x100 (origin 381,0,399), 282 trees, sandbox gives 5 bricktrons -> spawn a 6th at start.
- NO ore from walls: every ore voxel (Iron 54, OrangeCrystal 44, BlueCrystal 38, crusts 55/57/59) becomes Stone 8, in the sim loader AND in real-game runs (DEV `econoore`). Ore veins are random, the agent must not learn them. Iron / orange crystal come only from surface nature nodes (map iron.json, pyrite.json) and whatever else the game gives.
- Full player view: per-unit inventory (carried resource + qty, equipped tool) is in the observation; dropping/handing over carried items, picking up loose items, tools and everything else the player can do via selection stays in the action set.
- Placement phase (user): each episode starts with 7 s (real game: game clock frozen) in which the model puts each of the 6 workers on any walkable surface cell; nothing else is allowed. Sim: phase Placement before t=0 (lab-only command PlaceWorker), real game: DEV `ecoplace` (pause, teleport, resume), trace `PLACE|netId|x,y,z`.

## Training system (user, 2026-11-02): milestone archive + "start and wait" + several PCs
### Milestone archive (Go-Explore style reverse curriculum, no outside data)
- Every episode records a sim snapshot at each newly reached milestone (snapshot = sparse world diff + full entity state, < 1 ms save/load).
- Archive per milestone M: best K (e.g. 32) snapshots ranked by game time at M, kept diverse (different placements / build orders: dedupe by a coarse state signature).
- Episode start mix (auto-tuned): fresh start with the placement phase (always >= 30%, the only source for the honest metric), else a start from an archived snapshot of milestone M. M is picked by learning progress: weight high where the success rate of reaching the next milestone from M is between 0.2 and 0.8 (the frontier), low where it is ~0 or ~1.
- The clock is inherited: an episode started from the furnace snapshot at t=310 s keeps t=310, so reward for "goal at time T" is comparable across starts. One-shot milestone rewards before the snapshot are not paid again.
- Improvement flows backwards automatically: when a fresh or earlier-start run reaches M faster, its snapshot replaces a slower one in M's archive, so later stages train from better positions; the late stages being solved make earlier milestones the frontier.
- Honest metric = fresh starts only: median / p10 time to goal, success rate, stall rate.
### Autopilot (start once, then just wait)
- One command `ecolab train --run lbN` runs forever: auto curriculum (archive + frontier weights), periodic eval, best checkpoint by honest metric, rollback on regression (like LuminaZero --rollback), auto entropy floor, crash-safe resume (checkpoint + archive on disk every N minutes), compact status file `lab/runs/lbN/status.txt` + metrics.csv for rl-analyst / MCP.
### Distributed (several PCs)
- Learner on the main PC (PPO update, archive, checkpoints). Rollout workers on any PC: `ecolab worker --server <host>:<port> --threads N`, a self-contained single-folder .NET exe + the map file, no install.
- TCP protocol: worker pulls weights (version), start states (fresh or archive snapshot), pushes trajectories + new milestone snapshots. Stale trajectories are corrected with V-trace / PPO importance clipping, dropped if older than N versions. Workers can join/leave any time.
- Default assumption until the user says otherwise: Windows PCs on the same LAN; port configurable; no auth beyond a shared token in the command line.
- Update (user): the other PCs are over the internet, but now only the architecture is needed; the first runs are on ONE PC. Implementation order: (1) single-process trainer with in-process rollout workers behind an `IRolloutSource` interface (weights in, trajectories + snapshots out), (2) later the same interface over TCP for internet workers (learner port forwarded or a VPN like Tailscale, shared token, compression of trajectories). No networking code now.

## Mechanics facts from the user (2026-11-02, tag [user])
- Several workers on one crafting station share progress only when assigned manually (never auto-join).
- Iron / pyrite node: 40 resources, 1 per hit, then gone.
- A unit carries one resource type at a time.
- Planks: at the chopping block, a separate building from a blueprint (not the stump left by a tree).
- Capture ward: crafted at the Altar.
- Blueprints may be placed where they can never be built (shown red). Allowed (player can do it), never built, small penalty per invalid placement (InvalidBlueprintPenalty -0.02) so early random play wastes less time.

## Measured building costs (DEV ecobpcost, 2026-11-02; footprint = cells relative to the placement point)
- ChoppingBlock: WoodBlock*1 (4 cells) | Furnace: StoneBlock*4 | Workbench: PlankBlock*6 (4 cells) | Toolrack: PlankBlock*3 | SingleToolrack: PlankBlock*2 (1x3 tall)
- Altar: StoneBlock*1, Iron*2, OrangeCrystal*40, PlankBlock*3 (2 cells) | Forge: Iron*5, PlankBlock*3, StoneBlock*1 | Loom: PlankBlock*6 | Chest: WoodBlock*1 | Pallet: PlankBlock*1 | Wood_Stump: WoodBlock*1
- Stockpile is not a blueprint: created by the Accepter commands (CmdAccepterPosition/Rayon).
- Ore: SetVoxelType ore->stone is reverted by DeepLayer.GetDeepType; econoore patches it (DEV). Ore count differs per game (2914..3335): random, confirms the user's rule.
