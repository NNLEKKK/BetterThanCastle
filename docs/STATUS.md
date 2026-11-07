# STATUS (read first; keep <=60 lines; update at the end of every session)
Updated: 2026-11-07 (v0.3.0 released: replay instant seek).

## Done (code committed; user confirmed in game: hotkeys, input lock, menu look, thumbnail scale, handicap dmg/hp/income/penalty, symmetry crystal)
- Port complete (125 patches after Anim Sync removal), net core, replay, ResourcePackManager menu, perf fixes (see git log).
- Menu: UI/BtcUiStyle.cs shared styles, wrapped tabs (LayoutTabs C02), Debug tab removed (dump buttons at bottom of Packs tab), tab ids 0 Packs 1 Overlays 2 BC 3 Hotkeys 4 Gameplay 5 Share 6 Profile 7 Replay, input lock via uGUI EventSystem when InputModeController null, selected Profile sub-tab green (onNormal styles).
- Hotkeys: Shoot at (terrain layer first, ignores triggers), Break object (no units), Dig voxel (blocked only if a building is on the cursor ray before the voxel: BuildingOnRay).
- Symmetry: markers within 4 voxels of mirror image collapse to one (Kept/Near tolerance); Alt preview + hint; terrain offset: user says the map itself was crooked, adjust axis shift manually.
- Rules warning: BtcGameplay.IsRule shared predicate; Players/gcol/recipes warn; non-blocking banner (yellow, red EditorWarn); logs moved to cfg Logging; Movement subtab single.
- Handicap: income/dmg/HP/death-penalty(flat energy) work; start workers via Lua SetAsHomeCrystal wrap (Patches/AssetGamemodeStartWorkersPatches.cs, conquest/sandbox only, BC ON, can only LOWER vanilla); cap hook exists but user reports no effect: check `[Handicap] cap` log line (missing = For(nest.faction) null).
- Session 2 (BC ON only, NOT tested in MP yet): editor panel for everyone (gen.editor, Net/BtcEditorShare.cs msg 67, Patches/LuaFactoryPatches.cs), spawn selection (Features/BtcSpawnPick.cs, UI/BtcSpawnPickUI.cs, msgs 65/66), auto replay to clients (Replay/BtcReplayAuto.cs msgs 68/69, cfg Replay/AutoShare), Animation Sync removed (msg 90 unused), extended colours/teams in vanilla lobby menu (Patches/LobbyTeamInfosPatches.cs; custom picker removed), call-to-arms fallback (Patches/CallToArmsPatches.cs, logs `[CallToArms]` line), rainbow-safe reconnect identity (BtcSpectate K()).

## GitHub + releases (LIVE since 2026-11-03)
- Public repo https://github.com/NNLEKKK/BetterThanCastle (gh account NNLEKKK, renamed from lokillFamalli 2026-11-03). Local branch `public` = snapshot chain (scripts/public-snapshot.ps1: HEAD minus CLAUDE.md, .claude, .mcp.json, docs/gen, docs/proposed-claude-config; noreply author) pushed as `main`; refactor/main history is NEVER pushed; tags point at public commits.
- Release: `scripts/release.ps1 -Version x.y.z -Notes ... [-Publish]` (bumps Plugin.cs + csproj, Release + verify-dist, scripts/build-setup.ps1 -> dist-setup/BetterThanCastle-Setup.exe = BepInEx 5.4.23.5 zip (sha pinned) + DLL, commit, snapshot, tag). User allowed (2026-11-03): git push origin public:main, git push origin v*, gh release create (run commands exactly, no pipes). Before snapshot: fetch origin main; if the user committed on GitHub, bring the files into refactor/main and base `public` on origin/main. Needs a clean tree (stash tests/scenarios/duel-par*.txt).
- Released: v0.2.0, v0.2.1 (updater broken by the account rename), v0.2.2 (updater accepts any owner of BetterThanCastle). 0.2.0/0.2.1 users must install 0.2.2 once by hand. Installer GUI + Steam detection still untested on the real game.
- Git identity in local repo config; never put the email into tracked files.

## Agent test automation (DEV only, see docs/perf/MASTER.md, docs/RUNBOOK.md)
- Hidden game via scripts/run-game.ps1 / scenario.ps1 (`-Visible`). `scripts/scenario.ps1 -Name X [-Clients 1] [-Bc 0]`; results BTClogs/prof-*.txt, engine.txt, agent.log. Scenarios: replay-stress, match-basic, match-stress, match-conquest (original only), engine-ab, engine-spike, engine-cull, mp-basic. smoke.ps1 skips if the game is running.

