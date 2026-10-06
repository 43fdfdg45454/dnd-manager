# Fase 10 — Arreglos rápidos, `App:PublicUrl` obligatoria y bootstrap del admin en `/admin`

Contrato cerrado. Dos bloques independientes (servidor y cliente) que pueden implementarse en
paralelo; ninguno de los dos hace commits.

## Servidor

### `App:PublicUrl` obligatoria

- `AppOptions.PublicUrl` pasa a ser `string` obligatoria: URL absoluta `http(s)`, sin ruta, query
  ni userinfo, sin barra final (se normaliza con `Trim().TrimEnd('/')`). Validación en
  `DependencyInjection.cs` con `ValidateOnStart`: mensaje
  `App:PublicUrl is required and must be an absolute http(s) URL without path, for example https://dnd.example.com.`
- `IPublicUrlProvider.GetOriginAsync` devuelve siempre esa URL; `CurrentOrigin` desaparece de la
  interfaz (o devuelve la misma URL) y se eliminan **por completo**: `PublicOriginStore`,
  `PublicOriginMiddleware` (`UsePublicOriginTracking`), `PublicOrigin`, `InstanceSetting`,
  `IInstanceSettingsRepository`, `InstanceSettingsRepository`, `InstanceSettingConfiguration`, el
  `DbSet` en `AppDbContext`, y se añade migración `DropInstanceSettings` (borra la tabla).
  `PublicLinkBuilder` ya no genera rutas relativas ni avisos.
- `UseForwardedHeaders` se mantiene tal cual (esquema/host correctos en logs).
- `appsettings.Development.json` fija `"PublicUrl": "http://localhost:8080"`. `appsettings.json`
  no trae valor (obligatorio por entorno).

### Bootstrap del admin en `/admin`

- Se eliminan `InitialAdminSeeder`, `AppOptions.InitialAdminEmail`, `AppOptions.SeedInitialAdmin`,
  su registro en DI y la llamada en `StartupTasks`. No queda ningún enlace de alta en los logs.
- Nuevo `server/src/Dnd.Application/Setup/`:
  - `SetupStatusDto(bool NeedsSetup)`; `GetSetupStatusHandler` → `NeedsSetup = !users.Any`.
  - `CreateInitialAdminRequest(string Email, string DisplayName, string Password)` con validador
    FluentValidation (email válido y ≤ `User.EmailMaxLength`; nombre no vacío ≤
    `User.DisplayNameMaxLength`; contraseña según `PasswordPolicy`, mismos mensajes que
    `SetPassword`).
  - `CreateInitialAdminHandler`: si ya existe algún usuario → `AppException.Conflict("La instancia ya está configurada.")`.
    Crea `User.Create(email, displayName, UserRole.Admin, now)`, fija la contraseña con
    `IPasswordHasher` (reutilizar el método de dominio que usa `SetPasswordHandler`) y guarda.
    Carrera: `private static readonly SemaphoreSlim Gate = new(1, 1)` alrededor de comprobación +
    guardado. Devuelve `UserDto`.
- Endpoints en `server/src/Dnd.Api/Endpoints/SetupEndpoints.cs`, grupo `/api/v1/setup`, anónimos:
  - `GET /status` → `200 { needsSetup }`.
  - `POST /admin` → `201 UserDto` la primera vez; `409` después; `400` por validación.
- Página `wwwroot/admin.html` servida por `GET /admin` en `PageEndpoints` (mismo patrón que
  `set-password.html`: `Cache-Control: no-store`, `ExcludeFromDescription`, texto en español,
  CSS inline, JS vanilla sin dependencias). Al cargar llama a `GET /api/v1/setup/status`:
  - `needsSetup = true`: formulario "Crear administrador" con correo, nombre, contraseña y
    repetición (mínimo `PasswordPolicy.MinLength`), envía a `POST /api/v1/setup/admin` y muestra
    "Administrador creado. Ya puedes iniciar sesión en la app de D&D Companion."
  - `needsSetup = false`: "Esta instancia ya está configurada. La administración se hace desde la app."
  - Error de red o 409: mensaje claro.
- Tests (`server/tests/Dnd.Api.Tests/SetupEndpointsTests.cs`), con `ApiFactory`:
  - estado inicial `needsSetup = true`; `POST` → 201 y login con ese correo/contraseña funciona y
    es Admin; segundo `POST` → 409; `status` ahora `false`.
  - contraseña corta → 400 con mensaje en español.
  - `GET /admin` → 200 `text/html` y `Cache-Control: no-store`.
