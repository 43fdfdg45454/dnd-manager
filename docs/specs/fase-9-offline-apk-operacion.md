# Fase 9 — Caché offline de lectura, actualización de APK y operación

Contrato cerrado.

## Caché offline (solo lectura)

- Dependencia `drift` (SQLite) con tabla genérica `cached_responses (key TEXT PK, body TEXT,
  fetched_at INTEGER)`; `key` = ruta + query. Sin esquema por entidad: se cachea el JSON ya
  parseado de las respuestas GET.
- `CachedRepository` mixin o decorador sobre `ApiClient`: en cada GET, (1) emite el valor cacheado
  si existe, (2) pide a red y, si responde, actualiza caché y re-emite; si falla por red, mantiene
  el cacheado y marca `isStale`. Las pantallas muestran una franja "Sin conexión · datos de
  {hace X}" cuando `isStale`.
- Qué se cachea: mis campañas, detalle de campaña, miembros, personajes de campaña, detalle de
  personaje (hoja + combate + inventario), catálogo (listas y detalles ya visitados), lore, mapas
  (JSON) e imágenes de mapas y retratos (`cached_network_image` con caché en disco), sesiones,
  biblioteca (metadatos; los PDF se descargan explícitamente).
- Escrituras: deshabilitadas sin red (botones en gris con tooltip "Necesitas conexión"). Se
  detecta con `connectivity_plus` y, además, por fallo de red en la última petición.
- Sesión: si al arrancar `/auth/me` falla por red pero hay refresh token y usuario cacheado, la app
  entra en modo `signedIn` con el usuario cacheado (cambia la decisión de la fase 1).
- Botón "Vaciar caché" en ajustes.

## Actualización de APK

- Servidor: `AppRelease` (Id, Version (semver), BuildNumber (int), FileId, Notes, IsMandatory,
  PublishedAt). Endpoints: `GET /api/v1/app/latest` (anónimo) → `{ version, buildNumber, notes,
  isMandatory, downloadUrl, sizeBytes }` o `204` si no hay; `POST /api/v1/admin/releases`
  (Admin, multipart o `fileId` + metadatos) → `201`; `GET /api/v1/admin/releases` lista. La descarga
  del APK es anónima (`GET /api/v1/app/download/{buildNumber}`) para poder instalarlo desde el
  navegador sin sesión.
- Cliente: al arrancar y una vez al día compara `buildNumber` con `package_info_plus`; si hay
  versión nueva muestra un diálogo con notas y botón "Descargar" (abre `downloadUrl` con
  `url_launcher`; Android instala el APK con el instalador del sistema). Si `isMandatory`, bloquea la
  app hasta actualizar.
- CI: workflow `release.yml` manual (`workflow_dispatch` con `version`) que compila el APK firmado
  con una clave de los *secrets* del repositorio (`ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASSWORD`,
  `ANDROID_KEY_ALIAS`, `ANDROID_KEY_PASSWORD`), lo adjunta a una *release* de GitHub y, si se
  configuran `DND_API_URL` y `DND_ADMIN_TOKEN`, lo publica en `/admin/releases`.

## Operación

- `deploy/README.md` completo: requisitos, primer arranque (admin inicial y enlace en logs),
  bloque de reverse proxy para el nginx del operador (la API escucha directamente), actualización
  (`docker compose pull/build`, migraciones automáticas), copias (`backup.sh` + volumen de ficheros
  con `tar`), restauración, rotación de `JWT_SECRET` (invalida sesiones), límites de subida,
  colocar el SRD en PDF en `system/`.
- `backup.sh` incluye también el volumen `files` (`docker run --rm -v ... tar`).
- Salud: `GET /health/ready` en el `healthcheck` de compose; panel admin muestra versión de la API,
  nº de usuarios, campañas y tamaño de ficheros (`GET /api/v1/admin/stats`).
- Logs: Serilog a consola en JSON con `RequestId`; nivel por variable `Logging__LogLevel__Default`.

## Pruebas

Cliente: repositorio cacheado devuelve el valor guardado cuando la red falla y marca `isStale`;
diálogo de actualización aparece cuando `buildNumber` remoto es mayor; `isMandatory` bloquea.
Servidor: `/app/latest` devuelve la última por `buildNumber`; subir release con versión repetida →
409; descarga anónima funciona y responde `Content-Disposition` con el nombre del APK.
