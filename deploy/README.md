# Despliegue y operación

Guía para alojar tu propia instancia del servidor con Docker Compose: `postgres` (datos) y `api`
(ASP.NET Core). La API escucha directamente en un puerto del host y **tú pones delante tu propio
reverse proxy** (nginx, Caddy, Traefik...) con TLS; el Compose no levanta ninguno. Todos los dominios y correos de los ejemplos
(`dnd.example.com`, `admin@example.com`) son ficticios: sustitúyelos por los tuyos en `deploy/.env`,
que está en `.gitignore` y nunca debe subirse al repositorio.

## Requisitos

- Un host Linux con **Docker Engine 24+** y el plugin **Docker Compose v2** (`docker compose version`).
- 1 vCPU, 1 GB de RAM y espacio para la base de datos, los ficheros subidos (mapas, PDF, APK) y las copias.
- Un reverse proxy propio con TLS (ver [Reverse proxy](#reverse-proxy)) o una LAN/VPN de
  confianza donde sirvas la API por HTTP.
- Un servidor SMTP para los correos de alta, recuperación de contraseña y recordatorios de sesión
  (cualquiera: tu proveedor de correo, un relay propio...).

## Primer arranque

```bash
cd deploy
cp .env.example .env     # rellenar POSTGRES_PASSWORD, JWT_SECRET, ADMIN_EMAIL y SMTP_*
docker compose up -d --build
docker compose ps        # api y postgres deben quedar "healthy"
curl http://127.0.0.1:8080/health/ready
```

- `JWT_SECRET`: genera uno con `openssl rand -base64 48`. Debe tener al menos 32 caracteres.
- **URL pública**: la decide tu reverse proxy. La API toma el esquema y el host de las cabeceras
  `X-Forwarded-Proto` y `X-Forwarded-Host` (solo si llegan desde `TRUSTED_PROXY_*`) y los usa en los
  enlaces de los correos. Recuerda el último origen visto, así los recordatorios que se envían en
  segundo plano también llevan la URL correcta. `PUBLIC_URL` queda como respaldo opcional para los
  correos enviados antes de la primera petición a través del proxy (p. ej. el del administrador
  inicial): si no lo informas, ese primer enlace sale en los logs como ruta relativa y basta con
  anteponerle tu URL.
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

## Variables de `.env`

| Variable | Para qué sirve | Por defecto |
|----------|----------------|-------------|
| `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | Base de datos (la contraseña es obligatoria) | `dnd`, `dnd`, — |
| `PUBLIC_URL` | Respaldo opcional de la URL pública; normalmente vacío (la aporta tu proxy) | vacío |
| `TRUSTED_PROXY_0..3` | Redes (CIDR) desde las que se aceptan las cabeceras `X-Forwarded-*` | loopback y redes privadas |
| `ALLOWED_HOSTS` | Dominio(s) admitidos en la cabecera `Host`; ponlo al de tu proxy para que nadie pueda forjar enlaces | `*` |
| `ADMIN_EMAIL` | Correo del administrador inicial | `admin@example.com` |
| `JWT_SECRET` | Firma de los tokens y enlaces de asistencia (obligatoria) | — |
| `MAX_UPLOAD_MB` | Tamaño máximo de subida en la API (ver [Límites de subida](#límites-de-subida)) | `200` |
| `DEFAULT_TIME_ZONE` | Zona horaria IANA de las campañas nuevas (cada campaña puede cambiarla en sus ajustes) | `Europe/Madrid` |
| `REMINDERS_ENABLED` | Activa el envío de recordatorios de sesión por correo | `true` |
| `REMINDERS_POLL_SECONDS` | Cada cuántos segundos busca recordatorios pendientes | `60` |
| `LOG_LEVEL` | Nivel mínimo de logs: `Trace`, `Debug`, `Information`, `Warning`, `Error` | `Information` |
| `SMTP_HOST`, `SMTP_PORT`, `SMTP_SECURITY`, `SMTP_USERNAME`, `SMTP_PASSWORD`, `SMTP_FROM_ADDRESS`, `SMTP_FROM_NAME` | Correo saliente. `SMTP_SECURITY`: `Auto` (465 → TLS implícito, 587 → STARTTLS), `SslOnConnect`, `StartTls` o `None` | puerto `465`, `Auto` |
| `SMTP_CHECK_REVOCATION` | Comprobar revocación del certificado del SMTP; `false` solo si tu CA no publica CRL/OCSP | `true` |
| `SSL_CERT_FILE` / `SSL_CERT_DIR` | CA propia para el SMTP u otras conexiones TLS salientes (variables estándar de OpenSSL que .NET respeta) | bundle del sistema |
| `API_BIND`, `API_PORT` | Dirección y puerto del host en los que escucha la API (`127.0.0.1` solo para un proxy local; `0.0.0.0` para exponerla en la LAN/VPN) | `127.0.0.1`, `8080` |
| `API_IMAGE` | Imagen de la API (por defecto se construye desde el código) | `dnd-companion-api:local` |
| `BACKUP_RETENTION_DAYS` | Días que `backup.sh` conserva las copias | `14` |

Cada recordatorio se calcula en la zona horaria de su campaña y los avisos (24 h y 2 h antes por
defecto) los ajusta el DM en los ajustes de la campaña. Con `REMINDERS_ENABLED=false` no se envía
ninguno.

## Correo con tu propio SMTP

La API envía los correos de alta, recuperación de contraseña y recordatorios con MailKit. Con
`SMTP_PORT=465` y `SMTP_SECURITY=Auto` conecta con TLS implícito; con `587` usa STARTTLS.

Si tu servidor de correo presenta un certificado firmado por **tu propia CA**, el contenedor debe
confiar en ella. .NET en Linux respeta las variables estándar de OpenSSL, así que basta con montar
el fichero y apuntar la variable:

```yaml
# docker-compose.yml (servicio api)
    environment:
      SSL_CERT_FILE: /certs/ca.pem        # un bundle PEM; las CA públicas siguen cargándose del directorio por defecto
    volumes:
      - ./certs/ca.pem:/certs/ca.pem:ro
```

Alternativa con directorio: `SSL_CERT_DIR=/certs` con los PEM procesados por `openssl rehash /certs`
(los nombres deben ser los hashes que genera ese comando). Nunca se desactiva la validación del
certificado.

Prueba rápida tras arrancar: crea un usuario desde la app o pide «He olvidado mi contraseña»; si el
envío falla, `docker compose logs api | grep -i smtp` muestra el motivo.

## Reverse proxy

La API escucha en `http://API_BIND:API_PORT` (por defecto `127.0.0.1:8080`). Un solo bloque en tu
nginx la expone con TLS; los certificados los gestionas tú como con cualquier otro sitio (certbot,
tu CA interna, etc.):

```nginx
server {
    listen 443 ssl;
    http2 on;
    server_name dnd.example.com;

    ssl_certificate     /ruta/a/fullchain.pem;
    ssl_certificate_key /ruta/a/privkey.pem;

    # Igual o mayor que MAX_UPLOAD_MB (mapas, PDF y APK).
    client_max_body_size 200m;

    location / {
        proxy_pass         http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Real-IP         $remote_addr;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_read_timeout 300s;
        proxy_send_timeout 300s;
    }
}
```

Todo va bajo `location /`: además de `/api`, la API sirve en la raíz las páginas de contraseña
(`/set-password`) y de asistencia (`/sessions/{id}`) y Swagger (`/swagger`). Si el proxy corre en otra
máquina, pon `API_BIND=0.0.0.0` y apunta `proxy_pass` a la IP del host.

**CA propia o certificado autofirmado**: la app Android confía en los certificados de usuario del
dispositivo. Instala tu CA en Ajustes → Seguridad → Credenciales de usuario (o fija la huella del
certificado desde la pantalla «Servidor» de la app). Con Caddy basta `reverse_proxy 127.0.0.1:8080`
dentro de su bloque de sitio.

**LAN o VPN sin TLS**: puedes prescindir del proxy y exponer la API directamente con
`API_BIND=0.0.0.0`; sin proxy, la API usa el `Host` de cada petición (p. ej. `http://192.168.1.50:8080`)
para los enlaces. No lo hagas en Internet: contraseñas y
tokens viajarían en claro.

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
2. **Tu reverse proxy**: `client_max_body_size` en nginx (Caddy no limita por defecto). El proxy
   responde `413` antes de que la petición llegue a la API.

Para cambiarlo, edita ambos y aplica: `docker compose up -d` y recarga tu proxy. Deja siempre el
proxy con el mismo valor o más que la API.

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

