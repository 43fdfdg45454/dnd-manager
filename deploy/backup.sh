#!/usr/bin/env bash
# Volcado diario de PostgreSQL. Programar en el cron del host, por ejemplo:
#   0 4 * * * /ruta/al/repo/deploy/backup.sh
set -euo pipefail

cd "$(dirname "$0")"
set -a; source .env; set +a

RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-14}"
mkdir -p backups
stamp="$(date +%Y%m%d-%H%M%S)"
file="backups/dnd-${stamp}.sql.gz"

docker compose exec -T postgres pg_dump -U "${POSTGRES_USER:-dnd}" "${POSTGRES_DB:-dnd}" | gzip > "$file"
echo "Backup escrito en $file"

find backups -name 'dnd-*.sql.gz' -mtime "+${RETENTION_DAYS}" -delete
