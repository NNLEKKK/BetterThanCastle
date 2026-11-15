# Terraforming task (design, 2026-11-12; NOT started in code)

## User requirements (2026-11-12, chat)
- New task type "Terraforming" in the vanilla task bar / task list like Tunnel, Quarry, Flatten, Build: own priority,
  delete, crew etc. Lua only shows the task entry; the editing UI is a custom C# IMGUI panel (nice, intuitive, vanilla-like).
- Binds for the task (create, tools) like the other tasks.
- Features:
  1. Tunnel/dig of any shape: width 1 or 2 (or any), height fully configurable by hand.
  2. Fill ("nasypka", like Flatten): fills earth (vanilla Flatten fill = 10 stone per full block; a partial block (fill on
     crust) is cheaper - CHECK the vanilla cost and make partial fills behave correctly). Single blocks and any custom
     shape. Also placeable from the Buildings (blueprint) menu. Fill order: from the bottom up.
  3. Tunnel ladder anywhere, and quarry ladder anywhere (both, they differ: tunnel ladder can go INTO entities, crystals,
     structures - do not fix; quarry ladder replaces a block). Rotatable. Also placeable from the Buildings menu.
  - Anti-abuse = vanilla rules: tunnel ladder needs 2 supports (bottom main + side extra; crust counts as side support,
    like full blocks; without side support it cannot be placed). Just repeat the vanilla logic of those tasks.
  - Resource harvest toggles like the other tasks (Stone, Brimstone, Iron; NO blue crystals, they do not exist).
  - Mode switch: when digging a cube staircase, place quarry ladders where needed for fast climbing instead of a stair of full cubes.
- Brushes: block + drag (line/path), box with height, eraser + Ctrl+Z.
- Multiplayer: only with BetterConnection ON (BC OFF = no tab, vanilla wire).

## Vanilla findings (explorer reports 2026-11-12)
- Project base ref/vanilla/Brix/Game/AI/Project.cs:36 (priority :611-655, :1000-1036, RequestDelete :371, Lua API :1098-1200).
  ProjectPriority.WeightsFor (ProjectPriority.cs:16) switches on type: a new type needs a case.
- Spawn/modify: UNetGroupe.cs CmdSpawnMineProject :52, CmdSpawnLandscapeProject :64, CmdSetMine :138, CmdSetLandscape :157,
  CmdDelete :169, Raise/LowerPriority :206/:217. Tunnel: UNetProjectCmd CmdInstantiateTunnelUnit :540, CmdRemoveTunnelUnit :566.
  Toggles: CmdTunnelToggleResource :637, CmdQuarryToggleResource :620 (GroundResource Stone 1 Brimstone 2 Iron 4 All 7).
- Tunnel: units 2x3x2 on even grid (TunnelUnit.GetVolume :32, TunnelVolume.PositionKey :103). Dig goals
  TunnelProject.VoxelNeedsADigGoal :185; ladders StairsDirection :147, VoxelNeedsAStairGoal :173, StairGoalSetup :205.
- Quarry: QuarryGoalProvider2.VoxelNeedsGoal :149; ladders StairsComputer (ComputeStairs :105, StairIsValid :200, crust = empty
  in ComputeCornerY :154), StairOrientation.All (8 pairs).
- Ladder blocks: Blocks.Mine_Staircase / Mine_Staircase_Block, blueprints Blueprints.Mine_Stairs / Mine_Stairs_Block;
  BuildStairsGoal.SpawnStairs :71 (forward==up -> _Block), IsValid :102 (voxel+up empty), RemoveCroute :92.
  Validators MineStairBlockValidator.cs:32, DefaultBPValidator :15 (mainSupportReq not traced yet).
- Flatten: LandscapeGoalProvider.cs:117-143, bottom-most fill voxel first, RawResources.RawStone; LandscapeGoal fill cost NOT traced.
- Harvest check: DigGoal.MustOverkill :237 (TypesToHarvest).
- Lua: task bar LUI/GS_Taskbar.lua, list LUI/Menus/Game/Menu_TaskList.lua (hooks 1106-1170), creation GameMenu.lua:1926-1987 ->
  UIGameObserver.cs:1082-1125 -> Instructions "Tool_*" (Instructions.cs:125) -> InputModeController.PlaceBeacon :215.
  Binds userconfigs.lua:69-160, GameMenu.lua:40-300. Task UI is hard-coded per type; no generic task API.

