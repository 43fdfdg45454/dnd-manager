#!/usr/bin/env bash
# Copia de seguridad de PostgreSQL (dnd-<fecha>.sql.gz) y del volumen de ficheros (files-<fecha>.tgz).
# Programar en el cron del host, por ejemplo:
#   0 4 * * * /ruta/al/repo/deploy/backup.sh
# Las copias se guardan en deploy/backups y se borran pasados BACKUP_RETENTION_DAYS días (14 por defecto).
set -euo pipefail

cd "$(dirname "$0")"

# Lee un valor de .env sin ejecutarlo como script (valores como "D&D Companion" romperían "source").
env_value() {
  sed -n "s/^$1=//p" .env 2> /dev/null | tail -n 1 | sed -e 's/^"\(.*\)"$/\1/' -e "s/^'\(.*\)'\$/\1/"
}
POSTGRES_USER="${POSTGRES_USER:-$(env_value POSTGRES_USER)}"
POSTGRES_DB="${POSTGRES_DB:-$(env_value POSTGRES_DB)}"
BACKUP_RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-$(env_value BACKUP_RETENTION_DAYS)}"

RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-14}"
# Compose llama al volumen <proyecto>_<volumen>: el proyecto es "opentrpg" (campo name del compose).
FILES_VOLUME="${FILES_VOLUME:-opentrpg_files}"

if ! docker volume inspect "$FILES_VOLUME" > /dev/null 2>&1; then
  echo "No existe el volumen $FILES_VOLUME. Revisa FILES_VOLUME (docker volume ls)." >&2
  exit 1
fi

mkdir -p backups
stamp="$(date +%Y%m%d-%H%M%S)"
db_file="backups/dnd-${stamp}.sql.gz"
files_file="backups/files-${stamp}.tgz"

# Se escribe en un fichero temporal y se renombra al final: una copia a medias nunca parece válida.
docker compose exec -T postgres pg_dump -U "${POSTGRES_USER:-dnd}" "${POSTGRES_DB:-dnd}" | gzip > "${db_file}.tmp"
mv "${db_file}.tmp" "$db_file"
echo "Backup de la base de datos escrito en $db_file"

docker run --rm \
  -v "${FILES_VOLUME}:/data:ro" \
  -v "$PWD/backups:/backup" \
  alpine:3.20 tar czf "/backup/files-${stamp}.tgz.tmp" -C /data .
mv "${files_file}.tmp" "$files_file"
echo "Backup de los ficheros escrito en $files_file"

find backups \( -name 'dnd-*.sql.gz' -o -name 'files-*.tgz' \) -mtime "+${RETENTION_DAYS}" -delete
