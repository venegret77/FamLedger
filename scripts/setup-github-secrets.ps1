# Requires: gh auth login (repo admin)
# Sets Actions secrets + read-only deploy key for VPS git pull.

$ErrorActionPreference = "Stop"
$env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")

$repo = "venegret77/FamLedger"
$root = Split-Path -Parent $PSScriptRoot
$keyFile = Join-Path $PSScriptRoot "_deploy_ssh_key.tmp"
$pubFile = Join-Path $PSScriptRoot "_deploy_git_pub.tmp"

if (-not (Test-Path $keyFile)) { throw "Missing $keyFile — run server CI setup first" }
if (-not (Test-Path $pubFile)) { throw "Missing $pubFile — run server CI setup first" }

gh auth status

Write-Host "Setting secrets on $repo ..."
gh secret set DEPLOY_HOST --repo $repo --body "159.195.114.77"
gh secret set DEPLOY_USER --repo $repo --body "root"
Get-Content -Raw $keyFile | gh secret set DEPLOY_SSH_KEY --repo $repo

$pub = (Get-Content -Raw $pubFile).Trim()
Write-Host "Adding deploy key..."
# Remove old key with same title if exists
$existing = gh api "repos/$repo/keys" --jq '.[] | select(.title=="famledger-vps-git-pull") | .id' 2>$null
if ($existing) {
  gh api -X DELETE "repos/$repo/keys/$existing" | Out-Null
}
gh api -X POST "repos/$repo/keys" -f title="famledger-vps-git-pull" -f key="$pub" -F read_only=true | Out-Null

Write-Host "OK. Secrets + deploy key configured."
Write-Host "Next: commit/push workflow on master to trigger deploy."
