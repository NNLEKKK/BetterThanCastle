param([Parameter(Mandatory=$true)][string]$Names, [int]$MaxSeconds = 1200, [switch]$NoBuild)
# Lab runs for sim calibration: one hidden game per scenario name (tests/scenarios/<name>.txt), BTC_LOGDIR=labN.
# Output: BTClogs\labN\agent.log, lab-dump.txt; duel logs game\csmod\BetterThanCastle\duel-logs\labN\. Never touches a running game.
$list = @($Names -split ','); $root = Split-Path -Parent $PSScriptRoot; $g = Join-Path $root 'game\csmod'; $logs = Join-Path $root 'BTClogs'
if (Get-Process -Name 'Castle Story' -ErrorAction SilentlyContinue) { 'LAB SKIP: game is running (user may be playing).'; exit 3 }
if (-not $NoBuild) { $b = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'build.ps1') 2>&1 | Out-String; if (-not $b.Contains('OK (Debug)')) { 'LAB FAIL: build'; exit 1 } }
$exe = Join-Path $g 'Castle Story.exe'; $args0 = @('-screen-fullscreen','0','-screen-width','640','-screen-height','360')
$env:BTC_PROF = '1'; $env:BTC_HIDDEN = '1'; Remove-Item Env:BTC_ROLE -ErrorAction SilentlyContinue; Remove-Item Env:BTC_BC -ErrorAction SilentlyContinue
$procs = @()
for ($i = 0; $i -lt $list.Count; $i++) {
    $sub = "lab$i"; $d = Join-Path $logs $sub; New-Item -ItemType Directory -Force $d | Out-Null
    Get-ChildItem $d -File -ErrorAction SilentlyContinue | Where-Object { -not $_.Name.StartsWith('btc-') } | Remove-Item -Force -ErrorAction SilentlyContinue
    $dl = Join-Path $g "BetterThanCastle\duel-logs\$sub"; if (Test-Path $dl) { Remove-Item $dl -Recurse -Force }
    $sc = Join-Path $root "tests\scenarios\$($list[$i]).txt"; if (-not (Test-Path $sc)) { "LAB FAIL: $sc not found"; exit 1 }
    $env:BTC_LOGDIR = $sub; $env:BTC_SCENARIO = $sc
    $procs += Start-Process -FilePath $exe -WorkingDirectory $g -PassThru -WindowStyle Minimized -ArgumentList $args0
    Start-Sleep -Seconds 12
}
Remove-Item Env:BTC_LOGDIR, Env:BTC_SCENARIO -ErrorAction SilentlyContinue
$t0 = Get-Date
while (((Get-Date) - $t0).TotalSeconds -lt $MaxSeconds) {
    Start-Sleep -Seconds 5; $all = $true
    for ($i = 0; $i -lt $procs.Count; $i++) { if (-not (Test-Path (Join-Path $logs "lab$i\done.txt")) -and -not $procs[$i].HasExited) { $all = $false } }
    if ($all) { break }
}
Start-Sleep -Seconds 3
foreach ($p in $procs) { if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force } }
for ($i = 0; $i -lt $list.Count; $i++) {
    $d = Join-Path $logs "lab$i"; $dl = Join-Path $g "BetterThanCastle\duel-logs\lab$i"
    $done = Test-Path (Join-Path $d 'done.txt'); $n = if (Test-Path $dl) { @(Get-ChildItem $dl -Filter 'duel-*.txt').Count } else { 0 }
    $err = if (Test-Path (Join-Path $d 'agent.log')) { @(Select-String -Path (Join-Path $d 'agent.log') -Pattern '\|E\|').Count } else { -1 }
    "$($list[$i]) lab$i done=$done duellogs=$n agentErrors=$err secs=$([int]((Get-Date) - $t0).TotalSeconds)"
}
