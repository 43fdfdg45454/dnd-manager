# Fase 33 — División de la app en núcleo y módulo 5e

Contrato de implementación. Cierra la separación que empezó el servidor en la fase 32 (ADR 0009):
la app Flutter queda en tres paquetes Dart, `opentrpg` (anfitrión), `opentrpg_core` (núcleo
genérico) y `opentrpg_dnd5e` (módulo D&D 5e), y el núcleo habla con el módulo solo a través del
contrato `GameSystemUi`. Parte del inventario y del contrato de `docs/specs/fase-30-inventario-y-contrato.md`
(§2 y §5) y de las rutas del servidor de la fase 32B (`/api/v1/systems/dnd5e/...`).

Sin compatibilidad hacia atrás (decisión del propietario, 2026-10-10): se puede cambiar el nombre
del paquete Dart, el `applicationId` Android y cualquier forma interna; lo que no cambia es el
comportamiento visible, los caminos de las rutas de `GoRouter` y las claves (`Key`) que usan los
tests.

## 0. Estado de partida (medido en master, 2026-10-10)

`app/lib` tiene 289 ficheros Dart: 168 de núcleo, 81 de 5e, 38 mixtos y 2 del anfitrión (`main.dart`,
`app.dart`), con la clasificación de la fase 30 §2. Las aristas de importación que impiden una
división mecánica son 225: 21 de ficheros de núcleo a ficheros 5e, 83 de núcleo a mixtos y 121 de 5e a
mixtos. Los concentradores son `features/characters/data/models.dart` (2 590 líneas, 72 tipos),
`characters_controller.dart`, `characters_repository.dart`, `core/router/app_router.dart` (44 rutas),
`items/data/models.dart`, `session/data/models.dart` y `session_controllers.dart`,
`core/realtime/realtime_events.dart` y `realtime_provider.dart`, `core/ui/stat_value.dart`,
`stat_tiles.dart`, `source_chip.dart` y `features/dice/data/dice_controller.dart`.

Un script de comprobación reproduce la medida (lo incluye la entrega 33A como test, §4).

## 1. Resultado final

```
app/
  pubspec.yaml            paquete `opentrpg` (anfitrión) y raíz del workspace de pub
  lib/main.dart           arranque: proveedores de plataforma + OpenTrpgApp(systems: [Dnd5eUi()])
  lib/app.dart            OpenTrpgApp (MaterialApp.router, tema, localización) — o se mueve al núcleo
  android/, assets/       sin cambios de contenido; fuentes e iconos siguen declarados aquí
  test/                   TODOS los tests actuales siguen aquí (ver §5)
  packages/core/          paquete `opentrpg_core`: lib/core (todo salvo lo indicado) + features de núcleo
  packages/dnd5e/         paquete `opentrpg_dnd5e`: catálogo, hoja, asistente, subida de nivel, grupo…
```

Dependencias: `opentrpg` → `opentrpg_core`, `opentrpg_dnd5e`; `opentrpg_dnd5e` → `opentrpg_core`;
`opentrpg_core` no depende de ningún módulo. Los tres comparten versiones con un **workspace de pub**
(`workspace:` en `app/pubspec.yaml`, `resolution: workspace` en los dos paquetes; un solo
`pubspec.lock`). Los assets (iconos SVG, licencias, fuentes) se quedan declarados en el anfitrión:
los paquetes los cargan con las mismas claves (`assets/icons/...`) y los tests del anfitrión los
tienen disponibles; nada se mueve a `packages/*/assets`.

Nombres: paquete Dart `dnd_companion` → `opentrpg`; `DndCompanionApp` → `OpenTrpgApp`;
`applicationId` y `namespace` Android `com.dndcompanion.dnd_companion` → `com.opentrpg.app`
(con la carpeta de `MainActivity.kt`), `android:label` → `OpenTRPG`. El cambio de `applicationId`
va en un commit propio ("Cambiar el identificador Android a com.opentrpg.app") para poder revertirlo
si el propietario decide conservar el antiguo.

## 2. Contrato `GameSystemUi` (núcleo)

Vive en `opentrpg_core/lib/systems/game_system_ui.dart`. Adapta el §5.1 de la fase 30 a lo
aprendido en la 32: el detalle del personaje del servidor es un JSON plano (núcleo + campos del
sistema al mismo nivel), así que **los modelos mixtos del núcleo conservan el JSON completo** y el
módulo lee de ahí su parte.

