param([int]$Seconds = 40, [switch]$KillRunning, [switch]$Shot, [switch]$NoGame, [string]$Replay = '')
# One-call smoke test for agents: build Debug -> (launch game, wait, kill) -> digest. Prints <= 8 lines, exit 0 only if everything is clean.
#   SMOKE OK|FAIL build=OK patches=ok/fail hooks(called/never)=a/b hookExc=n errors=n alive=bool
# -Shot : DEV build writes BTClogs/shot.png near the end of the run (read it only if you need to see the UI).
# -NoGame : build only. Refuses to run while the game is running (the DLL is locked; never kill a game the user may be playing).
$root = Split-Path -Parent $PSScriptRoot
$logs = Join-Path $root 'BTClogs'
if (Get-Process -Name 'Castle Story' -ErrorAction SilentlyContinue) {
    if ($KillRunning) { Get-Process -Name 'Castle Story' | Stop-Process -Force; Start-Sleep -Seconds 2 }
    else { 'SMOKE SKIP: game is running (user may be playing). Close it or pass -KillRunning.'; exit 3 }
}
$b = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'build.ps1') 2>&1 | Out-String
if ($LASTEXITCODE -ne 0 -or $b -notmatch 'OK \(Debug\)') {
    'SMOKE FAIL build'
    ($b -split "`r?`n" | Where-Object { $_ -match 'error|FAILED' } | Select-Object -First 6) | ForEach-Object { if ($_.Length -gt 200) { $_.Substring(0, 200) } else { $_ } }
    exit 1
}
if ($NoGame) { 'SMOKE OK build=OK (game not run)'; exit 0 }
if ($Replay) { $env:BTC_AUTOREPLAY = $Replay; if ($Seconds -lt 90) { $Seconds = 90 } }
if ($Shot) { $env:BTC_SHOT_AT = [string][Math]::Max(10, $Seconds - 8) }
$r = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'run-game.ps1') -Seconds $Seconds 2>&1 | Out-String
$alive = $r -match 'alive after \d+s: True'
$digest = Join-Path $logs 'digest.txt'
$errs = 0; if (Test-Path $digest) { $m = Select-String -Path $digest -Pattern 'RESULT: (\d+) distinct' ; if ($m) { $errs = [int]$m.Matches[0].Groups[1].Value } }
$pOk = '?'; $pFail = '?'; $called = '?'; $never = '?'; $hexc = '?'
$js = Join-Path $logs 'last-run.json'
if (Test-Path $js) {
    $j = Get-Content $js -Raw
    $v = @{}; foreach ($k in 'patchOk', 'patchFail', 'called', 'never', 'hookExceptions') { if ($j -match "`"$k`":(\d+)") { $v[$k] = $Matches[1] } }
    if ($v.ContainsKey('patchOk')) { $pOk = $v['patchOk']; $pFail = $v['patchFail']; $called = $v['called']; $never = $v['never']; $hexc = $v['hookExceptions'] }
}
if ($r -match 'BepInEx did not write') { 'SMOKE FAIL BepInEx did not write LogOutput.log (check BepInEx.cfg [Preloader.Entrypoint] Assembly = UnityEngine.dll)'; exit 2 }
$ok = ($alive -and $errs -eq 0 -and $pFail -ne '?' -and [int]$pFail -eq 0 -and ($hexc -eq '?' -or [int]$hexc -eq 0))
"SMOKE $(if ($ok) { 'OK' } else { 'FAIL' }) build=OK patches=$pOk/$pFail hooks(called/never)=$called/$never hookExc=$hexc errors=$errs alive=$alive"
if (-not $ok) { "details: BTClogs\digest.txt (errors), BTClogs\hits.txt (hooks), BTClogs\agent.log (compact events)" ; exit 1 }
exit 0
