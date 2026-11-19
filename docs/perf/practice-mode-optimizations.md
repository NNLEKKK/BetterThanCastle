# Fast, freeze-free practice modes (learned on the aim trainer, 2026-11-19)

Reusable recipe for any offline mini mode (aim trainer, duel, future drills, a separate game built on Castle Story).
User verdict after all steps: "ни одного фриза во время игры".

## Load time (15.8 s world load -> 3.7 s, floor ready 4 s -> 1 s)
- Own empty map: generated at runtime into `Info/UGC/Maps/<Name>` (Features/Practice/BtcAimMap.cs). Copy the stock Void heightmap/thumbnail;
  stock Void itself is unusable (playable context 0 = no asset query finds it; sandbox errors "No spawnpoints in map").
  Needed files: meta.json (PLAYABLECONTEXT 1, SPAWNCOUNT 1), gameobjects.json with ONE `Balises.WorldEditorMarker` + `LuaMarker`
  group "Player Starting Point" (markers live in gameobjects.json, NOT LuaMarkersCount.json), trees.json `[]`, version stamp file.
  After writing: `AssetDispensary.maps.RefreshCache()`; then sandbox finds it by codename.
- Skip the game HUD: Prefix on `LuaMenuComponent.Load` returning `__result = true` for `LUI/Menus/GameMenu.lua` (the only in-game Lua menu:
  taskbar, task panels, mini map). Biggest single win (~12 s of load, ~1.1 GB less memory, fps 85 -> 150). Our own clicks/HUD do not need it.
- Never wait for "calm fps" after a terrain edit with a long timeout: the old 60 s limit was always hit because other things kept fps low.
  0.5 s of calm frames, max 3 s.
- Build terrain ONCE at the largest size any setting needs; settings changes only respawn units (no voxel work, no settle wait).
- 1-voxel slab: `SetColumnTopVoxel(t)` + `SetColumnBottom(t)`, check `GetColumnData` after apply; if bottom >= top use `t-1`
  (here the result was b=234, t=235). Place it over the void (search camera `BoundsLimit` for an empty square + 40 border).
  `Camera.farClipPlane` = depth of the farthest floor corner + 4 hides everything under it at any camera distance.

## Unwanted units / churn (the "10 bricktrons respawning" freezes)
- Sandbox start workers: wrap Lua `SetAsHomeCrystal` (same hook point as BtcHandicap) and set `sv_Settings.startingWorkersCount = 0`
  in the `Asset_Gamemode.Disk_LoadGamemode` postfix. Wrapping `gamemode.OnNewLevelLoaded` did NOT work (not a global there).
- `FireflyNest.ProcessHomeSpawns` prefix false while the mode runs.
- Mod features that spawn units (phantoms!) must be gated off in practice modes. Never "fix" strays by despawning them in a loop:
  despawn -> firefly respawn -> despawn churn is what froze the game. Log stray names once to find the source.
- Fog of war off (`LocalFactionVision.ToggleFogSystem(false)`) or enemy targets are invisible without own units.

## GC (Unity 5.6 Mono / Boehm: it cannot be switched off; a full collect here ~290 ms)
- Measure first: profile only the round (DEV `waitaim` + `prof reset` + `dump`), not the load. Round alloc was ~3.6 MB/s, 0 GC in 13 s.
- Collect yourself where nobody plays: on the result screen 0.25 s after it is drawn (if heap grew > 16 MB), plus a safety check in the
  countdown (only if growth >= max(48 MB, measured rate * (round + 10 s))). Measure the per-round allocation rate and adapt; works on any PC.
- With a fresh collect before each round there is enough free heap that Boehm never collects inside a 15-60 s round.

## Per-frame spikes that are not GC
- `ResourcePackManager.Update` runs ~30 match systems every frame (crystal scan 34 ms every few s, bomb map, predictions, stat units,
  path units, terraform, match stats, timer). Gate them with `!BtcAim.Active` (keep menu, hotkeys, HP rings, visuals). p99 39 -> 8 ms.
- Replay recording off in the mode (BtcReplay recording condition).
- Lab artefact: `BtcDevProf.SlowTick` (FindObjectsOfType x5 every 5 s) makes ~107 ms frames in profiled lab runs only; ignore it.
- Vanilla HP ring "linger": on heal the ring is dropped from the dispenser at once but released 1 s later; a re-wounded unit shows a stale
  ring. Prefix `SelectionCircleDispenser.Remove(GameObject, float)` delay = 0 while the mode runs.

## Lab hygiene
- DEV commands: `aim`, `waitaim`, `aimauto <ms>` (auto clicker through the same ray), `aimagain`; scenarios aim-load, aim-gc.
- Lab runs must never write player data (history/PBs): skip Record for DEV auto and for 0-click runs. (They once did: 8492 fake record.)
- Check `game_status` before every scenario; restore any cfg values changed for a test.
