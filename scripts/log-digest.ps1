# Condenses the latest BepInEx/BTC logs into BTClogs/digest.txt (small file for the explorer/haiku agent to read).
param([string]$Sub = "")
$root = Split-Path -Parent $PSScriptRoot
$logs = if ($Sub) { Join-Path (Join-Path $root 'BTClogs') $Sub } else { Join-Path $root 'BTClogs' }
$bep = Join-Path $root 'game\csmod\BepInEx\LogOutput.log'
$btc = Get-ChildItem $logs -Filter 'btc-*.log' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$src = @(); if (-not $Sub -and (Test-Path $bep)) { $src += $bep }; if ($btc) { $src += $btc.FullName }
$bad = '\[(Error|Fatal)|Exception|MissingMethod|MissingField|TypeLoad|HarmonyException|\[Patch\] FAIL|Could not load'
$noise = 'GA Error|Steamworks is not initialized|Coroutine couldn.t be started|GA_Controller|Hat apply failed'
$out = New-Object Collections.Generic.List[string]
$errors = @{}
$modLog = Join-Path $logs 'mod_log.txt'
$lines = if ($btc) { [IO.File]::ReadAllLines($btc.FullName) + $(if (Test-Path $modLog) { [IO.File]::ReadAllLines($modLog) } else { @() }) } elseif (-not $Sub -and (Test-Path $bep)) { [IO.File]::ReadAllLines($bep) } else { @() }
$btcLines = $lines | Where-Object { $_ -match '\[BTC\]|\[Patch\]' }
for ($i = 0; $i -lt $lines.Length; $i++) {
    $l = $lines[$i]
    if ($l -match $bad -and $l -notmatch $noise) {
        $key = ($l -replace '\d+', '#')
        if ($errors.ContainsKey($key)) { $errors[$key].N++ ; continue }
        $ctx = New-Object Collections.Generic.List[string]
        $ctx.Add($l)
        for ($j = $i + 1; $j -lt [Math]::Min($i + 15, $lines.Length) -and $lines[$j] -match '^\s+at |^\s*$|^\s*\(Filename'; $j++) { if ($lines[$j].Trim()) { $ctx.Add($lines[$j]) } }
        $errors[$key] = @{ N = 1; Ctx = $ctx; First = $i + 1 }
    }
}
$out.Add("# digest of " + (($src | ForEach-Object { Split-Path $_ -Leaf }) -join ', ') + " @ " + (Get-Date -Format 'HH:mm:ss'))
$out.Add("lines=$($lines.Length) distinct-errors=$($errors.Count)")
$out.Add('')
$out.Add('## [BTC]/[Patch] lines (first 60)')
$btcLines | Select-Object -First 60 | ForEach-Object { $out.Add($_) }
$out.Add('')
$out.Add('## Errors (deduplicated; xN = repeats)')
foreach ($e in $errors.Values) { $out.Add("x$($e.N) @line $($e.First)"); $e.Ctx | ForEach-Object { $out.Add('  ' + $_) } }
$out.Add('')
$hits = Join-Path $logs 'hits.txt'
if (Test-Path $hits) { $out.Add('## hook probe (BTClogs/hits.txt)'); [IO.File]::ReadAllLines($hits) | Where-Object { $_ -match '^# patched|\texceptions|^[0-9]+\t[1-9]' } | Select-Object -First 12 | ForEach-Object { $out.Add($_) }; $out.Add('') }
$out.Add($(if ($errors.Count -eq 0) { 'RESULT: OK (no errors)' } else { "RESULT: $($errors.Count) distinct error(s)" }))
[IO.File]::WriteAllLines((Join-Path $logs 'digest.txt'), $out)
$out | Select-Object -Last 1
