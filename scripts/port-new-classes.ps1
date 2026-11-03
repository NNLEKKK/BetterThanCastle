param([switch]$Force)
$marker = Join-Path (Split-Path -Parent $PSScriptRoot) 'src\BetterThanCastle\.ported'
if ((Test-Path $marker) -and -not $Force) { Write-Host 'Already ported: src/ was hand-edited after generation. Use -Force to regenerate (overwrites Net/Replay/UI/Features/ResourcePacks).'; exit 0 }
# Copies mod-only files from ref/mod-old into src/BetterThanCastle (wave 1): strips dnSpy Token comments,
# wraps global-namespace files in `namespace BetterThanCastle`, splits ResourcePackManager into partial files (~1500 lines each).
# Skips dead *__0 duplicates (docs/gen/added-types.md note "dead"). Idempotent: overwrites generated files in the target folders.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$mod = Join-Path $root 'ref\mod-old'
$van = Join-Path $root 'ref\vanilla'
$dst = Join-Path $root 'src\BetterThanCastle'
$tokenRe = New-Object Text.RegularExpressions.Regex '^\s*//.*Token: 0x', 'Compiled'

$dead = @{}
foreach ($l in [IO.File]::ReadAllLines((Join-Path $root 'docs\gen\added-types.md'))) {
    if ($l -match '^\| (\S+) \| (\S+\.cs) \|.*\| dead \|') { $dead[$Matches[2]] = 1 }
}
$dead['Brix/Input/PickerOrderGiver__0.cs'] = 1   # dnSpy duplicate of PickerOrderGiver (vanilla file, not an added type)

$folderOf = @{
    Net = 'BtcAnimBuf|BtcAnimSync|BtcKick|BtcLobby|BtcNetDiag|BtcNetId|BtcGameplay$|BtcWorld|BtcGc'
    Replay = 'BtcReplay\w*|BtcShareUI|BtcSaveFormat|BtcLzma|BtcPng'
    UI = 'BtcGameplayUI|BtcHotkeysUI|BtcOverlays|BtcPlate\w*|BtcAvatar|BtcNick|BtcLang|BtcDiscord'
}
function FolderFor([string]$cls, [string]$rel) {
    if ($rel -like 'Brix\*') { return 'Features\Input' }
    foreach ($k in $folderOf.Keys) { if ($cls -match ('^(' + $folderOf[$k] + ')$')) { return $k } }
    return 'Features'
}
function Clean([string]$path) {
    $res = New-Object Collections.Generic.List[string]
    foreach ($l in [IO.File]::ReadAllLines($path)) {
        if ($tokenRe.IsMatch($l)) { continue }
        $l = $l.TrimEnd()
        # dnSpy artifact: string-switch hash helper -> BtcStr.ComputeStringHash
        $l = [regex]::Replace($l, '<PrivateImplementationDetails>[^.]*\.ComputeStringHash\(', 'BtcStr.ComputeStringHash(')
        # members the legacy mod ADDED to vanilla types -> plugin shims (see Net/BtcBc.cs, Net/BtcAnimShim.cs)
        $l = [regex]::Replace($l, '\bUNetManager\.(Bc[A-Z]\w*)', 'BtcBc.$1')
        $l = [regex]::Replace($l, '(\w+)\.BtcIsLoadingLevel\b', 'BtcBc.IsLoadingLevel($1)')
        if ($path -like '*BtcAnimSync.cs') {
            $l = [regex]::Replace($l, '\ba\.buf\s*=(?!=)\s*(.+);', 'BtcAnimShim.SetBuf(a, $1);')
            $l = [regex]::Replace($l, '\ba\.buf\b', 'BtcAnimShim.GetBuf(a)')
        }
        $res.Add($l)
    }
    return ,$res
}
function WriteFile([string]$p, $lines) {
    New-Item -ItemType Directory -Force (Split-Path $p) | Out-Null
    [IO.File]::WriteAllLines($p, $lines, (New-Object Text.UTF8Encoding($true)))
}
function Wrap($lines) {
    $i = 0
    while ($i -lt $lines.Count -and ($lines[$i] -match '^using ' -or $lines[$i].Trim() -eq '')) { $i++ }
    $out = New-Object Collections.Generic.List[string]
    for ($k = 0; $k -lt $i; $k++) { $out.Add($lines[$k]) }
    $out.Add('namespace BetterThanCastle')
    $out.Add('{')
    for ($k = $i; $k -lt $lines.Count; $k++) { $out.Add($lines[$k]) }
    $out.Add('}')
    return ,$out
}

