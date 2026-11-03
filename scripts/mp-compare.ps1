param([string]$SubA = "host", [string]$SubB = "client1", [int]$Max = 10)
# Compares "Sync t= units= hash= h1= c=" lines (agent.log) of two instances; t is the same wall-clock 10 s bucket on both. Prints <= $Max mismatch lines + a summary.
$root = Split-Path -Parent $PSScriptRoot
function Read-Sync($sub) {
    $f = Join-Path $root "BTClogs\$sub\agent.log"; $h = @{}
    if (-not (Test-Path $f)) { return $h }
    foreach ($l in [IO.File]::ReadAllLines($f)) {
        if ($l -match '\|Sync\|t=(\d+) units=(\d+) hash=(\w+) h1=(\w+) all=(\d+) nid=(\d+) fac=(\S*) c=(-?[\d.]+),(-?[\d.]+)') {
            $h[[long]$Matches[1]] = @{ U = [int]$Matches[2]; H = $Matches[3]; H1 = $Matches[4]; AL = $Matches[5]; NID = $Matches[6]; F = $Matches[7]; X = [double]$Matches[8]; Z = [double]$Matches[9] }
        }
    }
    return $h
}
$a = Read-Sync $SubA; $b = Read-Sync $SubB
$common = @($a.Keys | Where-Object { $b.ContainsKey($_) } | Sort-Object)
$bad = 0; $shown = 0; $cntBad = 0; $maxd = 0.0
foreach ($t in $common) {
    $x = $a[$t]; $y = $b[$t]; $d = [math]::Sqrt([math]::Pow($x.X - $y.X, 2) + [math]::Pow($x.Z - $y.Z, 2)); if ($d -gt $maxd) { $maxd = $d }
    if ($x.H -ne $y.H -or $x.U -ne $y.U) {
        $bad++
        if ($x.U -ne $y.U) { $cntBad++ }
        if ($shown -lt $Max) { $shown++; "MISMATCH t=$t units $($x.U)/$($y.U) all $($x.AL)/$($y.AL) nid $($x.NID)/$($y.NID) fac $($x.F) | $($y.F) hash $($x.H)/$($y.H) coarse $(if ($x.H1 -eq $y.H1) {'same'} else {'diff'}) centroid_d=$([math]::Round($d,2))" }
    }
}
"SYNC samples=$($common.Count) (host=$($a.Count) client=$($b.Count)) hash_mismatch=$bad unit_count_mismatch=$cntBad max_centroid_d=$([math]::Round($maxd,2))"
