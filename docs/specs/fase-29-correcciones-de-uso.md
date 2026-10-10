# Fase 29 — Correcciones de uso

Siete hallazgos del propietario usando la app en una campaña real. Ninguno cambia reglas (SRD 5.1);
los textos visibles en español; colores solo de los tokens; sin reglas caseras. Cada bloque dice qué
se arregla, dónde y qué prueba lo cubre. Las claves de widgets existentes se conservan salvo que el
bloque diga lo contrario: las pruebas que ya pasan son el contrato.

Bloques independientes (se implementan en paralelo):

- **A** — bloques 1, 2, 4, 5 y 6 (solo app, salvo las descripciones del bloque 2 que tocan `Dnd.Application`).
- **B** — bloque 7 (navegación del jugador y de la ficha; solo app).
- **C** — bloque 3 (altura y peso: dominio, migración, DTO, paquetes, asistente y paquete PHB privado).

## 1. "Próxima sesión" del inicio que no caduca

Síntoma: la tarjeta `next-session-card` del inicio sigue mostrando una sesión cuya hora ya pasó, y
también una sesión de una campaña que se borró.

Causa: `NextSessionCard` (`app/lib/features/sessions/ui/next_session_card.dart`) toma
`sessions.first` del `mySessionsControllerProvider` tal cual se cacheó; el servidor sí filtra
(`ListMySessionsHandler`: `Scheduled` y `IsUpcomingOrInProgress(now)` = inicio + duración ≥ ahora, con
duración supuesta si no hay), pero el cliente no vuelve a pedir ni filtra por tiempo, y borrar una
campaña (`campaigns_controller.dart`) solo invalida la lista de campañas.

Corrección:

- `NextSessionCard` elige la primera sesión que **no haya terminado** según el reloj local: fin =
  `startsAt + duración` (si el modelo `Session` no trae duración, usar el mismo supuesto que el
  servidor: ver `GameSession.AssumedDurationMinutes`; exponer la constante en el modelo de la app con
  un comentario que cite al servidor) y que pertenezca a una campaña presente en
  `campaignsControllerProvider` cuando esa lista está cargada. Si no queda ninguna, la tarjeta se
  oculta.
- Invalidar `mySessionsControllerProvider` al borrar o abandonar una campaña
  (`campaigns_controller.dart`, junto a `_refreshList`) y al volver al inicio (en el `build` de la
  página de inicio, o con `ref.invalidate` en el pull-to-refresh si existe; elegir lo que ya hace la
  lista de campañas y hacer lo mismo).
- El reloj se inyecta como ya lo hace el resto de la app (`clockProvider` o equivalente; si no hay,
  `DateTime.now()` con un parámetro `now` opcional para pruebas).

Pruebas (widget, `app/test/features/sessions/`): con dos sesiones, la primera terminada hace una hora
y la segunda mañana, la tarjeta muestra la segunda; con una sola sesión terminada, no hay tarjeta;
con una sesión de una campaña ausente de la lista de campañas, no hay tarjeta; borrar una campaña
invalida el proveedor (prueba del controlador).

## 2. Descripciones en "Elecciones de raza y trasfondo"

Síntoma: en el paso de origen del asistente (`step_origin.dart` → `OriginChoiceView` →
`LevelUpOptionCard`) varias opciones salen solo con su nombre.

`LevelUpOptionCard` ya muestra `option.description` cuando no está vacía, así que el fallo está en
qué manda el servidor por cada tipo de elección (`OriginChoicesPlanner.Plan` en
`server/src/Dnd.Application/Characters/OriginChoices.cs`). Recorrer **cada** `yield` de `Plan` y
garantizar una descripción no vacía:

- Habilidades: `SkillDefinition.Description` (ya viene).
- Idiomas elegidos de lista: construir la descripción con el dataset del SRD
  (`server/seed/srd/5e-SRD-Languages.json`: tipo estándar/exótico, hablantes típicos, escritura), en
  español: "Idioma estándar. Hablantes típicos: humanos. Escritura común." Si los idiomas aún no se
  siembran como catálogo, sembrarlos (`LanguageDefinition`, seed idempotente) y pasar sus textos.
  Las elecciones de texto libre (`FreeText`) no cambian.
