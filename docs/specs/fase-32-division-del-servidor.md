# Fase 32 — División del servidor en núcleo y módulo 5e

Fase mecánica (ADR 0009): mover, no reescribir. Parte del inventario y de los contratos de
`docs/specs/fase-30-inventario-y-contrato.md` (§1 clasificación fichero a fichero con el reparto de
cada Mixto, §3 `Character` del núcleo, §4 `IGameSystem`, §4.5 migraciones, §4.6 alias de rutas, §7
lista de comprobación "Fase 32"). La API que ve la app instalada **no cambia** en ninguna entrega:
mismas rutas (las antiguas como alias en B), mismo JSON, misma base de datos (sin migración de datos).

Se entrega en **tres partes**, cada una un PR que deja `master` desplegable con los tests verdes:

| Parte | Qué | Resultado |
|---|---|---|
| **32A** | Proyectos nuevos, traslado de ficheros núcleo y 5e, reparto de los Mixtos, `IGameSystem` completo delegando en los servicios actuales | Solución con `OpenTrpg.Core.*` y `OpenTrpg.Systems.Dnd5e.*`; rutas y JSON idénticos; modelo EF idéntico |
| **32B** | Endpoints 5e bajo `/api/v1/systems/dnd5e/...` con alias de las rutas actuales | Tabla de alias de la fase 30 §4.6 cumplida y probada |
| **32C** | Renombrado de imagen Docker, APK, nombres visibles de build, documentación; el propietario renombra el repositorio en GitHub | `ghcr.io/<owner>/opentrpg-api`, `opentrpg-X.Y.Z.apk` |

## 32A. Proyectos y traslado

### Estructura

```
server/
  OpenTrpg.slnx
  src/Core/OpenTrpg.Core.Domain
  src/Core/OpenTrpg.Core.Application
  src/Core/OpenTrpg.Core.Infrastructure
  src/Core/OpenTrpg.Core.Api            (proyecto web: Program.cs, hosting, auth, endpoints del núcleo)
  src/Systems/Dnd5e/OpenTrpg.Systems.Dnd5e.Domain
  src/Systems/Dnd5e/OpenTrpg.Systems.Dnd5e.Application
  src/Systems/Dnd5e/OpenTrpg.Systems.Dnd5e.Infrastructure   (configuraciones EF del módulo, SrdDataset, seed, paquetes)
  src/Systems/Dnd5e/OpenTrpg.Systems.Dnd5e.Api               (biblioteca con los endpoints 5e: `MapDnd5eEndpoints`)
  src/Systems/Dnd5e/seed/srd/…                               (antes server/seed/srd)
  tests/OpenTrpg.Core.Domain.Tests, OpenTrpg.Core.Api.Tests
  tests/OpenTrpg.Systems.Dnd5e.Domain.Tests, OpenTrpg.Systems.Dnd5e.Api.Tests
```

Dependencias: `Core.Api` → `Core.*` + `Systems.Dnd5e.Api` (es quien registra el módulo:
`AddGameSystem<Dnd5eSystem>()` y `MapDnd5eEndpoints`); `Systems.Dnd5e.*` → `Core.*` de su misma
capa o inferior; **nunca** `Core.*` → `Systems.*`. Las migraciones y `AppDbContext` siguen en
`Core.Infrastructure` (una sola historia, ADR 0009 enmendado); el módulo aporta sus configuraciones
EF mediante `IModelConfigurator` que `AppDbContext.OnModelCreating` recorre (DI). Dockerfile, CI y
`dotnet ef` apuntan a `src/Core/OpenTrpg.Core.Api`. Namespaces `OpenTrpg.Core.<Capa>.<Área>` y
`OpenTrpg.Systems.Dnd5e.<Capa>.<Área>`.

### Orden de trabajo (cada paso compila y pasa los tests antes del siguiente)

1. Crear los proyectos vacíos y la solución; mover los proyectos actuales a `Core` con `git mv`
   (historia conservada) y renombrar namespaces en bloque (`Dnd.` → `OpenTrpg.Core.`). Tests verdes.
2. Mover al módulo los ficheros clasificados **5e** en la fase 30 §1.1–§1.4 (y `seed/srd`), con sus
   tests. Tests verdes.
