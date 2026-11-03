# Fixes for places where the legacy compiler (mcs) accepted code that today's C# compiler rejects (numeric narrowing,
# definite assignment, struct-returning properties). Called at the end of port-new-classes.ps1. Each fix must match exactly once.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root 'src\BetterThanCastle'
$bad = 0
function Fix([string]$file, [string]$old, [string]$new) {
    $p = Join-Path $src $file
    $t = [IO.File]::ReadAllText($p)
    $n = ([regex]::Matches($t, [regex]::Escape($old))).Count
    if ($n -lt 1) { Write-Host "FIX NOT FOUND: $file :: $old"; $script:bad++; return }
    [IO.File]::WriteAllText($p, $t.Replace($old, $new), (New-Object Text.UTF8Encoding($true)))
}
function InitLocal([string]$file, [string]$name) {   # "T name;" -> "T name = default(T);" (first declaration)
    $p = Join-Path $src $file
    $t = [IO.File]::ReadAllText($p)
    $m = [regex]::Match($t, '(?m)^(\s*)([\w\[\]<>,\.\?]+)\s+' + [regex]::Escape($name) + ';\s*$')
    if (-not $m.Success) { Write-Host "INIT NOT FOUND: $file :: $name"; $script:bad++; return }
    $rep = $m.Groups[1].Value + $m.Groups[2].Value + ' ' + $name + ' = default(' + $m.Groups[2].Value + ');'
    [IO.File]::WriteAllText($p, $t.Remove($m.Index, $m.Length).Insert($m.Index, $rep), (New-Object Text.UTF8Encoding($true)))
}

Fix 'Replay\BtcLzma.cs' '(ulong)-16777216' 'unchecked((ulong)-16777216)'
Fix 'Replay\BtcLzma.cs' 'this.Put(maxValue + b);' 'this.Put((byte)(maxValue + b));'
Fix 'Replay\BtcPng.cs' 'array8[9] = (hasAlpha ? 6 : 2);' 'array8[9] = (byte)(hasAlpha ? 6 : 2);'
Fix 'Replay\BtcSaveFormat.cs' 'memoryStream.WriteByte(isMap ? 1 : 0);' 'memoryStream.WriteByte((byte)(isMap ? 1 : 0));'
Fix 'Replay\BtcSaveFormat.cs' 's.WriteByte(((bool)t) ? 2 : 1);' 's.WriteByte((byte)(((bool)t) ? 2 : 1));'
Fix 'Replay\BtcSaveFormat.cs' 'return (long)(v >> 1 ^ -(long)(v & 1UL));' 'return (long)(v >> 1 ^ unchecked((ulong)(-(long)(v & 1UL))));'
Fix 'UI\BtcAvatar.cs' 'memoryStream.WriteByte(b ^ array7[num17]);' 'memoryStream.WriteByte((byte)(b ^ array7[num17]));'
Fix 'UI\BtcAvatar.cs' 'byte b = array[num6 + l * num + m] ^ array5[m];' 'byte b = (byte)(array[num6 + l * num + m] ^ array5[m]);'
Fix 'UI\BtcPlateImg.cs' 'memoryStream.WriteByte(b ^ array7[num17]);' 'memoryStream.WriteByte((byte)(b ^ array7[num17]));'
Fix 'UI\BtcPlateImg.cs' 'byte b = array[num8 + l * num6 + m] ^ array5[m];' 'byte b = (byte)(array[num8 + l * num6 + m] ^ array5[m]);'
Fix 'UI\BtcPlate.cs' 'int num = (int)(alignment % TextAnchor.MiddleLeft);' 'int num = (int)alignment % (int)TextAnchor.MiddleLeft;'
Fix 'UI\BtcPlate.cs' 'int num2 = (int)(alignment / TextAnchor.MiddleLeft);' 'int num2 = (int)alignment / (int)TextAnchor.MiddleLeft;'
Fix 'UI\BtcPlate.cs' 'color.a * uivertex.color.a / byte.MaxValue)' '(byte)(color.a * uivertex.color.a / byte.MaxValue))'
Fix 'Replay\BtcSaveFormat.cs' 'long num3;' 'long num3 = 0L;'
$q = Join-Path $src 'ResourcePacks\ResourcePackManager.C12.cs'
[IO.File]::WriteAllText($q, [regex]::Replace([IO.File]::ReadAllText($q), '(?m)^\s*kv = default\(KeyValuePair<string, int>\);\r?\n', ''), (New-Object Text.UTF8Encoding($true)))

# definite-assignment differences
InitLocal 'Features\BtcHandicap.cs' 'array2'
InitLocal 'Features\BtcHandicap.cs' 'h'
InitLocal 'Features\BtcMapScale.cs' 'num20'
InitLocal 'Features\BtcSymmetry.cs' 'num15'

# Unity 5.6 ParticleSystem modules are structs: assign through a local copy
$p = Join-Path $src 'ResourcePacks\ResourcePackManager.C14.cs'
$t = [IO.File]::ReadAllText($p)
$t = [regex]::Replace($t, '(?m)^(\s*)([\w\[\]\.]+?)\.(main|collision|lights|trails|emission)\.(\w+) = (.*);\s*$', '$1{ var m_ = $2.$3; m_.$4 = $5; }')
$p2 = Join-Path $src 'ResourcePacks\ResourcePackManager.C14.cs'
[IO.File]::WriteAllText($p2, $t, (New-Object Text.UTF8Encoding($true)))
$p = Join-Path $src 'ResourcePacks\ResourcePackManager.C14.cs'
if ($bad) { Write-Host "post-port-fixes: $bad problem(s)"; exit 1 }
Write-Host 'post-port-fixes: OK'
