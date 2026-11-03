# Generates ref/diff/*.diff (vanilla -> mod-old, decompiler noise stripped), _added-types.md, _members.md, _summary.md.
# Copies the three summaries to docs/ (ref/ is not committed). No code is read by eye.
param([string]$Vanilla = 'ref\vanilla')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$van = Join-Path $root $Vanilla
$mod = Join-Path $root 'ref\mod-old'
$out = Join-Path $root 'ref\diff'
$scr = Join-Path $env:TEMP 'btc-diff'
if (Test-Path $scr) { Remove-Item $scr -Recurse -Force }
New-Item -ItemType Directory -Force $scr, $out | Out-Null
Get-ChildItem $out -File -ErrorAction SilentlyContinue | Remove-Item -Force

$tokenRe = New-Object Text.RegularExpressions.Regex '^\s*//.*Token: 0x', 'Compiled'
$noiseRe = New-Object Text.RegularExpressions.Regex '\b(num|flag|text|obj|array|list|vector|result|item|enumerator|collection|component|gameObject|transform|position|rotation|key|value|name|index|count|length|stringBuilder|dictionary|bounds|ray|string|int|float|bool|byte|array|ptr|type|time|dist|size|data|keyValuePair|current|flag)\d+\b|<>c__DisplayClass\d+_\d+|<>f__am\$cache\w+|<>f__mg\$cache\w+|CS\$<>8__locals\d+|CS\$\d\$\w+', 'Compiled'
$netRe = New-Object Text.RegularExpressions.Regex 'NetworkWriter|NetworkReader|MessageBase|MsgType|RegisterHandler|NetworkServer|NetworkClient|NetworkConnection|\bCmd[A-Z]\w*|\bRpc[A-Z]\w*|SyncVar|OnSerialize|OnDeserialize|InvokeCmd|InvokeRpc|SendToAll|SendByChannel|\b[Vv]ersion|protocol|Lobby|BetterConnection|BinaryWriter|BinaryReader', 'Compiled'
$memberRe = New-Object Text.RegularExpressions.Regex '^(\t{1,2})(?:\[[^\]]*\]\s*)*(public|private|protected|internal)\b.*$', 'Compiled'
$declRe = New-Object Text.RegularExpressions.Regex '^\t{1,3}(?:public|private|protected|internal)[^=;]*\)\s*$|^\t{1,3}(?:public|private|protected|internal)[^=;(]*\s(?:get|set)\b', 'Compiled'

