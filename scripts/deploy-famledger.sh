#!/usr/bin/env bash
set -euo pipefail

APP_DIR=/root/FamLedger
BRANCH=master

cd "$APP_DIR"

echo "==> FamLedger deploy $(date -u +%Y-%m-%dT%H:%M:%SZ)"

if [[ ! -d .git ]]; then
  echo "ERROR: $APP_DIR is not a git repo" >&2
  exit 1
fi

git fetch --prune origin "$BRANCH"
git reset --hard "origin/$BRANCH"
git clean -fd -e .env -e '.env.*' -e 'data/' -e 'backups/'

docker compose up -d --build api bot web

echo "==> Health check"
for i in $(seq 1 15); do
  if curl -fsS "http://127.0.0.1:8080/health" >/dev/null; then
    echo "API healthy"
    break
  fi
  sleep 3
  if [[ $i -eq 15 ]]; then
    echo "API health check failed" >&2
    docker compose ps
    docker logs famledger-api --tail 80 || true
    exit 1
  fi
done

# Recreate web so nginx re-resolves api (also mitigated by Docker DNS resolver in nginx.conf)
docker compose up -d --force-recreate --no-deps web
sleep 2
code=$(curl -sS -o /dev/null -w "%{http_code}" "http://127.0.0.1:5173/api/health" || true)
echo "web→api HTTP $code (404 from API is OK if /api/health is missing)"

echo "==> FamLedger deploy done"
docker compose ps
