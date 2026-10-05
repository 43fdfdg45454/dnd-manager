# Despliegue

```bash
cp .env.example .env            # rellenar contraseñas, JWT_SECRET, SMTP y PUBLIC_URL
mkdir -p certs                  # fullchain.pem y privkey.pem (Let's Encrypt, certbot, etc.)
docker compose up -d --build
curl -k https://localhost/health
```

- Perfil de desarrollo con bandeja de correo: `docker compose --profile dev up -d` y abrir
  `http://localhost:8025`. En `.env` dejar `SMTP_HOST=mailhog` y `SMTP_PORT=1025`.
- Copias de seguridad: `./backup.sh` (programar en cron). Se guardan en `backups/` y se borran a
  los 14 días (`BACKUP_RETENTION_DAYS`).
- Restaurar: `gunzip -c backups/dnd-XXXX.sql.gz | docker compose exec -T postgres psql -U dnd dnd`.
- Los ficheros subidos (mapas, PDF, APK) viven en el volumen `files`.
