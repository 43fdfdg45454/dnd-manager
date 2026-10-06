# Fase 13 — Sistema de diseño, modos de vista, temas por clase y cliente en tiempo real

Contrato cerrado. Todo en `app/`. Sub-fases en orden: 13a → 13b → 13c → 13d. Cada una termina con
`flutter analyze` sin avisos y `flutter test` en verde. Textos visibles en español; código en inglés.

## 13a — Sistema de diseño "místico" (Sonnet)

Objetivo: identidad épica, mística y medieval, legible en claro y oscuro, sin romper los widgets
existentes (que siguen usando `Theme.of(context).colorScheme.*`).

### Tokens (`app/lib/core/theme/tokens.dart`)

`class AppTokens extends ThemeExtension<AppTokens>` con estos colores (claro / oscuro):

| Token | Claro | Oscuro | Uso |
|---|---|---|---|
| `parchment` | `#F3E9D2` | `#1C1814` | fondo de página |
| `parchmentDeep` | `#E6D6B4` | `#26211B` | tarjetas "pergamino" |
| `stone` | `#D8D1C5` | `#332E28` | app bar, tarjetas secundarias |
| `ink` | `#2B2118` | `#EDE3CF` | texto principal |
| `inkMuted` | `#6B5B4B` | `#A8997F` | texto secundario |
| `gold` | `#B8860B` | `#D4A83A` | acentos, bordes, pestaña activa |
| `crimson` | `#8B1E1E` | `#C0392B` | acción principal, daño |
| `arcane` | `#4B3F8F` | `#8C7BE0` | magia, hechizos |
| `emerald` | `#2E6B3F` | `#5BBF7A` | curación, éxito |
| `rune` | `#7A6A52` | `#5A4E3C` | líneas, contornos |

`AppTokens.light`, `AppTokens.dark`, `lerp`, `copyWith`, y extensión `context.tokens`.

### Esquema de color y tema (`app_theme.dart`, `components.dart`, `typography.dart`)

- `ColorScheme` construido a mano (no `fromSeed`): `primary = crimson`, `onPrimary` claro,
  `secondary = gold`, `tertiary = arcane`, `error` rojo accesible, `surface = parchment`,
  `surfaceContainerLow/High = parchmentDeep/stone`, `onSurface = ink`, `onSurfaceVariant = inkMuted`,
  `outline = rune`. Mismos roles en oscuro. `useMaterial3: true`.
- Componentes: `CardThemeData` (relleno `parchmentDeep`, borde `gold` al 40 %, radio 10, margen
  16/8 como hoy), `AppBarTheme` (fondo `stone`, texto `ink`, centrado), `FilledButtonTheme`
  (crimson), `OutlinedButtonTheme` (borde gold), `TabBarTheme` (indicador gold 3 px, etiquetas en
  Cinzel), `ChipTheme` (contorno rune), `NavigationBarTheme` (indicador gold translúcido),
  `ProgressIndicatorTheme` (pista stone), `DividerTheme` (rune), `SnackBarTheme`, `DialogTheme`.
- Widgets nuevos: `ParchmentCard` y `StoneCard` (envoltorios finos de `Card`), `SectionHeader`
  (título en Cinzel con filete dorado a los lados), `RuneDivider`.
- Tipografía empaquetada (SIL OFL) en `assets/fonts/`: **Cinzel** (Regular, Bold) para
  display/headline/title; **Alegreya** (Regular, Italic, Bold) para body/label. Descargar los TTF
  desde el repositorio oficial de Google Fonts (github.com/google/fonts) y guardar las licencias en
  `assets/licenses/OFL-Cinzel.txt` y `OFL-Alegreya.txt`. Declarar `fonts:` en `pubspec.yaml`.
  Si la descarga no es posible desde el entorno, dejar el `pubspec` y el código preparados y
  documentar en el informe exactamente qué ficheros faltan (no inventar binarios).
- Test `app/test/theme_test.dart`: los tokens existen en ambos brillos; contraste `ink` sobre
  `parchment` y sobre `parchmentDeep` ≥ 4.5 (calcular luminancia relativa en el test); `MaterialApp`
  con `AppTheme.light()/dark()` renderiza una `Card` y un `FilledButton` sin errores.

### Iconos (`icons.dart`, `app_icon.dart`)

