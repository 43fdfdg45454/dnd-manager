# D&D 5e Companion — Plan maestro

Companion app para sesiones **presenciales** de D&D 5e: cada jugador lleva su personaje en Android,
el DM gestiona campaña, lore, mapas, tiendas y calendario, y un administrador del servidor crea los
usuarios. Servidor .NET autohospedado con Docker.

## Decisiones

| Tema | Decisión |
|------|----------|
| Cliente | Flutter (solo Android por ahora; iOS queda abierto) |
| Offline | Solo lectura: la app cachea el último estado; editar requiere red |
| Reglas / contenido | SRD 5.1 (2014), importado desde el dataset JSON de `5e-database` (MIT; contenido CC-BY 4.0 con atribución) |
| Hoja | Semiautomática: clase/raza/nivel → cálculo de modificadores, competencias, slots y rasgos; todo sobreescribible |
| Dados | Privados, sin tiempo real; historial local en el móvil |
| Email | SMTP del operador por variables de entorno (465 TLS implícito o 587 STARTTLS; CA propia con `SSL_CERT_FILE`) |
| Idioma | UI en español, contenido SRD en inglés (el DM puede renombrar) |
| Distribución | APK directo; endpoint de "última versión" para avisar de actualizaciones |
| Servidor en el cliente | URL configurable desde la app (LAN, VPN o dominio), con HTTP permitido y lista de servidores recientes; ver `docs/specs/cliente-servidor-configurable.md` |
| Mapas | Imagen con zoom + pines con notas enlazadas al lore; pines visibles u ocultos |
| Tiendas | Compra directa: descuenta oro, baja stock, ítem al inventario; DM abre/cierra tienda |
| Hosting | Docker Compose (api + postgres); la API escucha en un puerto del host y el operador pone delante su propio reverse proxy con TLS |
| Documentación | SRD 5.1 en PDF empaquetado (CC-BY); el admin sube otros PDF a la biblioteca de la instancia |

Los ADR en `docs/ADR/` detallan las decisiones con más impacto.

## Modelo de permisos

- Roles globales: `Admin` (crea usuarios, ve todo), `User`.
- Roles por campaña: `Owner` (único; por defecto el DM; transferible), `DM` (co-DMs), `Player`.
- Los ítems de un personaje pertenecen a la campaña; los ítems homebrew viven en la campaña.
- El jugador solo modifica ítems mediante operaciones: comprar/vender en tienda abierta, equipar,
  usar consumible, atunement.
- Auto-seguimiento en combate sin aprobación: HP, HP temporales, death saves, slots, recursos,
  condiciones, concentración, short/long rest.
- Todo lo demás pasa por `ChangeRequest` (pendiente / aprobado / rechazado): subir de nivel, editar
  stats o competencias, ítems a mano, ítems personalizados, oro fuera de una compra. El DM/Owner
  aplica estos cambios directamente.
- El Owner saliente queda como `DM` (o `Player` si elige) al transferir.

## Arquitectura

```
Android (Flutter) ──HTTPS/JSON──▶ Proxy del operador ──▶ API ASP.NET Core (.NET 10 LTS)
                                              ├─ PostgreSQL 17 (EF Core)
                                              ├─ Volumen /data/files (mapas, retratos, PDF, APK)
                                              └─ Worker (recordatorios → SMTP)
```

- Auth: usuarios locales, ASP.NET Identity + JWT (access corto) + refresh token rotatorio. Sin
  registro público. El admin crea el usuario; la API envía email con enlace de "establecer
  contraseña". Reset por email.
- Ficheros: volumen local; la API sirve `/files/{id}` con control de acceso por campaña y soporte
  de `Range` (PDF).
- Catálogo SRD: seed idempotente al arrancar desde `server/seed/srd/`.

## Modelo de dominio

