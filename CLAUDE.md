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
mantenerlos en `deploy/.env.example`. Los valores reales viven solo en `deploy/.env`, que está
en `.gitignore`.

## Estructura

```
server/   solución .NET (Dnd.Domain, Dnd.Application, Dnd.Infrastructure, Dnd.Api, tests/)
app/      proyecto Flutter (lib/core, lib/features/<feature>)
deploy/   docker-compose.yml, nginx/, .env.example, backup.sh
docs/     PLAN.md (plan maestro) y ADR/ (decisiones de arquitectura)
```

## Comandos

```bash
# Servidor
cd server && dotnet build && dotnet test
cd server && dotnet run --project src/Dnd.Api          # http://localhost:8080/swagger
cd server && dotnet ef migrations add <Nombre> -p src/Dnd.Infrastructure -s src/Dnd.Api

# Cliente
cd app && flutter pub get && flutter analyze && flutter test
cd app && flutter run   # la URL del servidor se configura dentro de la app (pantalla "Servidor")
cd app && flutter build apk --release --dart-define=API_BASE_URL=https://dnd.example.com

# Despliegue
cd deploy && cp .env.example .env && docker compose up -d --build
cd deploy && docker compose --profile dev up -d        # añade MailHog para probar correos
```

## Decisiones fijadas (ver docs/ADR)

- Reglas: **SRD 5.1 (2014)**. Contenido SRD en inglés, UI en **español**. Atribución CC-BY 4.0
  obligatoria en la app y en el README.
- Hoja de personaje **semiautomática**: la app calcula a partir de clase/raza/nivel; cualquier
  valor calculado puede sobreescribirse (`CharacterOverride`) y la UI lo marca.
- **Sin tiempo real**: dados privados con historial local. No usar SignalR.
- **Offline solo lectura**: la app cachea el último estado; las escrituras se deshabilitan sin red.
- Hosting: Docker Compose + Nginx. Email por **SMTP genérico** configurado por variables de entorno.
- Distribución: APK directo; la API expone la última versión disponible.
- Documentación oficial: **solo el SRD en PDF** (CC-BY) se empaqueta. Nada con copyright de
  Wizards of the Coast en el repo. El administrador puede subir otros PDF a la biblioteca de la
  instancia.

## Modelo de permisos

- Roles globales: `Admin` (crea usuarios, gestiona biblioteca), `User`.
- Roles por campaña: `Owner` (único, transferible), `DM` (co-DMs nombrados por el Owner), `Player`.
- Los ítems de un personaje pertenecen a la campaña. El jugador solo los modifica mediante
  **operaciones**: comprar/vender en tienda abierta, equipar, usar consumible, atunement.
- **Auto-seguimiento en combate sin aprobación**: HP, HP temporales, death saves, slots, usos de
  recursos, condiciones, concentración, short/long rest. (Supuesto; si el usuario pide aprobación
  también aquí, se añaden esos tipos a `ChangeRequest`.)
- Todo lo demás (subir de nivel, editar stats, ítems a mano, ítems personalizados, oro fuera de
  compras) crea un `ChangeRequest` que el DM aprueba o rechaza. El DM/Owner aplica directo.

## Convenciones de código

- .NET: Clean Architecture ligera. `Domain` sin dependencias; `Application` con casos de uso,
  DTOs y validación (FluentValidation); `Infrastructure` con EF Core/Npgsql, SMTP y ficheros;
  `Api` solo con endpoints, auth y hosting. Nunca exponer entidades EF en la API.
- Migraciones EF con nombre descriptivo en PascalCase. Seed del SRD idempotente.
- Tests: xUnit. Cálculos de hoja en `Dnd.Domain.Tests`; integración con SQLite en memoria en
  `Dnd.Api.Tests`.
- Flutter: `lib/core` (http, auth, caché, tema, i18n) y `lib/features/<feature>/{data,domain,ui}`.
  Riverpod para estado, `dio` para HTTP, `drift` para caché. Textos de UI en `lib/l10n`.
- Nombres de código, entidades y endpoints en inglés; textos visibles al usuario en español.
- Commits en imperativo, descriptivos, sin identificadores de modelos de IA.

## Uso de modelos

Ver "Asignación de modelos por tarea" en `docs/PLAN.md`. La sesión principal (Fable) diseña y
revisa; la implementación se delega a subagentes Opus/Sonnet/Haiku según esa tabla, siempre con
un contrato escrito en `docs/specs/<fase>.md`.