- SVG de **game-icons.net** (CC-BY 3.0) en `assets/icons/<nombre>.svg`, dependencia `flutter_svg`.
  Conjunto mínimo: 12 clases (`barbarian` axe-swing, `bard` lyre, `cleric` holy-symbol, `druid`
  oak-leaf, `fighter` crossed-swords, `monk` fist, `paladin` winged-shield, `ranger` bow-arrow,
  `rogue` dagger-rose, `sorcerer` fire-ray, `warlock` evil-eye, `wizard` wizard-staff) y glifos de
  UI: `d20`, `heart`, `shield`, `scroll`, `potion`, `campfire` (descanso corto), `moon` (descanso
  largo), `sun`, `anvil`, `sword`, `spellbook`, `map`, `castle`, `treasure`, `skull`, `envelope`,
  `crown` (DM), `hood` (jugador), `compass` (general), `sparkles` (magia), `backpack`, `coins`,
  `calendar`, `book`, `quill`, `users`. Nombres de autor y enlace por icono en
  `assets/icons/ATTRIBUTION.md`.
- `enum AppIcons` con la ruta de cada asset; widget `AppIcon(AppIcons.x, {size = 24, color})` que
  usa `SvgPicture.asset` con `ColorFilter` del `IconTheme` cuando no se pasa color.
- Pantalla "Atribuciones" (`app/lib/features/home/ui/attribution_page.dart`, ruta
  `/attributions`, `Key('home-attribution')` en el menú de usuario de la home): SRD 5.1 CC-BY 4.0,
  game-icons.net CC-BY 3.0 (lista de autores), fuentes OFL. Si ya existe una pantalla de atribución
  del SRD, ampliarla en lugar de duplicar.
- Si no se pueden descargar los SVG, mismo criterio que con las fuentes: dejar el código listo con
  un `AppIcon` que haga *fallback* a un `Icon` de Material y documentar lo que falta.

### Regla transversal de la UI: origen de cada bonificación

Cualquier valor con modificador (`+X`, CA, HP máx, CD, ataque, daño…) se muestra con el widget
`StatValue` de la fase 11 y abre su desglose con **un toque** (dos como máximo si está dentro de
una tarjeta plegada). Ninguna pantalla nueva (Mesa del DM, Mi sesión, asistente, paneles de clase)
muestra un número con bonificación sin ese desglose.

## 13b — Temas por clase y paneles de todas las clases (Opus)

- `app/lib/features/characters/domain/class_theme.dart`:
  `ClassTheme { String index; String labelEs; Color accentLight; Color accentDark; AppIcons icon; }`,
  `classThemeOf(String index)` con fallback `aventurero` (icono `d20`, acento `gold`). Acentos:
  barbarian `#A6341B`, bard `#B0579A`, cleric `#D9A441`, druid `#4F7F3A`, fighter `#7E5A3C`, monk
  `#3F8FA8`, paladin `#C7B46A`, ranger `#2F6B4F`, rogue `#4A4A4A`, sorcerer `#C2453F`, warlock
  `#5E3A8A`, wizard `#3A5CA8` (variante oscura aclarada ~20 %). `ClassAccent(child)` envuelve con
  `Theme(copyWith(colorScheme: primary: acento))` solo cabecera del personaje, `HpCard` y panel de clase.
- Paneles en `app/lib/features/characters/ui/combat/panels/<clase>.dart`; los tres existentes
  (bárbaro, mago, paladín) se mueven sin cambiar Keys. Nuevos, sobre la API genérica de recursos
  (`spendResource/restoreResource`, `findResource`, `onceSinceLongRest`) y datos del `ClassPanelDto`:
  - bardo: Inspiración bárdica (pips; dado por nivel d6/d8/d10/d12).
  - clérigo: Canalizar divinidad (usos) y texto de Destruir muertos vivientes por nivel.
  - druida: Forma salvaje (usos, CR máximo por nivel, interruptor local "en forma salvaje").
  - guerrero: Segundo aliento (botón que cura `1d10 + nivel` vía `patchCombat`), Oleada de acción,
    Indomable.
  - monje: puntos de Ki (pips) con botones Ráfaga de golpes / Defensa paciente / Paso del viento
    (gastan 1); dado de artes marciales por nivel.
  - pícaro: Ataque furtivo `Nd6` por nivel, recordatorio de Acción astuta, Esquiva asombrosa.
  - hechicero: Puntos de hechicería (pips) con convertir slot ↔ puntos (gastar/restaurar + slot).
  - brujo: slots de pacto (ya en `combat.pactSlots`), invocaciones desde los rasgos.
  - explorador: Enemigo predilecto / Explorador natural (texto), botón "Marca del cazador" que fija
    concentración (`setConcentration`).
  Keys: `class-panel-<index>` y `<index>-<accion>`.
