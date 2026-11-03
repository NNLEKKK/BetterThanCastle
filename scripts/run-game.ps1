param([int]$Seconds = 40, [switch]$KillRunning, [switch]$Visible)
# Smoke test: launches the game from game/csmod, waits, kills it, builds BTClogs/digest.txt. Prints only the RESULT line.
$root = Split-Path -Parent $PSScriptRoot
$g = Join-Path $root 'game\csmod'
$running = Get-Process -Name 'Castle Story' -ErrorAction SilentlyContinue
if ($running) { if ($KillRunning) { $running | Stop-Process -Force } else { 'RESULT: game is already running (maybe you are playing). Close it or pass -KillRunning.'; exit 3 } }
# keep BTClogs small: 3 newest btc-*.log, drop stale tagged build-error lists
Get-ChildItem (Join-Path $root 'BTClogs') -Filter 'btc-*.log' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -Skip 3 | Remove-Item -Force -ErrorAction SilentlyContinue
Get-ChildItem (Join-Path $root 'BTClogs') -Filter 'build-errors-*.txt' -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
$bepLog = Join-Path $g 'BepInEx\LogOutput.log'
$before = if (Test-Path $bepLog) { (Get-Item $bepLog).LastWriteTime } else { [DateTime]::MinValue }
if ($Visible) { $p = Start-Process -FilePath (Join-Path $g 'Castle Story.exe') -WorkingDirectory $g -PassThru }
else {
    # default for agents: windowed tiny, minimized, never activated (BtcDevWindow also minimizes/mutes in-game)
    $env:BTC_HIDDEN = '1'
    $p = Start-Process -FilePath (Join-Path $g 'Castle Story.exe') -WorkingDirectory $g -PassThru -WindowStyle Minimized -ArgumentList '-screen-fullscreen','0','-screen-width','640','-screen-height','360'
}
Start-Sleep -Seconds $Seconds
$alive = -not $p.HasExited
if ($alive) { Stop-Process -Id $p.Id -Force }
$after = if (Test-Path $bepLog) { (Get-Item $bepLog).LastWriteTime } else { [DateTime]::MinValue }
if ($after -le $before) { 'RESULT: BepInEx did not write LogOutput.log (check BepInEx.cfg [Preloader.Entrypoint] Assembly = UnityEngine.dll)'; exit 2 }
"game alive after ${Seconds}s: $alive"
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'log-digest.ps1')
