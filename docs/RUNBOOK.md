# RUNBOOK (commands and agent protocol; all scripts: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/<X>.ps1)
| need | command | prints |
|---|---|---|
| compile check, all errors | `errors.ps1 [-Tag t -Filter "regex"]` | count + first 25 (full: BTClogs/build-errors[-tag].txt) |
| build + deploy Debug (DEV) | `build.ps1` | `OK (Debug)` or errors; copies to game plugins; fails to copy while game runs |
| Release + leak check | `build.ps1 -Release` | `verify-dist: OK`; dist/ must hold exactly BetterThanCastle.dll |
| one-call smoke test | `smoke.ps1 [-Seconds 40] [-Shot] [-NoGame] [-KillRunning]` | `SMOKE OK|FAIL build patches=ok/fail hooks(called/never) hookExc errors alive` |
| launch only | `run-game.ps1 [-Seconds N] [-KillRunning]` | RESULT line; refuses if game already running |
| condensed log | `log-digest.ps1` (run by smoke) | BTClogs/digest.txt (deduped errors, patch summary, hook stats) |
| one vanilla type | `api.ps1 Type [member]` | ns/path/base/members (from docs/gen/game-api-index.md) |
| legacy hooks of a file | `hook-hunks.ps1 -File Brix/Game/AI/Labor [-All]` | hunks touching Btc*, with enclosing member |
| patch targets exist in legacy decompile | `check-targets.ps1` | missing targets |
| patch list doc | `sh scripts/gen-patches.sh` | rewrites docs/patches.md |
| clean-DLL workflow (tomorrow) | `redecompile.ps1` -> `check-dll.ps1` -> `gen-diff.ps1 -Vanilla ref\vanilla-clean` | see STATUS |
| regenerate docs/gen (only if ref changed) | `gen-diff.ps1`, `gen-featuremap.ps1`, `gen-api-index.ps1`, `gen-lua-index.ps1` | ASK USER first (minutes, many tokens if output read) |
Frozen: `port-new-classes.ps1` (+`post-port-fixes.ps1`), blocked by `src/BetterThanCastle/.ported` (use -Force only to redo wave 1 from scratch: overwrites hand edits).

## BTClogs (game logs, DEV build only; gitignored)
- `digest.txt` start here. `agent.log` compact `time|I/W/E|Tag|msg` from BtcLog. `hits.txt` per patched method: calls / exceptions / first exception + "never called" list. `last-run.json` totals. `btc-*.log` full BepInEx mirror (3 newest kept). `mod_log.txt`, `replay_log.txt`, `btc_*.txt`: legacy class logs. `shot.png` (F9 or smoke -Shot).
- Env switches (DEV): `BTC_NOPROBE=1` disable hit counters; `BTC_SHOT_AT=<sec>` screenshot.
- Known benign noise (filtered): `GA Error`, `Steamworks is not initialized` (direct exe launch), `Coroutine couldn't be started GA_Controller`, first `Hat apply failed ... Visuals` (retry succeeds).

## Parallel agent protocol
Each agent: own list of vanilla types -> own files `src/BetterThanCastle/Patches/<Type>Patches.cs`; compile with `errors.ps1 -Tag <tag> -Filter "<own files>"` (private obj/bin, csproj excludes obj/bin from Compile); never run build.ps1/run-game/smoke; no commits; report <=12-15 lines (hooks per type, skipped/uncertain, test list). Main session merges, fixes cross-file errors, runs smoke, commits.
Hook-port recipe: `hook-hunks.ps1 -File X` -> read enclosing member in ref/mod-old (range) -> choose Prefix/Postfix/Transpiler -> verify target name/overload with Grep in ref/vanilla -> errors.ps1 -> smoke ([Patch] FAIL / [Hook] errors show anchor misses at startup).

## Two-instance multiplayer (DEV, localhost, no Steam)
`scripts/scenario.ps1 -Name mp-basic -Clients 1 [-Bc 0]` -> host (BTC_ROLE=host, BTClogs/host) + client (BTC_ROLE=client, BTClogs/client1), scenarios `tests/scenarios/mp-basic.txt` + `.client.txt`. ~3.5 min. Commands: host/join/waitclients/waitlobby/ready/startmatch/waitfile (BtcDevNet). Port 3000. Output: per-instance prof-*.txt/engine.txt (`Net` line), agent.log `Sync t= units= hash= fac= nid=` ; `scripts/mp-compare.ps1`.
Notes: `-Bc 0` sets BTC_BC=0 (BC OFF in-memory, cfg untouched). Client sees only its own faction objects (nid/units differ, also with BC OFF) = fog/interest, not a BC bug; compare per faction. Host connection list includes a fake "btc-replay" RecConn.
