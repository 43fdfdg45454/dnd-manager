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
- `PUBLIC_URL` (obligatoria): la URL pública con la que tus usuarios llegan a la API a través del
  reverse proxy, por ejemplo `https://dnd.example.com` (esquema `http` o `https`, sin ruta ni barra
  final). Es la que llevan los enlaces de los correos (alta, contraseña, recordatorios). La API no
  arranca sin ella.
- **Administrador inicial**: cuando la base de datos no tiene usuarios, abre
  `https://dnd.example.com/admin` (tu `PUBLIC_URL` más `/admin`), rellena correo, nombre y contraseña
  y pulsa «Crear administrador». Solo funciona **la primera vez**: en cuanto existe un usuario, la
  página indica que la instancia ya está configurada. Después inicia sesión en la app con esos datos y
  crea al resto de usuarios desde ella. Hazlo nada más arrancar la instancia: hasta que exista el primer
  usuario, esa página está abierta a quien llegue a la URL.
- Las **migraciones de base de datos se aplican solas** al arrancar la API
  (`Database__AutoMigrate=true`), igual que la importación del catálogo SRD.

## Variables de `.env`

| Variable | Para qué sirve |
|----------|----------------|
| `API_IMAGE` | Imagen de la API en GitHub Packages (`:latest` o una versión fija `:X.Y.Z`, ambas publicadas por la CI en cada push a `master`). Para una compilada en local: `docker build -t opentrpg-api:local ../server` y `API_IMAGE=opentrpg-api:local` |
| `API_BIND`, `API_PORT` | Dirección y puerto del host en los que escucha la API (`127.0.0.1` solo para un proxy local; `0.0.0.0` para exponerla en la LAN/VPN) |
| `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | Base de datos |
| `DB_AUTO_MIGRATE` | Aplicar las migraciones al arrancar la API (`true`) |
| `ASPNETCORE_ENVIRONMENT` | `Production` (logs JSON) o `Development` (logs legibles, más detalle) |
| `PUBLIC_URL` | URL pública de la API tras tu reverse proxy (obligatoria, sin ruta ni barra final). Se usa en los enlaces de los correos |
| `JWT_SECRET` | Firma de los tokens y de los enlaces de asistencia (mínimo 32 caracteres) |
| `DEFAULT_TIME_ZONE` | Zona horaria IANA de las campañas nuevas (cada campaña puede cambiarla en sus ajustes) |
| `SMTP_HOST`, `SMTP_PORT`, `SMTP_USERNAME`, `SMTP_PASSWORD`, `SMTP_FROM_ADDRESS` | Correo saliente. Puerto `465` = TLS implícito, `587` = STARTTLS (se detecta por el puerto). El remitente debe estar autorizado para la cuenta |
| `SMTP_CHECK_REVOCATION` | Comprobar revocación (CRL/OCSP) del certificado del SMTP. `false` con una CA propia que no publica CRL (si no, el envío falla con `unable to get certificate CRL`); la cadena y el host se validan siempre |
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
| `Smtp__FromName` | `OpenTRPG` | Nombre del remitente |
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
# En el bloque http (p. ej. /etc/nginx/conf.d/websocket-map.conf): "upgrade" solo cuando el cliente
# lo pide, para que el resto de peticiones sigan cerrando la conexión con normalidad.
map $http_upgrade $connection_upgrade {
    default upgrade;
    ''      close;
}

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
        # La API usa Host y X-Forwarded-* para el esquema y el host correctos en sus logs;
        # los enlaces de los correos salen de PUBLIC_URL.
        proxy_set_header   Host              $http_host;
        proxy_set_header   X-Forwarded-Host  $http_host;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Real-IP         $remote_addr;
        proxy_read_timeout 300s;
        proxy_send_timeout 300s;
    }

    # Hub de tiempo real (SignalR) en su propia ruta: WebSocket con tiempos de espera largos.
    # Si tu nginx es anterior a 1.25 y no soporta WebSocket sobre HTTP/2, no pasa nada: el cliente
    # negocia HTTP/1.1 para esta ruta.
    location /hubs/ {
        proxy_pass         http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header   Host              $http_host;
        proxy_set_header   X-Forwarded-Host  $http_host;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Real-IP         $remote_addr;
        proxy_set_header   Upgrade           $http_upgrade;
        proxy_set_header   Connection        $connection_upgrade;
        proxy_buffering    off;
        proxy_read_timeout 3600s;
        proxy_send_timeout 3600s;
    }
}
```

Todo va bajo `location /`: además de `/api`, la API sirve en la raíz las páginas de contraseña
(`/set-password`), de asistencia (`/sessions/{id}`) y de creación del primer administrador (`/admin`), y Swagger (`/swagger`). Si el proxy corre en otra
máquina, pon `API_BIND=0.0.0.0` y apunta `proxy_pass` a la IP del host.

