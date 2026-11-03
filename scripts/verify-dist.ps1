# Fails (exit 1) if dist/ is not exactly one BetterThanCastle.dll, if the DLL references assemblies the
# game/BepInEx do not provide, or if DEV-only code leaked into it.
$root = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $root 'dist'
$dll = Join-Path $dist 'BetterThanCastle.dll'
$fail = @()

$files = @(Get-ChildItem $dist -Force -ErrorAction SilentlyContinue)
if ($files.Count -ne 1 -or $files[0].Name -ne 'BetterThanCastle.dll') {
    $fail += 'dist must contain exactly BetterThanCastle.dll, found: ' + (($files | ForEach-Object Name) -join ', ')
}

if (Test-Path $dll) {
    $allowed = @{}
    foreach ($d in @('game\csmod\Castle Story_Data\Managed', 'game\csmod\BepInEx\core')) {
        Get-ChildItem (Join-Path $root $d) -Filter *.dll -ErrorAction SilentlyContinue |
            ForEach-Object { $allowed[$_.BaseName] = $true }
    }
    foreach ($n in @('mscorlib', 'System', 'System.Core', 'System.Xml', 'System.Xml.Linq', '0Harmony', 'BepInEx')) { $allowed[$n] = $true }

    $asm = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom($dll)
    foreach ($r in $asm.GetReferencedAssemblies()) {
        if (-not $allowed.ContainsKey($r.Name)) { $fail += 'disallowed reference: ' + $r.Name }
    }

    $text = [System.Text.Encoding]::ASCII.GetString([System.IO.File]::ReadAllBytes($dll))
    if ($text.Contains('BTC_DEV_BUILD') -or $text.Contains('BtcDev')) {
        $fail += 'DEV code leaked into Release DLL'
    }
    if ($text -match '[A-Za-z]:\\Users\\') { $fail += 'absolute user path found in DLL' }
}

if ($fail.Count) { $fail | ForEach-Object { 'verify-dist: ' + $_ }; exit 1 }
'verify-dist: OK'
exit 0
