param(
    [Parameter(Mandatory=$true)][string]$Version,   # e.g. 0.2.0
    [string]$Notes = '',                            # changelog text for the release (also add it to CHANGELOG.md)
    [switch]$Prerelease,                            # GitHub pre-release (updater ignores it: /releases/latest skips pre-releases)
    [switch]$Publish                                # push `public` to GitHub main + tag, create the release with both files
)
# Local by default: bumps Plugin.Version + csproj Version, Release build + verify-dist, installer, commit on the current
# branch, public snapshot commit (scripts/public-snapshot.ps1) and tag on it. -Publish pushes ONLY the public branch.
# The release body contains "sha256: <hash of BetterThanCastle.dll>", which the in-game updater (BtcUpdater) verifies.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$repo = 'NNLEKKK/BetterThanCastle'
$gh = 'C:\Program Files\GitHub CLI\gh.exe'
if ($Version -notmatch '^\d+\.\d+\.\d+[a-z]?$') { throw "Version must be x.y.z or x.y.z + a letter" }
$tag = "v$Version"
if (git tag --list $tag) { throw "Tag $tag already exists" }
if (git status --porcelain --untracked-files=no) { throw "Working tree has uncommitted changes; commit them first" }

$plugin = Join-Path $root 'src\BetterThanCastle\Plugin.cs'
$proj = Join-Path $root 'src\BetterThanCastle\BetterThanCastle.csproj'
$plain = $Version -replace '[a-z]$'
$ptxt = [regex]::Replace([IO.File]::ReadAllText($plugin), 'const string Version = "[^"]*";', "const string Version = `"$Version`";")
[IO.File]::WriteAllText($plugin, [regex]::Replace($ptxt, 'const string BepVersion = "[^"]*";', "const string BepVersion = `"$plain`";"))
[IO.File]::WriteAllText($proj, [regex]::Replace([IO.File]::ReadAllText($proj), '<Version>[^<]*</Version>', "<Version>$($Version -replace '[a-z]$','')</Version>"))

& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'build.ps1') -Release
if ($LASTEXITCODE -ne 0) { git checkout -- $plugin $proj; throw "Release build / verify-dist failed" }
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'build-setup.ps1')
if ($LASTEXITCODE -ne 0) { git checkout -- $plugin $proj; throw "Installer build failed" }
$dll = Join-Path $root 'dist\BetterThanCastle.dll'
$setup = Join-Path $root 'dist-setup\BetterThanCastle-Setup.exe'
$hash = (Get-FileHash $dll -Algorithm SHA256).Hash.ToLower()
$hashSetup = (Get-FileHash $setup -Algorithm SHA256).Hash.ToLower()

git add $plugin $proj
git commit -q -m "release: $tag`n`nCo-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
$pub = (& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'public-snapshot.ps1') -Message "Better Than Castle $Version" | Select-Object -Last 1).Trim()
git tag $tag $pub

$body = @"
$Notes

**Install (new players):** download ``BetterThanCastle-Setup.exe``, run it, press Install. It finds Castle Story through Steam, installs BepInEx 5.4.23.5 if missing and the mod. Game files are not changed.
**Установка (новые игроки):** скачайте ``BetterThanCastle-Setup.exe``, запустите, нажмите «Установить». Он сам найдёт Castle Story в Steam, поставит BepInEx 5.4.23.5 (если его нет) и мод. Файлы игры не меняются.

**Update:** the mod offers the update itself at game start. Manual: replace ``BepInEx/plugins/BetterThanCastle/BetterThanCastle.dll``.

sha256: $hash
Setup SHA-256: $hashSetup
"@
$notesFile = Join-Path $env:TEMP "btc-release-notes.md"
[IO.File]::WriteAllText($notesFile, $body)
Write-Host "Built $tag (public commit $pub), dll sha256 $hash"

if (-not $Publish) { Write-Host "Local only. Re-run publishing with: git push origin public:main; git push origin $tag; gh release create $tag <dll> <setup> -F $notesFile"; exit 0 }
git push origin public:main
if ($LASTEXITCODE -ne 0) { throw "push public failed" }
git push origin $tag
if ($LASTEXITCODE -ne 0) { throw "push tag failed" }
$args2 = @('release', 'create', $tag, $dll, $setup, '--repo', $repo, '--verify-tag', '--title', "Better Than Castle $Version", '--notes-file', $notesFile)
if ($Prerelease) { $args2 += '--prerelease' }
& $gh @args2
if ($LASTEXITCODE -ne 0) { throw "gh release create failed" }
