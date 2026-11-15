param([int]$PortA = 47123, [int]$PortB = 47124)
# Lab check: does a 100% drop clumsy on port A disturb port B that has its own lag clumsy (like the MP outage lab)?
$lag = & (Join-Path $PSScriptRoot 'clumsy.ps1') -Filter "udp and outbound and loopback and (udp.DstPort == $PortB or udp.SrcPort == $PortB)" -CArgs '--lag on --lag-time 40'
$cut = & (Join-Path $PSScriptRoot 'clumsy.ps1') -Filter "udp and outbound and loopback and (udp.DstPort == $PortA or udp.SrcPort == $PortA)" -CArgs '--drop on --drop-chance 100.0'
Start-Sleep -Seconds 1
$rxA = New-Object System.Net.Sockets.UdpClient($PortA); $rxA.Client.ReceiveTimeout = 20
$rxB = New-Object System.Net.Sockets.UdpClient($PortB); $rxB.Client.ReceiveTimeout = 20
$tx = New-Object System.Net.Sockets.UdpClient
$epA = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Loopback, $PortA)
$epB = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Loopback, $PortB)
$gotA = 0; $gotB = 0
for ($i = 0; $i -lt 60; $i++) {
    $b = [byte[]](1, 2, 3); [void]$tx.Send($b, 3, $epA); [void]$tx.Send($b, 3, $epB)
    Start-Sleep -Milliseconds 25
    $any = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Any, 0)
    try { while ($true) { [void]$rxA.Receive([ref]$any); $gotA++ } } catch { }
    try { while ($true) { [void]$rxB.Receive([ref]$any); $gotB++ } } catch { }
}
Start-Sleep -Milliseconds 300
$any = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Any, 0)
try { while ($true) { [void]$rxB.Receive([ref]$any); $gotB++ } } catch { }
$rxA.Close(); $rxB.Close(); $tx.Close()
foreach ($o in @($lag, $cut)) { $id = $o | Where-Object { $_ -is [int] } | Select-Object -Last 1; if ($id) { Stop-Process -Id $id -Force -ErrorAction SilentlyContinue } }
"crosstest: A (cut) received=$gotA/60, B (lag only) received=$gotB/60"