## Plan (proposed, stages; each ends with smoke + hidden-game lab)
1. Core: Terraform project (host-authoritative, reuse a vanilla Project subclass where possible), voxel set dig of any shape,
   harvest toggles, task bar entry + priority + delete, save/load, BC gate. Minimal C# panel with block/line/box/eraser.
2. Fill: bottom-first fill goals reusing LandscapeGoal (exact vanilla cost incl. crust), blueprint in Buildings menu.
3. Ladders: tunnel + quarry ladders with vanilla support rules, rotation, Buildings menu entries; auto quarry-ladder mode.
4. UI polish, binds, persistence (BtcMenuState), undo, RU/EN, changelog, test plan.

## State (2026-11-12, stage 1 DONE)
- Code: Features/Terraform/BtcTerraform.cs (model, net 112/113, host spawn, save), BtcTerraformTool.cs (Ctrl+G tool), Patches/TerraformPatches.cs, Dev/BtcDevTerraform.cs (tfspawn/tfhire/tfdump), scenarios terraform-lab (harvest on) and terraform-lab0 (off).
- Lab results: pit 18/18 dug in 30 s, crust + first fill layer done; harvest off digs and destroys resources (no stone left to fill, expected).
- UNTESTED: the tool UI and drawing by a human, MP client creating a task, save/load restore, edit an existing task.
- NEXT: ladders (tunnel + quarry, rotation, vanilla supports), fill/ladders in the Buildings menu, auto quarry-ladder mode,
  Lua task bar entry + own name/icon, open our panel when the task is selected (edit), favourites/All page not needed (no tab).

## State (2026-11-12, stage 3 ladders DONE in code, lab OK)
- Ops 'T' tunnel ladder, 'Q' quarry ladder, 'B' quarry landing: one cell A, B = forward (low side, vanilla BuildStairsGoal; you climb towards -F).
  Shape.Ladders; Q/B remove the cell from dig/fill, T keeps dig (dug first, ladder after); D removes Q/B, F/E remove any.
- Goals: VoxelNeedsGoal spawns Test.BuildStairsGoal when BtcTerraform.LadderReady (vanilla rules: tunnel = empty cell, full block under,
  non-empty wall at cell-F (crust counts), empty above; quarry = terrain block, not building/invincible/nature, support under, empty above).
  The cell under a ladder and stair cells are never dug. CreateGoal prefix spawns it like QuarryGoalProvider2.SpawnStairGoal.
- Vanilla LandscapeProject.HasDigGoals/HasFillGoals and ReevaluateAllThresholds assume every goal is a LandscapeGoal: prefixes for shaped tasks
  (TerraformGoals.Has via GetAll). HasWorkFor = true when a ladder goal exists.
- Tool: mode Ladder (4th), kind Tunnel/Quarry/Landing, R / Shift+R rotate, tunnel ladder on a wall face auto-faces away from the wall;
  green/red preview (BtcTerraform.LadderPlaceable against the world as it will be after the shape).
- Lab terraform-ladder (tfladder [harvest]): harvest 0 -> pit dug, all 3 ladders built (tunnel fwd +x, quarry, landing). Harvest 7 at one map
  spot: workers joined and left (vanilla LandscapeProject.VerifyWorkFor skips the storage check only when the task has BOTH dig and
  fill goals; a dig-only task needs a stockpile, none in a fresh game). Same as vanilla Flatten. DEV trace Dev/BtcDevStairsTrace.cs.
- UNTESTED: drawing ladders by hand, ladders in MP (msg 113 carries them), save/load with ladders.
- NEXT: fill/ladders in the Buildings menu, auto quarry-ladder mode for cube staircases, Lua task bar entry + own name/icon, edit panel.

