#!/usr/bin/env bash
set -euo pipefail
umask 077

: "${FAMLEDGER_BACKUP_DIR:?Set a private directory for staging backups}"
: "${RESTIC_REPOSITORY:?Set an off-host restic repository}"

cd "${FAMLEDGER_APP_DIR:-"$HOME/FamLedger"}"
mkdir -p -- "$FAMLEDGER_BACKUP_DIR"
snapshot=$(mktemp -d "$FAMLEDGER_BACKUP_DIR/famledger-$(date -u +%Y%m%dT%H%M%SZ).XXXXXX")
mkdir -p "$snapshot/minio"

docker compose exec -T postgres sh -ec \
  'exec pg_dump --format=custom --no-owner -U "$POSTGRES_USER" -d "$POSTGRES_DB"' > "$snapshot/postgres.dump"
docker compose exec -T postgres pg_restore --list < "$snapshot/postgres.dump" >/dev/null
FAMLEDGER_MINIO_EXPORT_DIR="$snapshot/minio" FAMLEDGER_BACKUP_UID="$(id -u)" FAMLEDGER_BACKUP_GID="$(id -g)" \
  docker compose --profile backup run --rm --no-deps minio-export
restic backup --tag famledger "$snapshot"
echo "Off-host backup saved. Local checkpoint: $snapshot"
