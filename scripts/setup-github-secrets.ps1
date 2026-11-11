# Requires: gh auth login (repo admin)
# Sets Actions secrets + read-only deploy key for VPS git pull.

param(
    [Parameter(Mandatory)] [string]$Repo,
    [Parameter(Mandatory)] [string]$DeployHost,
    [Parameter(Mandatory)] [ValidatePattern('^(?!root$)[a-z_][a-z0-9_-]*$')] [string]$DeployUser
)

$ErrorActionPreference = "Stop"
$env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")

$keyFile = Join-Path $PSScriptRoot "_deploy_ssh_key.tmp"
$pubFile = Join-Path $PSScriptRoot "_deploy_git_pub.tmp"

if (-not (Test-Path $keyFile)) { throw "Missing $keyFile — run server CI setup first" }
if (-not (Test-Path $pubFile)) { throw "Missing $pubFile — run server CI setup first" }

gh auth status

Write-Host "Setting secrets on $repo ..."
gh secret set DEPLOY_HOST --repo $repo --body $DeployHost
gh secret set DEPLOY_USER --repo $repo --body $DeployUser
Get-Content -Raw $keyFile | gh secret set DEPLOY_SSH_KEY --repo $repo

$pub = (Get-Content -Raw $pubFile).Trim()
Write-Host "Adding deploy key..."
$existing = gh api "repos/$repo/keys" --jq '.[] | select(.title=="famledger-vps-git-pull") | .key'
if (-not $existing) {
  gh api -X POST "repos/$repo/keys" -f title="famledger-vps-git-pull" -f key="$pub" -F read_only=true | Out-Null
} elseif (($existing -split ' ')[1] -ne ($pub -split ' ')[1]) {
  throw "A different deploy key already exists. Verify the new server key in GitHub before changing it."
}

Write-Host "OK. Secrets + deploy key configured."