## State (2026-11-12, "No stockpiles" + menu-style panel)
- Shape.NoStore (bit 8 of the harvest field on the wire / sidecar; old readers mask it off): VerifyWorkFor prefix returns Node.Empty
  (vanilla skips the storage check only for dig+fill tasks), dig IsMatchFor without bag/storage checks, HasWorkFor without bag room,
  DigAction drops the whole bag (Recepteur.DropAll, next to the worker) when it cannot fit the voxel; fill picks the stones up.
- Lab terraform-nostore (tfladder 15 / 8): dig-only pit with everything kept and no stockpile: 26/27 dug in 90 s (was 0 without the
  toggle). Known: dropped piles can land on pending quarry ladder / landing cells and make them unreachable (seen once).
- Tool panel redrawn with the menu toolkit (BtcUi Section/Toggle/Stepper/Action, menu Bg/Accent/MenuFs, accent chips for modes).
  Setting persisted: terraform:nostore. UNTESTED visually.
- Auto quarry ladders ("Ladders on steps" toggle, terraform:autoladder, default off, applied on Create): BtcTerraform.AddStepLadders turns
  every 1-high step the shape carves (solid P, P+up dug, P+2up free, dug N beside it with a floor) into a 'Q' op climbing from N onto P.
  Lab terraform-steps (tfsteps): 4-step staircase width 2 -> 6 ladders, all dug and built in < 90 s.
- NEXT: fill/ladders in the Buildings menu, Lua task bar entry + own name/icon, edit an existing task (open the panel on select),
  drop piles away from pending ladder cells.
- Edit an existing task (2026-11-12): panel "Edit a task" -> click a cell of an own task (FindOwn: dig/fill/ladder cell, own faction)
  -> draft = its shape, "Save changes" -> msg 112 with text "E<netId>#shape" -> host Edit(): faction check, LandscapeData.Set(new AABB),
  Bind (re-walk; vanilla RemoveOutsideBounds releases goals of cells that left the shape), Broadcast msg 113. Lab terraform-edit
  (tfedit): fill erased + dig extended 18->26, old fill goals released, all dug in < 40 s.
- Not done: own name/icon in the task list (Lua Meta.Project names by TYPE, not instance: needs a Lua UI patch), Buildings-menu entries,
  drop piles away from pending ladder cells. Opening the panel by selecting the vanilla task is not hooked (use "Edit a task").

## State (2026-11-15, rework after the user's test; answers: auto-ladder replaced, Alt toggles depth drag, old 5 = defaults)
- Tool (BtcTerraformTool): Pick walks the ray through mark cells first (MarkRay, Amanatides-Woo; draft + pending cells of own tasks,
  up to 12 cells behind the terrain hit), so marks act like blocks. Crust: dig / quarry ladder / landing aim at the full block under it,
  tunnel ladder goes INTO the crust (LadderReady T accepts crust: BuildStairsGoal spawns over it), quarry/landing need Voxel.IsFull.
- Drag: plane of the clicked face (anchor coordinate along the normal kept), Alt at press = along the axis into (dig/erase) or out of
  (fill) the face (LineParam = closest point ray/line). Lines are 4-connected (no diagonal gaps). Ladder drag = LadderChain: Q/T step
  -F+up per ladder (a continuous ramp), landings a flat line; "Dig room for ladders" (terraform:autodig, default ON) adds D for the cell
  above (and a T chain cell itself when it is a full block). RMB / R rotate, Ctrl+LMB deletes a draft mark (or opens the own task with
  that cell erased), mode binds terraform_dig/fill/erase/ladder (unassigned), selecting an own task the vanilla way opens it for editing
  (poll of Project.selection, rising edge), panel lists own tasks (edit, vanilla CallCmdRaise/LowerPriority, CallCmdDelete with a 2nd
  click confirm). Panel clamps to the screen and scrolls when taller. Old "Ladders on steps" + AddStepLadders and "No stockpiles" removed
  (NoStore bit still decoded, unused).
- Resources: Harvest default 0 (key terraform:keep); every resource except brimstone/iron counts as stone, so all-off = ForceOverkill
  for every voxel: nothing drops. Host AddCrust: crust on top of a dug cell is dug too (bounds grow), crust under fill filled (as before).