3. Repartir los **Mixtos** en el orden de la lista "Fase 32" paso 4 de la fase 30 §7, siguiendo la
   columna "Reparto" de cada fila. Puntos fijos:
   - `Character`: división de tabla (fase 30 §3.4): `Character` del núcleo con los campos de §3.1 y
     `Dnd5eCharacter` del módulo sobre la misma tabla `Characters`; sin cambios de columnas.
   - `CharacterDetailDto`: JSON plano idéntico (fase 30 §3.2, decisión 2 de §7): el núcleo construye su
     parte y `ISheetSystem.BuildDetailAsync` aporta los campos del sistema al mismo nivel.
   - `ChangeRequestType`: cadena registrada; valores almacenados sin cambio; `EditSheet` y `Companion`
     los despacha el módulo (`IChangeRequestSystem`).
   - `RestRequests.HitDiceJson` → `PayloadJson` (decisión 8) con migración de renombrado de columna.
   - Dinero: columnas sin cambio; propiedades C# del núcleo con nombres neutros (decisión 7).
   - `ItemTemplate`/overrides: columnas 5e mapeadas por el módulo sobre la misma tabla (decisión 3).
   - `ICampaignNotifier`: tipos de evento registrables; el módulo declara los suyos
     (`IGameSystem.RealtimeEventKinds`).
4. Completar `IGameSystem` según la fase 30 §4.1 con `Dnd5eSystem` delegando en los servicios actuales
   (§4.2); el núcleo llama al contrato solo donde §4.3 lo indica. Sin cambiar reglas ni cálculos.
5. Comprobar el modelo: `dotnet ef migrations add Comprobacion -p src/Core/OpenTrpg.Core.Infrastructure
   -s src/Core/OpenTrpg.Core.Api` debe generar `Up`/`Down` vacíos salvo el renombrado de
   `HitDiceJson` (que va en su propia migración `RenameRestRequestPayload`); borrar la de comprobación.
6. Prueba de regresión de API: un test que recorre las rutas de la tabla de la fase 30 §4.6 (todas las
   actuales) más las del núcleo con un personaje de prueba y compara el JSON con la versión anterior
   (capturar fixtures antes de empezar, en `tests/.../Fixtures/api-before-32.json`, y comparar al
   final; los fixtures entran al repositorio).

Entrega: `dotnet build -warnaserror`, `dotnet test` (todos los proyectos), `docker build` del
Dockerfile actualizado, `flutter test` sin tocar la app (no debe cambiar nada en `app/`).

## 32B. Rutas del sistema y alias

- `Systems.Dnd5e.Api` mapea sus endpoints bajo `/api/v1/systems/dnd5e` con un grupo que comprueba que
  la campaña del personaje (o la campaña de la ruta) tiene `SystemId == "dnd5e"` (404 si no).
- Las rutas actuales de la tabla de la fase 30 §4.6 quedan como alias del mismo handler con cabeceras
  `Deprecation: true` y `Link: </api/v1/systems/dnd5e/...>; rel="successor-version"`; un único mapa
  `RouteAliases` (ruta antigua → nueva) alimenta tanto el registro de alias como el test.
- Test: recorre `RouteAliases`, llama a la ruta antigua y a la nueva con el mismo personaje y compara
  estado y cuerpo; comprueba las dos cabeceras; comprueba que una campaña con otro `SystemId` recibe
  404 en la ruta nueva.
- Swagger agrupa por sistema. La app **no** cambia (sigue usando las rutas antiguas hasta la fase 33).

## 32C. Renombrado

- CI (`.github/workflows/ci.yml`): imagen `ghcr.io/<owner>/opentrpg-api` (y, durante dos releases,
  también `dnd-companion-api` con las mismas etiquetas para no romper despliegues); APK
  `opentrpg-X.Y.Z.apk`, artefacto `opentrpg-apk`; rutas de los proyectos nuevos.
- `deploy/.env.sample`, `deploy/README.md`, `README.md`, `CLAUDE.md` (estructura y comandos),
  `docs/PLAN.md` (nombres), `Directory.Build.props` (`Authors`), `docker-compose.yml` (`name:
  opentrpg`), `GitVersion.yml` si nombra algo. Mantener `dnd.example.com` como ejemplo ficticio.
- El nombre visible de la app Android, el `applicationId` y el `package` **no** cambian en esta fase
  (cambiarlos obliga a reinstalar; se decide en la fase 33).
- El repositorio lo renombra el propietario en GitHub a `OpenTRPG` (GitHub redirige el antiguo); después
  se actualiza `org.opencontainers.image.source` y los enlaces del README en un commit propio.

Reglas de siempre: sin reglas inventadas; textos visibles en español; nada de datos reales ni
secretos; commits en imperativo y en español sin identificadores de modelos.
