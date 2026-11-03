param([string]$Dll = 'game\csmod\Castle Story_Data\Managed\Assembly-CSharp-firstpass.dll', [string]$Out = 'ref\vanilla-clean')
# TOMORROW (clean DLL): decompiles the given Assembly-CSharp-firstpass.dll into an ilspycmd project folder (read-only use of the DLL).
# Takes several minutes. Then: scripts/check-dll.ps1, then scripts/gen-diff.ps1 -Vanilla ref\vanilla-clean .
$root = Split-Path -Parent $PSScriptRoot
$dll = Join-Path $root $Dll; $out = Join-Path $root $Out
if (-not (Test-Path $dll)) { "missing $dll"; exit 1 }
if (Test-Path $out) { "output exists: $out (delete or choose -Out)"; exit 1 }
New-Item -ItemType Directory -Force $out | Out-Null
& ilspycmd -p -o $out $dll 2>&1 | Select-Object -Last 3
$n = (Get-ChildItem $out -Recurse -Filter *.cs | Measure-Object).Count
"decompiled files: $n -> $Out"
