# ARCHITECTURE (auto-loaded every session; HARD LIMIT 120 lines)
Keep terse, one line per fact. Detail goes to docs/*.md. Current state/next steps: docs/STATUS.md. Commands: docs/RUNBOOK.md.

## Overview
BTC = single-DLL BepInEx 5.4.23 (HarmonyX 2.9) port of a dnSpy-edited Castle Story mod. Unity 5.6.5, Mono, plugin net35. Legacy mod edited `Assembly-CSharp-firstpass` (Assembly-CSharp is an empty stub).
Legacy scale: 74 changed vanilla files (54 only add hook calls into Btc*/ResourcePackManager/CameraBookmarkHotkey) + 57 new files / 179 types. Much of the rest of the diff is decompiler noise or the FOREIGN MOD inside the current game DLL (see CLAUDE.md "dirty reference").
Game copy for builds: `game/csmod/` (gitignored). Debug build deploys to `game/csmod/BepInEx/plugins/BetterThanCastle/`.

## Source layout (src/BetterThanCastle/)
- `Plugin.cs` BtcPlugin: `BtcConfig.Init` -> DEV `BtcDevTools.Init` -> `BtcPatcher.Apply` (patches each [HarmonyPatch] class separately, logs `[Patch] OK|FAIL`, summary) -> DEV `BtcDevProbe.Init` -> `ResourcePackManager.Boot()` -> `[BTC] loaded`.
- `BtcConfig.cs` (Network: BetterConnection default ON, ShowPing, ShowPauseLabel, DiagLog), `BtcNetworkGate.cs` (runtime toggle), `BtcLog.cs` (tagged log; DEV agent.log), `BtcPatcher.cs`.
- `Patches/*Patches.cs`: Harmony patches, one file per vanilla type (133 patches; list docs/patches.md). `Shims/` replacements for members the legacy mod added to vanilla types (docs/stubs.md). `Util/BtcStr.cs`.
- Ported legacy classes (hand-owned now): `Net/` (BtcNetManager, BtcLobby, BtcKick, BtcNetId, BtcNetDiag, BtcGameplay, BtcWorld, BtcGc), `Replay/` (BtcReplay*, BtcSaveFormat, BtcLzma, BtcPng, BtcShareUI), `UI/` (BtcGameplayUI, BtcHotkeysUI, BtcOverlays, BtcPlate*, BtcAvatar, BtcNick, BtcLang, BtcDiscord), `Features/` (everything else, Input/ = CameraBookmarkHotkey, JobSelectHotkeys), `ResourcePacks/ResourcePackManager.C01..C16.cs` (partial MonoBehaviour, 30k lines; menu PgDown; user data under <game>/BetterThanCastle/).
- `Features/BtcShield.cs` (+ BtcShieldFx look/sound/HUD chip, `Patches/ShieldPatches.cs` ReceiveAttack/ApplyDamage/capture/arrow/catapult vetoes, `UI/Gameplay/BtcShieldPage.cs`): base shields, host-authoritative, rules group "shield", msg 109.
- `Features/Duel/` micro-duel vs bot (offline): BtcDuel (state, orders via Order.* directly, spawn), BtcDuelMatch (driver, rules, HUD, rematch), BtcDuelMap (100x20 plateau over sandbox map, zones = terrain types), BtcDuelBot (Chosen + tiers), BtcDuelLog (duel-logs/), BtcDuelConfig; `UI/BtcDuelUI` = RPM tab id 8; `Patches/DuelPatches.cs` (ApplyDamage events, AttackInfo.Strike cooldown guard, Labor.SendOrder log) all gated by `BtcDuel.Active`.
- Main menu (PgDown): shell `UI/Menu/BtcMenu.cs` (icon rail, header, `BtcMenuTab` registry) + `ResourcePacks/ResourcePackManager.Menu.cs`; migrated tabs = one `BtcMenuTab` class each (Replay: `Replay/BtcReplayTab.cs`, `BtcReplayLibrary.cs`, `BtcReplayPreview.cs`); details and migration steps: docs/menu.md. Replay loading screen: `Replay/BtcReplayLoadUI.cs`.
- `Dev/` (#if DEV only): BtcDevTools (BepInEx log mirror -> BTClogs), BtcDevProbe (hit counters via Finalizers, hits.txt, last-run.json, F9 screenshot). Release check: `verify-dist` rejects any `BtcDev` string.

## Where is what (never search twice)
| topic | location |
|---|---|
| one vanilla type API | `scripts/api.ps1 Type [member]` (index docs/gen/game-api-index.md; third-party/Ak* skipped) |
| legacy hooks of a vanilla file | `scripts/hook-hunks.ps1 -File <path>`; raw diffs ref/diff/<Path.With.Dots>.cs.diff; summary docs/gen/diff-summary.md |
| members the legacy mod added to vanilla types | docs/gen/changed-members.md (heuristic, noisy) |
| mod class -> vanilla types it uses | docs/gen/mod-deps.md |
| ResourcePackManager map (nested types, 16 chunks, long methods) | docs/gen/huge/ResourcePackManager.md |
| Lua index (legacy did NOT change Lua) | docs/gen/lua-index.md |
| features and status | docs/feature-map.md; patches: docs/patches.md |
| transport/lobby | `Net/BtcNetManager.cs` + `Patches/UNetManagerPatches.cs`, `LobbyPlayerPatches.cs`, `SimpleHostScriptPatches.cs` |
| replay recording | 101 Prefixes in `Patches/UNet{Project,Contextual,Groupe,Catapult,Blueprint,Recepteur}Cmd*Patches.cs` (`UNetCmdReplay`: skips command body when `BtcReplay.Active && Intercept`) |
| map preview / symmetry UI | pure IMGUI in RPM window: OnGUI(C01) -> C02 "Gameplay Settings" -> `BtcGameplayUI.Draw` tab Map -> `BtcMapScale.Draw`, `BtcSymmetry.Draw`; needs `BtcGameplay.CanEdit` (host + lobby + BC ON) |
| loading time-slicing | `Patches/GameLoaderPatches.cs` (+ TestPathfinding, PhysicsLoop, LuaMenuComponent): `BtcLoadBoost` |
| nick limit / rename chat | `LobbyPlayerPatches.cs`, `UNetManagerPatches.cs` (LocalPlayerName setter): 999 chars when BC ON, vanilla 30 when OFF |
| Boot hook | plugin Awake calls `ResourcePackManager.Boot()` (idempotent); `AkInitializer.Awake` Prefix also calls it |

## Hook map (vanilla -> plugin): see docs/patches.md (generated). Families
Input/camera (CameraScript, PickerOrderGiver, UIGameObserver Start/Lua results), AI/pathing (Locomotion4, Navigation*, DestinationResolver/RepathSearch, Labor, Task, Builder, Profession), gameplay/balance (Profession dmg/hp/cooldown, FireflyNest/Brewery, GeneralRules, Corpse, Projectile*, MeleeAttack, ExplosionGameplay, Build goals, Faction vision, Wards, DeepLayer/Asset_Gamemode), items (Tool, Garnotte, DamagedTree, BlocLibre -> `Volume.Initialize` Postfix installs ItemPass), loading (GameLoader, Architecte, GameParam), net (above), Corruptron (Minitron.CanMakeWay Postfix filtered by type).

## Networking notes (BetterConnection OFF must equal vanilla on the wire)
- Mod message ids (UNIQUE, grep before adding): 64 heartbeat, 65/66 spawn pick, 67 editor spawn, 68/69 replay chunk/ack, 70/71 ping, 72-74 voluntary pause, 75-77 gameplay, 78-83 spectate/hello/rel, 84/85 recipes (BtcRecipes), 87/88 handicap, 89 symmetry, 91/92 avatar, 93 plate up (+BtcPlate down?), 95/96 plate img, 97 map scale, 98/99 clean filter, 100 POV camera/cursor (BtcPovRec), 101 match stats, 102/103 rack filter, 104/105 timer, 106 focus mark, 107 craft queue tail, 108 version check (BtcNetCheck), 109 base shields state (BtcShield), 110 session end (host leaves), 111 rejoin name (BCv2, Net/BtcNetLink.cs) (see lessons: 2026-11-05 id collisions). Free: 112+. Intercepts client msg 33 (disconnect).
- OFF: no mod handlers registered, no mod sends, vanilla bytes. ON (BCv2, Net/BtcNetLink.cs): DisconnectTimeout 1h, MaxBufferedPackets 511, heartbeat 64 with payload; ONLY the host decides a link pause (msg 73 carries it), clients reconnect in place (offline scene blocked, no StopClient), host keeps the dropped User (no base OnServerDisconnect). Animator sync removed (2026-10-29): UNetAnimator/AnimatorParameter are pure vanilla.
- NOT symmetric: BC is a local setting; host ON + client OFF/vanilla breaks spawn data (open-questions #11). SimpleHostScript.Run is a copied-body Prefix (vanilla is closures).
- net: file (not wire): BtcSaveFormat, BtcReplay*, BtcPng, BtcLzma.

## Gotchas
- net35: no ConditionalWeakTable/Tuple/dynamic. HarmonyX here lacks `CodeInstruction.LoadLocal/LocalIndex`.
- Mono inlines tiny methods (getters): a Postfix may never fire; patch the caller or construction site.
- Static initializers (CookBook) may run before patches apply: such patches have no effect.
- dnSpy noise to ignore in ref/mod-old: `bool flag` chains, `result` temps, `!= 0UL` vs `> 0UL`, `fmgcache`; Token comments are stripped by gen-diff.
- Tagged parallel builds need csproj `Compile Remove="obj\**;bin\**"` (present).
- Details of everything above that failed once: docs/lessons.md.