- Tests existentes a adaptar: `ApiFactory` configura `App:PublicUrl=https://dnd.example.com` (ya
  no hay modo "sin URL"); `InitialAdminTests` se elimina; `PublicUrlTests`: sustituir los tests de
  respaldo/origen persistido por `Startup_fails_without_App_PublicUrl`,
  `Startup_fails_when_App_PublicUrl_has_a_path` y `Email_links_use_App_PublicUrl` (un correo de alta
  creado por un admin usa `https://dnd.example.com/set-password?token=…`). Cualquier test que
  dependa de `InstanceSettings` u origen por cabeceras se elimina o reescribe.
  **Nota:** `ApiFactory` ya no debe crear un admin inicial por configuración; si los tests
  necesitan un admin, debe existir un helper que lo cree directamente (revisar cómo lo hacen hoy y
  mantener el comportamiento para no reescribir cientos de tests: si hoy dependen del seeder,
  crear el admin en el helper con `User.Create` + hash de contraseña).

### Consumibles del SRD

- `SrdDataset.MapMagicItem`: los ítems cuya `equipment_category`/nombre sea poción (`Potion`),
  pergamino (`Scroll`, `Spell Scroll`), aceite (`Oil`), elixir, o munición mágica (`Arrow`,
  `Bolt`, `Ammunition`) pasan a `ItemCategory.Consumable`. En `MapEquipment`, la munición normal
  (categoría `ammunition`) también es `Consumable`.
- Subir `SrdDataset.Version` (nuevo sufijo con la fecha de hoy) para que el seeder reimporte.
- `CatalogSeedTests`: total de ítems sigue 599; añadir aserción de que existe al menos un
  `Consumable` (`potion-of-healing`) y que el conteo de no mágicos sigue siendo 237.

### Deploy y documentación

- `deploy/docker-compose.yml`: `App__PublicUrl: ${PUBLIC_URL}`; quitar `App__InitialAdminEmail`.
- `deploy/.env.sample`: añadir `PUBLIC_URL=https://dnd.example.com` (comentario: URL pública con
  la que los usuarios llegan a la API a través del reverse proxy; se usa en los correos); quitar
  `ADMIN_EMAIL`.
- `deploy/README.md`: "Primer arranque" explica abrir `https://dnd.example.com/admin` para crear
  el administrador (solo la primera vez); tabla de variables con `PUBLIC_URL` y sin `ADMIN_EMAIL`;
  quitar de "Ajustes opcionales" la fila `App__PublicUrl`; quitar toda mención al enlace en logs y
  al origen recordado. Mantener valores ficticios.
- `CLAUDE.md`: la frase de hosting pasa a "La URL pública se configura con `App:PublicUrl`
  (obligatoria, `PUBLIC_URL` en `.env`) y es la que llevan los correos. El primer administrador se
  crea desde `https://<host>/admin` la primera vez."
- `README.md` raíz: en "Puesta en marcha rápida" añadir el paso de abrir `/admin`.

## Cliente

- `app/lib/features/catalog/data/models.dart`: `Feature` gana `level` (int, 0 si no viene).
  `Subclass.fromJson` lee `levels[]` (`{level, features[]}`) y aplana a `features` ordenados por
  nivel, asignando `level` a cada rasgo; conserva compatibilidad si viniera `features` directo.
  `class_detail_page.dart`: la subclase muestra los rasgos agrupados por nivel ("Nivel N").
- `app/lib/features/items/ui/homebrew_tab.dart`: `initialSource: ItemSource.all`,
  `showSourceFilter: true`; título de la pestaña y texto vacío adaptados ("No hay objetos que
  coincidan"). El botón "Nuevo objeto" sigue creando homebrew.
- `app/lib/features/characters/ui/sheet_editor_page.dart`: el aviso "Los cambios de un personaje
  activo se envían al DM para su aprobación." solo cuando el usuario **no** es DM/Owner (usar la
  información de rol ya disponible en la página; si no está, obtenerla de
  `campaignDetailControllerProvider`). `maxLength` de la nota de override: 500.
- Tests Flutter:
  - `catalog_test.dart`: `Subclass.fromJson` con el JSON real del servidor (`levels[].features`)
    produce rasgos con nivel; la página de clase muestra "Nivel 2" y el rasgo.
  - `items_test.dart`: la pestaña de objetos de la campaña lista ítems SRD con el conmutador de
    fuente visible.
  - `characters_test.dart`: el aviso de aprobación no aparece para el DM y sí para el jugador.
- `flutter analyze` sin avisos y `flutter test` en verde.
