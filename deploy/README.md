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
cp .env.sample .env      # rellenar todas las variables (ninguna tiene valor por defecto)
docker compose pull      # descarga la imagen de la API indicada en API_IMAGE
docker compose up -d
docker compose ps        # api y postgres deben quedar "healthy"
curl http://127.0.0.1:8080/health/ready
```

- `JWT_SECRET`: genera uno con `openssl rand -base64 48`. Debe tener al menos 32 caracteres.
- **URL pública**: la decide tu reverse proxy. La API toma el esquema y el host de las cabeceras
  `X-Forwarded-Proto` y `X-Forwarded-Host` que envía tu proxy y los usa en los enlaces de los correos. Recuerda el último origen visto, así los recordatorios que se envían en
  segundo plano también llevan la URL correcta. `App__PublicUrl` queda como respaldo opcional para los
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
  `Warning`: no bajes el nivel de log a `Error` en el primer arranque. Si caduca, usa «He olvidado mi
  contraseña» (necesita SMTP).
- Las **migraciones de base de datos se aplican solas** al arrancar la API
  (`Database__AutoMigrate=true`), igual que la importación del catálogo SRD.

## Variables de `.env`

| Variable | Para qué sirve |
|----------|----------------|
| `API_IMAGE` | Imagen de la API en GitHub Packages (`:latest` de la CI en `master` o `:1.2.0` de una release). Para una compilada en local: `docker build -t dnd-companion-api:local ../server` y `API_IMAGE=dnd-companion-api:local` |
| `API_BIND`, `API_PORT` | Dirección y puerto del host en los que escucha la API (`127.0.0.1` solo para un proxy local; `0.0.0.0` para exponerla en la LAN/VPN) |
| `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | Base de datos |
| `DB_AUTO_MIGRATE` | Aplicar las migraciones al arrancar la API (`true`) |
| `ASPNETCORE_ENVIRONMENT` | `Production` (logs JSON) o `Development` (logs legibles, más detalle) |
| `ADMIN_EMAIL` | Correo del administrador inicial |
| `JWT_SECRET` | Firma de los tokens y de los enlaces de asistencia (mínimo 32 caracteres) |
| `DEFAULT_TIME_ZONE` | Zona horaria IANA de las campañas nuevas (cada campaña puede cambiarla en sus ajustes) |
| `SMTP_HOST`, `SMTP_PORT`, `SMTP_USERNAME`, `SMTP_PASSWORD`, `SMTP_FROM_ADDRESS` | Correo saliente. Puerto `465` = TLS implícito, `587` = STARTTLS (se detecta por el puerto). El remitente debe estar autorizado para la cuenta |
| `CA_BUNDLE_HOST_PATH`, `SSL_CERT_FILE` | Fichero PEM del host con tu CA propia y la ruta donde se monta en el contenedor, a la que apunta `SSL_CERT_FILE` (deja el bundle del sistema si no tienes CA propia) |

### Ajustes opcionales

Tienen un valor razonable dentro de la imagen. Para cambiarlos, añade la variable de entorno al
servicio `api` del `docker-compose.yml`:

