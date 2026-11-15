param([int]$PortA = 47123, [int]$PortB = 47124)
# Lab check of the toggle approach: one clumsy (lag on, drop off, chance 100) on port A; flip Drop on and off; port B has no filter.
$o = & (Join-Path $PSScriptRoot 'clumsy.ps1') -Filter "udp and outbound and loopback and (udp.DstPort == $PortA or udp.SrcPort == $PortA)" -CArgs '--lag on --lag-time 50 --drop off --drop-chance 100.0'
$id = $o | Where-Object { $_ -is [int] } | Select-Object -Last 1
Start-Sleep -Seconds 2
$rxA = New-Object System.Net.Sockets.UdpClient($PortA); $rxA.Client.ReceiveTimeout = 10
$rxB = New-Object System.Net.Sockets.UdpClient($PortB); $rxB.Client.ReceiveTimeout = 10
$tx = New-Object System.Net.Sockets.UdpClient; $tx2 = New-Object System.Net.Sockets.UdpClient
$epA = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Loopback, $PortA)
$epB = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Loopback, $PortB)
function Burst($tag) {
    $a = 0; $b = 0
    for ($i = 0; $i -lt 40; $i++) {
        $x = [byte[]](1, 2, 3); [void]$tx.Send($x, 3, $epA); [void]$tx2.Send($x, 3, $epB); Start-Sleep -Milliseconds 25
        $any = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Any, 0)
        try { while ($true) { [void]$rxA.Receive([ref]$any); $a++ } } catch { }
        try { while ($true) { [void]$rxB.Receive([ref]$any); $b++ } } catch { }
    }
    Start-Sleep -Milliseconds 300
    $any = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Any, 0)
    try { while ($true) { [void]$rxA.Receive([ref]$any); $a++ } } catch { }
    "$tag : A=$a/40 B=$b/40"
}
Burst 'lag only'
& (Join-Path $PSScriptRoot 'clumsy-toggle.ps1') -ClumsyPid $id -Box 'Drop'
Start-Sleep -Milliseconds 300
Burst 'drop on'
& (Join-Path $PSScriptRoot 'clumsy-toggle.ps1') -ClumsyPid $id -Box 'Drop'
Start-Sleep -Milliseconds 300
Burst 'drop off'
$rxA.Close(); $rxB.Close(); $tx.Close(); $tx2.Close()
Stop-Process -Id $id -Force -ErrorAction SilentlyContinue
