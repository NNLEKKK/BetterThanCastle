param([string]$Message = 'Update')
# Makes a commit on the local branch `public` whose tree = HEAD minus private/derived paths ($exclude).
# `public` has its own linear history (the dev history of refactor/main is never published); its commits use the
# GitHub noreply address. Pushing is separate: `git push origin public:main` (release.ps1 -Publish does it).
# Prints the new public commit id (or the existing one when nothing changed).
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$exclude = @('CLAUDE.md', '.claude', '.mcp.json', 'docs/gen', 'docs/proposed-claude-config')
$name = 'lokillFamalli'
$mail = '56294881+lokillFamalli@users.noreply.github.com'

$idx = Join-Path $root 'obj\public.index'
New-Item -ItemType Directory -Force (Split-Path $idx) | Out-Null
if (Test-Path $idx) { Remove-Item $idx }
$env:GIT_INDEX_FILE = $idx
try {
    git read-tree HEAD
    if ($LASTEXITCODE -ne 0) { throw 'read-tree failed' }
    git rm -r -q --cached --ignore-unmatch -- $exclude | Out-Null
    $tree = (git write-tree).Trim()
} finally { Remove-Item Env:\GIT_INDEX_FILE; if (Test-Path $idx) { Remove-Item $idx } }

$parent = (git rev-parse -q --verify refs/heads/public 2>$null)
if ($parent) {
    $parent = $parent.Trim()
    if ((git rev-parse "$parent^{tree}").Trim() -eq $tree) { $parent; exit 0 }
}
$env:GIT_AUTHOR_NAME = $name; $env:GIT_AUTHOR_EMAIL = $mail
$env:GIT_COMMITTER_NAME = $name; $env:GIT_COMMITTER_EMAIL = $mail
try {
    if ($parent) { $c = (git commit-tree $tree -p $parent -m $Message) } else { $c = (git commit-tree $tree -m $Message) }
} finally { 'GIT_AUTHOR_NAME', 'GIT_AUTHOR_EMAIL', 'GIT_COMMITTER_NAME', 'GIT_COMMITTER_EMAIL' | ForEach-Object { Remove-Item "Env:\$_" } }
$c = $c.Trim()
git update-ref refs/heads/public $c
$c