| Variable de entorno | Por defecto | Para qué sirve |
|---------------------|-------------|----------------|
| `Logging__LogLevel__Default` | `Information` | Nivel mínimo de logs: `Trace`, `Debug`, `Information`, `Warning`, `Error` |
| `FileStorage__MaxUploadMegabytes` | `200` | Tamaño máximo de subida (ver [Límites de subida](#límites-de-subida)) |
| `Reminders__Enabled`, `Reminders__PollSeconds` | `true`, `60` | Envío de recordatorios de sesión y frecuencia de comprobación |
| `Smtp__Security` | `Auto` | `Auto`, `SslOnConnect`, `StartTls` o `None` |
| `Smtp__CheckCertificateRevocation` | `true` | `false` solo si tu CA privada no publica CRL/OCSP y el envío falla por revocación |
| `Smtp__FromName` | `D&D Companion` | Nombre del remitente |
| `App__PublicUrl` | vacío | Respaldo de la URL pública para correos enviados antes de la primera petición por el proxy |
| `SSL_CERT_DIR` | — | Alternativa a `SSL_CERT_FILE`: directorio de PEM procesado con `openssl rehash` |
| `BACKUP_RETENTION_DAYS` (en `.env`) | `14` | Días que `backup.sh` conserva las copias |

Cada recordatorio se calcula en la zona horaria de su campaña y los avisos (24 h y 2 h antes por
defecto) los ajusta el DM en los ajustes de la campaña.

## Correo con tu propio SMTP

La API envía los correos de alta, recuperación de contraseña y recordatorios con MailKit. Con
`SMTP_PORT=465` conecta con TLS implícito; con `587` usa STARTTLS.

Si tu servidor de correo presenta un certificado firmado por **tu propia CA**, el contenedor debe
confiar en ella. .NET en Linux respeta las variables estándar de OpenSSL, así que basta con montar
el fichero y apuntar la variable:

```bash
# .env
CA_BUNDLE_HOST_PATH=/ruta/a/tu/ca.pem
```

El Compose lo monta en la ruta de `SSL_CERT_FILE`.

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

    # Igual o mayor que el límite de subida de la API (200 MB por defecto).
    client_max_body_size 200m;

    location / {
        proxy_pass         http://127.0.0.1:8080;
        proxy_http_version 1.1;
        # Host y X-Forwarded-* son los que la API usa para construir los enlaces de los correos.
        proxy_set_header   Host              $http_host;
        proxy_set_header   X-Forwarded-Host  $http_host;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Real-IP         $remote_addr;
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

# Imagen publicada en GitHub Packages (ghcr.io/<propietario>/dnd-companion-api):
#   - :latest y :sha-<commit>      → la CI las publica en cada push a master
#   - :1.2.0 y :latest             → las publica el workflow "Release"
# Cambia API_IMAGE en .env si quieres otra etiqueta y luego:
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

  El nivel se controla con `Logging__LogLevel__Default` en el servicio `api`; tras cambiarlo,
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

El tamaño máximo de un fichero (mapa, PDF, APK) es de 200 MB por defecto y se aplica
en dos sitios que deben coincidir:

1. **API**: `FileStorage__MaxUploadMegabytes` en el servicio `api` del Compose (200 por defecto). Responde `413` por encima.
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

Las releases de GitHub (pestaña **Releases**) contienen solo el APK:

- **`vX.Y.Z-build.N`**: la CI la crea en cada push a `master`, con el APK firmado con el keystore de
  los *secrets* del repositorio (sin keystore, firma de depuración y marcada como prerelease). El
  mismo APK queda también como artefacto de la ejecución.
- **`vX.Y.Z`**: el workflow manual **Release** (`.github/workflows/release.yml`, Actions → Release →
  Run workflow con la versión). Además de la release, publica la imagen Docker `:X.Y.Z` en GHCR y, si
  están definidos los *secrets* `DND_API_URL` y `DND_ADMIN_TOKEN` (o `DND_ADMIN_EMAIL` y
  `DND_ADMIN_PASSWORD`), sube el APK a tu servidor para que la app avise de la actualización.

La versión `X.Y.Z` sale de `version:` en `app/pubspec.yaml`.

Android no actualiza una app instalada con un APK firmado con otra clave: al pasar de la clave de
depuración a tu keystore (o viceversa) hay que desinstalar antes.

Para publicar a mano en el servidor un APK ya compilado:

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
| `ANDROID_KEYSTORE_BASE64` | (recomendado) Keystore de firma en base64 (`base64 -w0 upload-keystore.jks`). Sin él se firma con la clave de depuración |
| `ANDROID_KEYSTORE_PASSWORD` | Contraseña del keystore |
| `ANDROID_KEY_ALIAS` | Alias de la clave |
| `ANDROID_KEY_PASSWORD` | Contraseña de la clave |
| `DND_API_URL` | (opcional) URL de tu servidor, por ejemplo `https://dnd.example.com` |
| `DND_ADMIN_TOKEN` | (opcional) Token de acceso de un administrador; caduca a los 15 minutos |
| `DND_ADMIN_EMAIL`, `DND_ADMIN_PASSWORD` | (opcional) Alternativa a `DND_ADMIN_TOKEN`: el workflow inicia sesión y obtiene el token |

Crear el keystore una sola vez:

```bash
keytool -genkeypair -v -keystore upload-keystore.jks -alias upload -keyalg RSA -keysize 2048 -validity 10000
base64 -w0 upload-keystore.jks   # valor de ANDROID_KEYSTORE_BASE64
```

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

