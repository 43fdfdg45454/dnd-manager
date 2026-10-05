# D&D 5e Companion — Plan preliminar

> Estado: BORRADOR. Las secciones marcadas con `[?]` dependen de respuestas pendientes
> (ver "Preguntas abiertas" al final). Lo no marcado es la propuesta por defecto.

## 1. Objetivo

App companion para sesiones **presenciales** de D&D 5e. Cada jugador lleva su personaje
en el móvil (Android); el DM gestiona la campaña, el lore, los mapas, las tiendas y el
calendario. Un administrador del servidor crea los usuarios. El servidor es .NET,
autohospedado con Docker.

## 2. Arquitectura propuesta

```
┌──────────────┐  HTTPS/JSON  ┌────────────────────────┐      ┌───────────┐
│ App Android  │ ───────────▶ │ API .NET (ASP.NET Core)│ ───▶ │ PostgreSQL│
│ [?] Compose/ │ ◀─────────── │ + SignalR (tiempo real)│      └───────────┘
│   MAUI / PWA │   WebSocket  │ + Worker (emails/cron) │ ───▶ │ SMTP [?]  │
└──────────────┘              └────────────────────────┘      │ Ficheros  │
                                                               └───────────┘
```

- **Backend**: ASP.NET Core (.NET 10 LTS), EF Core, PostgreSQL, SignalR para
  tiradas/combate en vivo, `BackgroundService` + Quartz para recordatorios.
- **Auth**: usuarios locales (sin registro público), JWT + refresh tokens. Roles
  globales: `Admin`, `User`. Roles por campaña: `Owner`, `DM`, `Player`.
- **Ficheros** (mapas, retratos): volumen Docker local, servidos por la API.
- **Despliegue**: `docker compose` con `api`, `db`, `reverse-proxy` (Caddy, HTTPS
  automático).
- **Cliente Android**: `[?]` pendiente de decidir (Kotlin/Compose, .NET MAUI o PWA).
- **Offline**: `[?]` pendiente. Si la mesa no tiene conexión fiable, hace falta
  caché local + cola de cambios (impacto grande en diseño).

## 3. Modelo de dominio (resumen)

- `User` (admin crea), `Campaign` (owner, dm, miembros), `Character` (pertenece a un
  usuario; asignado a una campaña).
- Reglas: `Class`, `Subclass`, `Race`, `Background`, `Feature`, `Spell`, `ItemTemplate`
  (SRD importado `[?]` + homebrew de campaña).
- Inventario: `CharacterItem` = referencia a `ItemTemplate` + *overrides* (nombre,
  daño, bonus, propiedades, descripción). "Creación rápida" = elegir plantilla;
  "creación avanzada" = plantilla + overrides o ítem desde cero.
- Combate: `SpellSlots`, `Resource` (usos por short/long rest), `Condition`,
  `HitPoints`, `DeathSaves`, `Concentration`.
- Campaña: `Shop` + `ShopItem` (precio, stock), `LoreEntry` (markdown, visibilidad
  jugadores/DM, adjuntos), `Map` (imagen + pins), `Session` (fecha, lugar, notas,
  recordatorios), `DiceRoll` (historial).

## 4. Funcionalidades por fase (propuesta)

| Fase | Contenido |
|------|-----------|
| 0 | Esqueleto: API, DB, auth, admin crea usuarios, Docker compose, CI |
| 1 | Campañas (crear, miembros, transferir ownership), personajes básicos, vista detallada |
| 2 | Ítems (plantillas SRD + rápido/avanzado), tiendas, dados virtuales |
| 3 | Vista de combate (slots, recursos, rests, condiciones), vistas por clase |
| 4 | Lore + mapas |
| 5 | Calendario + recordatorios por email |
| 6 | Tiempo real (tiradas compartidas, iniciativa) `[?]`, offline `[?]` |

## 5. Preguntas abiertas

Ver el mensaje de la sesión de planificación. Resumen de temas: cliente Android,
offline, reglas 2014 vs 2024 y fuente SRD, idioma, nivel de automatización de la hoja,
clases en v1, mapas, tiendas, dados compartidos, combate/iniciativa, modelo de
permisos, email/SMTP, calendario, hosting/dominio, distribución del APK, lore.
