# Despliegue y operación

Guía para alojar tu propia instancia del servidor con Docker Compose: `postgres` (datos), `api`
(ASP.NET Core) y `nginx` (proxy con TLS). Todos los dominios y correos de los ejemplos
(`dnd.example.com`, `admin@example.com`) son ficticios: sustitúyelos por los tuyos en `deploy/.env`,
que está en `.gitignore` y nunca debe subirse al repositorio.

## Requisitos

- Un host Linux con **Docker Engine 24+** y el plugin **Docker Compose v2** (`docker compose version`).
- 1 vCPU, 1 GB de RAM y espacio para la base de datos, los ficheros subidos (mapas, PDF, APK) y las copias.
- Un nombre de DNS apuntando al host y los puertos 80 y 443 libres (o una LAN/VPN, ver
  [Sin TLS](#lan-o-vpn-sin-tls)).
- Un servidor SMTP para los correos de alta, recuperación de contraseña y recordatorios de sesión
  (cualquiera: tu proveedor de correo, un relay propio...).

## Primer arranque

```bash
cd deploy
cp .env.example .env     # rellenar POSTGRES_PASSWORD, JWT_SECRET, PUBLIC_URL, ADMIN_EMAIL y SMTP_*
mkdir -p certs           # fullchain.pem y privkey.pem (ver "Certificados")
docker compose up -d --build
docker compose ps        # api y postgres deben quedar "healthy"
curl -k https://localhost/health/ready
```

- `JWT_SECRET`: genera uno con `openssl rand -base64 48`. Debe tener al menos 32 caracteres.
- `PUBLIC_URL`: la URL pública (`https://dnd.example.com`). Se usa en los enlaces de los correos.
- **Administrador inicial**: cuando la base de datos no tiene usuarios, la API crea un administrador
  con `ADMIN_EMAIL`, le envía el correo de alta y además escribe el enlace para fijar su contraseña en
  los logs (válido 48 h), por si el SMTP aún no funciona:

  ```bash
  docker compose logs api | grep "Initial admin" | grep -o 'https\?://[^"\\ ]*set-password?token=[A-Za-z0-9_-]*' | head -n 1
  ```

  Abre ese enlace, elige tu contraseña e inicia sesión en la app. El enlace se escribe con nivel
  `Warning`: no pongas `LOG_LEVEL=Error` en el primer arranque. Si caduca, usa «He olvidado mi
  contraseña» (necesita SMTP).
- Las **migraciones de base de datos se aplican solas** al arrancar la API
  (`Database__AutoMigrate=true`), igual que la importación del catálogo SRD.
- Perfil de desarrollo con bandeja de correo: `docker compose --profile dev up -d` y abrir
  `http://localhost:8025`. En `.env` deja `SMTP_HOST=mailhog` y `SMTP_PORT=1025`.

## Variables de `.env`

| Variable | Para qué sirve | Por defecto |
|----------|----------------|-------------|
| `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | Base de datos (la contraseña es obligatoria) | `dnd`, `dnd`, — |
| `PUBLIC_URL` | URL pública, para los enlaces de los correos | `https://localhost` |
| `ADMIN_EMAIL` | Correo del administrador inicial | `admin@example.com` |
| `JWT_SECRET` | Firma de los tokens y enlaces de asistencia (obligatoria) | — |
| `MAX_UPLOAD_MB` | Tamaño máximo de subida en la API (ver [Límites de subida](#límites-de-subida)) | `200` |
| `DEFAULT_TIME_ZONE` | Zona horaria IANA de las campañas nuevas (cada campaña puede cambiarla en sus ajustes) | `Europe/Madrid` |
| `REMINDERS_ENABLED` | Activa el envío de recordatorios de sesión por correo | `true` |
| `REMINDERS_POLL_SECONDS` | Cada cuántos segundos busca recordatorios pendientes | `60` |
| `LOG_LEVEL` | Nivel mínimo de logs: `Trace`, `Debug`, `Information`, `Warning`, `Error` | `Information` |
| `SMTP_HOST`, `SMTP_PORT`, `SMTP_USE_STARTTLS`, `SMTP_USERNAME`, `SMTP_PASSWORD`, `SMTP_FROM_ADDRESS`, `SMTP_FROM_NAME` | Correo saliente | — |
| `HTTP_PORT`, `HTTPS_PORT` | Puertos publicados por nginx | `80`, `443` |
| `API_IMAGE` | Imagen de la API (por defecto se construye desde el código) | `dnd-companion-api:local` |
| `NGINX_CONF` | Configuración de nginx (`./nginx/nginx.http.conf` para LAN/VPN sin TLS) | `./nginx/nginx.conf` |
| `BACKUP_RETENTION_DAYS` | Días que `backup.sh` conserva las copias | `14` |

Cada recordatorio se calcula en la zona horaria de su campaña y los avisos (24 h y 2 h antes por
defecto) los ajusta el DM en los ajustes de la campaña. Con `REMINDERS_ENABLED=false` no se envía
ninguno.

## Certificados

nginx espera `deploy/certs/fullchain.pem` y `deploy/certs/privkey.pem` (la carpeta está en
`.gitignore`).

### Opción A: Let's Encrypt con certbot en el host

Con el DNS ya apuntando al host. nginx ocupa el puerto 80, así que certbot lo para mientras valida:

```bash
sudo certbot certonly --standalone -d dnd.example.com \
  --pre-hook "docker compose -f /ruta/al/repo/deploy/docker-compose.yml stop nginx" \
  --post-hook "docker compose -f /ruta/al/repo/deploy/docker-compose.yml start nginx"

sudo cp /etc/letsencrypt/live/dnd.example.com/fullchain.pem /ruta/al/repo/deploy/certs/
sudo cp /etc/letsencrypt/live/dnd.example.com/privkey.pem   /ruta/al/repo/deploy/certs/
docker compose up -d
```

Los hooks se guardan en la configuración de renovación de certbot, de modo que `certbot renew`
(el temporizador del paquete lo ejecuta dos veces al día) también para y arranca nginx. Después de
cada renovación hay que copiar los certificados; automatízalo con un hook de despliegue:

```bash
sudo tee /etc/letsencrypt/renewal-hooks/deploy/dnd-companion.sh > /dev/null <<'HOOK'
#!/usr/bin/env bash
set -euo pipefail
cp /etc/letsencrypt/live/dnd.example.com/fullchain.pem /ruta/al/repo/deploy/certs/
cp /etc/letsencrypt/live/dnd.example.com/privkey.pem   /ruta/al/repo/deploy/certs/
HOOK
sudo chmod +x /etc/letsencrypt/renewal-hooks/deploy/dnd-companion.sh
```

### Opción B: certificado manual

Copia a `deploy/certs/` el certificado completo (con la cadena intermedia) y la clave privada que te
haya dado tu proveedor, con esos dos nombres. Para una prueba rápida puedes generar uno
autofirmado (la app Android **no** lo aceptará; sirve para `curl -k`):

```bash
mkdir -p certs
openssl req -x509 -nodes -newkey rsa:2048 -days 365 \
  -keyout certs/privkey.pem -out certs/fullchain.pem -subj "/CN=dnd.example.com"
```

Tras cambiar los certificados: `docker compose restart nginx`.

## LAN o VPN sin TLS

Para una red de confianza (casa, VPN tipo WireGuard) puedes prescindir de certificados con la
variante `nginx/nginx.http.conf`: escucha solo en el puerto 80 y no redirige a HTTPS. **No la uses
expuesta a Internet**: contraseñas y tokens viajarían en claro.

```bash
# En .env
NGINX_CONF=./nginx/nginx.http.conf
PUBLIC_URL=http://192.168.1.50        # el host o la IP con la que se accede desde la LAN/VPN
HTTP_PORT=80

docker compose up -d
curl http://192.168.1.50/health/ready
```

`PUBLIC_URL` debe empezar por `http://` para que los enlaces de los correos funcionen. En la app,
configura esa misma URL en la pantalla «Servidor».

## Actualización

```bash
cd deploy
./backup.sh                         # siempre antes de actualizar: las migraciones no son reversibles
git pull

# Construyendo desde el código
docker compose up -d --build

# O con la imagen publicada por el workflow "Release" (ghcr.io/<propietario>/dnd-companion-api)
#   en .env:  API_IMAGE=ghcr.io/<propietario>/dnd-companion-api:1.2.0
docker compose pull api
docker compose up -d
```

La API aplica las **migraciones automáticamente** al arrancar; `docker compose logs api` muestra el
resultado y `docker compose ps` debe volver a indicar `healthy`. Si el paquete de GHCR es privado,
haz `docker login ghcr.io` antes con un token de acceso personal con permiso `read:packages`.

## Logs y salud

- Los logs de la API salen por consola en **JSON compacto** (un evento por línea) con `RequestId`
  y `TraceId` en cada evento de una petición; las peticiones se resumen en una línea
  (`RequestMethod`, `RequestPath`, `StatusCode`, `Elapsed`). Las sondas `/health*` no se registran.
  Se ven con `docker compose logs -f api`, por ejemplo con `jq`:

  ```bash
  docker compose logs --no-log-prefix api | jq -c 'select(.StatusCode >= 500)'
  docker compose logs --no-log-prefix api | jq -c 'select(.RequestId == "0HNXXXXXXXXXX")'
  ```

  El nivel se controla con `LOG_LEVEL` (`Logging__LogLevel__Default` en la API); tras cambiarlo,
  `docker compose up -d api`.
- `GET /health`: la API responde (liveness). `GET /health/ready`: además PostgreSQL responde
  (readiness); es el `healthcheck` del servicio `api` en el compose.
- Estadísticas para el administrador: `GET /api/v1/admin/stats` devuelve la versión de la API, nº de
  usuarios (activos y totales), campañas, personajes, sesiones programadas, ficheros (cantidad y
  bytes) y la última release del APK:

  ```bash
  curl -fsS -H "Authorization: Bearer $TOKEN" https://dnd.example.com/api/v1/admin/stats
  ```

  (`$TOKEN` se obtiene como se explica en [Publicar una versión del APK](#publicar-una-versión-del-apk).)

## Copias de seguridad

`./backup.sh` guarda en `deploy/backups/`:

- `dnd-<fecha>.sql.gz`: volcado de PostgreSQL (`pg_dump`).
- `files-<fecha>.tgz`: contenido del volumen de ficheros (mapas, retratos, PDF, APK), con
  `docker run --rm -v dnd-companion_files:/data ... tar czf`. El volumen se llama
  `<proyecto>_files` y el proyecto es `dnd-companion` (campo `name` del compose); si lo has
  renombrado, exporta `FILES_VOLUME=<nombre>` (consulta `docker volume ls`).

Borra las copias de más de 14 días (`BACKUP_RETENTION_DAYS`). Prográmalo en el cron del host y copia
`backups/` fuera de la máquina (otro disco, `rsync`, almacenamiento de objetos):

```cron
0 4 * * * /ruta/al/repo/deploy/backup.sh >> /var/log/dnd-backup.log 2>&1
```

### Restauración

Elige una pareja de copias (`dnd-X.sql.gz` y `files-X.tgz`, de la misma fecha). Para volver al
estado de la copia sobre una instalación en marcha:

```bash
cd deploy
POSTGRES_USER=dnd POSTGRES_DB=dnd        # los valores de tu .env
docker compose stop api

# Base de datos: recrearla y cargar el volcado
docker compose exec -T postgres psql -U "$POSTGRES_USER" -d postgres \
  -c "DROP DATABASE IF EXISTS $POSTGRES_DB" -c "CREATE DATABASE $POSTGRES_DB"
gunzip -c backups/dnd-X.sql.gz | docker compose exec -T postgres psql -U "$POSTGRES_USER" "$POSTGRES_DB"

# Ficheros: vaciar el volumen y extraer la copia (como root; la imagen usa el UID 1654)
docker run --rm -v dnd-companion_files:/data -v "$PWD/backups:/backup" alpine:3.20 \
  sh -c 'find /data -mindepth 1 -delete && tar xzf /backup/files-X.tgz -C /data && chown -R 1654:1654 /data'

docker compose up -d
```

En una máquina nueva: haz primero el [primer arranque](#primer-arranque) hasta `docker compose up -d`,
restaura con los comandos anteriores y comprueba con `docker compose ps` y `GET /health/ready`. Usa la
misma `JWT_SECRET` que la instalación original si quieres conservar los enlaces de asistencia ya
enviados por correo.

## Rotación de `JWT_SECRET`

```bash
openssl rand -base64 48            # copiar el valor a JWT_SECRET en .env
docker compose up -d api           # recrea la API con el secreto nuevo
```

Con el secreto nuevo dejan de valer los tokens de acceso emitidos (15 minutos de vida; la app
obtiene otros automáticamente con su token de refresco) y los **enlaces de asistencia de los
correos ya enviados** (los recordatorios nuevos llevarán enlaces válidos). Para cerrar además todas
las sesiones y obligar a iniciar sesión de nuevo:

```bash
docker compose exec -T postgres psql -U dnd dnd -c 'DELETE FROM "RefreshTokens"'
```

Rota el secreto si sospechas que se ha filtrado y, de forma periódica, en las fechas que decidas.

## Límites de subida

El tamaño máximo de un fichero (mapa, PDF, APK) es `MAX_UPLOAD_MB` (200 MB por defecto) y se aplica
en dos sitios que deben coincidir:

1. **API**: `MAX_UPLOAD_MB` en `.env` (`FileStorage__MaxUploadMegabytes`). Responde `413` por encima.
2. **nginx**: `client_max_body_size` en `nginx/nginx.conf` (y en `nginx/nginx.http.conf` si lo usas).
   nginx responde `413` antes de que la petición llegue a la API.

Para cambiarlo, edita ambos y aplica: `docker compose up -d` y `docker compose restart nginx`. Deja
siempre nginx con el mismo valor o más que la API.

## SRD en PDF para la biblioteca

El PDF del SRD 5.1 (CC-BY 4.0) no se incluye en el repositorio. Para que aparezca como documento del
sistema en la biblioteca, descárgalo de la fuente oficial, nómbralo `SRD_CC_v5.1.pdf` y cópialo al
volumen de ficheros, dentro de `system/`:

```bash
docker compose exec api mkdir -p /data/files/system
docker compose cp ./SRD_CC_v5.1.pdf api:/data/files/system/SRD_CC_v5.1.pdf
docker compose restart api          # se registra al arrancar; es idempotente
```

El archivo debe ser legible por todos (`chmod 644` antes de copiarlo). Los documentos del sistema no se
pueden borrar desde la app.

## Publicar una versión del APK

La app comprueba `GET /api/v1/app/latest` y, si hay un `buildNumber` mayor que el suyo, ofrece
descargar `GET /api/v1/app/download/{buildNumber}` (anónimo, para poder instalarlo desde el navegador).

El workflow manual **Release** (`.github/workflows/release.yml`) compila el APK firmado, lo adjunta a
una *release* de GitHub, publica la imagen Docker en GHCR y, si están definidos los *secrets*
`DND_API_URL` y `DND_ADMIN_TOKEN` (o `DND_ADMIN_EMAIL` y `DND_ADMIN_PASSWORD`), lo sube también a tu
servidor. Para hacerlo a mano con un APK ya compilado:

```bash
URL=https://dnd.example.com
# El token de acceso dura 15 minutos: pídelo justo antes de usarlo.
TOKEN=$(curl -fsS -X POST "$URL/api/v1/auth/login" \
  -H 'Content-Type: application/json' \
  -d '{"email":"admin@example.com","password":"..."}' | jq -r .accessToken)

curl --fail-with-body -X POST "$URL/api/v1/admin/releases" \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@dnd-companion-1.2.0.apk;type=application/vnd.android.package-archive" \
  --form-string "version=1.2.0" \
  --form-string "buildNumber=12" \
  --form-string "notes=Mejoras en la hoja de personaje" \
  --form-string "isMandatory=false"
```

- `version` es semver simple (`1.2.0`) y `buildNumber` un entero que crece con cada compilación
  (el workflow usa el número de ejecución). Ambos deben ser únicos: si se repiten, responde `409`.
- Con `isMandatory=true` la app bloquea su uso hasta actualizar.
- También se acepta JSON con el `fileId` de un APK subido antes con `kind=AppRelease` a
  `POST /api/v1/files`.
- `GET /api/v1/admin/releases` lista las releases y `DELETE /api/v1/admin/releases/{id}` borra una
  (con su APK). `GET /api/v1/app/latest` responde `204` si no hay ninguna.
- El límite de subida de la sección anterior también afecta al APK.

### Secrets del repositorio para el workflow Release

| Secret | Contenido |
|--------|-----------|
| `ANDROID_KEYSTORE_BASE64` | Keystore de firma en base64 (`base64 -w0 upload-keystore.jks`) |
| `ANDROID_KEYSTORE_PASSWORD` | Contraseña del keystore |
| `ANDROID_KEY_ALIAS` | Alias de la clave |
| `ANDROID_KEY_PASSWORD` | Contraseña de la clave |
| `DND_API_URL` | (opcional) URL de tu servidor, por ejemplo `https://dnd.example.com` |
| `DND_ADMIN_TOKEN` | (opcional) Token de acceso de un administrador; caduca a los 15 minutos |
| `DND_ADMIN_EMAIL`, `DND_ADMIN_PASSWORD` | (opcional) Alternativa a `DND_ADMIN_TOKEN`: el workflow inicia sesión y obtiene el token |

Guarda el keystore y sus contraseñas fuera del repositorio: si lo pierdes no podrás publicar
actualizaciones que Android acepte sobre la app ya instalada.

### Firma del APK en Gradle

El workflow escribe `app/android/key.properties` (ignorado por git) y el keystore en
`app/android/app/upload-keystore.jks`, y falla si `app/android/app/build.gradle.kts` no lee ese
fichero. La configuración de firma `release` de ese fichero debe ser:

```kotlin
import java.io.FileInputStream
import java.util.Properties

val keystoreProperties = Properties()
val keystorePropertiesFile = rootProject.file("key.properties")
if (keystorePropertiesFile.exists()) {
    keystoreProperties.load(FileInputStream(keystorePropertiesFile))
}

android {
    // ...
    signingConfigs {
        create("release") {
            keyAlias = keystoreProperties["keyAlias"] as String?
            keyPassword = keystoreProperties["keyPassword"] as String?
            storeFile = (keystoreProperties["storeFile"] as String?)?.let { file(it) }
            storePassword = keystoreProperties["storePassword"] as String?
        }
    }
    buildTypes {
        release {
            // Sin key.properties (compilaciones locales) se firma con la clave de depuración.
            signingConfig = if (keystorePropertiesFile.exists()) {
                signingConfigs.getByName("release")
            } else {
                signingConfigs.getByName("debug")
            }
        }
    }
}
```

(`import` va al principio del fichero, antes de `plugins { ... }`.) Para generar un keystore:
`keytool -genkey -v -keystore upload-keystore.jks -keyalg RSA -keysize 2048 -validity 10000 -alias upload`.

