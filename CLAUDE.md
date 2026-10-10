# CLAUDE.md — Lineamientos del proyecto

Companion app para campañas de Dungeons & Dragons 5e en sesiones **presenciales**.
Servidor ASP.NET Core autohospedado con Docker; cliente Flutter (Android).

## Regla de privacidad (obligatoria)

Este repositorio es público. **Nunca** incluir en código, documentación, seeds, tests, logs de
ejemplo, commits ni issues:

- correos electrónicos reales, nombres o apodos de personas reales;
- dominios, IPs, puertos expuestos o rutas de servidores reales;
- contraseñas, claves JWT, API keys, cadenas de conexión con credenciales.

Usar siempre valores ficticios (`admin@example.com`, `dnd.example.com`, `change-me`) y
mantenerlos en `deploy/.env.sample`. Los valores reales viven solo en `deploy/.env`, que está
en `.gitignore`.

## Estructura

```
server/   solución .NET: núcleo en src/Core (OpenTrpg.Core.*), módulo 5e en src/Systems/Dnd5e
          (OpenTrpg.Systems.Dnd5e.*, con seed/srd) y tests/
app/      proyecto Flutter (lib/core, lib/features/<feature>)
deploy/   docker-compose.yml, .env.sample, backup.sh, README.md (reverse proxy del operador)
docs/     PLAN.md (plan maestro) y ADR/ (decisiones de arquitectura)
```

## Comandos

```bash
# Servidor
cd server && dotnet build && dotnet test
cd server && dotnet run --project src/Core/OpenTrpg.Core.Api   # http://localhost:8080/swagger
cd server && dotnet ef migrations add <Nombre> -p src/Core/OpenTrpg.Core.Infrastructure -s src/Core/OpenTrpg.Core.Api

# Cliente
cd app && flutter pub get && flutter analyze && flutter test
cd app && flutter run   # la URL del servidor se configura dentro de la app (pantalla "Servidor")
cd app && flutter build apk --release --dart-define=API_BASE_URL=https://dnd.example.com

# Despliegue
cd deploy && cp .env.sample .env && docker compose pull && docker compose up -d
```

## Decisiones fijadas (ver docs/ADR)

- Reglas: **SRD 5.1 (2014)**. Contenido SRD en inglés, UI en **español**. Atribución CC-BY 4.0
  obligatoria en la app y en el README.
- Hoja de personaje **semiautomática**: la app calcula a partir de clase/raza/nivel; cualquier
  valor calculado puede sobreescribirse (`CharacterOverride`) y la UI lo marca.
- **Origen de cada punto**: todo valor con bonificación viaja con su desglose
  (`breakdowns` en la hoja, `attackBreakdown`/`damageBreakdown` en los ataques) y la UI lo muestra
  con un toque (`StatValue`). Nunca se muestra un `+X` sin poder explicarlo.
- **Tiempo real solo para eventos de campaña** (ADR 0006): hub SignalR en `/hubs/campaign` que
  notifica ids (`character.updated`, `party.rest`, `message.received`…); el cliente vuelve a pedir
  por HTTP. Dados privados con historial local.
- **Offline solo lectura**: la app cachea el último estado; las escrituras se deshabilitan sin red.
- Hosting: Docker Compose solo con `api` y `postgres`; la API escucha en un puerto del host y el
  operador pone su propio reverse proxy con TLS. La URL pública se configura con `App:PublicUrl`
  (obligatoria, `PUBLIC_URL` en `.env`) y es la que llevan los correos. El primer administrador se
  crea desde `https://<host>/admin` la primera vez.
  Email por **SMTP del operador** (465 TLS implícito o 587 STARTTLS; CA propia vía `SSL_CERT_FILE`).
- La app Android confía en los certificados de usuario del dispositivo (CA propia) y permite fijar
  la huella de un certificado por host.
- Distribución: APK directo; la API expone la última versión disponible.
- **Versionado semántico con GitVersion** (`GitVersion.yml`, ADR 0008): la CI calcula `X.Y.Z` en cada
  push a `master`, etiqueta `vX.Y.Z` y publica APK e imagen con esa versión. Para subir menor o
  mayor, `+semver: minor` / `+semver: major` en el mensaje de un commit. No editar la versión a mano.