- `User`, `RefreshToken`, `PasswordSetupToken`.
- `Campaign`, `CampaignMember` (role), `OwnershipTransfer`.
- `Character`, `CharacterClass` (multiclase simple), `CharacterOverride`, `CharacterSpell`,
  `CharacterResource`, `SpellSlotState`.
- Catálogo: `Race`, `Class`, `Subclass`, `ClassLevelFeature`, `Feature`, `Spell`, `ItemTemplate`
  (`campaignId` nulo = SRD, no nulo = homebrew), `Condition`.
- Inventario: `CharacterItem` (plantilla + overrides: name, description, damageDice, damageType,
  bonus, properties, weight, charges). Creación rápida = plantilla; avanzada = plantilla + overrides
  o ítem sin plantilla.
- `ChangeRequest`, `Shop`, `ShopItem`, `Transaction`.
- `LoreEntry` (markdown, visibilidad Player/DM, adjuntos), `LibraryDocument`, `Map`, `MapPin`.
- `GameSession` (numerada por campaña, con `SummaryMarkdown` que solo el DM edita), `Reminder`, `SessionRsvp`, `AppRelease`.

## Vistas del personaje

- **Detallada**: Resumen / Habilidades y competencias / Rasgos / Hechizos / Inventario / Notas.
- **Combate**: HP, AC, iniciativa, velocidad, death saves, condiciones, ataques con arma equipada,
  spell slots por nivel, recursos por rest, consumibles rápidos, short/long rest, dado rápido.
- **Panel por clase** (widget registrado por `classIndex`): Bárbaro (Rage, Reckless Attack), Mago
  (libro de hechizos, preparados, Arcane Recovery), Paladín (Lay on Hands, Divine Smite, Channel
  Divinity). El resto usa el panel genérico de recursos.

## Otras funcionalidades

- **Diario de sesiones**: solo el DM crea sesiones y escribe el resumen de cada una; todos los
  miembros lo leen en la pestaña "Diario" de la campaña, en orden cronológico.
- **Recordatorios**: `BackgroundService` que envía por SMTP (MailKit) los `Reminder` vencidos;
  offsets por defecto 24 h y 2 h, editables por campaña; zona horaria por campaña.
- **Biblioteca**: SRD en PDF como documento de sistema; el admin sube PDF (límite configurable);
  visor integrado, descarga para lectura offline, memoria de última página.
- **Dados**: parser (`2d6+3`, `1d20 adv/dis`, `4d6kh3`), tiradas desde la hoja; historial local.
- **Offline**: caché local (drift) por pantalla; aviso "sin conexión"; escrituras deshabilitadas.

## Asignación de modelos por tarea

No es viable ejecutar todo con Fable. Regla general: **Fable** diseña contratos, decide y revisa;
**Opus** implementa la lógica con reglas de negocio o seguridad; **Sonnet** implementa CRUD, UI y
tests convencionales; **Haiku** hace lo mecánico (DTOs, textos, plantillas, documentación).

| Tipo de tarea | Modelo | Motivo |
|---------------|--------|--------|
| Orquestación, diseño de dominio y contratos de API, revisión de cada fase, decisiones de seguridad | Fable 5.1 | Requiere criterio global y detectar errores sutiles |
| Auth/JWT, autorización por rol, `ChangeRequest`, cálculos de hoja, compra atómica, seed SRD, paneles por clase, caché offline | Opus 5.5 | Lógica con muchas ramas donde un error cuesta caro |
| CRUD de campañas/lore/mapas/calendario, pantallas Flutter, tests de integración, Docker/CI | Sonnet 5.5 | Trabajo estándar con patrón claro |
| DTOs y modelos Dart desde OpenAPI, textos de UI, plantillas de email, README, migraciones triviales | Haiku 4.5 | Mecánico y voluminoso |