# clean previous generated output
foreach ($d in 'Net', 'Replay', 'UI', 'Features', 'ResourcePacks') { $p = Join-Path $dst $d; if (Test-Path $p) { Remove-Item $p -Recurse -Force } }

$count = 0; $skipped = 0
foreach ($f in Get-ChildItem $mod -Recurse -File -Filter *.cs) {
    $rel = $f.FullName.Substring($mod.Length).TrimStart('\')
    if (Test-Path -LiteralPath (Join-Path $van $rel)) { continue }          # existing vanilla file
    $relSlash = $rel.Replace('\', '/')
    if ($dead.ContainsKey($relSlash) -or $dead.ContainsKey($f.Name)) { $skipped++; continue }
    $cls = [IO.Path]::GetFileNameWithoutExtension($f.Name)
    $lines = Clean $f.FullName
    if ($cls -eq 'ResourcePackManager') { continue }                          # handled below
    $hasNs = @($lines | Where-Object { $_ -match '^namespace ' }).Count -gt 0
    if ($hasNs) {
        # file lives in a vanilla namespace (Brix.Input): UnityEngine.Input must be explicit, mod classes need the using
        $fixed = New-Object Collections.Generic.List[string]
        foreach ($l in $lines) {
            $l = [regex]::Replace($l, '(?<![\w.])Input\.(?=[A-Za-z])', 'UnityEngine.Input.')
            $fixed.Add($l)
            if ($l -eq 'using UnityEngine;') { $fixed.Add('using BetterThanCastle;') }
        }
        $lines = $fixed
    }
    $out = if ($hasNs) { $lines } else { Wrap $lines }
    WriteFile (Join-Path $dst ((FolderFor $cls $rel) + '\' + $f.Name)) $out
    $count++
}

# ---- ResourcePackManager -> partial chunks ---------------------------------------------------
$r = Clean (Join-Path $mod 'ResourcePackManager.cs')
$usings = @($r | Where-Object { $_ -match '^using ' })
$d = 0; while ($r[$d] -notmatch '^public (sealed |static )?class ResourcePackManager') { $d++ }
$decl = $r[$d] -replace '^public (sealed |static )?class', 'public partial class'
$last = $r.Count - 1; while ($r[$last].Trim() -eq '') { $last-- }          # final "}"
$body = $r.GetRange($d + 2, $last - ($d + 2))                               # between "{" and final "}"
$chunks = New-Object Collections.Generic.List[object]
$cur = New-Object Collections.Generic.List[string]
foreach ($l in $body) {
    $cur.Add($l)
    if ($l -eq "`t}" -and $cur.Count -ge 1500) { $chunks.Add($cur); $cur = New-Object Collections.Generic.List[string] }
}
if ($cur.Count) { $chunks.Add($cur) }
$n = 0
foreach ($c in $chunks) {
    $n++
    $out = New-Object Collections.Generic.List[string]
    $usings | ForEach-Object { $out.Add($_) }
    $out.Add(''); $out.Add('namespace BetterThanCastle'); $out.Add('{'); $out.Add($decl); $out.Add('{')
    $out.AddRange($c)
    $out.Add('}'); $out.Add('}')
    WriteFile (Join-Path $dst ('ResourcePacks\ResourcePackManager.C{0:D2}.cs' -f $n)) $out
}
Write-Host "copied=$count skipped-dead=$skipped rpm-chunks=$n"

& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "post-port-fixes.ps1")
