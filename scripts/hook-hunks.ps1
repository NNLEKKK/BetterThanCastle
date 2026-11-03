param([Parameter(Mandatory = $true)][string]$File, [switch]$All)
# Prints the meaningful hunks of ref/diff/<File>.diff (File = path under Assembly-CSharp-firstpass without .cs, e.g. Brix/Game/AI/Labor or Tool).
# Default: only hunks that touch mod classes (Btc*, ResourcePackManager, CameraBookmarkHotkey, JobSelectHotkeys). -All: every hunk (mostly dnSpy noise).
# Each hunk is preceded by the enclosing member declaration (vanilla method to patch). Needs scripts/gen-diff.ps1 to have run.
$root = Split-Path -Parent $PSScriptRoot
$rel = $File.Replace('/', '\').Replace('.cs', '')
$diffFile = Join-Path $root ('ref\diff\' + $rel.Replace('\', '.') + '.cs.diff')
if (-not (Test-Path $diffFile)) { Write-Host "no diff: $diffFile"; exit 1 }
$mfile = Join-Path $env:TEMP ('btc-diff\p1\m\' + $rel + '.cs')
$ml = if (Test-Path $mfile) { [IO.File]::ReadAllLines($mfile) } else { @() }
$declRe = New-Object Text.RegularExpressions.Regex '^\t{1,3}(?:public|private|protected|internal)[^=;]*\)\s*$|^\t{1,3}(?:public|private|protected|internal)[^=;(]*\s(?:get|set)\b', 'Compiled'
$modRe = New-Object Text.RegularExpressions.Regex '\b(Btc[A-Z]\w*|ResourcePackManager|CameraBookmarkHotkey|JobSelectHotkeys)\b'
$hunks = New-Object Collections.Generic.List[object]; $cur = $null
foreach ($l in [IO.File]::ReadAllLines($diffFile)) {
    if ($l -like '@@*') { if ($cur) { $hunks.Add($cur) }; $cur = @{ H = $l; B = New-Object Collections.Generic.List[string] } }
    elseif ($cur) { $cur.B.Add($l) }
}
if ($cur) { $hunks.Add($cur) }
$n = 0; $shown = 0
foreach ($h in $hunks) {
    $n++
    $isMod = @($h.B | Where-Object { $_ -match '^[+-]' -and $modRe.IsMatch($_) }).Count -gt 0
    if (-not $All -and -not $isMod) { continue }
    $ln = 1; if ($h.H -match '\+(\d+)') { $ln = [int]$Matches[1] }
    $meth = '(top)'
    for ($i = [Math]::Min($ln + 3, $ml.Length) - 1; $i -ge 0; $i--) { if ($declRe.IsMatch($ml[$i])) { $meth = $ml[$i].Trim(); break } }
    "### hunk $n  member: $meth"
    $h.B | Select-Object -First 70 | ForEach-Object { if ($_.Length -gt 170) { $_.Substring(0, 170) } else { $_ } }
    $shown++
}
"--- $shown of $($hunks.Count) hunks shown" + $(if (-not $All) { ' (use -All for the rest, mostly decompiler noise)' } else { '' })
