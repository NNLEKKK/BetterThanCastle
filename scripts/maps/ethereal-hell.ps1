param([int]$Seed = 7, [string]$Out = "")
# Generates the Ethereal Hell map folder into <game>/Info/UGC/Maps (a new folder each run unless -Out is given).
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition ([IO.File]::ReadAllText("$PSScriptRoot\EtherealHell.cs"))
$maps = Join-Path $root "game\csmod\Info\UGC\Maps"
$tmpl = Get-ChildItem $maps -Directory | Where-Object { $_.Name -like "we_newwinter_*" } | Sort-Object LastWriteTime | Select-Object -First 1
if (-not $tmpl) { throw "need a template map folder we_newwinter_* (create any winter map in the editor once)" }
if ($Out -eq "") { $Out = Join-Path $maps ("we_etherealhell_" + [guid]::NewGuid().ToString()) }
New-Item -ItemType Directory -Force $Out | Out-Null
foreach ($f in "Factions.json","PlacedBlocks.json","RemovedVoxels.json","TerrainDamage.json","boulders.json","iron.json","plants.json","pyrite.json","trees.json","gameobjects.json","meta.json") {
    Copy-Item (Join-Path $tmpl.FullName $f) (Join-Path $Out $f) -Force
}
$code = Split-Path -Leaf $Out
$meta = [IO.File]::ReadAllText((Join-Path $Out "meta.json"))
$meta = [regex]::Replace($meta, '"__CODENAME":\s*"[^"]*"', '"__CODENAME": "' + $code + '"')
$meta = [regex]::Replace($meta, '"__DISPLAYNAME":\s*"[^"]*"', '"__DISPLAYNAME": "Ethereal Hell"')
[IO.File]::WriteAllText((Join-Path $Out "meta.json"), $meta, (New-Object Text.UTF8Encoding($false)))
$log = [EtherealHell]::Run($Out, $Seed)
Write-Output $log
Write-Output "OUT $Out"
$sz = (Get-ChildItem $Out | Measure-Object Length -Sum).Sum
Write-Output ("folder size {0:N0} KB" -f ($sz / 1024))