- AI (TerraformPatches): PriorityFor = inventory bonus (stones carried -> fill, empty -> dig) minus distance*1024 (vanilla used height
  first: workers walked round a hill to start at its top). IsMatchFor dig: no storage check ever, bag room only for kept voxels.
  HasWorkFor: dig needs room for a whole kept voxel, so a full bag with nothing to fill = no work -> the worker leaves and stores by
  itself. VerifyWorkFor = Node.Empty for every terraform task.
- Labs: terraform-steps (tfsteps = pit + 3-ladder quarry chain: 2 placed, the 3rd on crust correctly refused, all built, task done
  in 33 s), terraform-lab (harvest 7: 18/18 dug < 30 s, fill from the bag; top fill layer unreachable = same as before).
- UNTESTED by a human: all tool input (drag planes, Alt depth, mark picking, Ctrl+click, RMB), the task list, auto-open on selection.
- Fix 2026-11-15 (user test): crust above a ladder blocked it (vanilla BuildStairsGoal.IsValid wants p+up empty although it removes
  the crust: IsValid postfix + LadderReady accept a crust above); fill on a crust targets the crust itself; a quarry staircase inside a
  hill sealed each room (p+up only touches the previous room through p+up+F, the cell over the previous ramp): the chain digs it too.
  Lab terraform-steps (pit 6 deep + 4-ladder chain into rock): all 42 cells + 4 ladders done, task finished in ~87 s.
- Crust is IGNORED unless marked by hand (user 2026-11-15): room digs (Rock) skip crust, host no longer digs crust over dug cells,
  a dig cell with a crust neighbour counts as open (OpenToDig), built ladder blocks are not "dig left". Lab terraform-ladder: Q and B
  in the full block under a crust built without digging the crust (BuildStairsGoal removes it), task done in ~40 s.
- Goal choice (user 2026-11-15: "they prefer the top blocks, detour or stand idle; freshly opened blocks are not dug at once"):
  vanilla Flatten uses GoodEnoughFromSample (best of the first few goals in list order) -> terraform tasks switch to CompareAll
  (host, Bind); vanilla BuildStairsGoal priority (1e7 - dist) is far below any dig -> TerraformPriority for both (empty bag:
  nearest dig or ladder; stones: fill, always lowest layer first); knowledge regions lag after a dig -> TerraformReach: a goal
  within arm's reach (horizontal <= 1.45, vertical <= 2.1) counts as reachable. Finished tasks whose crew left early are
  auto-deleted from BtcTerraform.Tick. Lab terraform-steps: 41 cells + 4 ladders 87 s -> 52 s.
- 2026-11-15 (late) two-phase labs (tfpit, then tftunnel N / tfclimb from the pit floor, no other way out):
  - VoxelGoalProvider re-walks only every AutoRefreshDelay = 15 s (no voxel-change hook): terraform sets 0.5 s. Tunnel 1x2x6: all
    12 cells in 33 s (was: one block, then idle).
  - Goal access (dumped): the 14 cells around a goal (6 faces + 8 edge diagonals, no corners); a worker on a ramp stands IN the
    ramp cell. So a worker reaches at most standing level + 1. A 1:1 quarry staircase from below: ramp i+1 is reachable from
    ramp i, but the block over ramp i+1 is 2 above ramp i. Fix: LadderReady / StairIsValid accept a block over the ramp that the
    task digs (ramp first, block after, from the ramp). Lab terraform-climb: ramps 1 and 2 built, then the block over ramp 2 stays
    unreachable (a worker does not enter a ramp with a solid block over it). OPEN.
  - 1-high tunnel (tftunnel 1): not a bug, bricktrons are 2 high (user).
  - FIX (a87f5f6): terraform dig cells spawn the vanilla Tunnel/Quarry DigGoal (Test.DigGoal) instead of a Flatten LandscapeGoal.
    Its access reaches 2 up (dumped offsets include dy -2), like the user said vanilla tunnels do. TerraformDigSetting
    (IDigGoalResourceSetting = harvest toggles, also marks the goal by its goal parent because DigGoals are pooled), patches
    DigGoal IsMatchFor (no storage), PriorityFor (reach + nearest), ApplyDamage (overkill everything not kept). Fill stays a
    LandscapeGoal, ladders BuildStairsGoal. Lab terraform-climb: 3 ramps + rooms from the pit floor done in ~11 s, task closed.
    UNTESTED after the switch: dig+fill with kept stone (terraform-lab), long tunnels; the user's game was running.