| Fase | Fable | Opus | Sonnet | Haiku |
|------|-------|------|--------|-------|
| 1 Auth + admin | Contrato y revisión de seguridad | Servidor: usuarios, JWT, refresh, tokens de alta/reset | Flutter: login, sesión, pantalla admin | Plantillas de email |
| 2 Campañas | Reglas de roles y transferencia | Autorización por rol | CRUD servidor + Flutter | DTOs |
| 3 Catálogo SRD | Mapeo dataset → entidades | Importador y seed | Endpoints de consulta + buscador Flutter | Modelos Dart |
| 4 Personajes | Modelo de cálculo y overrides | Cálculos, `ChangeRequest`, creación semiautomática | Vista detallada Flutter | Textos |
| 5 Ítems y tiendas | Contrato de overrides | Compra/venta atómica, homebrew | UI inventario y tiendas | DTOs |
| 6 Combate y dados | Diseño de la vista | Paneles Bárbaro/Mago/Paladín, rests | Vista genérica, parser de dados | Textos |
| 7 Lore, mapas, biblioteca | Revisión | — | Todo (servidor + Flutter + visor PDF) | README |
| 8 Calendario y correos | Revisión | — | Sesiones, RSVP, worker SMTP | Plantillas |
| 9 Offline, APK, backups | Revisión | Caché offline (drift) | Aviso de versión, backups | README de despliegue |
| 10 Arreglos, `PublicUrl`, `/admin` | Contrato | — | Todo | — |
| 11 Modificadores de ítems | Contrato | Dominio y seed | UI | — |
| 12 Mesa del DM, mensajes, SignalR | Contrato y ADR 0006 | Todo | — | — |
| 13 Diseño, modos de vista, clases, tiempo real | Contrato y revisión visual | Paneles, shell, cliente SignalR | Sistema de diseño, Vista General | — |
| 14 Asistente de personaje | Contrato | — | Todo | — |
| 15 Paquetes de contenido | Contrato y ADR 0007 | Importador | UI admin | Docs del formato |
| 16 Juego guiado por el PHB | Contratos, datos SRD de elecciones, revisión | Hub endurecido, descansos aprobados, elecciones y subida de nivel, asistente, animaciones, iconos | Correcciones, conexión, peticiones, piel | — |

Cómo se aplica en la práctica: la sesión principal corre con Fable y lanza subagentes con el modelo
indicado (`Agent` con `model: opus | sonnet | haiku`), dándoles el contrato escrito en
`docs/specs/` y revisando su resultado antes de confirmar.

## Estado

Fases 0 a 24 implementadas y en `master` (cada una con su contrato en `docs/specs/`). Verificado en
este entorno: servidor compila sin avisos y pasa sus tests (SQLite en memoria); cliente sin
incidencias de análisis y con sus tests en verde. CI publica en cada push a `master` la imagen
`ghcr.io/<owner>/dnd-companion-api:latest` y una release con el APK firmado. **No verificado aquí**
(sin PostgreSQL ni Android SDK): migraciones contra PostgreSQL real, aspecto de las fuentes variables
y los SVG en dispositivo, SignalR a través del reverse proxy, envío SMTP real, apertura de documentos
con aplicaciones externas. Primeros pasos recomendados: `docker compose pull && up -d`, abrir
`/admin` para crear el administrador, instalar el APK y hacer una partida de prueba con un DM y un
jugador en dos móviles; cualquier desviación se corrige sobre los contratos de `docs/specs/`.

## Fases

