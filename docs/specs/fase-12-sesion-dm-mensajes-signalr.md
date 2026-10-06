# Fase 12 — Herramientas de sesión del DM, mensajes secretos y tiempo real (servidor)

Contrato cerrado. Todo lo de esta fase es servidor; el cliente llega en la fase 13.

## Grupo de la campaña (DM/Owner)

Todos bajo `/api/v1/campaigns/{campaignId}/party`, `access.RequireAsync(campaignId, user, CampaignRole.DM)`.

- `GET /party` → `PartyDto { Characters: [PartyMemberDto] }` con los personajes **activos** de la
  campaña (`CharacterStatus.Active`), ordenados por nombre:
  `PartyMemberDto(Id, Name, OwnerUserId, OwnerDisplayName, PortraitUrl, Classes[], Level, HitPointsCurrent,
  HitPointsMax, TemporaryHitPoints, ArmorClass, Initiative, PassivePerception, Speed, Conditions[{index, note}],
  ExhaustionLevel, DeathSaveSuccesses, DeathSaveFailures, ConcentratingOnSpellIndex, Inspiration,
  SpellSlots[{level, max, used}], PactSlots?)`. Reutilizar `ICharacterSheetService.CalculateAsync` /
  el builder del resumen de combate; una sola carga por lote (`ListByCampaignAsync` + hojas).
- `POST /party/rest` body `PartyRestRequest(string Kind /* "short" | "long" */, IReadOnlyList<Guid>? CharacterIds)`:
  vacío/nulo = todos los activos. Descanso corto sin gastar dados de golpe (el DM fuerza el
  descanso; cada jugador gasta dados desde su vista si quiere). Respuesta `PartyDto` actualizado.
  Un solo `SaveChanges`. 404 si algún id no pertenece a la campaña.
- `POST /party/adjust` body `IReadOnlyList<PartyAdjustment>`:
  `PartyAdjustment(Guid CharacterId, int? HitPointsDelta, int? TemporaryHitPoints, IReadOnlyList<ConditionRequest>? AddConditions,
  IReadOnlyList<string>? RemoveConditions, int? HitPointsMax)`.
  - Nuevo en dominio: `Character.ApplyDamage(int amount, DateTimeOffset now)` (temporales
    primero, mínimo 0) y `Character.Heal(int amount, int maxHp, DateTimeOffset now)` (tope en máx;
    curar desde 0 resetea death saves). `HitPointsDelta < 0` = daño, `> 0` = curación.
  - `HitPointsMax` fija/elimina el override `hitPointsMax` (null en JSON = no tocar; 0 = quitar
    override) vía la misma ruta de dominio que `SheetPatch.Overrides`, y recalcula.
  - Condiciones: añade (sin duplicar índice) y quita por índice sobre `ApplyCombatUpdate`.
  - Validación: cada id de la campaña; delta entre −999 y 999; condiciones con índice no vacío.
  - Respuesta `PartyDto`. Un solo `SaveChanges`.
- Tests de integración: jugador → 403 en los tres; `rest long` restaura HP de todos; `adjust` con
  daño que supera temporales; `adjust` con `hitPointsMax`; `RemoveConditions` elimina.

## Mensajes secretos (DM → personaje)

- Entidad `DirectMessage` (`server/src/Dnd.Domain/Messages/DirectMessage.cs`, `EntityBase`):
  `CampaignId`, `SenderUserId`, `RecipientUserId`, `CharacterId` (personaje destinatario), `Body`
  (markdown, 1–2000, `Trim`), `SentAt`, `ReadAt?`. Métodos `Create(...)`, `MarkRead(now)`.
  Configuración EF (tabla `DirectMessages`, índices por `(CampaignId, RecipientUserId, ReadAt)` y
  `(CampaignId, SenderUserId)`), repositorio `IDirectMessageRepository` (Add, GetAsync, ListForRecipientAsync,
  ListSentAsync, CountUnreadAsync), migración `AddDirectMessages`.
- Endpoints (`server/src/Dnd.Api/Endpoints/MessageEndpoints.cs`):
  - `POST /api/v1/campaigns/{id}/messages` (DM) body `SendMessageRequest(IReadOnlyList<Guid> CharacterIds, string Body)`:
    una fila por personaje; el destinatario es `Character.OwnerUserId` (404 si el personaje no es
    de la campaña, 400 si no tiene dueño). Respuesta `201 [MessageDto]`.
  - `GET /api/v1/campaigns/{id}/messages?unreadOnly=false&limit=50` → DM: enviados (más recientes
    primero); jugador: recibidos. `MessageDto(Id, CampaignId, SenderUserId, SenderDisplayName,
    RecipientUserId, CharacterId, CharacterName, Body, SentAt, ReadAt)`.
  - `POST /api/v1/messages/{id}/read` (solo el destinatario) → `MessageDto`.
  - `GET /api/v1/campaigns/{id}/messages/unread-count` → `{ count }` (0 para DM).