- Herramientas e instrumentos: descripción de la plantilla de objeto (`ItemTemplate`) o, si no la
  tiene, su categoría ("Herramientas de artesano").
- Trucos y conjuros de raza (alto elfo, etc.): primer párrafo de la descripción del conjuro, y en la
  app un `DetailInfoButton` que abre `openSpellDetail` (como hacen las tarjetas de conjuro del nivel).
- Opciones de rasgo (`TraitOptions`) y dotes: ya traen descripción; verificar con prueba.
- Trasfondo (`BackgroundChoices`): mismo recorrido.

Pruebas: en `Dnd.Application.Tests` (o `Dnd.Api.Tests` con SQLite), un personaje alto elfo con
trasfondo del SRD obtiene un plan donde **todas** las opciones de **todas** las elecciones tienen
`Description.Count > 0`. Widget: una opción de truco muestra el botón de información.

## 3. Altura y peso del personaje

Reglas (PHB cap. 4, "Altura y peso"): la altura es base + tirada de modificador (en pulgadas); el
peso es base + (esa misma tirada de altura × tirada de modificador de peso) en libras. No tienen efecto
mecánico; son datos libres que el jugador puede escribir o tirar.

### Servidor

- `Character`: `HeightInches` (`int?`, 1–200) y `WeightPounds` (`int?`, 1–2000), editables por la
  misma vía que los textos de personalidad (`SheetPatch` → `UpdateSheet`; el jugador propietario los
  cambia en directo, como `PersonalityTraits`, porque no afectan a la hoja). Migración EF
  `AddCharacterHeightAndWeight`. DTO `CharacterDetailDto` con ambos campos; `CreateCharacter` los
  acepta opcionales.
- Tabla de altura y peso en el catálogo: `RaceDefinition.HeightWeightJson` y
  `SubraceDefinition.HeightWeightJson` (`string?`), con el registro
  `HeightWeightTable(int BaseHeightInches, string HeightModifier, int BaseWeightPounds, string
  WeightModifier)` donde los modificadores son expresiones de dados (`"2d10"`, `"2d4"`, `"1"` para
  ×1). El SRD no trae la tabla, así que en el SRD queda nulo. La subraza manda sobre la raza.
- Paquetes de contenido (`docs/content-packs.md`, formato 2): campo `heightWeight` en `Race` y
  `Subrace` (`{ "baseHeightInches": 56, "heightModifier": "2d10", "baseWeightPounds": 110,
  "weightModifier": "2d4" }`), permitido también en razas con `extends` (se añade a la raza base por
  `RaceExtensionDefinition.HeightWeightJson`; con la misma regla: la subraza del paquete manda). Validar
  en el importador (rangos y expresión de dados `NdM` o entero).
- `GET /api/v1/catalog/races/{index}` y el DTO de subraza exponen `heightWeight` (nulo si no hay).

### App

- Modelo `CharacterDetail` con `heightInches`/`weightPounds`; `SheetPatch` de la app con ambos.
- Asistente, paso "Básico" (`step_basics.dart`): sección "Altura y peso" con dos campos numéricos
  (`basics-height`, `basics-weight`) y, cuando la raza/subraza elegida trae tabla, un botón "Tirar"
  (`basics-roll-height-weight`) que tira con el motor de dados de la app y rellena los dos campos
  mostrando la tirada en un `SnackBar` ("Altura 2d10 = 11 → 5' 7\" · Peso 2d4 = 5 → 165 lb"). Si no
  hay tabla, solo los campos. Se muestran en unidades imperiales como el PHB con conversión al lado
  (`5' 7" (170 cm) · 165 lb (75 kg)`), redondeando; helper `formatHeight`/`formatWeight` en
  `features/characters/domain/` con prueba unitaria.
- Hoja: en Resumen (`character_tabs.dart`), junto a alineamiento y edad, línea "Altura y peso" con el
  mismo formato (`summary-height-weight`); editable desde el editor de hoja (`sheet_editor_page.dart`)
  con los mismos campos y botón "Tirar".
- Detalle de raza del compendio (`race_detail_page.dart`): si hay tabla, fila "Altura y peso: base
  4' 8" + 2d10; 110 lb × 2d4".

### Paquete PHB privado

