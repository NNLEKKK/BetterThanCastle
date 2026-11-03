# Builds dist-setup/BetterThanCastle-Setup.exe: one exe with the official BepInEx win_x64 zip and dist/BetterThanCastle.dll embedded.
# Run after `build.ps1 -Release`. The BepInEx zip is downloaded once into obj/setup and checked against a pinned SHA256.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$bepVer = '5.4.23.5'
$bepSha = '82f9878551030f54657792c0740d9d51a09500eeae1fba21106b0c441e6732c4'
$cache = Join-Path $root 'obj\setup'
$out = Join-Path $root 'dist-setup'
$dll = Join-Path $root 'dist\BetterThanCastle.dll'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $dll)) { throw "dist\BetterThanCastle.dll missing: run build.ps1 -Release first" }
New-Item -ItemType Directory -Force $cache, $out | Out-Null

$zip = Join-Path $cache "BepInEx_win_x64_$bepVer.zip"
if (-not (Test-Path $zip)) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    (New-Object Net.WebClient).DownloadFile("https://github.com/BepInEx/BepInEx/releases/download/v$bepVer/BepInEx_win_x64_$bepVer.zip", $zip)
}
if ((Get-FileHash $zip -Algorithm SHA256).Hash.ToLower() -ne $bepSha) { Remove-Item $zip; throw "BepInEx zip checksum mismatch" }

$plugin = [IO.File]::ReadAllText((Join-Path $root 'src\BetterThanCastle\Plugin.cs'))
$ver = [regex]::Match($plugin, 'Version = "([^"]+)"').Groups[1].Value
$gen = Join-Path $cache 'BuildInfo.g.cs'
[IO.File]::WriteAllText($gen, "namespace BtcSetup { internal static class BuildInfo { internal const string ModVersion = `"$ver`"; } }")

$exe = Join-Path $out 'BetterThanCastle-Setup.exe'
$src = Join-Path $root 'setup\BtcSetup.cs'
$res = & $csc /nologo /codepage:65001 /target:winexe /optimize+ /platform:anycpu "/out:$exe" `
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.IO.Compression.dll `
    "/resource:$zip,BepInEx.zip" "/resource:$dll,BetterThanCastle.dll" $src $gen 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) { $res; "FAILED setup build"; exit 1 }
"OK setup v$ver -> dist-setup\BetterThanCastle-Setup.exe ($([math]::Round((Get-Item $exe).Length / 1MB, 2)) MB)"
exit 0