- Tests: DM envía a dos personajes → dos filas, cada jugador solo ve la suya; otro jugador no ve
  nada; `read` por otro usuario → 403; `unread-count` baja al leer; cuerpo vacío → 400.

## Tiempo real (SignalR)

- `Microsoft.AspNetCore.SignalR` (incluido en el framework compartido; no hace falta paquete).
  Hub `CampaignHub` en `/hubs/campaign` (`server/src/Dnd.Api/Realtime/`), `[Authorize]`.
  JWT por query `access_token` solo en esa ruta: en `AuthenticationSetup` añadir
  `bearer.Events.OnMessageReceived` que lee `access_token` cuando `Path.StartsWithSegments("/hubs")`.
- Métodos del hub: `JoinCampaign(Guid campaignId)` → comprueba `ICampaignAccess.GetRoleAsync`
  (no miembro → `HubException("No perteneces a esta campaña.")`), añade la conexión a
  `campaign:{id}`; `LeaveCampaign(Guid campaignId)`. En `OnConnectedAsync` la conexión entra en
  `user:{userId}`.
- Contrato de evento (mensaje `campaignEvent`):
  `CampaignEvent(string Type, Guid CampaignId, Guid? CharacterId, Guid? EntityId, DateTimeOffset At)`.
  Tipos: `message.received` (solo grupo `user:{recipient}`), `character.updated` (grupo campaña;
  cualquier cambio de combate, descanso, hoja, inventario o solicitud aprobada), `party.rest`
  (grupo campaña; `EntityId` nulo), `shop.updated` (grupo campaña), `changeRequest.updated` (grupo
  campaña), `session.updated` (grupo campaña). Solo ids: el cliente vuelve a pedir lo que puede ver.
- Abstracción en Application: `ICampaignNotifier { Task NotifyAsync(CampaignEvent e, CancellationToken ct); }`
  + implementación nula `NoopCampaignNotifier` por defecto (tests de dominio/aplicación) y
  `SignalRCampaignNotifier(IHubContext<CampaignHub>)` en Api. Emitir **después** de `SaveChanges`
  en: `CharacterTracker.SaveAsync` (→ `character.updated`), `UpdateSheetHandler` (directo o
  aprobación), inventario (equipar/añadir/quitar), `PartyRest/PartyAdjust` (→ `party.rest` o
  `character.updated` por personaje), `SendMessage` (→ `message.received` por destinatario),
  `UpdateShopHandler` (→ `shop.updated`), `Submit/Approve/Reject/Cancel` de solicitudes
  (→ `changeRequest.updated`), crear/editar/cancelar sesión (→ `session.updated`).
  Fallos del notifier se registran y no rompen la petición.
- Tests (`RealtimeTests`): `HubConnection` contra `WebApplicationFactory` (`HttpMessageHandlerFactory`
  = `factory.Server.CreateHandler()`), con token de jugador: `JoinCampaign` propia OK, ajena lanza;
  enviar mensaje desde DM → el jugador recibe `message.received`; descanso forzado → recibe
  `party.rest`; sin token → la conexión falla (401).

## Documentación

- `docs/ADR/0006-tiempo-real-signalr.md` (acepta SignalR para eventos de campaña; los dados siguen
  siendo privados; sin estado compartido más allá de los grupos de conexión), `docs/ADR/0005` pasa
  a "Estado: reemplazado por ADR 0006".
- `deploy/README.md`, bloque nginx: añadir `proxy_set_header Upgrade $http_upgrade;` y
  `proxy_set_header Connection $connection_upgrade;` con el `map $http_upgrade $connection_upgrade
  { default upgrade; '' close; }` en el bloque `http`; nota de que SignalR cae a SSE/long polling si
  el proxy no negocia WebSocket. `CLAUDE.md`: sustituir "Sin tiempo real… No usar SignalR" por
  "Tiempo real solo para eventos de campaña vía SignalR (`/hubs/campaign`); dados privados".

## Verificación

`cd server && dotnet build && dotnet test` en verde. Manual: `wscat`/cliente de prueba no
necesario; los tests de hub cubren el flujo.