$tmpDeclRe = New-Object Text.RegularExpressions.Regex '^(\s*)[\w<>\[\],\.\?]+\s+((?:flag|num|text|obj|array|result|list|vector|item)\d*)\s*=\s*(.+);$', 'Compiled'
# ---- dnSpy "debug-mode" canonicalisation: inline bool flagN temporaries and their negation chains ----
function StripParens([string]$e) {
    $e = $e.Trim()
    while ($e.Length -ge 2 -and $e[0] -eq '(' -and $e[$e.Length - 1] -eq ')') {
        $d = 0; $ok = $true
        for ($k = 0; $k -lt $e.Length - 1; $k++) { if ($e[$k] -eq '(') { $d++ } elseif ($e[$k] -eq ')') { $d--; if ($d -eq 0) { $ok = $false; break } } }
        if ($ok) { $e = $e.Substring(1, $e.Length - 2).Trim() } else { break }
    }
    return $e
}
function Neg([string]$e) {
    $e = StripParens $e
    if ($e -match '^![\w\.\[\]]+$') { return $e.Substring(1) }
    if ($e.StartsWith('!(') -and (StripParens $e.Substring(1)) -ne $e.Substring(1)) { return StripParens $e.Substring(1) }
    if ($e -notmatch ' && | \|\| |\?') {
        if ($e -match '^([^=!<>]+?) == ([^=!<>]+)$') { return $Matches[1] + ' != ' + $Matches[2] }
        if ($e -match '^([^=!<>]+?) != ([^=!<>]+)$') { return $Matches[1] + ' == ' + $Matches[2] }
    }
    if ($e -match '^[\w\.\[\]]+(\([^()]*\))?$') { return '!' + $e }
    return '!(' + $e + ')'
}
function Wrap1([string]$e) { if ($e -match '^[\w\.\[\]]+(\([^()]*\))?$') { return $e } else { return '(' + $e + ')' } }
function Canon($lines) {
    $reassigned = @{}
    foreach ($l in $lines) { if ($l -match '^\s*(flag\d*) = ') { $reassigned[$Matches[1]] = 1 } }
    $map = @{}
    $out = New-Object Collections.Generic.List[string]
    foreach ($l in $lines) {
        if ($l -match '^\s*bool (flag\d*) = (.+);$' -and -not $reassigned.ContainsKey($Matches[1])) {
            $name = $Matches[1]; $e = $Matches[2].Trim()
            if ($e -match '^!(flag\d*)$' -and $map.ContainsKey($Matches[1])) { $e = Neg $map[$Matches[1]] }
            elseif ($e -match '^(flag\d*)$' -and $map.ContainsKey($Matches[1])) { $e = $map[$Matches[1]] }
            else { $e = [regex]::Replace($e, '!?\bflag\d*\b', { param($m) $t = $m.Value; $neg = $t.StartsWith('!'); $k = $t.TrimStart('!'); if ($map.ContainsKey($k)) { if ($neg) { Wrap1 (Neg $map[$k]) } else { Wrap1 $map[$k] } } else { $t } }) }
            $map[$name] = StripParens $e
            continue
        }
        if ($l -match '\bflag\d*\b') {
            $l = [regex]::Replace($l, '!?\bflag\d*\b', { param($m) $t = $m.Value; $neg = $t.StartsWith('!'); $k = $t.TrimStart('!'); if ($map.ContainsKey($k) -and -not $reassigned.ContainsKey($k)) { if ($neg) { Wrap1 (Neg $map[$k]) } else { Wrap1 $map[$k] } } else { $t } })
        }
        if ($l -match '^(\s*(?:if|while|else if)) \((.*)\)$') { $l = $Matches[1] + ' (' + (StripParens $Matches[2]) + ')' }
        elseif ($l -match '^(\s*return) \((.*)\);$') { $l = $Matches[1] + ' ' + (StripParens $Matches[2]) + ';' }
        $out.Add($l)
    }
    return ,$out
}
function Norm([string]$p, [bool]$deep) {
    $res = New-Object Collections.Generic.List[string]
    foreach ($l in [IO.File]::ReadAllLines($p)) {
        if ($tokenRe.IsMatch($l)) { continue }
        $res.Add($l.TrimEnd())
    }
    if ($deep) {
        $res = Canon $res
        # Inline decompiler temporaries: "bool flag = EXPR; if (flag)" -> "if (EXPR)", "int num = EXPR; return num;" -> "return EXPR;"
        $o = New-Object Collections.Generic.List[string]
        for ($i = 0; $i -lt $res.Count; $i++) {
            $m = $tmpDeclRe.Match($res[$i])
            if ($m.Success -and $i + 1 -lt $res.Count) {
                $v = $m.Groups[2].Value; $e = $m.Groups[3].Value; $n = $res[$i + 1]
                if (([regex]::Matches($n, "\b$v\b")).Count -eq 1) {
                    $n2 = $n.Replace("(!$v)", "(!($e))").Replace("($v)", "($e)").Replace("return $v;", "return $e;")
                    if ($n2 -ne $n) { $o.Add($n2); $i++; continue }
                }
            }
            $o.Add($res[$i])
        }
        $res = New-Object Collections.Generic.List[string]
        foreach ($l in $o) { $res.Add($noiseRe.Replace($l, '$1#')) }
    }
    return $res
}
function Rel([string]$full, [string]$base) { return $full.Substring($base.Length).TrimStart('\') }

# ---- 1. changed files -------------------------------------------------------------------------
$changed = New-Object Collections.Generic.List[string]
$added = New-Object Collections.Generic.List[string]
foreach ($f in Get-ChildItem $mod -Recurse -File -Filter *.cs) {
    $rel = Rel $f.FullName $mod
    $vp = Join-Path $van $rel
    if (-not (Test-Path -LiteralPath $vp)) { $added.Add($rel); continue }
    $a = Norm $vp $false; $b = Norm $f.FullName $false
    if (($a -join "`n") -ne ($b -join "`n")) {
        $changed.Add($rel)
        foreach ($pass in 'p1', 'p2') {
            $deep = ($pass -eq 'p2')
            foreach ($side in @(@('v', $vp), @('m', $f.FullName))) {
                $dst = Join-Path $scr ("$pass\" + $side[0] + '\' + $rel)
                New-Item -ItemType Directory -Force (Split-Path $dst) | Out-Null
                [IO.File]::WriteAllLines($dst, (Norm $side[1] $deep))
            }
        }
    }
}
Write-Host "changed=$($changed.Count) added-files=$($added.Count)"

# ---- 2. diffs, hunks, summary ------------------------------------------------------------------
function Get-Diff([string]$pass, [string]$rel) {
    Push-Location (Join-Path $scr $pass)
    try { $d = & git -c core.safecrlf=false diff --no-index --no-color -U3 -- ("v/" + $rel.Replace('\', '/')) ("m/" + $rel.Replace('\', '/')) 2>$null } finally { Pop-Location }
    return @($d)
}
$rows = New-Object Collections.Generic.List[object]
$memberLines = New-Object Collections.Generic.List[string]
foreach ($rel in $changed) {
    $d1 = Get-Diff 'p1' $rel
    $d2 = Get-Diff 'p2' $rel
    $name = $rel.Replace('\', '.')
    [IO.File]::WriteAllLines((Join-Path $out ($name + '.diff')), $d1)
    $h1 = @($d1 | Where-Object { $_ -like '@@*' }).Count
    $h2 = @($d2 | Where-Object { $_ -like '@@*' }).Count
    $plus = @($d1 | Where-Object { $_ -match '^\+[^+]' }).Count
    $minus = @($d1 | Where-Object { $_ -match '^-[^-]' }).Count
    $net = @($d1 | Where-Object { ($_ -match '^[+-][^+-]') -and $netRe.IsMatch($_) }).Count
    # enclosing methods for real hunks (from pass 2, mapped by mod-side line number)
    $modLines = [IO.File]::ReadAllLines((Join-Path $scr ("p2\m\" + $rel)))
    $methods = New-Object Collections.Generic.List[string]
    foreach ($h in ($d2 | Where-Object { $_ -like '@@*' })) {
        if ($h -match '\+(\d+)') {
            $ln = [int]$Matches[1]
            $m = '(top)'
            for ($i = [Math]::Min($ln, $modLines.Length) - 1; $i -ge 0; $i--) {
                if ($declRe.IsMatch($modLines[$i])) { $m = ($modLines[$i].Trim() -replace '\s+', ' '); if ($m.Length -gt 70) { $m = $m.Substring(0, 70) }; break }
            }
            if (-not $methods.Contains($m)) { $methods.Add($m) }
        }
    }
    foreach ($l in $d1) {
        if ($l -match '^[+-][^+-]' -and $memberRe.IsMatch($l.Substring(1)) -and $l -notmatch '[;]\s*$' -or ($l -match '^[+-]\t{1,2}(public|private|protected|internal)\b.*;\s*$')) {
            $memberLines.Add($name + ' | ' + $l.Substring(0, 1) + ' ' + ($l.Substring(1).Trim() -replace '\s+', ' '))
        }
    }
    $rows.Add([pscustomobject]@{ Rel = $rel; Name = $name; Plus = $plus; Minus = $minus; H1 = $h1; H2 = $h2; Net = $net; Methods = ($methods | Select-Object -First 6) -join '; ' })
}
$sb = New-Object Text.StringBuilder
[void]$sb.AppendLine('# Diff summary (vanilla -> mod-old, dnSpy Token noise stripped). Generated by scripts/gen-diff.ps1')
[void]$sb.AppendLine('real = hunks left after local-variable renaming noise removed; noise = hunks - real; net = changed lines matching networking keywords (heuristic, review!).')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('| type file | +/- | hunks | real | noise | net-hits | enclosing members of real hunks |')
[void]$sb.AppendLine('|---|---|---|---|---|---|---|')
foreach ($r in ($rows | Sort-Object { - $_.Net }, { - ($_.Plus + $_.Minus) })) {
    [void]$sb.AppendLine("| $($r.Rel.Replace('\','/')) | +$($r.Plus)/-$($r.Minus) | $($r.H1) | $($r.H2) | $($r.H1 - $r.H2) | $($r.Net) | $($r.Methods) |")
}
$onlyNoise = @($rows | Where-Object { $_.H2 -eq 0 })
[void]$sb.AppendLine('')
[void]$sb.AppendLine("Files whose hunks all vanish after noise normalisation (likely decompiler noise only): $($onlyNoise.Count)")
[void]$sb.AppendLine(($onlyNoise | ForEach-Object { $_.Rel.Replace('\', '/') }) -join ', ')
[IO.File]::WriteAllText((Join-Path $out '_summary.md'), $sb.ToString())

# ---- 3. members added/removed in existing vanilla types ---------------------------------------
$mb = New-Object Text.StringBuilder
[void]$mb.AppendLine('# Changed member declarations in existing vanilla types (+ = only in mod-old, - = only in vanilla)')
[void]$mb.AppendLine('Added fields / changed visibility / changed signatures show up here. Heuristic; includes decompiler noise.')
[void]$mb.AppendLine('')
foreach ($l in ($memberLines | Select-Object -Unique)) { [void]$mb.AppendLine($l) }
[IO.File]::WriteAllText((Join-Path $out '_members.md'), $mb.ToString())

# ---- 4. types present in mod-old but absent in vanilla ----------------------------------------
$typeRe = New-Object Text.RegularExpressions.Regex '^(\t*)(?:\[[^\]]*\]\s*)*(?:(?:public|internal|private|protected|static|sealed|abstract|partial|unsafe)\s+)*(class|struct|enum|interface)\s+([A-Za-z_]\w*)', 'Compiled'
function Get-Types([string]$base) {
    $set = @{}
    foreach ($f in Get-ChildItem $base -Recurse -File -Filter *.cs) {
        $ns = ''; $stack = New-Object Collections.Generic.List[object]
        foreach ($l in [IO.File]::ReadAllLines($f.FullName)) {
            if ($l -match '^namespace\s+([\w\.]+)') { $ns = $Matches[1]; continue }
            $m = $typeRe.Match($l)
            if ($m.Success) {
                $depth = $m.Groups[1].Length
                while ($stack.Count -and $stack[$stack.Count - 1].D -ge $depth) { $stack.RemoveAt($stack.Count - 1) }
                $full = (@($ns) + @($stack | ForEach-Object { $_.N }) + @($m.Groups[3].Value) | Where-Object { $_ }) -join '.'
                $stack.Add(@{ D = $depth; N = $m.Groups[3].Value })
                if (-not $set.ContainsKey($full)) { $set[$full] = (Rel $f.FullName $base) }
            }
        }
    }
    return $set
}
$tv = Get-Types $van
$tm = Get-Types $mod
$newTypes = @($tm.Keys | Where-Object { -not $tv.ContainsKey($_) } | Sort-Object)
$goneTypes = @($tv.Keys | Where-Object { -not $tm.ContainsKey($_) } | Sort-Object)

# external-reference check: which added top-level names are mentioned outside their own file
$modText = @{}
foreach ($f in Get-ChildItem $mod -Recurse -File -Filter *.cs) { $modText[(Rel $f.FullName $mod)] = [IO.File]::ReadAllText($f.FullName) }
$ab = New-Object Text.StringBuilder
[void]$ab.AppendLine('# Types in mod-old but not in vanilla (port as ordinary plugin code, no Harmony)')
[void]$ab.AppendLine('refs = number of OTHER mod-old files mentioning the name; dead = candidate dnSpy duplicate (no refs, name ends with __N).')
[void]$ab.AppendLine('')
[void]$ab.AppendLine('| type | file | lines | refs | note |')
[void]$ab.AppendLine('|---|---|---|---|---|')
foreach ($t in $newTypes) {
    $file = $tm[$t]
    $short = $t.Split('.')[-1]
    if ($t.Contains('.') -and $tm.ContainsKey($t) -and ($newTypes -contains ($t.Substring(0, $t.LastIndexOf('.'))))) { continue } # nested inside another new type
    $refs = 0
    foreach ($k in $modText.Keys) { if ($k -ne $file -and $modText[$k].Contains($short)) { $refs++ } }
    $lines = ($modText[$file] -split "`n").Count
    $note = ''
    if ($short -match '__\d+$' -and $refs -eq 0) { $note = 'dead' }
    elseif ($short -match '__\d+$') { $note = 'dup?' }
    [void]$ab.AppendLine("| $t | $($file.Replace('\','/')) | $lines | $refs | $note |")
}
[void]$ab.AppendLine('')
[void]$ab.AppendLine("Types present in vanilla but missing in mod-old: $($goneTypes.Count)")
foreach ($g in ($goneTypes | Select-Object -First 80)) { [void]$ab.AppendLine("- $g ($($tv[$g].Replace('\','/')))") }
[IO.File]::WriteAllText((Join-Path $out '_added-types.md'), $ab.ToString())

Copy-Item (Join-Path $out '_summary.md') (Join-Path $root 'docs\gen\diff-summary.md') -Force
Copy-Item (Join-Path $out '_added-types.md') (Join-Path $root 'docs\gen\added-types.md') -Force
Copy-Item (Join-Path $out '_members.md') (Join-Path $root 'docs\gen\changed-members.md') -Force
Write-Host "done: diffs=$($changed.Count) new-types=$($newTypes.Count) gone-types=$($goneTypes.Count) member-lines=$(($memberLines | Select-Object -Unique).Count)"
