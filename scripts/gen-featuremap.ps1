# Builds docs/feature-map.md (+ docs/gen/huge/ResourcePackManager.md, docs/gen/mod-deps.md) from ref/diff + ref/mod-old. Run gen-diff.ps1 first.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$mod = Join-Path $root 'ref\mod-old'
$van = Join-Path $root 'ref\vanilla'
$diffDir = Join-Path $root 'ref\diff'
$scr = Join-Path $env:TEMP 'btc-diff\p1\m'
if (-not (Test-Path $scr)) { throw 'run scripts/gen-diff.ps1 first' }
New-Item -ItemType Directory -Force (Join-Path $root 'docs\gen\huge') | Out-Null

$btcRe = New-Object Text.RegularExpressions.Regex '\b(Btc[A-Z]\w*|ResourcePackManager|CameraBookmarkHotkey|JobSelectHotkeys)\b', 'Compiled'
$yesRe = New-Object Text.RegularExpressions.Regex 'MessageBase|NetworkWriter|NetworkReader|MsgType|RegisterHandler|SyncVar|OnSerialize|OnDeserialize|\bCmd[A-Z]\w*|\bRpc[A-Z]\w*|InvokeCmd|InvokeRpc|SendToAll|SendByChannel|SendUnreliable|BetterConnection|\.Send\(', 'Compiled'
$weakRe = New-Object Text.RegularExpressions.Regex 'NetworkServer|NetworkClient|NetworkConnection|isServer|isClient|NetworkManager|UNetManager|Lobby|[Vv]ersion|Steam', 'Compiled'
$fileRe = New-Object Text.RegularExpressions.Regex 'BinaryWriter|BinaryReader|File\.(Read|Write|Open)|FileStream|Serialize', 'Compiled'
$luaRe = New-Object Text.RegularExpressions.Regex 'DynValue|MoonSharp|ScriptExecutionContext|LuaMenu|\.Call\(|DoString', 'Compiled'
$declRe = New-Object Text.RegularExpressions.Regex '^\t{1,3}(?:public|private|protected|internal)[^=;]*\)\s*$|^\t{1,3}(?:public|private|protected|internal)[^=;(]*\s(?:get|set)\b', 'Compiled'
$netFileRe = '^(Brix/Game/Network/|Brix/Network/|UNetAnimator|AnimatorParameter)'

function NetTier([string]$text) {
    if ($yesRe.IsMatch($text)) { return 'yes' }
    if ($weakRe.IsMatch($text)) { return '?' }
    if ($fileRe.IsMatch($text)) { return 'file' }
    return 'no'
}