- **Servidor** (pequeño ajuste permitido en esta sub-fase): `ClassResourceRules` debe generar los
  recursos automáticos `bardic-inspiration`, `channel-divinity`, `wild-shape`, `second-wind`,
  `action-surge`, `indomitable`, `ki`, `sorcery-points` con máximos por nivel SRD y recarga
  correcta; comprobar y completar, con tests en `Dnd.Domain.Tests`.
- HP máximo editable: en `HpCard`, tocar "/ máx" (`Key('hp-max-edit')`) abre diálogo numérico;
  guardar envía `saveSheet` con el override `hitPointsMax` (o lo quita con "Volver al cálculo").
  Marca de override visible.
- Tests: `class_theme_test.dart` (12 entradas + fallback), `class_panels_test.dart` (un test por
  panel nuevo: renderiza y gasta un recurso), `hp-max-edit` envía el override.

## 13c — Shell de campaña y modos General / DM / Jugador (Opus shell y DM, Sonnet General)

- `app_router.dart`: `StatefulShellRoute.indexedStack` bajo `/campaigns/:id` con ramas
  `/campaigns/:id/general` (todos), `/campaigns/:id/dm` (DM/Owner) y `/campaigns/:id/player`
  (Player). `AppRoutes.campaign(id)` devuelve `/campaigns/$id/general`; `/campaigns/:id` redirige a
  `/general`. Función pura `campaignModeRedirect(CampaignRole? role, String location) → String?`
  junto a `authRedirect`, con tests. Rutas hijas (lore, mapas, sesiones, tiendas, solicitudes…)
  siguen como rutas planas del navegador raíz.
- `campaign_shell.dart` (`features/campaigns/ui/campaign_shell.dart`): `NavigationBar` con dos
  destinos: "General" (`nav-general`, icono `compass`) y, según rol, "Mesa del DM" (`nav-dm`,
  `crown`) o "Mi sesión" (`nav-player`, `hood`). App bar con nombre de campaña, icono de estado de
  tiempo real (`realtime-status`, 13d) y menú.
- **Vista General** (`features/campaigns/ui/general/campaign_general_page.dart`): cuadrícula de
  tarjetas de sección con `AppIcon` (Lore `book`, Mapas `map`, Sesiones `calendar`, Diario `quill`,
  Miembros `users`, Tiendas `coins`, Biblioteca `scroll`, Contenido `anvil`, Ajustes). Cada tarjeta
  abre una página completa (`/campaigns/:id/general/<seccion>` como rutas hijas de la rama) que
  envuelve el widget existente sin cambios: `LoreTab`, `MapsTab`, `SessionsTab`, `JournalTab`,
  `ShopsTab`, `HomebrewTab`, `_MembersTab` (extraer a `members_section.dart`), `_SummaryTab`
  (ajustes: editar, transferir, eliminar, calendario, salir). Keys `general-<seccion>`.
- **Vista DM** (`features/session/ui/dm/dm_session_page.dart`, carpeta nueva `features/session`
  con `data/` (repositorio `PartyRepository`: `GET /party`, `POST /party/rest`, `POST /party/adjust`;
  `MessagesRepository`: enviar, listar, leer, no-leídos) y `ui/`):
  - `PartyRoster`: una fila por personaje activo con retrato, nombre, clase/nivel con acento de
    clase, barra de HP (temporales en `arcane`), chips de condiciones, icono de concentración,
    death saves si HP 0. Tocar abre `DmCharacterSheet` (bottom sheet) con: daño/curación rápidos
    (`party/adjust`), temporales, condiciones (añadir/quitar), PG máximos, y enlace "Ver hoja
    completa" (`/characters/:id`).
  - Barra de acciones: "Descanso corto" (`campfire`) y "Descanso largo" (`moon`) para todos o para
    seleccionados (selección múltiple en el roster); "Mensaje secreto" (`envelope`) → compositor con
    selección de personajes y texto; "Dados" (`showDiceSheet`).
  - Tarjeta "Botín del grupo" (`AppIcon treasure`): oro común y objetos del alijo
    (`GET /stash`); acciones del DM: "Añadir botín" (buscador de catálogo + cantidad, u objeto
    personalizado con `item_fields_form`), "Añadir oro", "Repartir oro" (a todos o seleccionados),
    editar cantidad/notas, quitar, y "Dar a…" (tomar en nombre de un personaje). Keys `stash-*`.
  - Tarjetas: "Tiendas" (lista con conmutador abierta/cerrada → `PATCH /shops/{id}` `isOpen`),
    "Solicitudes pendientes" (contador + enlace a `ChangeRequestsPage`), "Próxima sesión"
    (`NextSessionCard` existente).
