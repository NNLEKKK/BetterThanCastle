param([Parameter(Mandatory = $true)][string]$A, [Parameter(Mandatory = $true)][string]$B)
# Compares two BTClogs/prof.txt dumps: header deltas + TOP total rows whose ms_total changed by >20% (max 25 lines).
function Read-Prof([string]$p) {
    $r = @{ H = @{}; Top = @{}; Spikes = 0 }
    $sec = ''
    foreach ($l in [IO.File]::ReadAllLines($p)) {
        if ($l -like 'PROF *') {
            foreach ($m in [regex]::Matches($l, '(\S+?)=(\S+)')) { $r.H[$m.Groups[1].Value] = $m.Groups[2].Value }
            continue
        }
        if ($l -match '^(TOP total|TOP max|SPIKES|COUNTERS|IDLE)') { $sec = $Matches[1]; continue }
        if ($sec -eq 'TOP total' -and $l -match '^(\S+) (\d+) (\S+) (\S+) (.+)$') { $r.Top[$Matches[5]] = [double]$Matches[1] }
        if ($sec -eq 'SPIKES' -and $l.Trim()) { $r.Spikes++ }
    }
    $r
}
$ra = Read-Prof $A; $rb = Read-Prof $B
$out = @()
$out += 'HEADER   A -> B'
foreach ($k in 'fps', 'p50', 'p99', 'max', '>33ms', '>100ms', 'gc0/s', 'mem') {
    $x = $ra.H[$k]; $y = $rb.H[$k]
    if ($x -ne $null -and $y -ne $null) {
        $dx = [double]$x; $dy = [double]$y
        $pct = if ($dx -ne 0) { '{0:+0;-0}%' -f (($dy - $dx) * 100 / $dx) } else { 'n/a' }
        $out += ('{0,-7} {1} -> {2} ({3})' -f $k, $x, $y, $pct)
    }
}
$out += ('spikes>50ms rows {0} -> {1}' -f $ra.Spikes, $rb.Spikes)
$out += 'TOP total changed >20%: name A_ms B_ms delta%'
$names = @($ra.Top.Keys) + @($rb.Top.Keys) | Sort-Object -Unique
$rows = foreach ($n in $names) {
    $x = 0.0; $y = 0.0
    if ($ra.Top.ContainsKey($n)) { $x = $ra.Top[$n] }
    if ($rb.Top.ContainsKey($n)) { $y = $rb.Top[$n] }
    $d = if ($x -gt 0) { ($y - $x) * 100 / $x } else { 100 }
    if ([math]::Abs($d) -gt 20) { [pscustomobject]@{ N = $n; X = $x; Y = $y; D = $d } }
}
$room = 25 - $out.Count
foreach ($r in ($rows | Sort-Object { [math]::Abs($_.Y - $_.X) } -Descending | Select-Object -First $room)) {
    $out += ('{0} {1} {2} {3:+0;-0}%' -f $r.N, $r.X, $r.Y, $r.D)
}
$out