**Tiempo real**: la app recibe los eventos de la campaña (descansos, daño, mensajes del DM...) por
SignalR en `/hubs/campaign`. Las cabeceras `Upgrade`/`Connection` de arriba permiten WebSocket; si el
proxy no lo negocia, SignalR cae solo a Server-Sent Events o long polling, que funcionan sin tocar
nada pero con más latencia y peticiones. Caddy reenvía WebSocket sin configuración extra.

**Límites de peticiones y diagnóstico**: cada usuario puede tener 5 conexiones abiertas al hub a la
vez (`Realtime__MaxConnectionsPerUser`) y 30 intentos de conexión por minuto (`RateLimits__Hub__PermitLimit`,
por usuario o por IP si no hay sesión; los sondeos de una conexión ya abierta no cuentan). El login
admite 10 intentos por minuto por IP (`RateLimits__Login__PermitLimit`) y otros 10 por cuenta, sea cual
sea la IP (`RateLimits__LoginAccount__PermitLimit`); la ventana se cambia con `…__WindowSeconds`. Al
superarlos la API responde `429` con `Retry-After`. El límite por IP usa la IP que manda el proxy en
`X-Forwarded-For` (sin esa cabecera todos los clientes compartirían la IP del proxy); como esa cabecera
se puede falsear, el límite por cuenta es el que protege de verdad cada contraseña. Para cambiar un
valor, añade la variable al bloque `environment` de `api` en `docker-compose.yml`. La pantalla
«Servidor → Probar conexión» de la app comprueba la API, la sesión, el hub a través del proxy y el
transporte usado (`GET /api/v1/realtime/status`); si no es WebSockets, revisa `Upgrade`/`Connection`.

**CA propia o certificado autofirmado**: la app Android confía en los certificados de usuario del
dispositivo. Instala tu CA en Ajustes → Seguridad → Credenciales de usuario (o fija la huella del
certificado desde la pantalla «Servidor» de la app). Con Caddy basta `reverse_proxy 127.0.0.1:8080`
dentro de su bloque de sitio.

**LAN o VPN sin TLS**: puedes prescindir del proxy y exponer la API directamente con
`API_BIND=0.0.0.0`; sin proxy, pon en `PUBLIC_URL` esa dirección (p. ej. `http://192.168.1.50:8080`)
para los enlaces. No lo hagas en Internet: contraseñas y
tokens viajarían en claro.

## Actualización

```bash
cd deploy
./backup.sh                         # siempre antes de actualizar: las migraciones no son reversibles
git pull

# Imagen publicada en GitHub Packages (ghcr.io/<propietario>/opentrpg-api):
#   - :latest, :X.Y.Z y :sha-<commit> → la CI las publica en cada push a master
#     (X.Y.Z es la versión semántica que calcula GitVersion; ver "Versionado")
# Cambia API_IMAGE en .env si quieres otra etiqueta y luego:
docker compose pull api
docker compose up -d
```

La API aplica las **migraciones automáticamente** al arrancar; `docker compose logs api` muestra el
resultado y `docker compose ps` debe volver a indicar `healthy`. Si el paquete de GHCR es privado,
haz `docker login ghcr.io` antes con un token de acceso personal con permiso `read:packages`.

## Nombre anterior

El proyecto se llamaba antes `dnd-companion`. La imagen antigua ya no se publica y el proyecto de
Compose (y con él los volúmenes) se llama `opentrpg`; una instalación con el nombre antiguo se vuelve
a crear desde cero.

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
  `docker run --rm -v opentrpg_files:/data ... tar czf`. El volumen se llama
  `<proyecto>_files` y el proyecto es `opentrpg` (campo `name` del compose); si lo has
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
docker run --rm -v opentrpg_files:/data -v "$PWD/backups:/backup" alpine:3.20 \
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

## Paquetes de contenido

El repositorio y la imagen solo traen el SRD 5.1. Para usar en tu instancia subclases, objetos,
conjuros, razas o trasfondos de material que poseas, crea un **paquete de contenido** JSON con el
formato de [`docs/content-packs.md`](../docs/content-packs.md) e impórtalo como administrador:

- **Desde la app**: Administración → Contenido → "Importar paquete". La lista muestra la versión, la
  fecha y los recuentos; si el paquete tiene errores, se listan con su ruta
  (`items[3].modifiers[0].kind: ...`) y no se importa nada.