```dart
/// Lo que un sistema de juego aporta a la app. El núcleo lo elige por `campaign.systemId`.
abstract class GameSystemUi {
  String get id;                                   // 'dnd5e'
  String get name;                                 // 'D&D 5e (SRD 5.1)'
  List<SystemAttribution> get attributions;        // SRD CC-BY 4.0 → AttributionPage

  /// Rutas propias (subida de nivel, preparar conjuros, elecciones, detalles del compendio…).
  /// Los caminos no cambian respecto a hoy.
  List<RouteBase> routes(GlobalKey<NavigatorState> rootNavigatorKey);

  // Ficha
  Widget combatView(BuildContext context, CharacterDetail character, {Widget? header});
  List<SheetTab> detailTabs(CharacterDetail character);     // Resumen, Habilidades, Rasgos, Hechizos
  Widget? pendingActionsCard(BuildContext context, CharacterDetail character);
  Widget? sheetEditorSection(SheetEditorScope scope);        // parte 5e del editor manual
  Widget? heightWeightRoller(HeightWeightScope scope);       // botón "tirar" con la tabla de la raza

  // Creación
  Future<void> openCreationWizard(BuildContext context, String campaignId);

  // Catálogo y objetos
  List<CompendiumTab> compendiumTabs();
  Widget itemExtras(EffectiveItem item);                     // chips de daño, CA, rareza, sintonía
  Widget? itemFormSection(ItemFormController form);          // campos 5e del homebrew
  bool isCombatUsable(EffectiveItem item);
  Future<void> openAttunement(BuildContext context, String characterId, CharacterItem item);

  // Campaña
  Widget partyPanel(BuildContext context, String campaignId); // barra de acciones + roster del DM
  String rosterSubtitle(CharacterSummary summary);           // "Elfo · Mago 3"
  ChangeDetail? describeChangeRequest(ChangeRequest request); // EditSheet, Companion…
  String? staleScope(String campaignId);                     // ruta cuya caché cuenta para el aviso offline (party)

  // Dados, moneda, tiempo real
  RollClass classifyRoll(DiceResult result);                 // crítico, pifia, normal
  String formatMoney(int minorUnits);                        // "12 po 5 pp"
  void onRealtimeEvent(WidgetRef ref, UnknownCampaignEvent event); // party.rest, levelUp.granted
}
```

Reglas:

- **Modelos del núcleo con `raw`.** `CharacterSummary`, `CharacterDetail`, `EffectiveItem`,
  `CharacterItem`, `ItemOverrides`, `ChangeRequest`, `RestRequest` y `PartyStash`/`StashItem` se
  quedan en el núcleo con sus campos genéricos (fase 30 §3.1 y §2.2) y un campo
  `Map<String, dynamic> raw` con el JSON tal como llegó. Lo que hoy es 5e en esos tipos (`sheet`,
  `classes`, `spellcasting`, daño, CA, rareza, sintonía, modificadores…) pasa a tipos del módulo que
  se construyen desde `raw` (`Dnd5eCharacter.fromDetail(detail)`, `Dnd5eItem.of(item)`), cacheados
  con providers del módulo (`dnd5eCharacterProvider(characterId)` deriva de `characterProvider`).
- **`ValueBreakdown`/`BreakdownPart`** son del núcleo (`core/ui/breakdown.dart`); `StatValue`
  recibe `Color? accent` en vez de buscar el tema de la clase.
- **`Page<T>` y `CatalogSource`** (paginación y fuentes del catálogo) pasan al núcleo
  (`core/catalog/`), con `catalogSourcesProvider`; el resto del catálogo es del módulo.
- **Dinero**: `copperToGoldText` deja el núcleo; `formatMoney` del sistema. Las pantallas de
  núcleo (inventario, tienda, alijo, peticiones) lo obtienen con `ref.watch(campaignSystemUiProvider(campaignId))`.
- **Registro**: `gameSystemsProvider` (lista que registra el anfitrión), `gameSystemUiProvider(systemId)`,
  `campaignSystemUiProvider(campaignId)` y `characterSystemUiProvider(characterId)`. Un sistema que
  la app no trae devuelve `UnsupportedSystemUi`, que pinta "Esta app no incluye el sistema X" en
  ficha, compendio y Mesa del DM sin romper el resto de la campaña.
