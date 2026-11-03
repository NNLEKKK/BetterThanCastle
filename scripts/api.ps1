param([Parameter(Mandatory = $true)][string]$Type, [string]$Member = '')
# Prints the index line of one vanilla type (docs/gen/game-api-index.md) with members optionally filtered; avoids huge Grep output.
#   scripts/api.ps1 UNetManager            -> ns | path | base | first ~40 members
#   scripts/api.ps1 UNetManager Bc         -> only members containing "Bc"
$root = Split-Path -Parent $PSScriptRoot
$idx = Join-Path $root 'docs\gen\game-api-index.md'
$hit = Select-String -Path $idx -Pattern ("^((class|struct|enum|interface) )?" + [regex]::Escape($Type) + " \|") | Select-Object -First 3
if (-not $hit) { "no type '$Type' in index (third-party/Ak* types are skipped; try Grep on ref/vanilla)"; exit 1 }
foreach ($h in $hit) {
    $c = $h.Line -split ' \| ', 5
    "$($c[0]) | ns=$($c[1]) | $($c[2]) | base=$($c[3])"
    $mem = @(); if ($c.Count -ge 5) { $mem = $c[4] -split ' ; ' }
    if ($Member) { $mem = @($mem | Where-Object { $_ -like "*$Member*" }) }
    "members: $($mem.Count)"
    $mem | Select-Object -First 40 | ForEach-Object { '  ' + $(if ($_.Length -gt 160) { $_.Substring(0, 160) } else { $_ }) }
}