- Documentación oficial: **solo el SRD en PDF** (CC-BY) se empaqueta. Nada con copyright de
  Wizards of the Coast en el repo. El administrador puede subir otros PDF a la biblioteca de la
  instancia y **paquetes de contenido** JSON (ADR 0007, `docs/content-packs.md`) que viven solo en
  su base de datos; `content-packs/` está en `.gitignore` y los ejemplos son ficticios.
- Fuentes e iconos empaquetados: Almendra, Cinzel e IM Fell English (títulos) y Source Sans 3,
  Atkinson Hyperlegible Next y Lora (texto), todas SIL OFL con su aviso de licencia en
  `app/assets/licenses/`, e iconos **propios** dibujados en `app/assets/icons/` (licencia del
  proyecto). Seis paletas con variante oscura y clara (`app/lib/core/theme/palettes.dart`, contraste
  AA verificado por test; Obsidiana y brasa por defecto, Grafito sin naranja); el usuario elige
  paleta, modo y fuentes en "Personalización". Ningún color fijo fuera de los tokens.
- Descansos corto y largo los **pide** el jugador y los aprueba el DM; el nivel lo **concede** el DM
  y el jugador completa el asistente de subida (PG por tirada física; dotes siempre disponibles).

## Modelo de permisos

- Roles globales: `Admin` (crea usuarios, gestiona biblioteca), `User`.
- Roles por campaña: `Owner` (único, transferible), `DM` (co-DMs nombrados por el Owner), `Player`.
- Los ítems de un personaje pertenecen a la campaña. El jugador solo los modifica mediante
  **operaciones**: comprar/vender en tienda abierta, equipar, usar consumible, atunement.
- **Auto-seguimiento en combate sin aprobación**: HP, HP temporales, death saves, slots, usos de
  recursos, condiciones, concentración, short/long rest.
- Todo lo demás (subir de nivel, editar stats, ítems a mano, ítems personalizados, oro fuera de
  compras) crea un `ChangeRequest` que el DM aprueba o rechaza. **El DM/Owner aplica directo y
  nunca pasa por aprobación dentro de su campaña**; además dispone de acciones de grupo
  (`/campaigns/{id}/party/*`) y mensajes secretos a personajes concretos.
- Un jugador solo ve la hoja de sus propios personajes.
- Vistas de campaña: General (todos), Mesa del DM (solo rol DM/Owner) y Mi sesión (solo Player).

## Convenciones de código

- .NET: Clean Architecture ligera. `Domain` sin dependencias; `Application` con casos de uso,
  DTOs y validación (FluentValidation); `Infrastructure` con EF Core/Npgsql, SMTP y ficheros;
  `Api` solo con endpoints, auth y hosting. Nunca exponer entidades EF en la API.
- Migraciones EF con nombre descriptivo en PascalCase. Seed del SRD idempotente.
- Tests: xUnit. Cálculos de hoja en `OpenTrpg.Systems.Dnd5e.Domain.Tests`; integración con SQLite en
  memoria en `OpenTrpg.Core.Api.Tests` (y `OpenTrpg.Systems.Dnd5e.Api.Tests` para las rutas del módulo).
- Flutter: `lib/core` (http, auth, caché, tema, i18n) y `lib/features/<feature>/{data,domain,ui}`.
  Riverpod para estado, `dio` para HTTP, `drift` para caché. Textos de UI en `lib/l10n`.
- Nombres de código, entidades y endpoints en inglés; textos visibles al usuario en español.
- Commits en imperativo, descriptivos, sin identificadores de modelos de IA.

## Uso de modelos

Ver "Asignación de modelos por tarea" en `docs/PLAN.md`. La sesión principal (Fable) diseña y
revisa; la implementación se delega a subagentes Opus/Sonnet/Haiku según esa tabla, siempre con
un contrato escrito en `docs/specs/<fase>.md`.
