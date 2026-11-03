param([string]$FeatList = '63', [int]$Units = 8, [int]$Matches = 10, [int]$Opp = 1, [int]$OppTier = 4, [int]$Skip = 1, [int]$OppLevel = 1, [int]$MaxSeconds = 1500, [string]$Extra = '', [switch]$NoBuild)
# Parallel duel A/B: one hidden game instance per entry of -Feats (own BTClogs\parN and duel-logs\parN), current bot (side 1, feature mask) vs opponent
# (-Opp 0 old v1, 1 frozen bot v2, 2 current bot). -Extra = extra scenario lines (one per ';'). Prints a tally per instance.
$Feats = @($FeatList -split ',' | ForEach-Object { [int]$_ }); $root = Split-Path -Parent $PSScriptRoot; $g = Join-Path $root 'game\csmod'; $logs = Join-Path $root 'BTClogs'
if (Get-Process -Name 'Castle Story' -ErrorAction SilentlyContinue) { 'DUELPAR SKIP: game is running (user may be playing).'; exit 3 }
if (-not $NoBuild) { $b = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'build.ps1') 2>&1 | Out-String; if (-not $b.Contains('OK (Debug)')) { 'DUELPAR FAIL: build'; exit 1 } }
$exe = Join-Path $g 'Castle Story.exe'; $args0 = @('-screen-fullscreen','0','-screen-width','640','-screen-height','360')
$env:BTC_PROF = '1'; $env:BTC_HIDDEN = '1'; Remove-Item Env:BTC_ROLE -ErrorAction SilentlyContinue; Remove-Item Env:BTC_BC -ErrorAction SilentlyContinue
$procs = @()
for ($i = 0; $i -lt $Feats.Count; $i++) {
    $sub = "par$i"; $d = Join-Path $logs $sub; New-Item -ItemType Directory -Force $d | Out-Null
    Get-ChildItem $d -File -ErrorAction SilentlyContinue | Where-Object { -not $_.Name.StartsWith('btc-') } | Remove-Item -Force -ErrorAction SilentlyContinue
    $dl = Join-Path $g "BetterThanCastle\duel-logs\$sub"; if (Test-Path $dl) { Remove-Item $dl -Recurse -Force }
    $lines = @('prof start', "duelunits 4 $Units $Units", "duelskip $Skip", 'duelheal 0', "duelplayerbot $OppLevel $Opp $OppTier", "duelfeat $($Feats[$i])")
    if ($Extra) { $lines += $Extra.Split(';') }
    $lines += @('duel', 'waitduel 240')
    for ($m = 1; $m -lt $Matches; $m++) { $lines += @('waitduelover 150', 'duelrematch', 'wait 4') }
    $lines += @('waitduelover 150', 'wait 1', 'quit')
    $sc = Join-Path $root "tests\scenarios\duel-par$i.txt"; Set-Content -Path $sc -Value $lines -Encoding ASCII
    $env:BTC_LOGDIR = $sub; $env:BTC_SCENARIO = $sc
    $procs += Start-Process -FilePath $exe -WorkingDirectory $g -PassThru -WindowStyle Minimized -ArgumentList $args0
    Start-Sleep -Seconds 12
}
Remove-Item Env:BTC_LOGDIR, Env:BTC_SCENARIO -ErrorAction SilentlyContinue
$t0 = Get-Date
while (((Get-Date) - $t0).TotalSeconds -lt $MaxSeconds) {
    Start-Sleep -Seconds 5; $all = $true
    for ($i = 0; $i -lt $procs.Count; $i++) { if (-not (Test-Path (Join-Path $logs "par$i\done.txt")) -and -not $procs[$i].HasExited) { $all = $false } }
    if ($all) { break }
}
Start-Sleep -Seconds 3
foreach ($p in $procs) { if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force } }
for ($i = 0; $i -lt $Feats.Count; $i++) {
    $dl = Join-Path $g "BetterThanCastle\duel-logs\par$i"; $b = 0; $p = 0; $o = 0; $tsum = 0.0; $lossBot = 0; $lossOpp = 0
    if (Test-Path $dl) { foreach ($f in Get-ChildItem $dl -Filter 'duel-*.txt') {
        $e = Select-String -Path $f.FullName -Pattern '\|END\|winner=(\w+) reason=(.*) t=([0-9.]+)' | Select-Object -First 1
        $lossBot += @(Select-String -Path $f.FullName -Pattern '\|KILL\|u\d+ s1 ').Count; $lossOpp += @(Select-String -Path $f.FullName -Pattern '\|KILL\|u\d+ s0 ').Count; if ($e) { $w = $e.Matches[0].Groups[1].Value; if ($w -eq 'bot') { $b++ } elseif ($w -eq 'player') { $p++ } else { $o++ }; $tsum += [double]$e.Matches[0].Groups[3].Value } else { $o++ } } }
    $n = $b + $p; "feat=$($Feats[$i]) units=$Units opp=$Opp : v3=$b opp=$p other=$o botLosses/match=$(if ($n) { [math]::Round($lossBot / $n, 2) } else { 0 }) oppLosses/match=$(if ($n) { [math]::Round($lossOpp / $n, 2) } else { 0 }) avg_t=$(if ($n) { [math]::Round($tsum / $n, 1) } else { 0 })"
}

