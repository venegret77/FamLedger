#!/usr/bin/env bash
set -euo pipefail

release=${1:?Pass the verified Git commit SHA}
[[ "$release" =~ ^[0-9a-f]{40}$ ]]
APP_DIR=${FAMLEDGER_APP_DIR:-"$HOME/FamLedger"}
cd "$APP_DIR"

export FAMLEDGER_ENVIRONMENT=Production
export FAMLEDGER_RELEASE="$release"

git fetch origin master
if [[ "$(git rev-parse origin/master)" != "$release" ]]; then
  echo "Skipping a superseded release."
  exit 0
fi

git diff --quiet
git diff --cached --quiet
git checkout --detach "$release"
docker compose config --quiet

check_web() {
  local html asset content_type
  curl -fsS --max-time 5 http://127.0.0.1:8080/health >/dev/null || return 1
  if ! curl -fsS --max-time 5 http://127.0.0.1:5173/api/health >/dev/null; then
    [[ "${1:-}" == rollback ]] || return 1
    local status
    status=$(curl -sS --max-time 5 -o /dev/null -w '%{http_code}' http://127.0.0.1:5173/api/auth/me) || return 1
    [[ "$status" == 401 ]] || return 1
  fi
  html=$(curl -fsS --max-time 5 http://127.0.0.1:5173/) || return 1
  asset=$(sed -n 's/.*type="module"[^>]*src="\([^"]*\)".*/\1/p' <<< "$html" | head -n1)
  [[ "$asset" =~ ^/assets/[a-zA-Z0-9._-]+\.js$ ]] || return 1
  content_type=$(curl -fsS --max-time 5 -o /dev/null -w '%{content_type}' "http://127.0.0.1:5173$asset") || return 1
  [[ "$content_type" == application/javascript* || "$content_type" == text/javascript* ]]
}

wait_for_web() {
  for ((attempt = 0; attempt < 30; attempt++)); do
    if check_web "$@"; then return 0; fi
    sleep 3
  done
  return 1
}

previous_available=true
for service in api bot web; do
  container=$(docker compose ps --status running -q "$service")
  if [[ -z "$container" ]]; then
    previous_available=false
    continue
  fi
  previous_image=$(docker inspect --format '{{.Image}}' "$container")
  docker tag "$previous_image" "famledger-$service:rollback"
done

docker compose build api bot web minio
if docker compose up -d --no-build api bot web && wait_for_web; then
  docker compose ps
  echo "Deployed $release"
  exit 0
fi

echo "Release failed its health checks." >&2
docker compose ps
docker compose logs --tail 50 api bot web || true
if [[ "$previous_available" == true ]]; then
  echo "Restarting the previous application images." >&2
  FAMLEDGER_RELEASE=rollback docker compose up -d --no-build --no-deps api bot web
  wait_for_web rollback || echo "Rollback health check failed; investigate on the server." >&2
fi
exit 1