- **Compendio global** (decisión 6 de la fase 30): pestañas del sistema por defecto de la instancia
  (`GET /api/v1/systems`, `isDefault`); selector solo si hay más de un sistema registrado en la app
  y en el servidor. Los dados globales clasifican con el sistema por defecto.
- **Rutas**: `AppRoutes` del núcleo conserva los caminos de núcleo; los ayudantes 5e
  (`levelUp(id)`, `prepareSpells(id)`, `invalidChoices(id)`, `restRolls(id)`, `characterWizard`,
  `spellDetail`, `itemDetail`, `classDetail`, `raceDetail`, `beastDetail`, `featureDetail`,
  `rollTable`) pasan a `Dnd5eRoutes` del módulo con los mismos caminos. `routerProvider` del
  núcleo concatena las rutas de todos los sistemas registrados.
- **Tiempo real**: `CampaignEvent` del núcleo mantiene los eventos de núcleo y añade
  `UnknownCampaignEvent(type, data)`; `PartyRest` y `LevelUpGranted` pasan al módulo, que los
  reconstruye en `onRealtimeEvent`. `realtime_provider.dart` deja de importar controladores 5e.
- **HTTP**: el módulo llama a `/api/v1/systems/dnd5e/...` (ya en master, fase 32B) con el
  `ApiClient` del núcleo.

## 3. Reparto de ficheros

Clasificación de la fase 30 §2 con estos ajustes (todo lo no listado sigue su clasificación):

| Fichero | Destino |
|---|---|
| `core/ui/action_type.dart`, `core/ui/spell_category.dart` | módulo |
| `core/ui/source_chip.dart` | núcleo (usa `CatalogSource` y `catalogSourcesProvider` del núcleo) |
| `core/ui/stat_value.dart`, `stat_tiles.dart` | núcleo (sin `class_theme`; `ValueBreakdown` del núcleo) |
| `core/realtime/realtime_events.dart`, `realtime_provider.dart` | núcleo + `UnknownCampaignEvent`; ramas 5e al módulo |
| `core/router/app_router.dart` | núcleo (`AppRoutes` + `routerProvider` que suma `routes()` de los sistemas) y `Dnd5eRoutes` en el módulo |
| `features/characters/data/models.dart` | se parte: `core/characters/models.dart` (summary, detail con `raw`, `ChangeRequest`, `ChangeRequestType/Status`, `CharacterStatus`, `RestRequest`, `PendingRest`, `RestKind`, `SheetSaveResult`) y `dnd5e/characters/models.dart` (el resto) |
| `characters_repository.dart` | núcleo (lista, crear, obtener, enviar, activar, retrato, dueño, borrar, peticiones de cambio, perfil) y `Dnd5eCharactersRepository` (hoja, orígenes, elecciones, daño, combate, compañero, concentración, espacios, recursos, descansos, acciones de clase, subida, preparación) |
| `characters_controller.dart` | núcleo (`CampaignCharactersController`, `CharacterController`, `ChangeRequestsController`, `pendingChangeRequestCountProvider`); `spellInfoProvider`, `invalidChoicesProvider` al módulo |
| `view_mode_controller.dart` | núcleo (`CharacterView`, memoria de vista, `PlayerSessionTab`); la lista de subpestañas la da `detailTabs` |
| `domain/character_format.dart` | `CharacterPermissions` al núcleo; `copperToGoldText` → `Dnd5eUi.formatMoney`; el resto módulo |
| `change_details.dart`, `payload_format.dart` | detalles de objeto, dinero y texto al núcleo; `SheetChangeDetail` y formatos 5e al módulo vía `describeChangeRequest` |
| `ui/character_page.dart`, `character_detail_tabs.dart` | núcleo (armazón, Inventario, Notas); vistas 5e por `combatView`/`detailTabs`/`pendingActionsCard` |
| `ui/character_tabs.dart` | `NotesTab`, `OverrideMark` al núcleo; `SummaryTab`, `SkillsTab`, `TraitsTab`, `SpellsTab` al módulo |
| `ui/characters_tab.dart`, `new_character_dialog.dart`, `height_weight_fields.dart`, `sheet_editor_page.dart`, `character_avatar.dart`, `change_owner_dialog.dart` | núcleo con los huecos del contrato |
| `features/catalog/ui/detail_widgets.dart` | núcleo (`core/ui/detail_widgets.dart`) salvo `ModifierLines` (módulo) |
| `features/catalog/data/models.dart` | `Page<T>`, `CatalogSource` al núcleo; el resto módulo |
| `features/items/data/models.dart` y `domain/items_format.dart` | núcleo con `raw`; daño/CA/rareza/sintonía/modificadores a `dnd5e/items/` |
| `items/domain/combat_usable.dart`, `ui/attunement_dialog.dart` | módulo (`isCombatUsable`, `openAttunement`) |
| `items/ui/inventory_tab.dart`, `effective_item_page.dart`, `item_fields_form.dart`, `item_composer.dart`, `homebrew_tab.dart`, `item_search_list.dart`, `shop_catalog_page.dart` | núcleo con `itemExtras`/`itemFormSection`; la búsqueda en catálogo recibe el repositorio por el sistema |
| `features/session/data/models.dart` | `StashItem`, `PartyStash`, `DirectMessage` al núcleo; `PartyMember`, `PartyAdjust*`, `PartyRestKind` al módulo |
| `session_controllers.dart` | `PartyController` al módulo; el resto núcleo |
| `party_repository.dart`, `dm/party_roster.dart`, `dm/dm_character_sheet.dart`, `player/combat_items_section.dart` | módulo |
| `rest_requests_repository.dart` | núcleo (tipo y payload opacos) |
| `ui/dm/dm_session_page.dart` | núcleo; barra de acciones y roster por `partyPanel` |
| `ui/player/player_session_page.dart` | núcleo; Combate y Detalle por el contrato |
| `change_requests/ui/change_requests_page.dart` | núcleo; detalle 5e por `describeChangeRequest` |
| `dice/data/dice_controller.dart`, `ui/dice_sheet.dart` | núcleo; crítico/pifia por `classifyRoll` |
| `catalog/ui/compendium_page.dart` | núcleo; pestañas por `compendiumTabs` |
| `home/ui/attribution_page.dart` | núcleo; atribuciones de `gameSystemsProvider` |
| `campaigns/ui/campaign_shell.dart`, `core/realtime/connection_banner.dart` | núcleo; el ámbito extra de caché por `staleScope` |
| `features/systems/` | núcleo |