# ---- added files -> features ---------------------------------------------------------------------
$added = @()
foreach ($f in Get-ChildItem $mod -Recurse -File -Filter *.cs) {
    $rel = $f.FullName.Substring($mod.Length).TrimStart('\')
    if (-not (Test-Path -LiteralPath (Join-Path $van $rel))) { $added += $f }
}
$modText = @{}
foreach ($f in Get-ChildItem $mod -Recurse -File -Filter *.cs) { $modText[$f.FullName.Substring($mod.Length).TrimStart('\')] = [IO.File]::ReadAllText($f.FullName) }

# diff files -> referenced mod classes, hunks
$diffInfo = @{}
foreach ($d in Get-ChildItem $diffDir -File -Filter *.diff) {
    $lines = [IO.File]::ReadAllLines($d.FullName)
    $refs = @{}
    foreach ($l in $lines) { if ($l -match '^\+[^+]') { foreach ($m in $btcRe.Matches($l)) { $refs[$m.Value] = 1 } } }
    $diffInfo[$d.Name.Substring(0, $d.Name.Length - 5)] = @{ Lines = $lines; Refs = @($refs.Keys) }
}
function RelFromDiffName([string]$n) { return $n }

$classRows = New-Object Collections.Generic.List[string]
$depsOut = New-Object Text.StringBuilder
[void]$depsOut.AppendLine('# Mod class -> vanilla types it references (generated; names only). Use to know what vanilla surface each ported class needs.')
[void]$depsOut.AppendLine('')
$vanNames = @{}
foreach ($f in Get-ChildItem $van -Recurse -File -Filter *.cs) { $vanNames[[IO.Path]::GetFileNameWithoutExtension($f.Name)] = 1 }

$id = 0
foreach ($f in ($added | Sort-Object Name)) {
    $rel = $f.FullName.Substring($mod.Length).TrimStart('\')
    $cls = [IO.Path]::GetFileNameWithoutExtension($f.Name)
    $text = $modText[$rel]
    $hooks = @($diffInfo.Keys | Where-Object { $diffInfo[$_].Refs -contains $cls } | Sort-Object)
    $extRefs = 0
    foreach ($k in $modText.Keys) { if ($k -ne $rel -and $modText[$k].Contains($cls)) { $extRefs++ } }
    if ($cls -match '__\d+$' -and $extRefs -eq 0) { continue }   # dead dnSpy duplicate
    $id++
    $fid = 'F{0:D2}' -f $id
    $size = $text.Length; $lc = ($text -split "`n").Count
    $net = NetTier $text
    if ($net -ne 'yes' -and ($hooks | Where-Object { $_ -match $netFileRe.Replace('/', '\.') })) { $net = 'yes' }
    if ($cls -match 'SaveFormat|Replay|Png|Lzma' -and $net -ne 'yes') { $net = 'file' }
    $lua = if ($luaRe.IsMatch($text)) { 'maybe' } else { 'no' }
    $dups = if ($cls -match '__\d+$') { ' (dup of ' + ($cls -replace '__\d+$', '') + '?)' } else { '' }
    if ($cls -eq 'ResourcePackManager') { $strategy = 'plain code, partial split by area (docs/gen/huge/ResourcePackManager.md); boot via hook'; $model = 'opus design -> sonnet chunks' }
    elseif ($net -eq 'yes') { $strategy = "plain code + $($hooks.Count) hook file(s), gate on BtcNetworkGate"; $model = 'opus' }
    elseif ($lc -gt 1500) { $strategy = "plain code (split) + $($hooks.Count) hook file(s)"; $model = 'opus design -> sonnet' }
    else { $strategy = "plain code + $($hooks.Count) hook file(s)"; $model = 'sonnet' }
    $hookStr = if ($hooks.Count) { (($hooks | ForEach-Object { $_.Replace('.cs', '') }) -join ', ') } else { '-' }
    $classRows.Add("| $fid | $cls$dups ($lc lines) | $(if($hooks.Count){$hooks.Count.ToString()+' hook files'}else{'none'}) | $hookStr | $net | $lua | $strategy | $model | TODO |")
    # deps
    $idents = @{}
    foreach ($m in [regex]::Matches($text, '\b[A-Z][A-Za-z0-9_]+\b')) { if ($vanNames.ContainsKey($m.Value)) { $idents[$m.Value] = 1 } }
    $names = ($idents.Keys | Sort-Object | Select-Object -First 60) -join ', '
    [void]$depsOut.AppendLine("- **$cls** ($($idents.Count)): $names")
}
[IO.File]::WriteAllText((Join-Path $root 'docs\gen\mod-deps.md'), $depsOut.ToString())

# ---- inline (non-Btc) diff files ---------------------------------------------------------------------
$inlineRows = New-Object Collections.Generic.List[string]
$bigHunk = @('Brix.Network.UNetManager.cs', 'UIGameObserver.cs', 'Brix.Game.Network.UNetProjectCmd.cs')
$hunkRows = @{}
$n = 0
foreach ($key in ($diffInfo.Keys | Sort-Object)) {
    $info = $diffInfo[$key]
    $lines = $info.Lines
    $mlines = $null
    $mp = Join-Path $scr ($key.Replace('.cs', '').Replace('.', '\') + '.cs')
    # path mapping: diff name uses '.' for '\'; recover real relative path from ref/mod-old
    $relPath = $null
    foreach ($cand in @($key)) {
        $guess = Get-ChildItem $mod -Recurse -File -Filter ($key.Split('.')[-2] + '.cs') | Where-Object { $_.FullName.Substring($mod.Length).TrimStart('\').Replace('\', '.') -eq $key } | Select-Object -First 1
        if ($guess) { $relPath = $guess.FullName.Substring($mod.Length).TrimStart('\') }
    }
    $hunks = New-Object Collections.Generic.List[object]
    $cur = $null
    foreach ($l in $lines) {
        if ($l -like '@@*') {
            if ($cur) { $hunks.Add($cur) }
            $cur = @{ Head = $l; Body = New-Object Collections.Generic.List[string] }
        } elseif ($cur) { $cur.Body.Add($l) }
    }
    if ($cur) { $hunks.Add($cur) }
    $allBody = ($hunks | ForEach-Object { ($_.Body | Where-Object { $_ -match '^[+-][^+-]' }) -join "`n" }) -join "`n"
    $netFile = NetTier $allBody
    if ($key -match $netFileRe.Replace('/', '\.')) { $netFile = 'yes' }
    if ($info.Refs.Count -eq 0) {
        $n++
        $inlineRows.Add("| I{0:D2} | inline logic in $($key.Replace('.cs','')) ($($hunks.Count) hunks) | $($key.Replace('.cs','')) | $key | $netFile | $(if($luaRe.IsMatch($allBody)){'maybe'}else{'no'}) | review diff: real change or version drift? then Postfix/Transpiler | sonnet (opus if Transpiler) | TODO |" -f $n)
    }
    if ($bigHunk -contains $key -and $relPath) {
        $mfile = Join-Path $scr $relPath
        $ml = [IO.File]::ReadAllLines($mfile)
        $rows = New-Object Collections.Generic.List[string]
        $hn = 0
        foreach ($h in $hunks) {
            $hn++
            $m = [regex]::Match($h.Head, '\+(\d+)(?:,(\d+))?')
            $ln = [int]$m.Groups[1].Value
            $meth = '(top)'
            for ($i = [Math]::Min($ln + 3, $ml.Length) - 1; $i -ge 0; $i--) {
                if ($declRe.IsMatch($ml[$i])) { $meth = ($ml[$i].Trim() -replace '\s+', ' '); if ($meth.Length -gt 60) { $meth = $meth.Substring(0, 60) }; break }
            }
            $chg = ($h.Body | Where-Object { $_ -match '^[+-][^+-]' }) -join "`n"
            $bt = @{}; foreach ($x in $btcRe.Matches($chg)) { $bt[$x.Value] = 1 }
            $hnet = NetTier $chg
            $add = @($h.Body | Where-Object { $_ -match '^\+[^+]' }).Count; $del = @($h.Body | Where-Object { $_ -match '^-[^-]' }).Count
            $strat = if ($bt.Count) { 'hook -> ' + (($bt.Keys | Sort-Object) -join ',') } else { 'inline: review' }
            $rows.Add("| $($key.Replace('.cs',''))-h$('{0:D2}' -f $hn) | $meth | +$ln (+$add/-$del) | $strat | $hnet | sonnet | TODO |")
        }
        $hunkRows[$key] = $rows
    }
}

# ---- ResourcePackManager structure map --------------------------------------------------------------------
$rpm = [IO.File]::ReadAllLines((Join-Path $mod 'ResourcePackManager.cs'))
$sections = New-Object Collections.Generic.List[object]
$nested = $null; $meth = $null
for ($i = 0; $i -lt $rpm.Length; $i++) {
    $l = $rpm[$i]
    if ($l -match '^\t(?:\[[^\]]*\]\s*)*(?:(?:public|private|protected|internal|static|sealed|abstract)\s+)*(class|struct|enum|interface)\s+(\w+)') { $nested = @{ N = $Matches[2]; S = $i + 1; E = 0; M = 0 }; $sections.Add($nested) }
    elseif ($nested -and $l -eq "`t}" -and $nested.E -eq 0) { $nested.E = $i + 1 }
    elseif ($l -match '^\t(?:\[[^\]]*\]\s*)*(?:(?:public|private|protected|internal|static|override|virtual)\s+)+[^=;(]+\([^;]*$|^\t(?:(?:public|private|protected|internal|static|override|virtual)\s+)+[^=;(]+\(.*\)\s*$') { if ($nested -and $nested.E -eq 0) { $nested.M++ } }
}
$methods = New-Object Collections.Generic.List[object]
for ($i = 0; $i -lt $rpm.Length; $i++) {
    if ($rpm[$i] -match '^\t(?:(?:public|private|protected|internal|static|override|virtual)\s+)+[^=;(]*?(\w+)\(.*\)\s*$' -and $i + 1 -lt $rpm.Length -and $rpm[$i + 1] -eq "`t{") {
        $name = $Matches[1]; $start = $i + 1
        $j = $i + 2; while ($j -lt $rpm.Length -and $rpm[$j] -ne "`t}") { $j++ }
        $methods.Add(@{ N = $name; S = $start; E = $j + 1; L = $j + 1 - $start })
        $i = $j
    }
}
$mb = New-Object Text.StringBuilder
[void]$mb.AppendLine('# ResourcePackManager (new class, port as plain code in partial files). Generated by scripts/gen-featuremap.ps1')
[void]$mb.AppendLine("Total lines: $($rpm.Length). Top-level methods (indent 1): $($methods.Count). Nested types: $($sections.Count)")
[void]$mb.AppendLine('')
[void]$mb.AppendLine('## Nested types')
[void]$mb.AppendLine('| type | lines | size |')
[void]$mb.AppendLine('|---|---|---|')
foreach ($s in $sections) { [void]$mb.AppendLine("| $($s.N) | $($s.S)-$($s.E) | $($s.E - $s.S + 1) |") }
[void]$mb.AppendLine('')
[void]$mb.AppendLine('## Proposed partial chunks (~1500 lines, cut at method boundaries; first chunk = fields/boot skeleton)')
[void]$mb.AppendLine('| chunk | lines | methods (first/last) | net-ish hits | IO hits |')
[void]$mb.AppendLine('|---|---|---|---|---|')
$chunk = 1; $cs = 1; $firstM = $null; $lastM = $null
foreach ($m in $methods) {
    if ($null -eq $firstM) { $firstM = $m.N }
    $lastM = $m.N
    if ($m.E - $cs -ge 1500) {
        $seg = ($rpm[($cs - 1)..($m.E - 1)]) -join "`n"
        [void]$mb.AppendLine("| C$('{0:D2}' -f $chunk) | $cs-$($m.E) | $firstM .. $lastM | $([regex]::Matches($seg,'Network|UNet|Lobby').Count) | $([regex]::Matches($seg,'File\.|Directory\.|Path\.|Application\.').Count) |")
        $chunk++; $cs = $m.E + 1; $firstM = $null
    }
}
if ($cs -le $rpm.Length) {
    $seg = ($rpm[($cs - 1)..($rpm.Length - 1)]) -join "`n"
    [void]$mb.AppendLine("| C$('{0:D2}' -f $chunk) | $cs-$($rpm.Length) | $firstM .. $lastM | $([regex]::Matches($seg,'Network|UNet|Lobby').Count) | $([regex]::Matches($seg,'File\.|Directory\.|Path\.|Application\.').Count) |")
}
[void]$mb.AppendLine('')
[void]$mb.AppendLine('## Methods > 150 lines')
foreach ($m in ($methods | Where-Object { $_.L -gt 150 } | Sort-Object { - $_.L })) { [void]$mb.AppendLine("- $($m.N): $($m.S)-$($m.E) ($($m.L) lines)") }
[IO.File]::WriteAllText((Join-Path $root 'docs\gen\huge\ResourcePackManager.md'), $mb.ToString())

# ---- write feature-map.md ---------------------------------------------------------------------------------------
$fm = New-Object Text.StringBuilder
[void]$fm.AppendLine('# Feature map (status: TODO / IN-PROGRESS / BUILT / VERIFIED / BLOCKED). Generated by scripts/gen-featuremap.ps1; edit statuses by hand.')
[void]$fm.AppendLine('net: yes = touches wire/serialization (gate on BtcNetworkGate); ? = mentions network types, review; file = file formats only; no. All heuristic: review `?`/`yes` before porting.')
[void]$fm.AppendLine('Hook files are listed in docs/gen/diff-summary.md; per-hunk detail in ref/diff/<File>.diff. Dead dnSpy duplicates (*__0 with no refs) are skipped: see docs/gen/added-types.md.')
[void]$fm.AppendLine('')
[void]$fm.AppendLine('## A. New mod classes (plain code, no Harmony) + the vanilla hooks that call them')
[void]$fm.AppendLine('| id | feature | vanilla types touched | diff files | net | lua | strategy | model | status |')
[void]$fm.AppendLine('|---|---|---|---|---|---|---|---|---|')
foreach ($r in $classRows) { [void]$fm.AppendLine($r) }
[void]$fm.AppendLine('')
[void]$fm.AppendLine('## B. Changed vanilla files without any Btc*/ResourcePackManager reference (inline edits or game-version drift)')
[void]$fm.AppendLine('| id | feature | vanilla types touched | diff files | net | lua | strategy | model | status |')
[void]$fm.AppendLine('|---|---|---|---|---|---|---|---|---|')
foreach ($r in $inlineRows) { [void]$fm.AppendLine($r) }
foreach ($k in $bigHunk) {
    [void]$fm.AppendLine('')
    [void]$fm.AppendLine("## C. Hunks of $k (one sub-row per hunk; port one hunk per commit)")
    [void]$fm.AppendLine('| id | method | mod line (+/-) | strategy | net | model | status |')
    [void]$fm.AppendLine('|---|---|---|---|---|---|---|')
    foreach ($r in $hunkRows[$k]) { [void]$fm.AppendLine($r) }
}
[IO.File]::WriteAllText((Join-Path $root 'docs\gen\feature-map-gen.md'), $fm.ToString())
Write-Host "features=$($classRows.Count) inline=$($inlineRows.Count) rpm-methods=$($methods.Count) nested=$($sections.Count) unetmgr-hunks=$($hunkRows['Brix.Network.UNetManager.cs'].Count) uigo-hunks=$($hunkRows['UIGameObserver.cs'].Count)"
