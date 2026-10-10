# Fase 31 — Sistema por campaña

Primer paso de la separación (ADR 0009, `docs/framework-roadmap.md`): cada campaña declara su sistema
de juego y el servidor expone los sistemas registrados. **Ningún cambio de comportamiento** para el
usuario: D&D 5e es el único sistema y todas las campañas existentes pasan a `dnd5e`. No se mueve
código de sitio (eso es la fase 32). Detalle de partida: lista de comprobación "Fase 31" en
`docs/specs/fase-30-inventario-y-contrato.md` §7.

## 1. Dominio y persistencia

- `Campaign.SystemId` (`string`, obligatorio, ≤ 32, minúsculas `[a-z0-9-]`), constante
  `Campaign.DefaultSystemId = "dnd5e"`. `Campaign.Create(...)` recibe el `systemId`; no es editable
  después (el sistema de una campaña no cambia).
- Configuración EF `HasMaxLength(32)` con valor por defecto `dnd5e`; migración
  `AddCampaignSystemId` que rellena las filas existentes con `dnd5e`.

## 2. Registro de sistemas (`Dnd.Application/Systems`)

- `IGameSystem` mínimo: `string Id`, `GameSystemInfo Info` (`Name`, `Version`,
  `IReadOnlyList<AttributionInfo> Attributions`, `IReadOnlyList<SystemDocumentInfo> SystemDocuments`),
  con los registros tal como los esboza la fase 30 §4.1. Nada más por ahora: el resto del contrato
  entra en la fase 32.
- `Dnd5eSystem : IGameSystem` en `Dnd.Application/Systems/Dnd5e/` con `Id = "dnd5e"`, nombre
  "Dungeons & Dragons 5e (SRD 5.1)", versión del SRD y la atribución CC-BY 4.0 que hoy devuelve
  `GetAttributionHandler`; ese handler pasa a leer `Info.Attributions` del sistema (respuesta JSON
  idéntica a la actual: prueba de regresión).
- `IGameSystemRegistry` (`IReadOnlyList<IGameSystem> All`, `IGameSystem? Find(string id)`,
  `IGameSystem Default`) y extensión de DI `AddGameSystem<TSystem>()`; `Dnd.Api` registra `Dnd5eSystem`.

## 3. API

- `GET /api/v1/systems` (autenticado): `[{ "id": "dnd5e", "name": "...", "version": "...",
  "isDefault": true }]`.
- `CreateCampaignRequest.SystemId` (`string?`): nulo → sistema por defecto; desconocido → 400 con
  mensaje en español ("Sistema de juego desconocido: «x».") y código de error `unknown-system`.
- `CampaignDto` y `CampaignSummaryDto` con `systemId`.

## 4. App

- `CampaignSummary` y `CampaignDetail` con `systemId` (por defecto `dnd5e` si el servidor no lo
  manda, para no romper la caché offline).
- `systemsProvider` (repositorio `GET /systems`, cacheado como el resto del catálogo).
- `CampaignFormDialog` (crear): muestra "Sistema de juego: Dungeons & Dragons 5e" como texto
  (clave `campaign-system`) cuando solo hay un sistema y un `DropdownButtonFormField`
  (`campaign-system-select`) cuando hay más de uno; al editar, solo texto. Envía `systemId`.
- Cabecera de la campaña (`campaign_shell.dart` o la vista General): el nombre del sistema bajo el
  título o en el panel de información, donde ya se muestra la descripción (clave `campaign-system`).

## 5. Pruebas

- Dominio: `Campaign.Create` normaliza y valida `systemId`.
- API: crear campaña sin `systemId` → `dnd5e`; con `dnd5e` → igual; con `gurps` → 400
  `unknown-system`; `GET /systems` devuelve el único sistema con `isDefault`; la migración deja las
  campañas existentes en `dnd5e` (prueba con SQLite: crear antes, leer después);
  `GET /api/v1/catalog/attribution` (o la ruta actual) devuelve exactamente lo de antes.
- App: el diálogo de creación muestra el sistema y envía `systemId`; con dos sistemas en el
  repositorio falso aparece el selector.

## Entrega

`dotnet build` sin avisos, `dotnet test`, `flutter analyze`, `flutter test` verdes; commits en
imperativo y en español sin identificadores de modelos. Fila 31 de `docs/PLAN.md` la añade el revisor.