- **Vista Jugador** (`features/session/ui/player/player_session_page.dart`): localiza mi personaje
  activo en la campaña (selector si hay varios; aviso "No tienes personaje en esta campaña" con
  botón crear). `SegmentedButton` `player-subview`: **Combate** / **Fuera de combate**.
  - Combate: cabecera con acento de clase, `HpCard`, `StatsCard`, `DeathSavesCard`, `ConditionsCard`,
    `AttacksSection` con desglose, `SpellSlotsSection`, `ResourcesSection`, `ClassPanelsSection`,
    `ConsumablesSection` filtrado por `isCombatUsable` (nuevo en `items/domain/combat_usable.dart`:
    armas, escudo, armadura, consumibles, objetos con cargas), sin `RestSection`.
  - Fuera de combate: `RestSection`, "Preparar hechizos" (`SpellsTab`), "Rasgos" (`TraitsTab`),
    trasfondo y notas (`NotesTab`), "Inventario" (`InventoryTab`), "Tiendas abiertas" (lista con
    enlace a `ShopPage`), "Botín del grupo" (alijo con oro común; botón "Tomar" por objeto con
    cantidad, visible solo si `playersCanTakeFromStash`; desde el inventario propio, acción
    "Devolver al grupo"), "Mensajes del DM" (bandeja con badge de no leídos; abrir marca leído).
  - La vista nunca enlaza hojas de otros jugadores. `CharacterPage` sigue existiendo para la hoja
    completa y los borradores.
- `CharactersTab` dentro de General muestra a cada jugador solo sus personajes con enlace; los de
  otros, solo nombre/clase/nivel sin enlace (el servidor ya devuelve HP nulo para ellos).

### Tests 13c
- Helper compartido `app/test/helpers/app_pump.dart` que construye el router real con fakes.
- Adaptar `campaigns_test.dart` y `characters_test.dart` (ya no hay `tab-*` en la campaña; las
  secciones se abren desde `general-*`). Conservar todos los Keys de combate, editor, cabecera,
  miembros, solicitudes.
- Nuevos: `campaign_modes_test.dart` (jugador redirigido fuera de `/dm`, DM fuera de `/player`;
  roster muestra HP y condiciones; descanso largo llama a `party/rest` con todos; ajuste de daño
  llama a `party/adjust`), `player_session_test.dart` (sub-vistas; consumibles filtrados; bandeja
  marca leído), `members_section_test` si se extrae lógica.

## 13d — Cliente en tiempo real (Opus)

- Dependencia `signalr_netcore`. `app/lib/core/realtime/`:
  - `realtime_events.dart`: `sealed class CampaignEvent` (`MessageReceived`, `CharacterUpdated`,
    `PartyRest`, `PartyStashUpdated`, `ShopUpdated`, `ChangeRequestUpdated`, `SessionUpdated`, `Unknown`) parseado de
    `{ type, campaignId, characterId, entityId, at }`.
  - `realtime_hub.dart`: interfaz `RealtimeHub { Stream<CampaignEvent> events; Future<void> connect(campaignId); Future<void> disconnect(); RealtimeStatus status; }`
    e implementación `SignalRRealtimeHub` (`HubConnectionBuilder` a `<servidor>/hubs/campaign`,
    `accessTokenFactory` desde el almacén de tokens, `withAutomaticReconnect`, `invoke('JoinCampaign')`
    tras conectar y tras reconectar).
  - `realtime_provider.dart`: `campaignRealtimeProvider = NotifierProvider.family<CampaignRealtime, RealtimeStatus, String>`;
    conecta al montarse el shell de campaña y desconecta al salir; sin red (`connectivityProvider`)
    no intenta conectar y marca `offline`. Al recibir eventos invalida: `CharacterUpdated` →
    `characterControllerProvider(id)` y lista de personajes y `party`; `PartyRest` → ídem + snackbar
    "El DM ha declarado un descanso corto/largo"; `ShopUpdated` → tiendas; `PartyStashUpdated` → alijo; `ChangeRequestUpdated` →
    solicitudes y contador; `MessageReceived` → bandeja y contador + banner "Mensaje del DM";
    `SessionUpdated` → sesiones.
  - Icono `realtime-status` en el shell: conectado (`sparkles` dorado), reconectando (gris),
    sin conexión (tachado).
- `test/helpers/fake_realtime_hub.dart` para emitir eventos en tests. `realtime_test.dart`: evento
  `character.updated` invalida el provider; sin red no conecta; `message.received` muestra banner.

## Verificación global de la fase

`cd app && flutter analyze && flutter test`. Manual en dispositivo: tema claro/oscuro; DM ve
"Mesa del DM" y no "Mi sesión" (y viceversa); descanso largo forzado aparece en el móvil del
jugador sin refrescar; mensaje secreto llega solo al destinatario.
