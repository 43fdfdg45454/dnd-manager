# Fase 16 — Juego guiado por el PHB 2014: descansos aprobados, subida de nivel, conexión clara y nueva piel

Contrato cerrado sobre `docs/specs/fase-16-propuesta-rediseno.md` y las decisiones del usuario:

| Decisión | Valor |
|---|---|
| Descansos | Corto **y** largo requieren aprobación del DM |
| Subida de nivel | La concede el DM (a uno o al grupo); el jugador completa el asistente |
| PG al subir | Tirada física; la app dice qué dado y el jugador escribe el resultado (1..dado) |
| Dotes | Siempre disponibles como alternativa a la mejora de característica, con prerrequisitos |
| Fuentes | Almendra (títulos) y Source Sans 3 (cuerpo y números), ambas SIL OFL |
| Paleta | Oscura por defecto; pantalla "Personalización" para cambiar a clara |
| Multiclase | En el asistente de subida desde el principio |
| Conexión | SignalR por el mismo puerto HTTPS, endurecido; estado derivado solo del hub |

Regla absoluta: la app acompaña una sesión presencial; asiste con lo que el personaje puede hacer
y lleva la cuenta de lo molesto. La fuente de reglas es la skill `phb-2014` (fuera del repo);
en el repositorio solo entra contenido SRD.

Sub-fases: 16a (correcciones y conexión) → 16b (descansos aprobados y concesión de nivel) →
16c (catálogo de elecciones y asistente de subida) → 16d (piel: paleta, iconos propios, fuentes,
animaciones, personalización). Cada una termina con servidor y app en verde, commit y push.

---

## 16a — Correcciones rápidas, estado de conexión y endurecimiento del hub

### App (Sonnet)
- **Resumen · Combate**: rejilla fija de 3 columnas × 2 filas con fichas iguales en este orden:
  CA, Iniciativa, Velocidad / PG máx, Percepción pasiva, Competencia. En anchos ≥ 600 px, 6 en
  una fila. Cada valor sigue siendo `StatValue`. Características: 6 fichas iguales en 3×2.
- **Imposición de manos** (`panels/paladin.dart`): quitar el interruptor; dos botones
  `loh-heal-self` ("Curarme") y `loh-heal-other` ("Curar a otro"). El segundo abre una hoja con la
  lista del grupo (`GET /party` no es de jugador: usar la lista de personajes de la campaña) y un
  campo "Otra criatura" libre; el nombre del objetivo se registra en la nota de la acción.
- **Estado de conexión**:
  - `RealtimeStatus` deriva solo del hub (nunca de `connectivity.lastRequestFailed`); sin red,
    `offline`.
  - `ConnectionBanner` en el shell, debajo de la app bar, solo cuando no está `connected`: ámbar
    "Reconectando… (N s)" con cuenta atrás del siguiente intento; roja "Sin conexión en vivo ·
    datos de hace X · Reintentar" (`realtime-retry`). Conectado: sin franja; el icono de la app
    bar (`realtime-status`) en dorado con tooltip "En vivo".
  - Pantalla **Servidor → "Probar conexión"** (`server-diagnose`): cuatro pasos con resultado por
    línea y causa probable: 1) `GET /health` (API alcanzable), 2) `GET /api/v1/auth/me`
    (sesión), 3) `POST /hubs/campaign/negotiate?negotiateVersion=1` con el token (hub y proxy),
    4) conexión real al hub y transporte usado ("WebSockets" / "ServerSentEvents" / "LongPolling";
    si no es WebSockets: "Tu proxy no reenvía Upgrade/Connection; funciona, pero más lento").
    Cada error muestra el código HTTP o la excepción resumida.
- Tests: rejilla (6 fichas con Keys `tile-*`), dos botones de imposición, banner por estado,
  diagnóstico con fakes de cada paso.

### Servidor (Opus)
- Hub: `MaximumReceiveMessageSize = 32 KB`, `ClientTimeoutInterval = 60 s`,
  `KeepAliveInterval = 15 s`, `EnableDetailedErrors` solo en Development.
- Máximo **5 conexiones simultáneas por usuario** (`ConnectionTracker` singleton; la sexta recibe
  `HubException("Demasiadas conexiones abiertas.")`).
- Rate limit en `/hubs/*` (`AddRateLimiter`, ventana fija 30 peticiones/min por usuario o IP) y
  en `POST /api/v1/auth/login` (10/min por IP) si no existía.
- Al quitar a un miembro o al salir de la campaña: evento `membership.removed` al grupo
  `user:{id}` y el hub saca sus conexiones del grupo `campaign:{id}` (`ConnectionTracker`).