- c89758e (user list 2026-11-15): BtcTerraform.Kick re-walks the provider when a terraform DigGoal kills its voxel or a ladder is
  spawned (now + 0.25 s later); a full bag keeps digging (no bag check in DigGoal match / HasWorkFor; fill still wins by
  priority while stones are carried); Ctrl+G / mode binds = new task (OpenNew drops an edit), "Edit a task" button removed (edit =
  vanilla task selection); brush modes removed: one W x H block stretched straight (Straight: biggest axis) along the face or,
  with Alt, into it; ladder drag up only, Shift = down; hovering a mark puts the new mark next to its face (except Erase).
  terraform-lab after the switch: dig 18/18 < 30 s, fill from the bag, top fill layer (2 above ground) still unreachable
  (LandscapeGoal fill reach +1, as before). NOT DONE: explicit pick-up of loose stones (vanilla fill Find() may already take
  stones lying around: untested).
- e9ae3c1: Kick(s, pos) also walks the 5x5x5 cube around the dug voxel for the next 0.4 s and queues ADDS only (a removal raced the
  vanilla re-walk: a goal released twice broke the spawner, no new goals at all: lab stalled at 16/27); CreateGoal reuses a live
  goal on a double add. Loose stones within 12 of the worker (labor.Find result not inside a Recepteur) give fill the inventory
  bonus (the Flatten project has no litter pick-up; the user's mov.noAutoPick only filters Harvest/Repair/Cleanup providers).
  Fill cells are DigGoals with TerraformDigSetting.Fill (TerraformFill: pick up stones, AddTerrainResources Sand, remove stones,
  done = full) for the 2-up reach. Lab terraform-lab (fill column 3 high): dig 27/27 < 30 s, fill 3/4, top cell (+2 over the
  standing level) not filled but the workers were trapped in their pit: reach of the top cell UNVERIFIED.
- 0b304a6 (user: still runs off, no stone pick-up, block over a ramp in a hill refused, ladder down by mouse): a freshly spawned
  goal is unknown to Knowledge (KnowsAbout false until the region finder catches up), so CanReach said no: TerraformReach treats
  an unknown goal within 6 as reachable (arm's reach widened to 2.55 / 2.6); fill PrepareAction picks up the nearest stone on the
  ground within 12 (TrackingRegistry SmallResources + LitterGoalProvider.IsOnGround, like vanilla litter) before vanilla
  FindAndPickUp; OpenToDig counts a built ramp next to the cell (the room over a ramp inside rock has no air around it); ladder
  drag goes up or down the slope by mouse direction (Shift no longer needed). Labs: climb 3 ramps + rooms 5 s, tunnel 1x2x6 23 s
  (was 33 s).

## HAND-OFF (2026-11-12 end of session) - user is testing the Debug build in game
Deployed: Debug build (DEV) 2026-11-12 ~16:00, all commits up to fb3cf3e. User will report symptoms + time.
Open questions asked to the user (my defaults in brackets, all without Lua, ~15-20% of the limit):
1. Name "Terraforming" in the task list (vanilla names by type -> shows "Flatten"): A own HUD card listing terraform tasks with
   edit/priority/delete, B label over the task beacon in the world, C keep "Flatten", D Lua task menu patch (~25%). [A]
2. Fill/ladders from the Buildings menu: A separate binds that open the tool in a mode, B own button strip next to the open vanilla
   Buildings menu (C#), C inside the vanilla menu (Lua, expensive). [A]
3. Open the panel in edit mode when the player selects a terraform task's vanilla beacon (C# selection hook). [yes]
4. "No stockpiles" piles landing on pending ladder cells: A drop on the dug floor of the task, B destroy the excess when the bag is
   full, C carry straight to the nearest fill cell. [A]
5. Extra binds per mode (dig/fill/ladder) and rotation besides R. [yes]
Wait for the user's choice before implementing; then smoke + terraform-* labs + Release.
