param([string]$Dir = 'ref\vanilla-clean')
# Checks whether a decompiled DLL folder is CLEAN vanilla: looks for foreign-mod markers and compares its type list with ref/mod-old
# (the legacy mod was built on clean vanilla, so clean vanilla types ⊆ mod-old types + a few version additions).
# Prints <= 25 lines. Run after scripts/redecompile.ps1.
$root = Split-Path -Parent $PSScriptRoot
$d = Join-Path $root $Dir; $m = Join-Path $root 'ref\mod-old'
if (-not (Test-Path $d)) { "missing $d (run scripts/redecompile.ps1 first)"; exit 1 }
$marks = 'Harpoon', 'Harpooner', 'SprintTracker', 'HarpoonMode'
$hits = @()
foreach ($t in $marks) { $f = Get-ChildItem $d -Recurse -Filter "*$t*.cs" | Select-Object -First 3; if ($f) { $hits += "marker file: $t -> $($f[0].Name)" } }
$txt = Select-String -Path (Join-Path $d '*.cs') -Pattern 'HarpoonMode|SprintTracker|HarpoonFlag' -List -ErrorAction SilentlyContinue | Select-Object -First 5
foreach ($x in $txt) { $hits += "marker text in $($x.Filename)" }
if ($hits.Count) { 'DIRTY: foreign-mod markers found'; $hits | Select-Object -First 8 } else { 'CLEAN: no harpoon/sprint markers' }
function Names($base) { $s = @{}; Get-ChildItem $base -Recurse -File -Filter *.cs | ForEach-Object { $s[$_.FullName.Substring($base.Length).TrimStart('\')] = 1 }; return $s }
$a = Names $d; $b = Names $m
$onlyClean = @($a.Keys | Where-Object { -not $b.ContainsKey($_) -and $_ -notmatch '^Properties' } | Sort-Object)
$onlyMod = @($b.Keys | Where-Object { -not $a.ContainsKey($_) -and $_ -notmatch '^Btc|^ResourcePackManager|CameraBookmarkHotkey|JobSelectHotkeys|__0' } | Sort-Object)
"files only in clean (newer game or foreign): $($onlyClean.Count)"; $onlyClean | Select-Object -First 8 | ForEach-Object { '  ' + $_ }
"files only in mod-old (excluding Btc*/RPM): $($onlyMod.Count)"; $onlyMod | Select-Object -First 8 | ForEach-Object { '  ' + $_ }
