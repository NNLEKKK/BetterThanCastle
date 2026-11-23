# Castle Story Plus 0.10.0 cross-check (2026-11-23)
Source: `CastleStoryPlus-0.10.0/` (untracked, GPLv3, Tricky12321). Their `docs/Editions.md` lists 62 "Essentials"
features (fixes, perf, worker AI, pathfinding; no content). Checked against BTC by name + headers.
0.9.3/0.10.0 changed no netcode and no perf code (FrameSavers/ModMessages: edition switch only).

## Already in BTC
Memory/* (GameLeakFixes, LuaHeapShrink, LuaStackShrink: BtcMemFix), FrameSavers (pure caches, see csp-audit.md),
PlayerOrdersFirst (mov.instant), PassThroughAllies + CrowdPathing (mov.allies, route search only), Pathfinding:
ImproveSearchLimit + KeepImprovedPath (mov.betterPaths), StraightPaths (mov.straight), HealthBarRefresh, FasterLoading
(partly, own FastLoad).

## Candidates (responsiveness / pathing / MP), not ported
| CSP | what | note for BTC |
|---|---|---|
| WorkerAI/LocomotionRecovery (StuckReset part, #156) | legs stuck in stun/thrown state after a missed anim "stand" event: unit takes no orders; reset after 2 s for player order | host only, bug fix, top pick |
| Combat/ArcherRetarget + Logic/BlockedShots (#165) | blocked target skipped 4-16 s from that block, front enemy first; ordered target -> walk to clear shot | host rule rng.* |
| Pathfinding/ClimbPastAllies | InPlace.Update waits for ally-free voxel before climb/step | our mov.allies does not touch the walk |
| Pathfinding/Logic/AllyClosures | no route after closed ally voxel -> reopen, 20 s "only way" | same bug in BtcAllyPass (1-wide stairs) |
| Pathfinding/ChillingPassAllies | idle ("chilling") workers pass allies | Builder.CanPassThrough postfix |
| Pathfinding/SearchBudgets | worker/build search 40 -> 100 nodes/tick | changes sim timing: BC ON |
| Pathfinding/SearchOwnVolume | skips ~512 useless node lookups per search slice (empty VoxelPresences) | pure perf, same result |
| Pathfinding/StaleNodeHeal | node left in solid voxel -> unit jumps between 2 cells forever | bug fix |
| Pathfinding/SearchFrameBudget | per-frame budget for all searches (spikes when waves start) | was "not taken"; measure |
| Pathfinding/HeuristicPatch, StairHeadroom, ReachMap | route shape / headroom / reach map | changes routes, later |
| WorkerAI/QuickNextTask | 10/30 tick wait after task -> 2 | was "not taken"; workers only |
| WorkerAI/BaseArea (#181) | unowned items only near base, no enemy within 20 | PvP: workers run to the enemy; host rule |
| Combat/UnarmedWorkers | workers' instinct no longer charges enemies | host rule |
| Army/KitReservation | no two units run to the same last kit at call to arms | AI fix |
| ResourceReservation, TaskReservation, JobBoard, TaskFlow, TaskPrediction, UnreachableTasks, LoadRelief, StockpileChoice, StockpilePickup | worker economy AI | big group, eco only |
| Multiplayer/HiddenDeliveries, AllyWorkerIcons | ally client: items heap on ground / exception breaks item net update | MP bug fixes, check on our 2v2 |
| Multiplayer/LobbyStart | lobby hangs at 0 % if someone un-readies during start | MP bug fix |
| Multiplayer/SavedTeams | allies become enemies in a loaded MP save | MP bug fix |
| Multiplayer/DesyncProtection | world vote + correction | big, own msgs; compare with our resync |
| Core/CameraMouseGuard, FocusInputGuard | camera drifts at start; alt+tab opens build menu | local, cheap |
| Saving/FastSave | sliced save, no freeze | perf |
| DragRelease, CraftFinishFix, DoorRepair, WorkSpotReserve, GlobalBuildJob | small bug fixes | cheap |
Not for us: SteamRelay (Unity 6 port), CsMenus, editions, content features.
