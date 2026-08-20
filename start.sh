#!/usr/bin/env bash
# FamLedger — one-command startup (Linux / macOS / Git Bash)
set -euo pipefail
cd "$(dirname "$0")"

REBUILD=false
LOGS=false
DOWN=false

for arg in "$@"; do
  case "$arg" in
    --rebuild|-r) REBUILD=true ;;
    --logs|-l)    LOGS=true ;;
    --down|-d)    DOWN=true ;;
  esac
done

if $DOWN; then
  echo "Stopping FamLedger..."
  docker compose down
  exit 0
fi

if [[ ! -f .env ]]; then
  cp .env.example .env
  echo "Created .env — set TELEGRAM_BOT_TOKEN and TELEGRAM_BOT_USERNAME"
fi

docker info >/dev/null

ARGS=(compose up -d)
$REBUILD && ARGS+=(--build)

echo "Starting FamLedger..."
docker "${ARGS[@]}"

echo ""
echo "FamLedger is up!"
echo "  Web:  http://localhost:5173"
echo "  API:  http://localhost:8080"
echo "  Stop: ./start.sh --down"
echo ""

$LOGS && docker compose logs -f
