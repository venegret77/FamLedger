# FamLedger — запуск всего стека одной командой (Windows PowerShell)
# Usage: .\start.ps1
#        .\start.ps1 -Rebuild
#        .\start.ps1 -Logs
#        .\start.ps1 -TryStartDocker   # попытаться запустить Docker Desktop и подождать

param(
    [switch]$Rebuild,
    [switch]$Logs,
    [switch]$Down,
    [switch]$TryStartDocker
)

Set-Location $PSScriptRoot

function Write-Info($msg)  { Write-Host $msg -ForegroundColor Cyan }
function Write-Ok($msg)    { Write-Host $msg -ForegroundColor Green }
function Write-Warn($msg)  { Write-Host $msg -ForegroundColor Yellow }
function Write-Err($msg)   { Write-Host $msg -ForegroundColor Red }

function Test-DockerReady {
    $prev = $ErrorActionPreference
    $ErrorActionPreference = "SilentlyContinue"
    $null = docker info 2>&1
    $ok = $LASTEXITCODE -eq 0
    $ErrorActionPreference = $prev
    return $ok
}

function Start-DockerDesktopIfNeeded {
    $paths = @(
        "${env:ProgramFiles}\Docker\Docker\Docker Desktop.exe",
        "${env:ProgramFiles(x86)}\Docker\Docker\Docker Desktop.exe",
        "$env:LOCALAPPDATA\Docker\Docker Desktop.exe"
    )
    foreach ($p in $paths) {
        if (Test-Path $p) {
            Write-Info "Starting Docker Desktop..."
            Start-Process $p
            return $true
        }
    }
    return $false
}

if ($Down) {
    Write-Info "Stopping FamLedger..."
    $prev = $ErrorActionPreference
    $ErrorActionPreference = "SilentlyContinue"
    docker compose down 2>&1 | Out-Host
    $ErrorActionPreference = $prev
    Write-Ok "Stopped."
    exit 0
}

if (-not (Test-Path ".env")) {
    Copy-Item ".env.example" ".env"
    Write-Warn "Created .env from .env.example"
    Write-Warn "Edit .env: TELEGRAM_BOT_TOKEN and TELEGRAM_BOT_USERNAME"
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Err "Docker CLI not found."
    Write-Host "Install Docker Desktop: https://www.docker.com/products/docker-desktop/"
    exit 1
}

Write-Info "Checking Docker..."

if (-not (Test-DockerReady)) {
    if ($TryStartDocker -or $true) {
        $started = Start-DockerDesktopIfNeeded
        if ($started) {
            Write-Info "Waiting for Docker (up to 90 sec)..."
            $ready = $false
            for ($i = 0; $i -lt 45; $i++) {
                Start-Sleep -Seconds 2
                if (Test-DockerReady) { $ready = $true; break }
                Write-Host "." -NoNewline
            }
            Write-Host ""
            if (-not $ready) {
                Write-Err "Docker Desktop is still starting. Wait until the whale icon is green, then run:"
                Write-Host "  .\start.ps1" -ForegroundColor White
                exit 1
            }
        } else {
            Write-Err "Docker is not running."
            Write-Host ""
            Write-Host "  1. Open Docker Desktop from Start menu" -ForegroundColor White
            Write-Host "  2. Wait until it says 'Engine running'" -ForegroundColor White
            Write-Host "  3. Run again:  .\start.ps1" -ForegroundColor White
            Write-Host ""
            exit 1
        }
    }
}

Write-Ok "Docker is ready."

$composeArgs = @("compose", "up", "-d")
if ($Rebuild) { $composeArgs += "--build" }

Write-Info "Starting FamLedger stack..."
Write-Host "  docker $($composeArgs -join ' ')" -ForegroundColor DarkGray

$prev = $ErrorActionPreference
$ErrorActionPreference = "SilentlyContinue"
& docker @composeArgs 2>&1 | Out-Host
$exitCode = $LASTEXITCODE
$ErrorActionPreference = $prev

if ($exitCode -ne 0) {
    Write-Err "docker compose failed (exit $exitCode). Try: .\start.ps1 -Rebuild"
    exit $exitCode
}

Write-Info "Waiting for API..."
$healthy = $false
for ($i = 0; $i -lt 30; $i++) {
    try {
        $r = Invoke-WebRequest -Uri "http://localhost:8080/health" -UseBasicParsing -TimeoutSec 3
        if ($r.StatusCode -eq 200) { $healthy = $true; break }
    } catch { Start-Sleep -Seconds 2 }
}
if ($healthy) { Write-Ok "API is healthy." } else { Write-Warn "API not ready yet — run: docker compose logs api" }

Write-Host ""
Write-Ok "FamLedger is up!"
Write-Host ""
Write-Host "  Web:      http://localhost:5173"
Write-Host "  API:      http://localhost:8080"
Write-Host "  MinIO:    http://localhost:9000  (console :9001)"
Write-Host ""
Write-Host "  Stop:     .\start.ps1 -Down"
Write-Host "  Rebuild:  .\start.ps1 -Rebuild"
Write-Host ""

$tokenLine = Get-Content .env -ErrorAction SilentlyContinue | Where-Object { $_ -match '^TELEGRAM_BOT_TOKEN=' } | Select-Object -First 1
if ($null -eq $tokenLine -or [string]::IsNullOrWhiteSpace(($tokenLine -replace '^TELEGRAM_BOT_TOKEN=', ''))) {
    Write-Warn "TELEGRAM_BOT_TOKEN is empty in .env — set it and run: docker compose up -d --build bot"
}

if ($Logs) {
    docker compose logs -f
}
