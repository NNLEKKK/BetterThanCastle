param([string]$CArgs = '--drop on --drop-chance 100.0', [int]$Port = 47123)
# Sends 40 loopback UDP packets to $Port with clumsy running $CArgs on that port; prints how many arrived and the mean delay.
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'tools\clumsy\clumsy.exe'
$filter = "udp and outbound and loopback and (udp.DstPort == $Port or udp.SrcPort == $Port)"
$p = $null
if ($CArgs) { $o = & (Join-Path $PSScriptRoot 'clumsy.ps1') -Filter $filter -CArgs $CArgs; $o | Where-Object { $_ -is [string] } | Write-Output; $p = Get-Process -Id ($o | Where-Object { $_ -is [int] } | Select-Object -Last 1) -ErrorAction SilentlyContinue; Start-Sleep -Seconds 1 }
$rx = New-Object System.Net.Sockets.UdpClient($Port); $rx.Client.ReceiveTimeout = 50
$tx = New-Object System.Net.Sockets.UdpClient
$ep = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Loopback, $Port)
$got = 0; $sum = 0.0
$sw = [Diagnostics.Stopwatch]::StartNew()
for ($i = 0; $i -lt 40; $i++) {
    $b = [BitConverter]::GetBytes($sw.Elapsed.TotalMilliseconds); [void]$tx.Send($b, $b.Length, $ep)
    Start-Sleep -Milliseconds 25
}
$end = $sw.Elapsed.TotalMilliseconds + 1500
while ($sw.Elapsed.TotalMilliseconds -lt $end) {
    try { $any = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Any, 0); $d = $rx.Receive([ref]$any); $got++; $sum += $sw.Elapsed.TotalMilliseconds - [BitConverter]::ToDouble($d, 0) } catch { }
}
$rx.Close(); $tx.Close()
if ($p -and -not $p.HasExited) { Stop-Process -Id $p.Id -Force }
"clumsy selftest args=[$CArgs] received=$got/40 meanDelayMs=$(if ($got) { [int]($sum / $got) } else { '-' })"