- Al desactivar un usuario (admin): se cierran sus conexiones (`Context.Abort` vía tracker).
- `GET /api/v1/realtime/status` (autenticado): `{ connections, transport }` del usuario actual,
  para el diagnóstico.
- Tests: sexta conexión rechazada; expulsado deja de recibir eventos; rate limit devuelve 429.

---

## 16b — Descansos con aprobación y concesión de nivel

### Servidor (Opus)
- Entidad `RestRequest` (`Dnd.Domain/Characters`): `CharacterId`, `CampaignId`, `Kind`
  (Short|Long), `HitDiceJson` (para corto: dados que gastará, `{ "fighter": 2 }`), `Status`
  (Pending|Approved|Rejected|Cancelled), `RequestedAt`, `ResolvedByUserId?`, `ResolvedAt?`,
  `Comment?`. Una sola pendiente por personaje. Migración `AddRestRequests`.
- Jugador: `POST /characters/{id}/rest-requests { kind, hitDice? }` → 201; `DELETE` la pendiente.
  `POST /characters/{id}/rest/short|long` pasan a **DM/Owner only** (jugador → 403 con mensaje
  "Pide el descanso al DM").
- DM: `GET /campaigns/{id}/rest-requests?status=Pending`; `POST /rest-requests/{id}/approve`
  (aplica el descanso con las reglas del PHB: corto gasta los dados indicados; largo = PG al
  máximo, mitad de dados de golpe, recargas, −1 agotamiento) y `/reject { comment }`. El
  descanso forzado del DM (`/party/rest`) sigue existiendo y cancela las pendientes de esos
  personajes.
- Concesión de nivel: `Character.PendingLevelUpTo (int?)`, `LevelGrantedByUserId`,
  `LevelGrantedAt`. `POST /campaigns/{id}/party/grant-level { characterIds? }` (DM) → fija
  `PendingLevelUpTo = nivel + 1` (si ya hay uno pendiente no lo acumula; máximo 20). `DELETE`
  para retirarlo. Migración `AddPendingLevelUp`.
- Eventos: `restRequest.updated` (grupo campaña), `levelUp.granted` (grupo `user:{dueño}` y
  campaña), ambos añadidos a `CampaignEventTypes`.
- `PartyMemberDto` gana `pendingRest { kind }` y `pendingLevelUpTo`.
- Tests: jugador pide corto con 2 dados → pendiente; DM aprueba → PG suben y dados bajan; jugador
  llama a `/rest/long` → 403; conceder nivel a dos → ambos con `pendingLevelUpTo`.

### App (Sonnet)
- Mi sesión · Fuera de combate · **Descansos**: "Pedir descanso corto" (selector de dados de
  golpe a gastar, con la curación esperada "2d10 + 4") y "Pedir descanso largo"; mientras está
  pendiente, tarjeta "Esperando al DM" con Cancelar; al aprobarse, snackbar y animación
  (hoguera/luna en 16d). `RestSection` deja de llamar a `/rest/*` para jugadores.
- Mesa del DM: tarjeta **Peticiones** con cada descanso pedido (nombre, tipo, dados, hace cuánto)
  y botones Aprobar/Rechazar en la propia fila; sigue el acceso a solicitudes de cambio. Botón
  **Conceder nivel** en la barra de acciones (a todos o a la selección) con confirmación.
- Mi sesión: tarjeta **"¡Puedes subir a nivel N!"** (`level-up-card`) cuando hay nivel
  pendiente; abre el asistente de 16c (hasta entonces, abre la hoja).
- Tests: flujo de petición y aprobación con fakes; tarjeta de nivel pendiente.

---

## 16c — Catálogo de elecciones por nivel y asistente de subida

Fuente: tabla "Elecciones por nivel" y `[RECURSO]`/`[MECÁNICA]` de la skill. En el repositorio
solo entra lo del SRD; el resto llega en el paquete privado (formato v2).

### Servidor (Opus)
- Catálogo nuevo (con `Source`): `OptionSetDefinition { SetId, Name }`, `OptionDefinition
  { Index, SetId, Name, Description[], PrerequisitesText?, PrerequisitesJson?, ModifiersJson,
  AbilityIncreaseJson?, GrantsJson?, ResourceJson? }`, `LevelChoiceRule { ClassIndex,
  SubclassIndex?, Level, Key, Name, Kind, SetId?, Choose, FromJson?, Replaces, Cumulative, Note }`.
  `Kind` ∈ { Subclass, OptionSet, AsiOrFeat, Expertise, Skill, Language, Tool, CantripsKnown,
  SpellsKnown, SpellbookSpells, Custom }. Migración `AddLevelChoices`.