Las carpetas del módulo siguen la misma forma `lib/<feature>/{data,domain,ui}` dentro de
`packages/dnd5e/lib/`; las del núcleo, `packages/core/lib/core/...` y `packages/core/lib/features/...`.
Las importaciones entre paquetes van con `package:opentrpg_core/...` y `package:opentrpg_dnd5e/...`;
dentro de un paquete, relativas como hoy.

## 4. Entregas

Dos entregas, cada una con el servidor intacto, `flutter analyze` limpio, los 956 tests en verde y su
propio PR.

### 33A — Capas de datos y contrato

Sin crear paquetes todavía (todo sigue en `app/lib`), deja el código en capas y el contrato
definido:

1. `GameSystemUi` (§2), `UnsupportedSystemUi`, registro de sistemas y `OpenTrpgApp(systems: …)`;
   `Dnd5eUi` en `lib/systems/dnd5e/dnd5e_ui.dart` implementando **todos** los miembros (por ahora
   devolviendo los widgets actuales).
2. Partición de los concentradores de datos y dominio de §3: modelos de personaje, objetos,
   sesión, repositorios, controladores, `character_format`, `change_details`, `catalog` (`Page`,
   `CatalogSource`), `ValueBreakdown`, eventos de tiempo real, `realtime_provider`, `stat_value`,
   `stat_tiles`, `source_chip`, `dice_controller`, `app_router` + `Dnd5eRoutes`.
3. Test de capas `app/test/architecture/layers_test.dart`: lee `lib/**` y comprueba que ningún
   fichero del conjunto **núcleo** (`lib/core/**` y las carpetas y ficheros marcados núcleo en §3 y en
   la fase 30 §2) importa un fichero del conjunto **5e**; el conjunto 5e queda enumerado en el test
   por carpeta (`features/catalog` salvo lo que sube al núcleo, `features/characters/{ui/combat,
   ui/level_up, ui/wizard, …}`, `lib/systems/dnd5e`…). En 33A el test cubre `lib/core/**`, todos los
   `data/` y `domain/` de núcleo y las páginas de núcleo que ya usen el contrato; 33B lo extiende a
   todo.
