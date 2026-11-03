param([switch]$Release)
# Debug (DEV) -> deploys to <GameDir>/BepInEx/plugins/BetterThanCastle. -Release -> dist/BetterThanCastle.dll + verify-dist.
# Prints only OK or compiler errors/warnings (max 40 lines) plus exit code.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root 'src\BetterThanCastle\BetterThanCastle.csproj'
$gameDir = Join-Path $root 'game\csmod'
$cfg = if ($Release) { 'Release' } else { 'Debug' }
$dist = Join-Path $root 'dist'

$extra = @()
if ($Release) {
    if (Test-Path $dist) { Get-ChildItem $dist -Force | Remove-Item -Recurse -Force }
    $extra = @('-o', $dist, '-p:DebugType=none', '-p:DebugSymbols=false')
}

$out = & dotnet build $proj -c $cfg -v q -nologo -clp:NoSummary $extra 2>&1 | Out-String
$code = $LASTEXITCODE
$lines = $out -split "`r?`n" | Where-Object { $_ -match '(error|warning) [A-Za-z]+\d*' } | Sort-Object -Unique
if ($code -ne 0 -or $lines) { $lines | Select-Object -First 40 | ForEach-Object { $_.Replace($root + '\', '') } }
if ($code -ne 0) { "FAILED exit=$code"; exit $code }

if ($Release) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'verify-dist.ps1')
    $code = $LASTEXITCODE
    if ($code -ne 0) { "FAILED verify-dist exit=$code"; exit $code }
} else {
    $plug = Join-Path $gameDir 'BepInEx\plugins\BetterThanCastle'
    New-Item -ItemType Directory -Force $plug | Out-Null
    $bin = Join-Path $root 'src\BetterThanCastle\bin\Debug\net35'
    Copy-Item (Join-Path $bin 'BetterThanCastle.dll') $plug -Force
    $pdb = Join-Path $bin 'BetterThanCastle.pdb'
    if (Test-Path $pdb) { Copy-Item $pdb $plug -Force }
}
"OK ($cfg) exit=0"
exit 0