- Seed SRD (`server/seed/srd/level-choices.json` y `option-sets.json`, escritos a mano desde la
  skill, **solo contenido SRD**): subclase en su nivel; `asiOrFeat` en los niveles de cada clase
  (incluidos los extra de guerrero y pícaro); pericia (bardo 3/10, pícaro 1/6); estilos de
  combate SRD (guerrero 1, paladín 2, explorador 2; set `fighting-styles` con sus modificadores:
  Defensa +1 CA con armadura, Arquería +2 ataque a distancia, Duelo +2 daño con un arma a una
  mano, etc.); invocaciones SRD (brujo 2: 2; luego +1 en 5, 7, 9, 12, 15, 18; sustituible);
  metamagia SRD (hechicero 3: 2; 10: +1; 17: +1); dones del pacto (brujo 3); enemigo predilecto y
  terreno (explorador 1, 6, 10/14); trucos y conjuros conocidos derivados de `ClassLevel`
  (`CantripsKnown`/`SpellsKnown` delta por nivel; mago: `SpellbookSpells` 2 por nivel); dote
  SRD `grappler` en el set `feats`.
- Paquete de contenido **formato v2**: `optionSets[]`, `classesExtended[].levelChoices[]`,
  `classesExtended[].subclasses[].levelChoices[]` y `grants`, `feats` como set, según el prompt
  ya definido. `formatVersion: 2`; v1 sigue aceptándose.
- Personaje: `CharacterChoice { CharacterId, Level, ClassIndex, Key, SelectedJson, CreatedAt }`.
  Efectos: los `Modifiers` de las opciones elegidas (y de las dotes) entran en `SheetCalculator`
  como partes de desglose con `source = feature` y `label = nombre de la opción (nivel N)`;
  `AbilityIncrease` de la mejora de característica como `AbilityBonus` con label "Mejora de
  característica (nivel N)"; `Resource` crea recursos automáticos (dados de superioridad…);
  `Grants` añade competencias y conjuros siempre preparados.
- Plan de subida: `GET /characters/{id}/level-up` → `{ targetLevel, classes: [{ classIndex, name,
  allowed, reason? /* multiclase: requisitos PHB */, hitDie, isNew }], automaticFeatures:
  [{ classIndex, level, feature }], choices: [{ key, name, kind, choose, replaces, options:
  [{ index, name, description, eligible, reason?, effectsPreview: [BreakdownPart] }] }] }` para la
  clase que se indique (`?classIndex=`); `spellcasting` con los límites.
- Aplicar: `POST /characters/{id}/level-up { classIndex, hitPointsRolled, choices: [{ key,
  selected: [...] | { asi: {str:1,dex:1} } | { feat: "index", ability?: "str" } }] }`.
  Valida: hay nivel pendiente; `hitPointsRolled` entre 1 y el dado (el servidor suma Con y
  rasgos retroactivos); todas las elecciones obligatorias completas y elegibles; multiclase
  cumple requisitos (05-multiclase). Aplica en una transacción, recalcula, borra el pendiente,
  emite `character.updated` y `levelUp.completed`. Directo para el jugador dueño (el DM ya
  concedió el nivel); el DM también puede aplicarlo.
- `CharacterDetailDto` gana `choices[]` (nivel, clase, nombre, seleccionado con nombres) para la
  hoja y los desgloses.
- Tests: plan de guerrero 1→2 sin elecciones; 1→3 pide subclase; 3→4 pide `asiOrFeat` y rechaza
  dote sin prerrequisito; brujo 2 pide 2 invocaciones; mago pide 2 conjuros de libro;
  multiclase a mago con Int 12 → no permitido; PG fuera de rango → 400; Defensa (estilo) suma +1
  CA con armadura y aparece en el desglose.

### App (Opus)
- Asistente `features/characters/ui/level_up/`: pantalla completa, pasos: 1) "Subes a nivel N"
  con clase (selector si multiclase, con motivo cuando no se permite) y rasgos automáticos
  (texto); 2) PG: "Tira 1d10 y escribe el resultado" con campo numérico validado y vista previa
  "+ Con 2 = +7"; 3) una pantalla por elección: tarjetas de opción con texto, prerrequisito y
  "efecto: CA 16 → 17"; `asiOrFeat` con dos pestañas (Mejora: +2 o +1/+1 con topes 20; Dote:
  lista con elegibilidad); sustituciones opcionales al final; 4) Resumen y "Confirmar" (bloqueado
  hasta completar). Al confirmar: animación de celebración (16d; en 16c un `AnimatedScale`
  sencillo) y vuelta a Mi sesión.
