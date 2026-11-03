# Verifies that every [HarmonyPatch(typeof(T), "Method", ...)] target exists in the LEGACY decompile (ref/mod-old = clean vanilla + mod edits),
# so the plugin does not depend on features that only exist in the (contaminated) development DLL. Name-level check only.
$root = Split-Path -Parent $PSScriptRoot
$mod = Join-Path $root 'ref\mod-old'
$patchDir = Join-Path $root 'src\BetterThanCastle\Patches'
$rx = New-Object Text.RegularExpressions.Regex '\[HarmonyPatch\(typeof\(([\w\.<>, ]+)\),\s*"([^"]+)"', 'Compiled'
$files = Get-ChildItem $mod -Recurse -File -Filter *.cs
$byType = @{}
$bad = 0; $n = 0
foreach ($pf in Get-ChildItem $patchDir -Filter *.cs) {
    foreach ($m in $rx.Matches([IO.File]::ReadAllText($pf.FullName))) {
        $type = $m.Groups[1].Value.Split('.')[-1] -replace '<.*', ''
        $name = $m.Groups[2].Value
        $n++
        if (-not $byType.ContainsKey($type)) {
            $hit = $files | Where-Object { $_.BaseName -eq $type } | Select-Object -First 1
            if (-not $hit) { $hit = Select-String -Path ($files | ForEach-Object FullName) -Pattern ("(class|struct) " + [regex]::Escape($type) + "\b") -List | Select-Object -First 1; if ($hit) { $hit = Get-Item $hit.Path } }
            $byType[$type] = if ($hit) { [IO.File]::ReadAllText($hit.FullName) } else { $null }
        }
        $t = $byType[$type]
        $bare = $name -replace '^(get_|set_)', ''
        if ($null -eq $t) { Write-Host "NO TYPE in legacy: $type ($($pf.Name))"; $bad++ }
        elseif ($t -notmatch ('\b' + [regex]::Escape($bare) + '\b')) { Write-Host "NO MEMBER in legacy: $type.$name ($($pf.Name))"; $bad++ }
    }
}
Write-Host "checked $n patch targets, $bad not found in legacy decompile"
