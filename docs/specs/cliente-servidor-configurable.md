# Cliente — URL del servidor configurable (LAN y VPN)

Contrato cerrado. Solo cliente. Se implementa tras la fase 5 y aplica a todas las pantallas.

## Reglas

- La URL base de la API **no es fija**: se guarda en el dispositivo (`shared_preferences`, clave
  `server.baseUrl`) y se puede cambiar en cualquier momento desde la app. El `--dart-define
  API_BASE_URL` queda solo como valor inicial sugerido la primera vez (por defecto vacío en
  release; `http://10.0.2.2:8080` en debug).
- Casos de uso objetivo: servidor en LAN (`http://192.168.1.50:8080`), por VPN
  (`http://10.8.0.1:8080` o nombre interno) y por dominio público con HTTPS. Por tanto **HTTP sin
  TLS está permitido** también en release (`android:usesCleartextTraffic="true"` en el manifest
  principal; se retira el ajuste duplicado del manifest de debug).
- Certificados HTTPS autofirmados: no se aceptan por defecto. Se ofrece un interruptor avanzado
  "Confiar en el certificado de este servidor" que, al activarse, descarga el certificado actual,
  muestra su huella SHA-256 para que el usuario la confirme y la **fija** (pinning) solo para ese
  host; nunca se desactiva la verificación de forma global.
- Al cambiar de servidor se cierra la sesión y se borran tokens y caché, porque pertenecen al
  servidor anterior.
- Si no hay URL configurada, la app arranca en la pantalla de servidor en lugar del login.

## Pantalla "Servidor" (`/server`)

- Campo URL (con `http://` o `https://`, se normaliza: sin barra final, esquema obligatorio, puerto
  opcional), botón **Probar conexión** que llama a `GET /api/v1/app/info` y muestra "Conectado a
  {name} v{version}" o el error en español (tiempo de espera, DNS, certificado no confiable con el
  atajo al interruptor de confianza, 404 "No parece un servidor de D&D Companion").
- Botón **Guardar y continuar**. Lista de **servidores recientes** (últimos 5) para cambiar con un
  toque, útil para alternar entre LAN y VPN.
- Acceso: enlace "Cambiar servidor" en el login (muestra la URL actual) y entrada "Servidor" en el
  menú de usuario del inicio.

## Implementación

- `ServerConfig` (Riverpod): `baseUrl`, `recentUrls`, `trustedFingerprints {host: sha256}`;
  `ServerConfigRepository` sobre `shared_preferences`.
- `apiClientProvider` pasa a depender de `serverConfigProvider`: al cambiar la URL se recrea el
  `Dio` con la nueva `baseUrl` y, si hay huella fijada para ese host, un `HttpClient` con
  `badCertificateCallback` que compara la huella del certificado presentado (`sha256` de
  `X509Certificate.der`) solo para ese host.
- `AuthController`: al cambiar de servidor, `logout()` local (sin llamar al servidor anterior) y
  limpieza de `TokenStorage`.
- Router: redirect a `/server` cuando `baseUrl` está vacío; `/server` accesible sin sesión.
- Tests: normalización de URL (añade esquema, quita barra final, rechaza texto inválido); probar
  conexión muestra el nombre del servidor con repositorio falso; cambiar servidor cierra sesión; la
  lista de recientes no repite entradas y mantiene 5.
