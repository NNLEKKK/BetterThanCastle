param([string]$Pattern = '')
# One line per MP role (BTClogs/host, client*, late subs): menu kicks, link pauses, drops, rejoins, spectator outcome, errors.
$root = Split-Path -Parent $PSScriptRoot
$logs = Join-Path $root 'BTClogs'
function C($t, $re) { ([regex]::Matches($t, $re)).Count }
foreach ($d in (Get-ChildItem $logs -Directory | Where-Object { $_.Name -match '^(host|client\w*|spec\w*)$' } | Sort-Object Name)) {
    $f = Join-Path $d.FullName 'agent.log'
    if (-not (Test-Path $f)) { continue }
    if ((Get-Item $f).LastWriteTime -lt (Get-Date).AddHours(-3)) { continue }
    $t = [IO.File]::ReadAllText($f)
    $s = "$($d.Name): kick=$(C $t 'MENU KICK') pauseOn=$(C $t 'link pause ON') lost=$(C $t 'connection lost') reconn=$(C $t 'reconnecting') reattach=$(C $t 're-attaching') stillConn=$(C $t 'still connected') specDeny=$(C $t '(?i)spectat\w* .*(deny|denied|refus)') err=$(C $t '\bE\b|\[Error|Exception')"
    $cap = [regex]::Matches($t, 'cap topo=\S+ maxConn=\S+ maxPlayers=\S+ lobbySlots=\S+ modMax=\S+') | Select-Object -Last 1
    if ($cap) { $s += " | $($cap.Value)" }
    $last = [regex]::Matches($t, 'netstat [^\r\n]{0,120}') | Select-Object -Last 1
    if ($last) { $s += " | last: $($last.Value)" }
    $s += " | ingame=$(Test-Path (Join-Path $d.FullName 'mark-ingame.txt')) done=$(Test-Path (Join-Path $d.FullName 'done.txt'))"
    $s
}
$np = Join-Path $logs 'net-plan.txt'; if (Test-Path $np) { "net-plan: " + ((Get-Content $np | Select-Object -Skip 1) -join ' ; ') }
