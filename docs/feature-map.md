# Feature map (compact; status: BUILT = compiles + loads + patch applied, TESTED = user verified in game). Per-hunk bootstrap map: docs/archive/feature-map-bootstrap.md
| area | classes (src/BetterThanCastle) | vanilla hooks (Patches/) | net | status |
|---|---|---|---|---|
| Mod menu (PgDown), resource packs, hats, HP rings, perf, combat keys | ResourcePacks/RPM C01-C16, BtcPaths | AkInitializer/Boot | no | TESTED (menu+checkboxes) |
| Hotkeys, camera bookmarks, job select | BtcHotkeys(+UI), CameraBookmarkHotkey, JobSelectHotkeys | CameraScript, UIGameObserver, PickerOrderGiver | no | BUILT |
| QoL (run orders, repath, ally pass, waypoints) | BtcQol, BtcWaypoints, BtcLineMove, BtcPov | Locomotion4, NavigationNodes, DestinationResolver/RepathSearch, Labor, Task, Builder, PathUnroll | no | BUILT |
| Gameplay/balance/handicap | BtcGameplay(+UI), BtcHandicap, BtcBalanceExtra, BtcFactionCap/Fix, BtcBuildAI, BtcRecipes | Profession, FireflyNest, GeneralRules, Corpse, Projectile*, MeleeAttack, Explosion, BuildGoal*, Faction vision, WardAttack, Navigation/Knowledge* | host data ? | BUILT |
| World: map scale, symmetry, preview | BtcMapScale, BtcSymmetry, BtcWorld | GameLoader, DeepLayer, GameParam | host->clients | BUILT (UI visibility untested) |
| Loading boost | BtcLoadBoost | GameLoader, TestPathfinding, PhysicsLoop, LuaMenu*, BuilderUtility | no | BUILT |
| Items physics | BtcSimplePhys | Tool, Garnotte, DamagedTree, BlocLibre->Volume | no | BUILT |
| Net core (BetterConnection) | Net/BtcNetManager, BtcBc, BtcNetId, BtcNetDiag, BtcLobby, BtcKick | UNetManager, LobbyPlayer, SimpleHostScript, Asset_Gamemode, GameParam | yes | BUILT (untested MP) |
| Nick/Plate/Avatar/Lang/Discord | BtcNick, BtcPlate(+Img), BtcAvatar, BtcLang, BtcDiscord | NetworkPlayersMenu, LobbyPlayer, UIGameObserver | yes (nick/avatar msgs) | BUILT |
| Spectate | BtcSpectate | UNetManager, GameParam, Architecte | yes | BUILT (untested) |
| Animator sync | (removed) | - | - | REMOVED 2026-10-29 (vanilla animator) |
| Replay / saves / share | BtcReplay(+Anchor/Boost/Fix/Share/UI), BtcSaveFormat, BtcLzma, BtcPng, BtcTerrainUndo, BtcBlockMarks, BtcGc | 101 Prefixes UNet*Cmd, SimpleHostScript, GameLoader | file | BUILT (replay start failed once) |
| Corruptron breaks walls | - | Minitron.CanMakeWay Postfix | no | BUILT |
