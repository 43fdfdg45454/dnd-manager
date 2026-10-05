# Fase 8 — Calendario, asistencia y recordatorios por correo

Contrato cerrado. Depende de la fase 2 y del `IEmailSender` de la fase 1.

## Reglas

- Cada campaña tiene `TimeZoneId` (IANA, por defecto la del servidor configurada en
  `App:DefaultTimeZone`, p. ej. `Europe/Madrid`) y `ReminderOffsetsMinutes` (por defecto
  `[1440, 120]` = 24 h y 2 h antes). Solo ≥ DM los edita.
- Las sesiones las crea y edita ≥ DM. Todos los miembros las ven y responden asistencia.
- Al crear o cambiar la fecha de una sesión se (re)generan sus `Reminder` pendientes: uno por
  offset cuya hora de envío sea futura. Al cancelar o borrar la sesión se eliminan los pendientes.
- Un `BackgroundService` (`ReminderDispatcher`) comprueba cada 60 s los `Reminder` con
  `SendAt <= ahora` y `SentAt = null`, envía un correo a cada miembro de la campaña con email y
  `NotificationsEnabled`, y marca `SentAt`. Fallos de envío se registran con `Attempts` y se
  reintentan hasta 3 veces con 5 minutos entre intentos; después `FailedAt`.
- Los usuarios pueden desactivar correos (`User.NotificationsEnabled`, editable en `/auth/me`
  con `PATCH`).
- Opción "Enviar aviso ahora" (≥ DM): correo inmediato con texto libre a los miembros.

## Entidades (Dnd.Domain/Sessions)

```
Campaign (añade)  TimeZoneId, ReminderOffsetsMinutesJson
GameSession       Id, CampaignId, Title, StartsAt (DateTimeOffset UTC), DurationMinutes?, Location?, Notes? (markdown),
                  Status (Scheduled|Cancelled|Done), CreatedByUserId, CreatedAt, UpdatedAt
SessionRsvp       Id, SessionId, UserId, Status (Yes|No|Maybe), Comment?, UpdatedAt   — único (SessionId, UserId)
Reminder          Id, SessionId, OffsetMinutes, SendAt, SentAt?, Attempts, FailedAt?, LastError?
User (añade)      NotificationsEnabled (default true)
```

## Endpoints (`/api/v1`, JWT)

| Método | Ruta | Body | Respuesta |
|--------|------|------|-----------|
| GET | `/campaigns/{id}/sessions?from=&to=&includePast=false` | — | `200 SessionDto[]` orden por fecha |
| POST | `/campaigns/{id}/sessions` | `{ title, startsAt, durationMinutes?, location?, notes? }` | `201 SessionDto` (≥ DM) |
| GET | `/sessions/{id}` | — | `200 SessionDto` con `rsvps` y `reminders` (reminders solo ≥ DM) |
| PATCH | `/sessions/{id}` | parcial + `status?` | `200` (≥ DM) |
| DELETE | `/sessions/{id}` | — | `204` (≥ DM) |
| PUT | `/sessions/{id}/rsvp` | `{ status, comment? }` | `200 SessionDto` (miembro) |
| POST | `/sessions/{id}/notify` | `{ subject, message }` | `202` (≥ DM) envía correo inmediato |
| PATCH | `/campaigns/{id}/settings` | `{ timeZoneId?, reminderOffsetsMinutes? }` | `200 CampaignDto` (≥ DM) |
| GET | `/me/sessions?from=&to=` | — | `200 SessionDto[]` próximas en todas mis campañas |
| PATCH | `/auth/me` | `{ displayName?, notificationsEnabled? }` | `200 UserDto` |

```
SessionDto  { id, campaignId, campaignName, title, startsAt, startsAtLocal (ISO con offset de la campaña), timeZoneId,
              durationMinutes?, location?, notes?, status, myRsvp?, rsvps: [{ userId, displayName, status, comment? }],
              counts: { yes, no, maybe, pending }, reminders?: [{ offsetMinutes, sendAt, sentAt?, failedAt? }] }
```

## Correos (español)

- **Recordatorio**: asunto "Recordatorio: {título} — {fecha local}", cuerpo con fecha/hora en la
  zona de la campaña, lugar, notas, estado de asistencia del destinatario y enlace
  `{PublicUrl}/sessions/{id}` (página estática mínima que muestra la sesión y permite responder
  asistencia con un token firmado en la URL, sin app).
- **Aviso del DM**: asunto "[{campaña}] {subject}", cuerpo = mensaje.
- Plantillas en `Infrastructure/Email/Templates`, texto + HTML.

## Cliente Flutter

- `features/calendar`: en la campaña, pestaña "Sesiones" con lista (próximas y pasadas) y vista
  mensual (`table_calendar`); detalle con asistencia (chips Sí/No/Quizá y comentario), lista de
  respuestas; DM crea/edita (selector fecha + hora en la zona de la campaña), cancela, "Enviar
  aviso"; ajustes de campaña (zona horaria y offsets).
- Inicio: tarjeta "Próxima sesión" (de `/me/sessions`).
- Perfil: interruptor "Recibir correos".
- Tests de widget: responder asistencia actualiza el chip; jugador no ve "Nueva sesión".

## Pruebas del servidor

Crear sesión a 3 días genera 2 recordatorios con `SendAt` correctos; mover la fecha los
regenera; cancelar los borra; el dispatcher envía (fake sender) y marca `SentAt`; usuario con
`NotificationsEnabled=false` no recibe; fallo del sender incrementa `Attempts` y reintenta;
`startsAtLocal` respeta `Europe/Madrid`; jugador recibe 403 al crear sesión.
