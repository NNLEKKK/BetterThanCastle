param([string]$Tag = '', [string]$Filter = '')
# Debug build that stores unique compiler errors in BTClogs/build-errors[-Tag].txt and prints a summary.
# -Tag X : builds into obj\X / bin\X so parallel agents do not collide with each other or with build.ps1.
# -Filter "Patches\\Foo": only show errors whose path matches (other people's half-written files are ignored).
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root 'src\BetterThanCastle\BetterThanCastle.csproj'
$extra = @()
if ($Tag) { $extra = @("-p:BaseIntermediateOutputPath=obj\$Tag\", "-p:OutDir=bin\$Tag\") }
$out = & dotnet build $proj -c Debug -v q -nologo -clp:NoSummary @extra 2>&1 | Out-String
$errs = $out -split "`r?`n" | Where-Object { $_ -match ': error (CS\d+|[A-Z]+\d+)' } | ForEach-Object { $_.Replace($root + '\', '').Trim() } | Sort-Object -Unique
if ($Filter) { $errs = @($errs | Where-Object { $_ -match $Filter }) }
New-Item -ItemType Directory -Force (Join-Path $root 'BTClogs') | Out-Null
$name = if ($Tag) { "build-errors-$Tag.txt" } else { 'build-errors.txt' }
[IO.File]::WriteAllLines((Join-Path $root "BTClogs\$name"), [string[]]@($errs))
"unique errors: $(@($errs).Count)  (full list: BTClogs\$name)"
$errs | Select-Object -First 25 | ForEach-Object { $s = ($_ -replace ' \[src.*', ''); if ($s.Length -gt 220) { $s.Substring(0, 220) } else { $s } }