El constructor vive fuera del repositorio en
`/tmp/claude-0/-home-claude-dnd-manager/14be42d2-7b9b-5f82-858b-d716d426d95d/scratchpad/phbpack/`
(`build.py` con `races.py`). Añadir `heightWeight` a cada raza ampliada y subraza según la tabla del
PHB (p. 121), subir `version` a `2.8.0`, reconstruir y copiar el resultado a
`/mnt/project-files/dnd-manager/phb-2014.json`. **Nada de ese contenido entra al repositorio**: los
ejemplos de `docs/content-packs.md` y de los tests usan valores ficticios.

Tabla (pulgadas y libras; modificador de altura en pulgadas; peso = base + tirada altura × mod. peso):
humano 56 +2d10, 110 ×2d4 · enano de las colinas 44 +2d4, 115 ×2d6 · enano de las montañas 48 +2d4,
130 ×2d6 · alto elfo 54 +2d10, 90 ×1d4 · elfo de los bosques 54 +2d10, 100 ×1d4 · drow 53 +2d6, 75 ×1d6
· mediano (ambas subrazas) 31 +2d4, 35 ×1 · dracónido 66 +2d8, 175 ×2d6 · gnomo (ambas) 35 +2d4, 35 ×1
· semielfo 57 +2d8, 110 ×2d4 · semiorco 58 +2d10, 140 ×2d6 · tiefling 57 +2d8, 110 ×2d4.

Pruebas: dominio (parseo y validación de la tabla; cálculo de peso con tiradas fijadas), API
(importar un paquete ficticio con `heightWeight` en raza con `extends` y en subraza, leer el detalle,
`PATCH` de altura/peso del propietario sin aprobación), app (formato, el botón "Tirar" rellena ambos
campos con un dado determinista, Resumen muestra la línea).

## 4. Todas las condiciones en el selector

Síntoma: `ConditionPickerDialog` (`vitals_section.dart`) es un `AlertDialog` con `ListView(shrinkWrap:
true)` dentro de un `SizedBox(width: double.maxFinite)`: con 15 condiciones del SRD la lista excede la
altura del móvil y las últimas (`stunned`, `unconscious`…) no se alcanzan ni se ven.

Corrección: contenido desplazable con altura acotada (`ConstrainedBox(maxHeight: 60 % de la
pantalla)` + `ListView` sin `shrinkWrap`, o `showModalBottomSheet` con `DraggableScrollableSheet`;
elegir el bottom sheet, que es lo que usan los demás selectores largos de la app, y mantener las
claves `pick-condition-<index>`). Cada fila lleva `DetailInfoButton` que abre la descripción de la
condición (`condition_sheet.dart`) sin cerrar el selector.

Prueba: con una pantalla de 360×640 y las 15 condiciones del SRD, `pick-condition-unconscious` se
alcanza con `scrollUntilVisible` y al tocarla el diálogo devuelve la condición.

## 5. Concentración junto a las condiciones

Síntoma: el chip "Concentración: <conjuro>" y el botón "Perder" están en `StatsCard` (debajo de la
rejilla de CA, iniciativa…). Es un estado del personaje y debe verse con los demás estados.

Corrección: mover el chip (`concentration-chip`) y el botón (`concentration-lose`) a
`ConditionsCard`, como primera fila antes de los chips de condiciones y con el mismo estilo de chip
(icono `AppIcons.anchor`); desaparece de `StatsCard`. Si no hay condiciones ni concentración,
`ConditionsCard` muestra el texto vacío que ya tiene. El mismo cambio aplica a la tarjeta de estado
del roster del DM si repite el chip (buscar `ConcentratingOn` en `features/session/ui/dm`).

Pruebas: adaptar las que buscan `concentration-chip` (ahora dentro de `ConditionsCard`); nueva: con
concentración activa y sin condiciones, la tarjeta de condiciones muestra el chip y "Perder" lo quita.

## 6. Botón de información en los rasgos de clase

Síntoma: tarjetas del panel de clase como "Magia flexible" (hechicero) no tienen botón de
información; las de conjuros y objetos sí (`DetailInfoButton`).

Corrección:

- App: `openFeatureDetail(context, index)` en `catalog_detail_links.dart` y `FeatureDetailPage` en
  `features/catalog/ui/` (nombre, nivel, clase/subclase y descripción, con el estilo de
  `spell_detail_page.dart`), sobre `GET /api/v1/catalog/features/{index}` (ya existe; añadir
  `featureDetailProvider` y el método del repositorio del catálogo).
