# Fase 2 — Campañas, miembros y transferencia de propiedad

Contrato cerrado. Depende de la fase 1 (usuarios y JWT).

## Reglas

- Cualquier usuario activo puede crear una campaña; el creador queda como `Owner` (y es el DM por
  defecto). `Owner` es único por campaña y se registra también como miembro con rol `Owner`.
- Roles por campaña, de mayor a menor: `Owner` > `DM` > `Player`. "Al menos DM" significa Owner o DM.
- Permisos:
  - Ver campaña y lista de miembros: cualquier miembro.
  - Editar nombre/descripción: al menos DM.
  - Añadir `Player`: al menos DM. Añadir o ascender a `DM`: solo Owner.
  - Cambiar el rol de un miembro (DM ↔ Player): solo Owner. El rol `Owner` no se asigna por aquí.
  - Quitar miembro: Owner a cualquiera (salvo a sí mismo); DM a un Player; cualquier miembro puede
    salirse (`leave`) salvo el Owner (debe transferir antes).
  - Transferir propiedad: solo Owner, a un miembro existente. El Owner saliente queda como `DM` o
    `Player` según indique. Se guarda un registro `OwnershipTransfer`.
  - Eliminar campaña: solo Owner. Borrado físico en cascada.
- Un usuario puede ser Owner/DM en unas campañas y Player en otras.
- Servicio reutilizable en Application: `ICampaignAccess` con
  `Task<CampaignRole?> GetRoleAsync(campaignId, userId)` y `RequireAsync(campaignId, userId, minimumRole)`
  que lanza `ForbiddenException` (→ 403) o `NotFoundException` (→ 404). Las fases siguientes lo usan.
- Un no miembro recibe `404` (no `403`) al consultar una campaña, para no revelar su existencia.

## Entidades (Dnd.Domain)

```
Campaign           Id, Name (1-100), Description (0-2000, markdown), OwnerId, CreatedAt, UpdatedAt
CampaignMember     Id, CampaignId, UserId, Role (Owner|DM|Player), JoinedAt   — único (CampaignId, UserId)
OwnershipTransfer  Id, CampaignId, FromUserId, ToUserId, PreviousOwnerNewRole (DM|Player), TransferredAt
```

## Endpoints (`/api/v1`, requieren JWT)

| Método | Ruta | Body | Respuesta |
|--------|------|------|-----------|
| GET | `/campaigns` | — | `200 CampaignSummaryDto[]` (solo las del usuario, orden por nombre) |
| POST | `/campaigns` | `{ name, description }` | `201 CampaignDto` |
| GET | `/campaigns/{id}` | — | `200 CampaignDto` · `404` |
| PATCH | `/campaigns/{id}` | `{ name?, description? }` | `200 CampaignDto` · `403` |
| DELETE | `/campaigns/{id}` | — | `204` · `403` |
| GET | `/campaigns/{id}/members` | — | `200 MemberDto[]` |
| POST | `/campaigns/{id}/members` | `{ userId, role: "DM"\|"Player" }` | `201 MemberDto` · `403` · `409` ya es miembro · `404` usuario no existe o inactivo |
| PATCH | `/campaigns/{id}/members/{userId}` | `{ role: "DM"\|"Player" }` | `200 MemberDto` · `403` · `400` si es el Owner |
| DELETE | `/campaigns/{id}/members/{userId}` | — | `204` · `403` · `400` si es el Owner |
| POST | `/campaigns/{id}/leave` | — | `204` · `400` si es el Owner |
| POST | `/campaigns/{id}/transfer-ownership` | `{ toUserId, previousOwnerRole: "DM"\|"Player" }` | `200 CampaignDto` · `403` · `400` destino no es miembro o es uno mismo |
| GET | `/users/search?q=&limit=10` | — | `200 UserSummaryDto[]` (usuarios activos cuyo email o nombre contiene `q`, mínimo 2 caracteres) |

```
CampaignSummaryDto { id, name, description, ownerId, ownerDisplayName, myRole, memberCount, createdAt }
CampaignDto        { id, name, description, ownerId, ownerDisplayName, myRole, members: MemberDto[], createdAt, updatedAt }
MemberDto          { userId, displayName, email, role, joinedAt }
UserSummaryDto     { id, displayName, email }
```

## Cliente Flutter

- `features/campaigns`: lista (tarjetas con nombre, rol propio, nº de miembros), botón "Nueva
  campaña" (nombre, descripción), detalle con pestañas "Resumen" (descripción markdown simple,
  editable si al menos DM) y "Miembros".
- Pestaña miembros: lista con rol; si al menos DM, botón "Añadir" con buscador por email/nombre
  (`/users/search`) y selector de rol (DM solo si Owner); menú por miembro según permisos (cambiar
  rol, quitar); "Salir de la campaña" para no Owners; "Transferir propiedad" (solo Owner) con
  selector de miembro y rol que conserva.
- El inicio (`/`) pasa a mostrar la lista de campañas.
- Tests de widget: lista muestra campañas; crear campaña la añade; miembro Player no ve "Añadir".

## Pruebas del servidor

Matriz de roles: cada endpoint con Owner, DM, Player y no miembro. Transferencia cambia `ownerId`,
el antiguo Owner tiene el rol indicado y existe el registro `OwnershipTransfer`. Owner no puede
salir ni ser quitado. `/users/search` no devuelve usuarios inactivos.
