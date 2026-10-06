# Fase 20 — El DM no tiene personajes propios, solo PNJ

Contrato cerrado. Decisión del usuario: dentro de una campaña, quien tiene rol **DM u Owner no
puede ser dueño de un personaje**. Sus personajes son **PNJ** (sin dueño, `OwnerUserId = null`),
que gestiona con la vista completa de la Mesa del DM (ya existe: la Mesa lista todos los personajes
activos, PNJ incluidos, y abre su hoja de DM). Un jugador sigue creando solo los suyos. Así nadie
necesita ver "Mi sesión" y la Mesa del DM a la vez.

## Servidor (Opus)

- Invariante: un personaje con dueño exige que el dueño sea miembro con rol `Player` en la campaña.
- `CreateCharacterHandler`:
  - Creador `Player`: igual que hoy (dueño = creador; no puede elegir otro).
  - Creador DM/Owner sin `ownerUserId` o con su propio id → 400 `ownerUserId` "Un DM no tiene
    personajes propios: crea un PNJ o asígnalo a un jugador." Con `ownerUserId: null` → PNJ; con un
    miembro `Player` → para ese jugador; con otro DM/Owner → 400 "El dueño debe ser un jugador de
    la campaña."
- `ChangeMemberRoleHandler` (Player → DM) y `TransferOwnershipHandler` (el destinatario pasa a
  Owner): si el miembro que asciende es dueño de personajes en la campaña → 409 "<Nombre> tiene
  personajes en la campaña (A, B). Reasígnalos o conviértelos en PNJ antes de nombrarlo DM." El
  Owner saliente que queda como `Player` no cambia nada (no tenía personajes).
- Nuevo `PUT /api/v1/characters/{id}/owner { ownerUserId: Guid | null }` (DM/Owner): reasigna a un
  `Player` de la campaña o convierte en PNJ (`null`). Emite `character.updated`. Sin
  `ChangeRequest` (lo hace el DM).
- Datos existentes: migración `MakeDmCharactersNpcs` (solo SQL de datos, sin cambio de modelo) que
  pone `OwnerUserId = NULL` en los personajes cuyo dueño es DM/Owner de su campaña.
- Tests: DM crea sin dueño → 400; DM crea PNJ → OK; DM crea para un jugador → OK; DM crea para
  otro DM → 400; ascender a DM a un jugador con personajes → 409; sin personajes → OK;
  transferir la propiedad a un jugador con personajes → 409; reasignar dueño y convertir en PNJ;
  jugador no puede reasignar → 403.

## App (Sonnet)

- Asistente de creación (`step_basics.dart`) y `NewCharacterDialog`: para DM/Owner desaparece
  "Yo (por defecto)". Selector "Para" con **"PNJ (sin jugador)"** por defecto y los miembros con
  rol `Player`. Revisión: "PNJ" o el nombre del jugador, nunca "Yo".
- Hoja de DM: acción "Cambiar jugador" (Key `character-owner`) con los jugadores y "Convertir en
  PNJ", que llama al nuevo endpoint.
- Miembros: al ascender a DM o transferir la propiedad, el 409 se muestra en línea con el mensaje
  del servidor.
- Lista de personajes del DM: insignia "PNJ" en las tarjetas sin dueño.
- Tests: el selector del DM no ofrece "Yo" y empieza en PNJ; un jugador no ve selector; cambio de
  dueño desde la hoja de DM; error 409 visible al ascender.

## Verificación

`cd server && dotnet build && dotnet test`; `cd app && flutter analyze && flutter test`.