- `CombatCard`, `ClassResourceActionCard` y `FeatureReminder` (`panel_support.dart`) aceptan
  `featureIndex` (`String?`) y, cuando lo traen, muestran `DetailInfoButton` a la derecha del título
  (clave `feature-info-<index>`).
- Cada panel (`panels/*.dart`) pasa el índice del SRD de cada rasgo: Magia flexible → `font-of-magic`
  (la conversión es parte de Fuente de magia en el SRD), Metamagia → `metamagic`, Acción adicional →
  `action-surge`, Forma salvaje → `wild-shape`, etc. **Comprobar cada índice contra
  `server/seed/srd/5e-SRD-Features.json`** y dejar sin botón (y con un comentario) los rasgos que el
  SRD no tiene como entrada propia; no inventar índices. Entregar la lista de rasgos con y sin índice en
  la respuesta.

Pruebas: la tarjeta "Magia flexible" de un hechicero de nivel 2 tiene `feature-info-font-of-magic` y
al tocarlo se abre `FeatureDetailPage` con el nombre del rasgo (repositorio falso); un `FeatureReminder`
sin índice no tiene botón.

## 7. Navegación del jugador: Combate y Detalle

Síntoma: la ficha tiene siete pestañas planas (`tab-combat` … `tab-notes`) y la sesión del jugador dos
subvistas ("Combate" y "Fuera de combate") más una rejilla de accesos (`player-open-*`) y "Hoja
completa". El propietario quiere solo **dos vistas principales**: Combate (lo que importa en combate) y
Detalle (todo lo demás, navegable con subpestañas).

Corrección (misma estructura en la ficha y en la sesión del jugador):

- Ficha (`character_page.dart`): `TabBar` de dos pestañas `tab-combat` y `tab-detail`. "Detalle"
  contiene una barra secundaria de subpestañas (`TabBar.secondary`, desplazable, claves `tab-summary`,
  `tab-skills`, `tab-traits`, `tab-spells`, `tab-inventory`, `tab-notes` **sin cambios**) con el mismo
  contenido de hoy. `CharacterTab` conserva sus valores; `characterTabProvider` sigue recordando la
  subpestaña, y además se recuerda si la vista principal era Combate o Detalle (ampliar el estado con
  `CharacterView {combat, detail}` o derivarlo: `combat` ↔ Combate, el resto ↔ Detalle). Entrar a
  Detalle abre la última subpestaña usada (Resumen por defecto).
- Sesión del jugador (`player_session_page.dart`): `PlayerSubview` pasa a `combat('Combate')` y
  `detail('Detalle')` (claves `player-subview-combat`, `player-subview-detail`). "Detalle" muestra la
  cabecera del jugador y las **mismas subpestañas** que la ficha (reutilizar un widget
  `CharacterDetailTabs(character, permissions)` compartido entre ambas páginas, nuevo en
  `features/characters/ui/`), más lo que hoy vive en "Fuera de combate" que no sea un acceso a una
  sección de la ficha: las tiendas abiertas (`player-shops`) y la tarjeta de subida de nivel van como
  primera subpestaña "Sesión" (`tab-session`) solo en esta página. Desaparecen la rejilla
  `player-open-*`, `player-open-sheet` y `_CharacterSectionPage`.
- Combate no cambia de contenido en ninguna de las dos páginas.
- Pruebas existentes que tocan `tab-<x>` directamente: añadir en los helpers de prueba
  (`app/test/...`) una función `openDetailTab(tester, 'tab-skills')` que toca `tab-detail` y luego la
  subpestaña, y usarla donde haga falta; las que usaban `player-open-<x>` pasan a `player-subview-detail`
  + subpestaña. Nuevas: la ficha muestra dos pestañas principales; Detalle recuerda la subpestaña al
  salir y volver; en la sesión del jugador, Detalle muestra "Sesión" con las tiendas y las seis
  subpestañas de la ficha.

## Entrega

Cada bloque: `flutter analyze` sin avisos, `flutter test` verde (y `dotnet build` + `dotnet test` en
los bloques con servidor), commits en imperativo y en español sin identificadores de modelos. La fila
29 de `docs/PLAN.md` la añade el revisor al integrar.
