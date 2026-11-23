# Castle Story Plus 0.9.2 performance audit (2026-11-21)

Source: `CastleStoryPlus-0.9.2/` (repo root, untracked; GPLv3, Tricky12321). Already ported earlier: everything under
`CastleStoryPlus/Memory/` (Features/BtcMemFix.cs, cfg Performance/MemoryFixes, decisions 2026-11-21).

## Taken now (cfg Performance/FrameSavers, default ON, Prepare-gated, DEV env BTC_FRAMESAVERS=0/1 for A/B)
Pure caches: same values, same signals, local only, no wire and no simulation change.
| CSP file | what | file in BTC |
|---|---|---|
| Performance/BehindCamera | camera pos/forward read once per frame for ~330 detectors | Patches/FrameSaveUiPatches.cs |
| Performance/OverheadDistance | camera distance limit once per frame | " |
| Performance/OverheadTestComponents | dev test Updates switch themselves off | " |
| Performance/ItemCountsUpdate | 300 no-op Updates off, counter reset lazily | " |
| Performance/ScreenPlanes | frustum planes once per frame and camera | " |
| Performance/SoundObjectPositions | AkGameObj reads transform once, still objects every 4th frame | " |
| Performance/InputActionIds | Rewired action name -> id once (~2% main thread) | " |
| Performance/PickingRaycasts | pickers share identical raycasts in one pass | " |
| Performance/NatureLodRest | plant LOD pass rests while the camera stands | " |
| Performance/MenuFrameCap | menus/loading capped (were thousands of fps) | " |
| Performance/FrameSavers (UI part) | hidden overhead canvas off, sort order only on change, shelf display on count change | " |
| Performance/AnimatorTrackerCache | animator layer state read once per update (~30k engine calls/frame at 100 units) | Patches/FrameSaveAiPatches.cs |
| Performance/CarriedTypeCache, BeaconProjectCache, WorkerTickSavers, LaborTickSavers, WorkerAI/StorageCheckCache | AI caches with identical answers | " |
| Performance/LazySignalLists | ~700k empty StructLinkedList lists share one (1/9 of managed objects, GC walk) | " |
| Core/TerrainLodFix | LOD section drawn only when its data finished (bug fix) | " |
(Exact list of what the agents really ported: see the patch file headers; skipped items are listed there.)

## Not taken (change AI timing / decisions: host simulation; only with BetterConnection ON and after measurement)
AiTickCap (AI ticks per frame capped at low fps), WorkSearchBudget (4 idle searches per tick), IdleSearchBackoff,
TaskSearchBudget, LitterRefreshRate, KnowledgeRegionsSpread, TunnelGoalRefresh, VisionRate (60 Hz cap), VisionScan
(same results, 480 lines, big: candidate for a later port), LocomotionSavers / IdleUnitMotion (conflict with
PhantomPatches ApplyAnimatorMotion), Pathfinding/SearchFrameBudget, HeuristicPatch (changes routes).
Visual changes (would need a Visuals option): Rendering/RenderTuning (shadow distance 400 -> 150, 2 cascades,
off-screen shadow casters), Performance/WardEffects (ward lights/aura LOD).
Loading: Loading/LuaCompileCache, LuaChunkReuse, LuaMenuBatchPatch (overlap BtcLoadBoost; less useful with the C# GameMenu).
Unity 6 port only: PlayerJobs, VisionJobs, VisionCollisions, ProgressionOnWorkers.

## Profiler
Dev/BtcDevAlloc.cs = Windows x64 port of CSP Bootstrap/AllocProfiler (Mono profiler allocation callback, native gate in
VirtualAlloc'd code, profile_allocs flag found by cross-reference). `scripts/scenario.ps1 -Name X -Alloc` -> BTClogs/alloc.txt
(types KB/s, sampled main-thread stacks). Combine with prof.txt (method timing, docs/perf/targets).

## Loading (2026-11-21, 0c53385)
Taken (Performance/FastLoad): per-frame load waits (we polled 0.1 s), LoadingStepsCheckpoint progress in one leap. Own: terrain quick draw batched (100 ms/frame), JSON warm-up (BtcJsonWarm), curtain x2.5, one post-load GC. Skipped: TerrainChunkSync + HeavyWorkerWake (InitFullFromLayers_Loop not called at load in this game version), background LoadGameObjects/LoadNature parse (small here), Lua compile cache / chunk reuse (compile ~80 ms per load with the C# menu), BlockSpawn 1500 (fresh maps have no placed blocks; saves only, maybe later).
