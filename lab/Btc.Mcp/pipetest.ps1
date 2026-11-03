# Real stdio round trip against the built server: powershell -NoProfile -ExecutionPolicy Bypass -File lab/Btc.Mcp/pipetest.ps1
$dll = Join-Path $PSScriptRoot 'bin\Release\net10.0\btc-mcp.dll'
$psi = New-Object Diagnostics.ProcessStartInfo 'dotnet', ('"' + $dll + '"')
$psi.RedirectStandardInput = $true; $psi.RedirectStandardOutput = $true; $psi.RedirectStandardError = $true; $psi.UseShellExecute = $false
$psi.StandardOutputEncoding = [Text.Encoding]::UTF8
$p = [Diagnostics.Process]::Start($psi)
$fail = 0
function Send($s) { $p.StandardInput.Write($s + "`n"); $p.StandardInput.Flush() }
function Recv($ms) { $t = $p.StandardOutput.ReadLineAsync(); if ($t.Wait($ms)) { return $t.Result } else { return $null } }
function Check($name, $ok, $detail) { if (-not $ok) { $script:fail++ }; "{0} {1} {2}" -f $(if ($ok) { 'PASS' } else { 'FAIL' }), $name, $detail }
Send '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2099-01-01","capabilities":{}}}'
$r = Recv 15000 | ConvertFrom-Json
Check 'initialize (unknown version -> 2025-06-18)' ($r.result.protocolVersion -eq '2025-06-18') ''
Send '{"jsonrpc":"2.0","method":"notifications/initialized"}'
Send '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'
$r = Recv 10000 | ConvertFrom-Json
Check 'tools/list' ($r.result.tools.Count -eq 21) ("n=" + $r.result.tools.Count)
# concurrency: slow call (errors, ~10+ s) then ping must answer first
Send '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"errors","arguments":{"tag":"mcpt"}}}'
Send '{"jsonrpc":"2.0","id":4,"method":"ping"}'
$a = Recv 10000 | ConvertFrom-Json
Check 'ping answered while a tool runs' ($a.id -eq 4) ("first id=" + $a.id)
$b = Recv 120000 | ConvertFrom-Json
Check 'errors tool result' ($b.id -eq 3 -and $b.result.content[0].text -match 'errors=\d+') ($b.result.content[0].text -replace "`n", ' / ')
Send '{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"game_cmd","arguments":{"dir":"nonexistent","lines":["wait 1"]}}}'
$c = Recv 10000 | ConvertFrom-Json
Check 'game_cmd error path' ($c.result.isError -eq $true) ''
Send 'not json'
$d = Recv 5000 | ConvertFrom-Json
Check 'parse error -32700' ($d.error.code -eq -32700) ''
$p.StandardInput.Close()
if (-not $p.WaitForExit(10000)) { $p.Kill() }
if ($fail -eq 0) { 'PIPETEST PASS' } else { "PIPETEST FAIL ($fail)" }
exit $fail