- Hoja: sección "Elecciones" por nivel; desgloses muestran las partes `feature`.
- Tests: plan con elecciones incompletas bloquea; completarlas habilita; PG inválido; envío.

---

## 16d — Piel: paleta oscura, iconos propios, fuentes, animaciones, personalización

### Tokens y tema (Sonnet)
- Oscuro (por defecto): `obsidian #14110F` fondo, `stone #231D19` tarjetas, `stoneRaised
  #2D2622`, `bone #E7DCC6` texto, `boneMuted #A89C87`, `ember #D9671E` acción principal,
  `arcane #8A6BD1`, `moss #5E7A4A` curación, `blood #9B2226` daño, `oldGold #B8923A` bordes y
  sellos, `rune #4A3F36` líneas. Claro: `bone` fondo, `clay #D8C9AE` tarjetas, `obsidian` texto,
  mismos acentos oscurecidos un 15 %. Contraste ≥ 4.5 en test.
- Texturas con `CustomPainter`: grano sutil (ruido determinista) en el fondo y bordes "a pincel"
  en tarjetas (`RuneCard`), sin imágenes.
- Fuentes: **Almendra** (Regular, Bold) para display/headline/title; **Source Sans 3**
  (Regular, SemiBold, Bold, variable) para el resto, con `fontVariations`. Licencias en
  `assets/licenses/`. Se eliminan Cinzel y Alegreya.
- Pantalla **Personalización** (`/settings/appearance`, desde el menú de usuario): tema oscuro /
  claro / sistema, tamaño de texto (normal / grande), animaciones (todas / reducidas), guardado en
  `shared_preferences`.

### Iconos propios (Opus)
- `assets/icons/` se vacía de game-icons y se sustituye por un set **dibujado en el repositorio**
  (licencia del proyecto): SVG 24×24, trazo 2, estilo talla en piedra (líneas rectas, esquinas
  marcadas). Lista: 12 clases, d20, corazón, escudo, espada, arco, bastón, llama (furia), rayo,
  luna, hoguera, sol, pergamino, libro, pluma, mapa, brújula, corona, capucha, sobre, cofre,
  mochila, monedas, calendario, yunque, poción, calavera, ojo, cadenas (condición), chispa
  (magia), sello (conexión), runa, flecha arriba (nivel), lágrima (curación), gota (daño),
  engranaje, usuarios, ancla (concentración), reloj. `AppIcons` conserva nombres; `ATTRIBUTION.md`
  deja de listar game-icons y la pantalla Atribuciones solo cita SRD y fuentes.

### Animaciones (Opus; `core/motion/`)
Todas con `AnimationController` + `CustomPainter`, respetan "animaciones reducidas" y nunca
bloquean la interacción. Duración en ms.
- Furia / Ataque temerario: llamas que suben por los bordes de la tarjeta al activar (600) y brasa
  constante mientras está activo.
- Castigo divino: destello dorado radial (400).
- Ki y pips de recurso: el pip se apaga con un trazo (200).
- Daño: sacudida horizontal de la barra de PG y tinte `blood` (350); curación: pulso `moss` (350).
- 0 PG: viñeta oscura en los bordes de la pantalla mientras esté a 0.
- Descanso corto: hoguera (1200); largo: luna que cruza (1500), ambos al aprobarse.
- Subir de nivel: runas que ascienden y el número de nivel que crece con rebote (1800).
- Mensaje del DM: sello de lacre que se rompe (600).
- Conexión en vivo: sello rúnico que late (ciclo 2400) solo cuando está conectado.
- Transiciones de página: desvanecido + desplazamiento 12 px (220).

### Tests 16d
`theme_test` con la nueva paleta; iconos existen; `appearance_test` (cambiar tema persiste);
cada animación tiene un test de que no lanza y respeta "reducidas".

---

## Documentación
- `CLAUDE.md`: descansos con aprobación del DM, nivel concedido por el DM, PG por tirada física,
  dotes siempre, paleta oscura por defecto, iconos propios (sin game-icons), fuentes Almendra y
  Source Sans 3; sección "Modelo de permisos" actualizada.
- `docs/content-packs.md`: formato v2. `docs/ADR/0004`: descansos y nivel. `deploy/README.md`:
  rate limit y diagnóstico de conexión.

## Verificación
`cd server && dotnet build && dotnet test`; `cd app && flutter analyze && flutter test`. Manual
con dos móviles: jugador pide descanso corto → DM aprueba → PG suben en el móvil del jugador sin
tocar nada; DM concede nivel → jugador completa el asistente → la hoja muestra la elección y su
desglose; pantalla Servidor → "Probar conexión" con los cuatro pasos en verde.