4. Renombrado del paquete Dart a `opentrpg` (imports `package:opentrpg/...` en los tests) y
   `OpenTrpgApp`; identificador Android en commit aparte (§1).

### 33B — Interfaz por contrato y paquetes

1. Resto de los ficheros mixtos de UI de §3 (`character_page`, `character_detail_tabs`,
   `character_tabs`, `characters_tab`, `new_character_dialog`, `height_weight_fields`,
   `sheet_editor_page`, `inventory_tab`, `effective_item_page`, formularios de objeto,
   `dm_session_page`, `player_session_page`, `change_requests_page`, `dice_sheet`,
   `compendium_page`, `attribution_page`, `campaign_shell`, `connection_banner`): el núcleo deja de
   importar nada 5e y usa el contrato.
2. Creación de `packages/core` y `packages/dnd5e` con el workspace de pub, movimiento físico con
   `git mv` (historial conservado), `cache_database.g.dart` regenerado en su paquete y commiteado como
   hoy.
3. `main.dart`: `OpenTrpgApp(systems: [Dnd5eUi()])`. Un test del anfitrión comprueba que el router
   incluye las rutas 5e (`/characters/:id/level-up`, `/compendium/spells/:index`) y que
   `gameSystemUiProvider('dnd5e')` resuelve.
4. CI (`.github/workflows/ci.yml`, trabajo "Cliente Flutter"): `flutter pub get` en `app`, análisis
   de los tres paquetes y `flutter test` en `app`. Si `flutter analyze` en `app` no recorre los
   paquetes del workspace, se analizan explícitamente (`flutter analyze packages/core packages/dnd5e .`).
5. Documentación: `CLAUDE.md` (estructura y comandos), `README.md`, `docs/PLAN.md` (filas 33A y 33B),
   y el test de capas pasa a ser redundante con los paquetes: se mantiene solo para `lib/` del
   anfitrión (que no debe contener lógica) o se elimina, a elección del implementador, dejándolo
   escrito en el PR.

## 5. Tests

- Todos los tests existentes se quedan en `app/test` y siguen pasando con las mismas claves y los
  mismos nombres; solo cambian las importaciones (`package:opentrpg_core/...`,
  `package:opentrpg_dnd5e/...`). Las fakes de `test/helpers/fakes.dart` se adaptan a los repositorios
  partidos.
- `test/features/characters/feature_indexes_test.dart` conserva su ruta relativa al SRD.
- Nuevos: `architecture/layers_test.dart` (33A), test del anfitrión (33B), tests unitarios de la
  partición de modelos (`CharacterDetail.fromJson` conserva `raw`; `Dnd5eCharacter.fromDetail`
  reproduce la hoja actual sobre el JSON de `test/fixtures` o de las fakes) y de `UnsupportedSystemUi`
  (ficha de una campaña con `systemId: 'otro'` muestra el aviso).

## 6. Lo que no cambia

- Comportamiento, textos, navegación y caminos de ruta.
- Servidor, migraciones y despliegue.
- Assets, fuentes, iconos y tema (siguen en el anfitrión y en `opentrpg_core/lib/core/theme`).
- El filtrado por paquetes activos y el formato de paquete 5e completo son de la fase 34.

## 7. Riesgos

| Riesgo | Mitigación |
|---|---|
| `models.dart` de personaje tiene 72 tipos entrelazados; partirlo rompe muchas importaciones a la vez | Partir primero el fichero en dos ficheros dentro de `app/lib` (33A) con `export` temporales y mover después (33B). Cada commit compila y pasa los tests. |
| `CharacterDetail` usado por pantallas de núcleo para datos 5e (nivel, clases en tarjetas y subtítulos) | Los datos pasan por `rosterSubtitle`/`raw`; el test de capas evita regresiones. |
| `flutter analyze` del workspace en CI | Verificar en local con los tres paquetes y dejar el comando explícito en `ci.yml`. |
| Tests que importan rutas de ficheros movidos | Los imports se cambian con `sed` por paquete en el mismo commit del movimiento; `flutter test` completo antes de cada push. |
