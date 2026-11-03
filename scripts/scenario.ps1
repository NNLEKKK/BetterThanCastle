param([Parameter(Mandatory=$true)][string]$Name, [int]$MaxSeconds = 420, [switch]$KillRunning, [switch]$NoBuild, [int]$Clients = 0, [int]$Bc = 1)
# Runs tests/scenarios/<Name>.txt in a hidden game with the DEV profiler; prints a short result. Details: BTClogs/prof.txt, digest.txt, agent.log.
# -Clients N (multiplayer): host runs <Name>.txt (BTC_ROLE=host, BTClogs/host), client i runs <Name>.client<i>.txt or <Name>.client.txt (BTC_ROLE=client, BTClogs/client<i>).
$root = Split-Path -Parent $PSScriptRoot
$logs = Join-Path $root 'BTClogs'
$g = Join-Path $root 'game\csmod'
$sc = Join-Path $root "tests\scenarios\$Name.txt"
if (-not (Test-Path $sc)) { "SCENARIO FAIL: $sc not found"; exit 1 }
if (Get-Process -Name 'Castle Story' -ErrorAction SilentlyContinue) {
    if ($KillRunning) { Get-Process -Name 'Castle Story' | Stop-Process -Force; Start-Sleep -Seconds 2 }
    else { 'SCENARIO SKIP: game is running (user may be playing).'; exit 3 }
}
if (-not $NoBuild) {
    $b = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'build.ps1') 2>&1 | Out-String
    if ($b -notmatch 'OK \(Debug\)') { 'SCENARIO FAIL: build'; ($b -split "`r?`n" | Where-Object { $_ -match 'error' } | Select-Object -First 5); exit 1 }
}
Get-ChildItem $logs -Filter 'btc-*.log' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -Skip 3 | Remove-Item -Force -ErrorAction SilentlyContinue
$exe = Join-Path $g 'Castle Story.exe'
$args0 = @('-screen-fullscreen','0','-screen-width','640','-screen-height','360')
$env:BTC_PROF = '1'; $env:BTC_HIDDEN = '1'; if ($Bc -eq 0) { $env:BTC_BC = '0' } else { Remove-Item Env:BTC_BC -ErrorAction SilentlyContinue }

if ($Clients -le 0) {
    Remove-Item (Join-Path $logs 'done.txt'), (Join-Path $logs 'cmd.txt'), (Join-Path $logs 'prof.txt') -ErrorAction SilentlyContinue
    $env:BTC_SCENARIO = $sc; Remove-Item Env:BTC_ROLE, Env:BTC_LOGDIR -ErrorAction SilentlyContinue
    $p = Start-Process -FilePath $exe -WorkingDirectory $g -PassThru -WindowStyle Minimized -ArgumentList $args0
    $t0 = Get-Date; $done = $false
    while (((Get-Date) - $t0).TotalSeconds -lt $MaxSeconds) {
        Start-Sleep -Seconds 3
        if (Test-Path (Join-Path $logs 'done.txt')) { $done = $true; break }
        if ($p.HasExited) { break }
    }
    Start-Sleep -Seconds 2
    if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'log-digest.ps1') | Out-Null
    $secs = [int]((Get-Date) - $t0).TotalSeconds
    $errs = 0; $digest = Join-Path $logs 'digest.txt'
    if (Test-Path $digest) { $m = Select-String -Path $digest -Pattern 'RESULT: (\d+) distinct'; if ($m) { $errs = [int]$m.Matches[0].Groups[1].Value } }
    "SCENARIO $(if ($done) {'DONE'} else {'INCOMPLETE'}) name=$Name secs=$secs errors=$errs"
    Get-ChildItem $logs -Filter 'prof*.txt' | ForEach-Object { $_.Name }
    if (Test-Path (Join-Path $logs 'prof.txt')) { Get-Content (Join-Path $logs 'prof.txt') -TotalCount 1 }
    "details: BTClogs\prof*.txt, digest.txt, agent.log"
    exit 0
}