| Fase | Entregable | Verificación |
|------|------------|--------------|
| 0 | Esqueleto: solución .NET, Flutter, Docker Compose + Nginx, CI, CLAUDE.md, ADRs | `docker compose up` sirve `/health`; CI verde |
| 1 | Auth + admin: crear usuarios, email de alta, login/refresh, pantalla admin | Integración de alta y login; email capturado por el sender falso de los tests |
| 2 | Campañas: CRUD, miembros, roles, transferir ownership | Tests de autorización por rol |
| 3 | Catálogo SRD: seed + endpoints de consulta; buscador en la app | Conteo de hechizos/ítems tras seed |
| 4 | Personajes: creación semiautomática, cálculos, vista detallada, overrides, `ChangeRequest` | Tests de cálculo |
| 5 | Ítems: inventario, creación rápida/avanzada, homebrew, tiendas y compra | Compra atómica |
| 5b | Cliente: URL del servidor configurable en la app (LAN/VPN, HTTP permitido, certificado fijado opcional) | Cambiar servidor cierra sesión; probar conexión muestra versión |
| 6 | Vista de combate + paneles Bárbaro/Mago/Paladín + rests + dados | Prueba en emulador |
| 7 | Lore + mapas con pines + biblioteca de documentos | Pin oculto invisible al jugador; PDF offline |
| 8 | Calendario, RSVP, recordatorios SMTP, diario de sesiones con resumen del DM | Reminder enviado por el sender falso; jugador lee el resumen y no lo edita |
| 9 | Caché offline, aviso de actualización de APK, backups, README de despliegue | Modo avión muestra la hoja |
| 10 | Rasgos de subclase y objetos SRD visibles; `App:PublicUrl` obligatoria; primer admin desde `/admin` | `/admin` crea el admin una sola vez |
| 11 | Modificadores estructurados de ítems (característica, salvación, CA, ataque, daño…), HP máximo editable, escudo real | Ítem con +3 DES sube la hoja al equiparlo |
| 12 | Mesa del DM: roster, descansos forzados, daño/estados en lote, inventario de grupo (alijo y oro común), mensajes secretos; hub SignalR | Jugador recibe `party.rest` sin refrescar |
| 13 | Sistema de diseño místico, Vista General / Mesa del DM / Mi sesión, temas y paneles por clase, cliente SignalR | Rol decide la vista; contraste ≥ 4.5 |
| 14 | Asistente paso a paso de creación de personaje | Mago nivel 1 completo desde el móvil |
| 15 | Paquetes de contenido privados (importador + pantalla admin) | Subclase del paquete elegible en un guerrero |
| 16 | Descansos pedidos y aprobados, nivel concedido por el DM, asistente de subida con todas las elecciones del PHB, conexión clara y diagnóstico, hub endurecido, piel oscura con iconos propios y animaciones | Jugador pide descanso → DM aprueba → PG en vivo; subir de nivel exige completar las elecciones |
| 17 | Equipo inicial estructurado (kits de clase y trasfondo, elecciones, oro inicial) | El equipo elegido aparece en el inventario al activar |
| 18 | Preparación de conjuros obligatoria y categorías de conjuro | Un clérigo no entra en juego sin preparar |
| 19 | Elecciones forzadas que faltaban (tirada de características, raza y trasfondo, concentración, sintonización, tiradas al descansar) | Semielfo pide +1 a dos y dos habilidades |
| 20 | El DM no tiene personajes propios, solo PNJ que puede ceder | Ceder un PNJ a un jugador lo convierte en su personaje |
| 21 | Baratija inicial y límite de idiomas | d100 de baratija en el asistente |
| 22 | Personalidad del trasfondo y tablas de tirada | Oleada de magia salvaje tirable |
| 23 | Personalización: paletas, fuentes y opciones de apariencia | Seis paletas con contraste AA verificado por test |
| 24 | Lote de correcciones de juego (dados y críticos, conjuros en combate, invitaciones, peticiones detalladas, tienda con catálogo, bestias para druidas, scroll infinito, documentos externos) y nueva navegación | Ver `docs/specs/fase-24-correcciones-y-navegacion.md` |

## Verificación end-to-end

1. `cd server && dotnet build && dotnet test`.
2. `cd app && flutter analyze && flutter test`.
3. `cd deploy && cp .env.sample .env && docker compose pull && docker compose up -d`, abrir
   `https://<host>/admin` a través del proxy para crear el administrador, luego crear un usuario
   desde la app y recibir el correo en tu SMTP.
4. `flutter build apk --release`, instalar en emulador, crear personaje, comprar en tienda, modo
   combate, gastar un slot; modo avión y comprobar que la hoja sigue visible.
