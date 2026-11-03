# BTC MCP server (`btc`)

Stdio MCP server, hand-rolled JSON-RPC (System.Text.Json only, no NuGet). Source: `lab/Btc.Mcp/`. Registered in `/.mcp.json` (absolute dll path), enabled in `.claude/settings.json` (`enabledMcpjsonServers`, allow `mcp__btc`).
Build: `dotnet build lab/Btc.Mcp/Btc.Mcp.csproj -c Release -v q -nologo`. Tests: `dotnet lab/Btc.Mcp/bin/Release/net10.0/btc-mcp.dll --selftest` (in-process) and `powershell -NoProfile -ExecutionPolicy Bypass -File lab/Btc.Mcp/pipetest.ps1` (real pipes).

## Tools (21)
| tool | what |
|---|---|
| build {config} | scripts/build.ps1 (Debug refuses while any Castle Story runs) |
| errors {tag?, filter?} | scripts/errors.ps1, parsed `CODE file:line msg` |
| smoke {seconds?} | scripts/smoke.ps1 one-line verdict (job, ~45-90 s) |
| deploy_status | src dll vs deployed dll vs dist vs newest .cs: OK/STALE |
| game_status / kill_my_instances | list games (ours = `-screen-width 640`); kill only ours |
| game_cmd {dir?, lines[]} | append to BTClogs/<dir>/cmd.txt, return new agent.log lines (<=5 s) |
| scenario_run {name, clients?, bc?, maxSeconds?} | job: scripts/scenario.ps1 |
| duel_par {featList, units, matches, opp, oppTier, oppLevel, skip, extra, noBuild} | job: scripts/duel-par.ps1 |
| duel_logs {dir?, last?} | per-match rows + totals; dir `''`, par0..par3, kirusha |
| duel_ab {a, b, last?} | bot win rate per set (comma lists of dirs), Wilson 95% CI, z-test verdict |
| logs {kind, grep?, last?, dir?, tag?} | digest / agent / hits (never-called) / errors |
| lab_build {project?} | cli (default) / tests / sln (sln includes Btc.Mcp: locked while connected) |
| lab {args, background?, lowPriority?} | btclab passthrough |
| job_status / job_wait / job_tail / job_cancel | job control |
| status | STATUS.md "## NEXT" + git log -5 + change count |
| api {type, member?} / ref_grep {pattern, path?, max?} | scripts/api.ps1; bounded regex search in ref/vanilla |

## Job model
`Jobs.Start` launches a child process (stdout+stderr tee'd to `BTClogs/jobs/<id>.log`, state in `<id>.json`, so finished jobs survive a server restart; a job running at restart shows `lost`). Tools that may be long call `Jobs.RunOrJob`: wait up to 50 s (10-15 s for scenario/duel_par so an early SKIP is visible), return the formatted result, else `RUNNING job <id>`; then poll `job_wait {id}`. `lowPriority` = BelowNormal. `job_cancel` kills the process tree. Default job cap 1 h (lab: 6 h).
Child output is decoded as the OEM console code page (PowerShell 5.1, dotnet), git as UTF-8. PowerShell is run via `-Command "& script ...; exit $LASTEXITCODE"`.

## Adding a tool
1. In `lab/Btc.Mcp/Tools/<Area>Tools.cs` write `static Task<ToolResult> MyTool(Args a)` (use `Args.Str/Int/Bool/List`, `Jobs.RunOrJob` for slow work, keep output < ~6k chars via `Txt.Head/Tail`).
2. Add one line in that class's `Register`: `l.Add(T("name", "one-line description", P(Str("x","..."), Int(...)), "requiredCsv", MyTool));`
3. New area file: add `XTools.Register(l)` in `Registry.Build`. Bump the count in SelfTest/pipetest.
4. Keep logic in scripts or btclab, not in the server.

## Restart note
The dll is locked while Claude Code is connected. To change the server: stop it (`/mcp` disconnect, or end the session), rebuild, `/mcp` reconnect. Tool-side logic that lives in scripts/btclab can change freely without a reconnect (scripts are read on each call).