# ---- multiplayer: host + N clients on one PC (localhost, no Steam) ----
$roles = @(@{ Role='host'; Sub='host'; Sc=$sc })
for ($i = 1; $i -le $Clients; $i++) {
    $c = Join-Path $root "tests\scenarios\$Name.client$i.txt"; if (-not (Test-Path $c)) { $c = Join-Path $root "tests\scenarios\$Name.client.txt" }
    if (-not (Test-Path $c)) { "SCENARIO FAIL: no client scenario for $Name"; exit 1 }
    $roles += @{ Role='client'; Sub="client$i"; Sc=$c }
}
foreach ($r in $roles) {
    $d = Join-Path $logs $r.Sub
    New-Item -ItemType Directory -Force $d | Out-Null
    Get-ChildItem $d -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -notmatch '^btc-' } | Remove-Item -Force -ErrorAction SilentlyContinue
    Get-ChildItem $d -Filter 'btc-*.log' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -Skip 2 | Remove-Item -Force -ErrorAction SilentlyContinue
    $r.Dir = $d
}
$t0 = Get-Date
foreach ($r in $roles) {
    $env:BTC_ROLE = $r.Role; $env:BTC_LOGDIR = $r.Sub; $env:BTC_SCENARIO = $r.Sc
    $r.P = Start-Process -FilePath $exe -WorkingDirectory $g -PassThru -WindowStyle Minimized -ArgumentList $args0
    if ($r.Role -eq 'host') {
        # wait for host-ready before the cold start of the clients (a client scenario also waits for it via waitfile)
        $hw = Get-Date
        while (((Get-Date) - $hw).TotalSeconds -lt 180 -and -not (Test-Path (Join-Path $r.Dir 'host-ready.txt')) -and -not $r.P.HasExited) { Start-Sleep -Seconds 2 }
        if (-not (Test-Path (Join-Path $r.Dir 'host-ready.txt'))) { 'SCENARIO WARN: host-ready not published in 180 s' }
    }
}
Remove-Item Env:BTC_ROLE, Env:BTC_LOGDIR, Env:BTC_SCENARIO -ErrorAction SilentlyContinue
$ok = @{}
while (((Get-Date) - $t0).TotalSeconds -lt $MaxSeconds) {
    Start-Sleep -Seconds 3
    $all = $true
    foreach ($r in $roles) {
        if (Test-Path (Join-Path $r.Dir 'done.txt')) { $ok[$r.Sub] = $true } elseif (-not $r.P.HasExited) { $all = $false }
    }
    if ($all) { break }
}
Start-Sleep -Seconds 2
foreach ($r in $roles) { if (-not $r.P.HasExited) { Stop-Process -Id $r.P.Id -Force } }
$secs = [int]((Get-Date) - $t0).TotalSeconds
$parts = @()
foreach ($r in $roles) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'log-digest.ps1') -Sub $r.Sub | Out-Null
    $errs = 0; $digest = Join-Path $r.Dir 'digest.txt'
    if (Test-Path $digest) { $m = Select-String -Path $digest -Pattern 'RESULT: (\d+) distinct'; if ($m) { $errs = [int]$m.Matches[0].Groups[1].Value } }
    $parts += "$($r.Sub)=$(if ($ok[$r.Sub]) {'DONE'} else {'INCOMPLETE'})/err$errs"
}
$alld = ($roles | Where-Object { -not $ok[$_.Sub] }).Count -eq 0
"SCENARIO MP $(if ($alld) {'DONE'} else {'INCOMPLETE'}) name=$Name secs=$secs $($parts -join ' ')"
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'mp-compare.ps1') | Select-Object -First 4
"details: BTClogs\host|client<i>\ prof*.txt engine.txt digest.txt agent.log ; scripts\mp-compare.ps1"