- **Con `curl`** (con `$URL` y `$TOKEN` como en [Publicar una versión del APK](#publicar-una-versión-del-apk)):

  ```bash
  curl --fail-with-body -X POST "$URL/api/v1/admin/content-packs" \
    -H "Authorization: Bearer $TOKEN" -F "file=@reinos-ejemplo.json;type=application/json"
  ```

Reimportar un paquete con el mismo `id` reemplaza su contenido (los objetos conservan su
identificador, así que los inventarios no se rompen). Borrarlo quita su contenido del catálogo; los
personajes que lo usaban siguen cargando y la ficha lo marca como "contenido no disponible".

Los paquetes viven en la base de datos: la [copia de seguridad](#copias-de-seguridad) los incluye y
se restauran con ella. Guarda los ficheros JSON fuera del repositorio (la carpeta `content-packs/`
está en `.gitignore`) y **no los subas nunca** a un repositorio público: su contenido suele tener
copyright.

## Publicar una versión del APK

La app comprueba `GET /api/v1/app/latest` y, si hay un `buildNumber` mayor que el suyo, ofrece
descargar `GET /api/v1/app/download/{buildNumber}` (anónimo, para poder instalarlo desde el navegador).

Cada push a `master` produce, con la misma versión `X.Y.Z`:

- la release de GitHub **`vX.Y.Z`** (pestaña **Releases**) con el APK firmado con el keystore de los
  *secrets* (sin keystore, firma de depuración y marcada como prerelease); el mismo APK queda como
  artefacto de la ejecución;
- la imagen `ghcr.io/<propietario>/opentrpg-api:X.Y.Z` (y `:latest`), que responde esa versión
  en `GET /api/v1/app/info`;
- si están definidos los *secrets* `DND_API_URL` y `DND_ADMIN_TOKEN` (o `DND_ADMIN_EMAIL` y
  `DND_ADMIN_PASSWORD`), la subida del APK a tu servidor para que la app avise de la actualización
  (las notas son la primera línea del mensaje del commit).

### Versionado

Versión semántica (`MAYOR.MENOR.PARCHE`) calculada por **GitVersion** con `GitVersion.yml` en la
raíz del repositorio:

- Cada push a `master` sube el **parche** respecto a la última etiqueta `vX.Y.Z` (la crea la propia
  CI al publicar la release): `0.2.0` → `0.2.1` → `0.2.2`.
- Para subir la **menor** o la **mayor**, incluye en el mensaje de algún commit del push
  `+semver: minor` (o `feature`) o `+semver: major` (o `breaking`). `+semver: none` no incrementa.
- Las otras ramas calculan una pre-release con el nombre de la rama (`0.2.1-mi-rama.3`) y no publican.
- En local: `dotnet tool install -g GitVersion.Tool` y `dotnet-gitversion` desde la raíz.

En el APK, `versionName` es la versión semántica y `versionCode` (`buildNumber`) el número de
ejecución de la CI, que siempre crece; la app compara `buildNumber` para avisar de actualizaciones.

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
  -F "file=@opentrpg-1.2.0.apk;type=application/vnd.android.package-archive" \
  --form-string "version=1.2.0" \
  --form-string "buildNumber=12" \
  --form-string "notes=Mejoras en la hoja de personaje" \
  --form-string "isMandatory=false"
```

- `version` es semver simple (`1.2.0`) y `buildNumber` un entero que crece con cada compilación
  (la CI usa el número de ejecución). Ambos deben ser únicos: si se repiten, responde `409`.
- Con `isMandatory=true` la app bloquea su uso hasta actualizar.
- También se acepta JSON con el `fileId` de un APK subido antes con `kind=AppRelease` a
  `POST /api/v1/files`.
- `GET /api/v1/admin/releases` lista las releases y `DELETE /api/v1/admin/releases/{id}` borra una
  (con su APK). `GET /api/v1/app/latest` responde `204` si no hay ninguna.
- El límite de subida de la sección anterior también afecta al APK.

### Secrets del repositorio para la CI

| Secret | Contenido |
|--------|-----------|
| `ANDROID_KEYSTORE_BASE64` | (recomendado) Keystore de firma en base64 (`base64 -w0 upload-keystore.jks`). Sin él se firma con la clave de depuración |
| `ANDROID_KEYSTORE_PASSWORD` | Contraseña del keystore |
| `ANDROID_KEY_ALIAS` | Alias de la clave |
| `ANDROID_KEY_PASSWORD` | Contraseña de la clave |
| `DND_API_URL` | (opcional) URL de tu servidor, por ejemplo `https://dnd.example.com` |
| `DND_ADMIN_TOKEN` | (opcional) Token de acceso de un administrador; caduca a los 15 minutos |
| `DND_ADMIN_EMAIL`, `DND_ADMIN_PASSWORD` | (opcional) Alternativa a `DND_ADMIN_TOKEN`: la CI inicia sesión y obtiene el token |

Crear el keystore una sola vez:

```bash
keytool -genkeypair -v -keystore upload-keystore.jks -alias upload -keyalg RSA -keysize 2048 -validity 10000
base64 -w0 upload-keystore.jks   # valor de ANDROID_KEYSTORE_BASE64
```

Guarda el keystore y sus contraseñas fuera del repositorio: si lo pierdes no podrás publicar
actualizaciones que Android acepte sobre la app ya instalada.

### Firma del APK en Gradle

La CI escribe `app/android/key.properties` (ignorado por git) y el keystore en
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

