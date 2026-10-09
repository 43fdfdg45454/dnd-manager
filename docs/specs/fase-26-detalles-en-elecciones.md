# Fase 26 — Detalle de lo que se elige

Problema: en el asistente de personaje, el paso "Trucos y hechizos" muestra solo nombre, nivel y
escuela; no hay forma de leer la descripción de un conjuro antes de elegirlo ni después. Ocurre lo
mismo en otras pantallas de elección. Regla de esta fase: **toda pantalla donde el usuario elige un
elemento del catálogo (conjuro, objeto, raza, clase, bestia) ofrece ver su detalle completo sin
salir de la elección.**

## Mecanismo común (app)

- `app/lib/features/catalog/ui/catalog_detail_links.dart` (nuevo):
  - `openSpellDetail(context, index)`, `openItemDetail(context, templateId)`,
    `openRaceDetail(context, index)`, `openClassDetail(context, index)`,
    `openBeastDetail(context, index)`: `Navigator.of(context).push(MaterialPageRoute(...))` con la
    página de detalle del compendio ya existente (`SpellDetailPage`, `ItemDetailPage`,
    `RaceDetailPage`, `ClassDetailPage`, `BeastPage`). Se usa `Navigator` y no `context.push` porque
    varios selectores ya están apilados con `Navigator.push` (selector de conjuros, de categoría de
    equipo, de bestia) y el detalle debe abrirse encima de ellos y volver con la flecha.
  - `DetailInfoButton({required onPressed, Key? key, String tooltip = 'Ver detalle'})`: `IconButton`
    con `Icons.info_outline`, tamaño compacto, `visualDensity` compacta. Toda la fase usa este botón
    con clave `detail-<tipo>-<índice>` (`detail-spell-fire-bolt`, `detail-item-<templateId>`,
    `detail-race-elf`, `detail-class-wizard`, `detail-beast-wolf`).

## Pantallas

1. **Selector de conjuros** (`spell_picker_page.dart`, lo usan el asistente y el editor de hoja):
   cada fila mantiene la clave `picker-spell-<índice>` y el toque sobre la fila sigue marcando o
   desmarcando; a la derecha, antes de la casilla, va `DetailInfoButton` → `openSpellDetail`. Se
   puede sustituir `CheckboxListTile` por `ListTile` con `trailing: Row[botón, Checkbox]`, siempre
   que el toque en la fila y en la casilla sigan alternando la selección (tests actuales).
2. **Paso "Trucos y hechizos"** (`step_spells.dart`): los `InputChip` de trucos y hechizos
   elegidos abren el detalle con `onPressed` (la X sigue quitando). Los `FilterChip` de
   "Preparados" no cambian.
3. **Preparar conjuros** (`prepare_spells_page.dart`): mismo botón en cada fila
   (`prepare-spell-<índice>` se conserva).
4. **Pestaña Conjuros de la ficha** (`character_tabs.dart`): cada `ListTile` de conjuro (de clase
   y de raza) abre el detalle con `onTap`.
5. **Opciones de subida de nivel y de origen** (`LevelUpOptionCard` en `level_up_widgets.dart`):
   cuando `option.spellLevel != null` (conjuros y trucos de la subida de nivel y del origen; el
   índice de la opción es el del conjuro), la fila del título lleva `DetailInfoButton`
   (`detail-spell-<índice>`) que abre el detalle. El toque en la tarjeta sigue seleccionando.
6. **Equipo inicial** (`step_equipment.dart`): cada objeto con `templateId` (líneas de objeto
   fijo, objetos dentro de una opción, contenido de un paquete y filas del selector de categoría,
   que tienen `EquipmentCategoryItem.templateId`) lleva `DetailInfoButton` → `openItemDetail`.
   En el selector de categoría la fila conserva `equipment-category-item-<índice>` y su casilla.
7. **Raza, clase y subclase** (`step_basics.dart`): `WizardChoiceCard` gana `onInfo` opcional que
   pinta `DetailInfoButton` junto al icono de selección. Razas → `openRaceDetail`; clases y
   subclases → `openClassDetail` con el índice de la clase (la página de clase lista las subclases
   con sus rasgos). Las subrazas no llevan botón (la página de la raza ya las muestra).
8. **Trasfondo** (`step_proficiencies.dart`): al elegir un trasfondo se muestra debajo, con clave
   `wizard-background-feature`, el rasgo (`featureName` en `titleSmall`) y su `featureDescription`
   con `ExpandableText` (ya existe en `level_up_widgets.dart`). Si el trasfondo no tiene rasgo, no
   se pinta nada.
9. **Compañero animal** (`companion_section.dart`, `CompanionPickerPage`): cada bestia lleva
   `DetailInfoButton` → `openBeastDetail`; el toque en la fila sigue eligiendo.

Nada cambia en el servidor.

## Tests (Flutter, `app/test`)

- `wizard_test.dart`: en el selector de trucos del mago, `detail-spell-fire-bolt` abre
  `SpellDetailPage` (el `FakeCatalogRepository` recibe `spellDetails` con ese truco) y la flecha
  vuelve al selector con la elección intacta; el chip de un truco elegido abre el detalle; la
  tarjeta de raza `detail-race-elf` abre `RaceDetailPage`; el trasfondo elegido muestra
  `wizard-background-feature`; en el equipo, `detail-item-<templateId>` abre `ItemDetailPage`.
- `level_up_test.dart`: una opción de conjuro muestra `detail-spell-<índice>` y abre el detalle; una
  opción que no es conjuro (dote, maniobra) no lo muestra.
- `spell_preparation_test.dart`: el botón de una fila abre el detalle.
- `companion_test.dart`: `detail-beast-wolf` abre `BeastPage`.
- `flutter analyze` sin avisos y `flutter test` en verde.