## Micro duel vs bot (2026-10-29, first playable, DEV+Release build OK, user has NOT played it yet)
- Menu tab Duel, arena 100x20 (safe 15/neutral 20/mid 30/neutral 20/safe 15), rules (safe loss, kill&escape with trade reset, space control 5 s), rematch without reload, skip toggle (Strike cooldown guard), infinite heal, difficulty tiers (Easy naive .. Chosen full), match log in <game>/BetterThanCastle/duel-logs, DEV scenarios duel-basic/skiplab/selfplay/kinds.
- Chosen v2 (2026-10-29, after the user's first games): threat map (knows whom every enemy attacks), wounded/focused units are dragged behind the friendly line (bait, chasers get punished), body block (4 slots, live swap of wounded blockers), chase leash, safe-edge guard (v1 lost a match by chasing into the human safe zone), position prediction. v2 beat v1 4/4 (8v8), mirror v2-v2 2/2. Rematch now removes bot units too; arena size x3 option (in-place rebuild); Wwise muted in hidden DEV runs (SoundEngine.mute). Dodging a strike by stepping away does NOT work (lab duel-dodge). Chosen v1 beat the naive bot 3/3 in selfplay (5v5). NOT yet: body-block, double-focus tuning, chasing a fleeing enemy, barrel-worker tactics (barrel is only picked up), non-halberdier tactics. Assumption to confirm: units standing in their own safe zone may attack out (bot avoids using it).

## Duel bot research (2026-10-30, see docs/duel-bot-research.md)
- Production bot = frozen v2 base + increment 2048 (engaged units stay on their target). Redesigned v3 (BtcDuelBotX, archived) LOST to v2 (3-7 .. 0-10); all bit-by-bit increments on v2 are within noise except 2048 (26/40). Tools: scripts/duel-par.ps1 (4 parallel hidden games, feature masks, human-like opponent tiers), duel-analyze.sh, duel-tally.sh. NOT reached: v3 10-0 over v2, v4. Spawn height fixed (units fell into landing stun), bots idle 1.2 s after spawn.

- 2026-11-04: replay auto marks (BtcAutoMarks), camera+cursor recording (BtcPovRec, msg 93). User tests: lane, Alt preview, minimap pulse, MP camera/cursor of clients.

- 2026-11-04: auto gamma (RPM Overlays/Other: checkbox + Master gamma, ResourcePackManager.Gamma.cs, options.txt AutoGamma/MasterGamma) and mov.noAutoPick (Gameplay/Movement, default ON, Patches/CleanUpGoalProviderPatches.cs: harvest/repair providers keep no goals; BC ON only). UNTESTED in game: user checks night brightness, workers skipping loose items.

- 2026-11-05 replay UI: layout editor (Replay/BtcReplayLayout.cs, F7: scale/move/hide modules, templates), 3D view cones (BtcPovCone.cs), client POV recording FIXED (msg 93 collided with BtcPlate; ids 100-105 now, see ARCHITECTURE), marks H (home crystal lost = flag) / N (crystal neutralised), death mark dedupe, POV list from tracks. Test plan docs/test-plans.md 'Replay UI'.

## NEXT
00. RELEASED v0.3.0 (2026-11-07, public abca5d8): replay v4 instant seek merged into refactor/main (keyframes: terrain layers, crystals, trees, dropped items, containers, orphan sweep + re-listing, host freeze skip). User confirmed seek OK on new replays and Komorebi spam. Memory project-replay-v4 has the details and open leads.
0. v0.2.3 released (2026-11-04): jump-off line (tag JumpLine), chop 1x1 (confirmed), rack kit filter (tag RackFilter/Livre; untested: Alt icon, save/load, MP host apply, hover pie), window mode + skip intro. User tests these.
1. User: test MP (2 players, BC ON): lobby colours 5-8 + teams E-H, call-to-arms on custom colours (send `[CallToArms]` log line), editor panel for client, spawn selection, replay auto-delivery; docs/test-plans.md sections F, G.
2. Handicap cap: send `[Handicap] cap`/`start workers` log lines if still wrong. Opt: menu checkbox for Replay/AutoShare (cfg only now). Profile checkbox shape confirmed square? (unchecked).
3. Releases: test updater with 0.2.1; installer GUI test on the real game. Release build + verify-dist before every release.
4. Perf backlog: MP hard pauses on level load; match load time; replay x4 cost; new Performance tab.
5. Unexplained: replay errors (NRE, Unknown message ID 56); BtcNetManager pendingReconnectUsers is FIFO without name check; open-questions #1/#2.

## Duel bot - session wrap-up (2026-10-30)
Session 4 (2026-10-30 evening):
- Production = 5b86a49 bot: anti-sweep, focus 1 / split 2. Testers rated it the strongest.
- Elo (rough): bot with skips ~2500, no skips ~1700. User ~2100, Kirusha ~2000, Monotony ~1850.
- Tactics layer (bit 536870912, BtcDuelBotTactics.cs + BtcDuelPath.cs) is OFF after a bad user test (7dbb0ae). Re-enable only rule by rule. Details are in the project-duel-mode memory.
- Other testers' logs: game/csmod/BetterThanCastle/duel-logs-kirusha/ and duel-logs_monotony.zip.
- Also this session: dig voxel shoots through loose items (335f54b), and the duelfeat int-parse and duel-par $Matches fixes.

## BreaktronZero = B0 (formerly LuminaZero; RL agent, lab/; docs/LAB.md, decisions 2026-11-01, sim gaps docs/sim/measured.md "Order streams")
- Self-play AlphaStar league (main + main exploiter), bots eval-only; GPU path (`--gpu 1`) exists but OpenCL is broken until a driver reset/reboot, training runs on CPU.
- Sim/game gap fixed (e4db902): orders to a running unit take effect after 0.35 s, newest replaces pending. Old policies spammed orders and froze in the real game (1/16 vs Chosen). Tools: `btclab trace-unit`, `reorder-lab`, lab-reorder*/lab-block2 scenarios.
- 2026-11-02: attack/defence roles (144122c, obs v3). Run lz6v6i (6v6 x1 no-heal fine-tune from lz6v6f best, roles 1 att .6 passive .03) now RUNNING from u400 as pure self-play (--bot-anneal 200, --rollback 0, user decision 2026-11-02). u360 sim: Chosen .75/.88ns two evals in a row, hunter .07, presser .17-.38. REAL GAME (duel_par 4x10, Chosen side 0, 6v6 skips, no heal): LuminaZero 36/39, all kill&escape. u360 INSTALLED as game/csmod/BetterThanCastle/policies/luminazero.btcpol (previous v2 file kept as luminazero-lz6v6-0155-v2.btcpol); Debug mod deployed with obs v3.
- Next: user plays u360 (6v6 x1, skips ON, heal OFF = its trained cell); fix hunter/presser holes; extend roles to more unit counts/x3/heal; then obs/value normalisation, entity attention.

## LuminaBuild (economy rush RL, 2026-11-02; docs/eco/DESIGN.md)
- Goal: 6 workers -> 8 halberdier kits on racks + 2 crafted capture wards, fastest; no auto worker AI; ~1.1M params (map conv + entity transformer + pointer heads).
- Done: lab/Btc.EcoShared (101-cmd grammar), lab/Btc.EcoSim (map loader, world, params, gameplay snapshot check), ecolab selftest OK (003ba07); DEV ecodump/ecotrace (31d2aa5, compiled, NOT run yet). New projects not in Btc.Lab.sln.
- Next: user gives cropped map -> ecodump in hidden game, check NOTE|res / MILE ids; eco-* measurement scenarios; ecoplay; sim behaviour (nav, units, items, crafting, stockpiles, blueprints). Training only on user command.

## Small list.md items (2026-11-07, user CONFIRMED in game except I2)
- Done: G8 (eco.factor default 60), J3 (adv timer only Neo.multiplayer), H2 (ResourcePackManager.HideEnemyHud, MP only), F5 (shoot-at ray skips ward auras), G7 (mov.noskip: BtcNoSkip), C1 (craft.max 8-40, BtcCraftQueue, msg 107 tail sync, BC ON), G1 (CanEdit/IsHost also offline+SP), F2 (mov.shootdown: BtcShootDown, 3D min range), G5 infinity heal saved in the save folder (BtcInfHeal, btc-infheal.txt "x y z regen", matched by position, dummy RegenAura per regen value; stuck = regen 0 / inactive / carried / not tracked / >25 blocks), carried wards saved (BtcHeldWards, btc-heldwards.txt, retry 30 s). C3 struck off. Extra files live inside the save folder (Asset_Map.Disk_Save postfix, Disk_ReadGameObjects prefix: Patches/InfHealPatches.cs), host + BC ON only.
- Heal never stacks: vanilla heals once per second by max(aura regen, own regen); we only set ExternalRegenerator when it is null.
- I2 NOT FIXED: BtcPovRec.SnapStart (first POV pose) did not help; ask the user for the symptom before touching it again.
- C1 caveat: the tail (>8 orders) is not saved; the Lua queue UI may show only 8 slots.
