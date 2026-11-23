param([Parameter(Mandatory=$true)][string]$Name, [int]$MaxSeconds = 420, [switch]$KillRunning, [switch]$NoBuild, [int]$Clients = 0, [int]$Bc = 1, [switch]$VisibleClient, [switch]$Alloc, [int]$Savers = -1)
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
if ($Alloc) { $env:BTC_ALLOC = '1' } else { Remove-Item Env:BTC_ALLOC -ErrorAction SilentlyContinue }
if ($Savers -ge 0) { $env:BTC_FRAMESAVERS = "$Savers" } else { Remove-Item Env:BTC_FRAMESAVERS -ErrorAction SilentlyContinue }

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
    $roles += @{ Role='client'; Sub="client$i"; Sc=$c; Nick="BtcClient$i" }
}
# stale result files of subs from earlier runs (more clients, late joiners) must not show up in mp-verdict
Get-ChildItem $logs -Directory | Where-Object { $_.Name -match '^(client\w+|spec\w*)$' } | ForEach-Object { Get-ChildItem $_.FullName -File | Where-Object { $_.Name -notmatch '^btc-' } | Remove-Item -Force -ErrorAction SilentlyContinue }
foreach ($r in $roles) {
    $d = Join-Path $logs $r.Sub
    New-Item -ItemType Directory -Force $d | Out-Null
    Get-ChildItem $d -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -notmatch '^btc-' } | Remove-Item -Force -ErrorAction SilentlyContinue
    Get-ChildItem $d -Filter 'btc-*.log' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -Skip 2 | Remove-Item -Force -ErrorAction SilentlyContinue
    $r.Dir = $d
}
function Launch($r) {
    $env:BTC_ROLE = $r.Role; $env:BTC_LOGDIR = $r.Sub; $env:BTC_SCENARIO = $r.Sc
    if ($r.Nick) { $env:BTC_NICK = $r.Nick } else { Remove-Item Env:BTC_NICK -ErrorAction SilentlyContinue }
    if ($VisibleClient -and $r.Sub -eq 'client1') {
        # the user watches this one: normal window at 80% of the screen (not minimized)
        Add-Type -AssemblyName System.Windows.Forms
        $sb = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
        $va = @('-screen-fullscreen', '0', '-screen-width', "$([int]($sb.Width * 0.8))", '-screen-height', "$([int]($sb.Height * 0.8))")
        $r.P = Start-Process -FilePath $exe -WorkingDirectory $g -PassThru -ArgumentList $va
    }
    else { $r.P = Start-Process -FilePath $exe -WorkingDirectory $g -PassThru -WindowStyle Minimized -ArgumentList $args0 }
    Start-Sleep -Seconds 4   # several instances started in the same second die before BepInEx loads
}
# ---- net plan (tests/scenarios/<Name>.net.txt): clumsy profiles, cuts, kills, late launches, triggered by mark files (DEV cmd 'mark X') ----
# at <sub>:<mark>+<sec> net <targets> <durSec|0=to end> <clumsy args...>   (targets: client1,client2 | clients | all)
# at <sub>:<mark>+<sec> cut <targets> <durSec>                                (100% drop both ways = cable pull, no disconnect event)
# at <sub>:<mark>+<sec> kill <targets>                                        (process killed = crash)
# at <sub>:<mark>+<sec> freeze <targets> <durSec>                             (process suspended: total silence both ways, no admin needed)
# at <sub>:<mark>+<sec> launch <newSub> <scenario file under tests/scenarios> <nick>
$plan = @(); $netf = Join-Path $root "tests\scenarios\$Name.net.txt"
if (Test-Path $netf) {
    foreach ($l in (Get-Content $netf)) {
        $l = $l.Trim(); if ($l -eq '' -or $l.StartsWith('#')) { continue }
        if ($l -notmatch '^at (\w+):(\w+)\+(\d+) (\w+) (.*)$') { "SCENARIO WARN: bad net line: $l"; continue }
        $plan += @{ Sub=$Matches[1]; Mark=$Matches[2]; Delay=[int]$Matches[3]; Act=$Matches[4]; Rest=$Matches[5].Trim(); Done=$false }
    }
}
Get-Process -Name clumsy -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
$clumsy = @()   # @{ Pid; Until }
$frozen = @()   # @{ R; Until }
if (-not ('BtcProc' -as [type])) { Add-Type 'using System; using System.Runtime.InteropServices; public static class BtcProc { [DllImport("ntdll.dll")] public static extern int NtSuspendProcess(IntPtr h); [DllImport("ntdll.dll")] public static extern int NtResumeProcess(IntPtr h); }' }
$netlog = Join-Path $logs 'net-plan.txt'; Set-Content $netlog "net plan $Name $(Get-Date -Format o)"
function NetLog($s) { $line = "$([int]((Get-Date) - $t0).TotalSeconds)s $s"; Add-Content $netlog $line }
function Targets($spec) {
    if ($spec -eq 'all' -or $spec -eq 'clients') { return @($roles | Where-Object { $_.Role -eq 'client' -and $_.P -and -not $_.P.HasExited }) }
    $names = $spec -split ','; return @($roles | Where-Object { $names -contains $_.Sub -and $_.P -and -not $_.P.HasExited })
}
function Ports($r) { @(Get-NetUDPEndpoint -OwningProcess $r.P.Id -ErrorAction SilentlyContinue | Where-Object { $_.LocalPort -ne 7777 } | ForEach-Object { $_.LocalPort } | Sort-Object -Unique) }
# ONE clumsy at a time: two WinDivert instances disturb each other (scripts/clumsy-crosstest.ps1: a drop instance on port A also
# killed port B of a lag instance). A 'net <targets> 0 <args>' line sets the base profile; 'cut'/'net' with a duration replace it
# for that time, then the base comes back. The base follows port changes (a client that reconnects gets a new UDP port).
$baseSpec = $null; $baseArgs = $null; $basePid = $null; $baseSig = ''; $baseCheck = Get-Date
function BasePorts { $p = @(); foreach ($r in (Targets $script:baseSpec)) { $p += Ports $r }; return ,@($p | Sort-Object -Unique) }
function StopBase { if ($script:basePid) { Stop-Process -Id $script:basePid -Force -ErrorAction SilentlyContinue; NetLog "base clumsy pid=$($script:basePid) stopped"; $script:basePid = $null } }
function StartBase {
    if (-not $script:baseSpec) { return }
    $ports = BasePorts; $script:baseSig = $ports -join ','
    if ($ports.Count -eq 0) { return }
    $script:basePid = StartClumsyPorts $ports 0 $script:baseArgs "base $($script:baseSpec)" $false
}
function StartClumsy($ts, $dur, $cargs) {
    $ports = @(); foreach ($r in $ts) { $ports += Ports $r }
    if ($ports.Count -eq 0) { NetLog "clumsy: no UDP ports for $(($ts | ForEach-Object { $_.Sub }) -join ',')"; return }
    StopBase
    [void](StartClumsyPorts $ports $dur $cargs (($ts | ForEach-Object { $_.Sub }) -join ',') $true)
}
function StartClumsyPorts($ports, $dur, $cargs, $who, $track) {
    $f = 'udp and outbound and loopback and (' + (($ports | ForEach-Object { "udp.SrcPort == $_ or udp.DstPort == $_" }) -join ' or ') + ')'
    NetLog ("ports now: " + (($roles | Where-Object { $_.P -and -not $_.P.HasExited } | ForEach-Object { "$($_.Sub)[$($_.P.Id)]=" + ((Ports $_) -join '/') }) -join ' '))
    $o = & (Join-Path $PSScriptRoot 'clumsy.ps1') -Filter $f -CArgs $cargs
    $id = $o | Where-Object { $_ -is [int] } | Select-Object -Last 1
    NetLog "clumsy pid=$id dur=$dur [$cargs] on $who ports=$($ports -join ',') $(($o | Where-Object { $_ -is [string] }) -join ' ')"
    if ($id -and $track) { $script:clumsy += @{ Pid=$id; Until=$(if ($dur -gt 0) { (Get-Date).AddSeconds($dur) } else { [DateTime]::MaxValue }) } }
    return $id
}
function NetTick {
    foreach ($a in $plan) {
        if ($a.Done) { continue }
        $mf = Join-Path (Join-Path $logs $a.Sub) "mark-$($a.Mark).txt"
        if (-not (Test-Path $mf)) { continue }
        if (-not $a.At) { $a.At = (Get-Item $mf).LastWriteTime.AddSeconds($a.Delay) }
        if ((Get-Date) -lt $a.At) { continue }
        $a.Done = $true; $w = $a.Rest -split ' '
        switch ($a.Act) {
            'net'    {
                $na = ($w | Select-Object -Skip 2) -join ' '
                if ([int]$w[1] -le 0) { StopBase; $script:baseSpec = $w[0]; $script:baseArgs = $na; StartBase }
                else { StartClumsy (Targets $w[0]) ([int]$w[1]) $na }
            }
            'cut'    {
                # block everything to/from the host port except the OTHER clients' ports: a reconnecting victim opens a new socket
                # (new port) that a filter on its old ports would let through (the 30+ s outages "recovered" during the cut)
                $ts = Targets $w[0]; $op = @(); foreach ($r in $roles) { if ($r.Role -eq 'client' -and $r.P -and -not $r.P.HasExited -and -not ($ts -contains $r)) { $op += Ports $r } }
                $f = 'udp and outbound and loopback and (udp.SrcPort == 3000 or udp.DstPort == 3000)'
                if ($op.Count -gt 0) { $f += ' and ' + (($op | ForEach-Object { "udp.SrcPort != $_ and udp.DstPort != $_" }) -join ' and ') }   # clumsy rejects 'not (...)'
                StopBase
                NetLog "cut filter: $f"
                $o = & (Join-Path $PSScriptRoot 'clumsy.ps1') -Filter $f -CArgs '--drop on --drop-chance 100.0'
                $id = $o | Where-Object { $_ -is [int] } | Select-Object -Last 1
                NetLog "clumsy pid=$id dur=$($w[1]) [--drop on --drop-chance 100.0] on $($w[0]) $(($o | Where-Object { $_ -is [string] }) -join ' ')"
                if ($id) { $script:clumsy += @{ Pid=$id; Until=(Get-Date).AddSeconds([int]$w[1]) } }
            }
            'freeze' { foreach ($r in (Targets $w[0])) { [void][BtcProc]::NtSuspendProcess($r.P.Handle); $script:frozen += @{ R=$r; Until=(Get-Date).AddSeconds([int]$w[1]) }; NetLog "froze $($r.Sub) for $($w[1]) s" } }
            'kill'   { foreach ($r in (Targets $w[0])) { Stop-Process -Id $r.P.Id -Force; $r.Killed = $true; NetLog "killed $($r.Sub)" } }
            'launch' {
                $nr = @{ Role='client'; Sub=$w[0]; Sc=(Join-Path $root "tests\scenarios\$($w[1])"); Nick=$w[2] }
                $d = Join-Path $logs $nr.Sub; New-Item -ItemType Directory -Force $d | Out-Null
                Get-ChildItem $d -File -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
                $nr.Dir = $d; Launch $nr; $script:roles += $nr; NetLog "launched $($nr.Sub) nick=$($nr.Nick)"
            }
        }
    }
    foreach ($fz in $script:frozen) { if (-not $fz.Gone -and (Get-Date) -ge $fz.Until) { [void][BtcProc]::NtResumeProcess($fz.R.P.Handle); $fz.Gone = $true; NetLog "resumed $($fz.R.Sub)" } }
    foreach ($c in $script:clumsy) { if (-not $c.Gone -and (Get-Date) -ge $c.Until) { Stop-Process -Id $c.Pid -Force -ErrorAction SilentlyContinue; $c.Gone = $true; NetLog "clumsy pid=$($c.Pid) stopped" } }
    $temp = @($script:clumsy | Where-Object { -not $_.Gone }).Count -gt 0
    if ($script:baseSpec -and -not $temp) {
        if (-not $script:basePid) { StartBase }
        elseif ((Get-Date) -ge $script:baseCheck) {
            $script:baseCheck = (Get-Date).AddSeconds(5)
            if (((BasePorts) -join ',') -ne $script:baseSig) { NetLog "base ports changed"; StopBase; StartBase }
        }
    }
}
# BepInEx keeps at most 5 disk logs (LogOutput.log + .1-.4): a 6th instance dies at load. With more instances turn the BepInEx disk
# log off for this run (the mod's own btc-*.log in BTClogs/<sub> stays) and restore the config at the end.
$bcfg = Join-Path $g 'BepInEx\config\BepInEx.cfg'; $bcfgOrig = $null
$late = @($plan | Where-Object { $_.Act -eq 'launch' }).Count
if ($roles.Count + $late -gt 5 -and (Test-Path $bcfg)) {
    $bcfgOrig = [IO.File]::ReadAllText($bcfg)
    $nc = [regex]::Replace($bcfgOrig, '(\[Logging\.Disk\][^\[]*?)Enabled = true', '$1Enabled = false')
    [IO.File]::WriteAllText($bcfg, $nc, (New-Object Text.UTF8Encoding($false)))
}
$t0 = Get-Date
foreach ($r in $roles) {
    Launch $r
    if ($r.Role -eq 'host') {
        # wait for host-ready before the cold start of the clients (a client scenario also waits for it via waitfile)
        $hw = Get-Date
        while (((Get-Date) - $hw).TotalSeconds -lt 180 -and -not (Test-Path (Join-Path $r.Dir 'host-ready.txt')) -and -not $r.P.HasExited) { Start-Sleep -Seconds 2 }
        if (-not (Test-Path (Join-Path $r.Dir 'host-ready.txt'))) { 'SCENARIO WARN: host-ready not published in 180 s' }
    }
}
$ok = @{}
while (((Get-Date) - $t0).TotalSeconds -lt $MaxSeconds) {
    Start-Sleep -Seconds 1
    NetTick
    $all = ($plan | Where-Object { -not $_.Done }).Count -eq 0
    foreach ($r in $roles) {
        if (Test-Path (Join-Path $r.Dir 'done.txt')) { $ok[$r.Sub] = $true } elseif ($r.Killed) { $ok[$r.Sub] = $true } elseif (-not $r.P.HasExited) { $all = $false }
    }
    # the host is done -> nothing left to test (clients wait for the host)
    if ($ok['host'] -and ((Get-Date) - (Get-Item (Join-Path $logs 'host\done.txt')).LastWriteTime).TotalSeconds -gt 20) { break }
    if ($all) { break }
}
Remove-Item Env:BTC_ROLE, Env:BTC_LOGDIR, Env:BTC_SCENARIO, Env:BTC_NICK -ErrorAction SilentlyContinue
foreach ($c in $clumsy) { Stop-Process -Id $c.Pid -Force -ErrorAction SilentlyContinue }
StopBase
Start-Sleep -Seconds 2
foreach ($r in $roles) { if (-not $r.P.HasExited) { Stop-Process -Id $r.P.Id -Force } }
if ($bcfgOrig) { [IO.File]::WriteAllText($bcfg, $bcfgOrig, (New-Object Text.UTF8Encoding($false))) }
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
