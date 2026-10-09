# Fase 27 — Combate: qué hace cada cosa, tipo de acción y alineación

Problema: en la pestaña Combate los conjuros aparecen sin poder leer qué hacen ni cómo se calcula su
daño; solo salen los que tienen ataque, salvación, daño o curación, así que un conjuro preparado sin
números (Mage Armor, Invisibility) no se puede "lanzar" desde su tarjeta; nada indica si algo es
acción, acción adicional o reacción; y algunos botones de detalle de la fase 26 no están alineados
con el icono de selección.

Reglas de esta fase: (1) todo lo que se muestra en Combate con nombre (conjuro, ataque, consumible)
abre su detalle completo; (2) todo valor numérico de un conjuro lleva su desglose con `StatValue`;
(3) el tipo de acción se muestra con un color de la paleta, igual en toda la app; (4) nada de reglas
caseras: solo lo que dicen el SRD y el PHB.

## 1. Tipo de acción (`app/lib/core/ui/action_type.dart`, nuevo)

- `enum ActionKind { action, bonusAction, reaction, other }` con `label` en español ("Acción",
  "Acción adicional", "Reacción", y para `other` el texto original del tiempo, p. ej. "1 minuto").
- `ActionKind.fromCastingTime(String? castingTime)`: `1 action` → `action`; `1 bonus action` →
  `bonusAction`; `1 reaction…` → `reaction`; cualquier otro → `other` con el texto traducido
  (`1 minute` → "1 minuto", `10 minutes` → "10 minutos", `1 hour` → "1 hora", `8 hours` → "8 horas",
  `24 hours` → "24 horas"; desconocido → el texto tal cual). Para `reaction` el texto de la
  condición (`, which you take when…`) se guarda en `note` para mostrarlo en el detalle.
- Colores **solo de los tokens** (`context.tokens`): `action` → `ember`, `bonusAction` → `oldGold`,
  `reaction` → `arcane`, `other` → `boneMuted`. El texto usa `emberText`/`oldGoldText`/`arcaneText`
  (añadir `emberText` a `AppTokens` con la misma `_readable` si no existe) para cumplir contraste.
  El test de paletas existente (`palettes_test.dart`) se amplía: los cuatro colores de acción son
  legibles sobre `stone` en las doce variantes.
- `ActionTypeChip(kind, {compact})`: chip pequeño con un icono propio de `AppIcons` (acción:
  `sword`; adicional: `bolt`; reacción: `shield`; otro: `clock`) y el `label`; fondo
  `color.withValues(alpha: 0.16)`, borde `color.withValues(alpha: 0.6)`, texto en el color legible.
  Clave `action-kind-<kind.name>` cuando se le pasa `key`.
- Se usa en: tarjetas de conjuro y ataque de Combate (abajo), `SpellDetailPage` (junto al nivel y la
  escuela), filas de la pestaña Conjuros de la ficha (en el subtítulo, chip compacto) y en el
  selector de conjuros y "Preparar conjuros" (chip compacto junto al subtítulo; `SpellSummary` ya
  trae `castingTime`).

## 2. Tarjetas de conjuro en Combate (`spells_section.dart`)

- Se listan **todos** los conjuros lanzables (`isCastable`), no solo los de combate: trucos,
  preparados, siempre preparados y conocidos. Orden: nivel y nombre, como ahora.
- Cabecera: nombre; a la derecha `DetailInfoButton` (`detail-spell-<índice>`, abre
  `openSpellDetail`), el icono de categoría y el nivel. Debajo de la cabecera: `ActionTypeChip` y
  una línea `bodySmall` con alcance, duración, "Concentración" y "Ritual" si aplica.
- Hechos con desglose (`StatValue`, cada uno con `title` y `statKey`):
  - Ataque: `text` "+5", `breakdown` = `character.sheet.breakdowns['spellAttackBonus.<clase>']`
    (competencia + característica; existe desde `SheetCalculator`).
  - Salvación: "CD 13 (Sabiduría)", `breakdown` = `breakdowns['spellSaveDc.<clase>']`.
  - Daño: `text` = dados a nivel elegido + tipo ("8d6 fuego"). `StatValue` gana un parámetro
    opcional `lines: List<String>` que la hoja de desglose pinta antes de las partes; aquí:
    "Tabla del conjuro a nivel N: 8d6" (o "a nivel de personaje N" en los trucos), "Crítico: dados
    doblados (16d6)" cuando está marcado, y "Sin modificador de característica" (los conjuros no
    suman el modificador al daño salvo que el texto lo diga; no se inventa nada).
  - Curación: "1d8+3", `lines` = "Tabla del conjuro a nivel N: 1d8 + MOD", y `breakdown` con una
    parte `ability` "Modificador de <habilidad> +3".
  - Sin números: no hay `StatValue`; la tarjeta muestra el primer párrafo de la descripción con
    `ExpandableText` (ya existe) para que se sepa qué hace sin abrir el detalle.
- Botones: los actuales (ataque, daño, curación, crítico). "Gastar espacio" pasa a llamarse
  **"Lanzar"** (misma clave `spell-spend-<índice>`): gasta el espacio del nivel elegido como ahora
  y, si el conjuro es de concentración, llama a `setConcentration(índice)` tras gastarlo (mensaje
  "Concentrándote en <nombre>"). Para trucos de concentración (p. ej. True Strike) el botón es
  "Concentrarse" (`spell-concentrate-<índice>`) y solo fija la concentración. Un truco sin
  concentración no tiene botón de lanzar (no gasta nada). La oleada de magia salvaje sigue igual.
- Tests existentes de `combat_test.dart` deben seguir pasando: se conservan las claves
  `combat-spell-*`, `spell-facts-*` (ahora la línea puede ser el `Row` de `StatValue`s; si un test
  busca texto en `spell-facts-*`, mantener un `Text` con el mismo contenido dentro o adaptar el test
  conservando lo que comprueba), `spell-attack-*`, `spell-damage-*`, `spell-heal-*`, `spell-crit-*`,
  `spell-spend-*`.

## 3. Ataques y consumibles (`attacks_section.dart`, consumibles rápidos)

- Cada ataque lleva `ActionTypeChip(action)` y, cuando `itemId != null`, `DetailInfoButton`
  (`detail-item-<itemId>`) → `openItemDetail(itemId)`. El modelo `Attack` de la app expone
  `itemId` si aún no lo hace (`AttackDto.ItemId` ya viaja).
- Los consumibles rápidos (`QuickConsumable`, `itemId`) llevan el mismo botón.

## 4. Paneles de clase (`combat/panels/*.dart`)

`ClassResourceActionCard` y `FeatureReminder` ganan `actionKind` opcional y pintan el chip bajo el
título. Se asigna **según el texto del SRD** del rasgo (solo donde el rasgo lo dice):

| Rasgo | Tipo |
| --- | --- |
| Rage (bárbaro) | Acción adicional |
| Bardic Inspiration | Acción adicional |
| Channel Divinity: Turn Undead; Divine Intervention (clérigo) | Acción |
| Wild Shape (druida) | Acción |
| Second Wind (guerrero) | Acción adicional |
| Action Surge, Indomitable | sin chip (no son una acción propia) |
| Flurry of Blows, Patient Defense, Step of the Wind (monje) | Acción adicional |
| Deflect Missiles, Slow Fall | Reacción |
| Lay on Hands, Divine Sense (paladín) | Acción |
| Cunning Action (pícaro) | Acción adicional |
| Uncanny Dodge | Reacción |
| Font of Magic: crear espacio / convertir (hechicero) | Acción adicional |
| Arcane Recovery, Natural Recovery, Song of Rest, Sneak Attack, Divine Smite, Tides of Chaos | sin chip |

Cualquier otro rasgo de los paneles: sin chip salvo que su texto del SRD diga "as an action",
"as a bonus action" o "reaction".

## 5. Alineación de los botones de detalle (fase 26)

- `spell_picker_page.dart`, `prepare_spells_page.dart` y el selector de categoría de equipo
  (`step_equipment.dart`): el botón sale del `title` y la fila pasa a `ListTile` con
  `trailing: Row(mainAxisSize: min, [DetailInfoButton, Checkbox])`, `onTap` en la fila que alterna
  la selección (misma semántica que antes: fila deshabilitada cuando el conjuro ya estaba elegido).
  Las claves `picker-spell-*`, `prepare-spell-*`, `equipment-category-item-*` se conservan en el
  `ListTile`; los tests que leían `CheckboxListTile` por esas claves se adaptan a leer el `Checkbox`
  dentro de la fila (`find.descendant`), conservando lo que comprueban.
- Línea de equipo fijo (`EquipmentLineTile`): se elimina el `Row` con `Padding(top: 14)` y el botón
  va dentro del `trailing` de la propia línea (`Row [EquipmentOriginBadge, DetailInfoButton]`). El
  test de `wizard_test.dart` que afirmaba "sin `IconButton`" en la línea `equipment-default-dagger`
  pasa a afirmar que no hay botón de borrar (`Icons.delete` o la clave que use la línea de "Otros
  objetos"), que era su intención.
- `LevelUpOptionCard`, `WizardChoiceCard`, `CompanionPickerPage`: comprobar que el botón queda
  centrado verticalmente con el icono de selección (`Row` con `crossAxisAlignment: center`); si una
  tarjeta tiene varias líneas bajo el título, el botón se queda en la fila del título.

## 6. Tests

- `combat_test.dart`: un conjuro preparado sin números (p. ej. un `SpellDetail` "mage-armor" sin
  daño) aparece en Combate con su chip de acción, su primer párrafo y "Lanzar"; "Lanzar" de un
  conjuro de concentración gasta el espacio y deja `concentratingOnSpellIndex` fijado (el fake del
  repositorio registra `setConcentration`); `detail-spell-<índice>` abre `SpellDetailPage`; la hoja
  de desglose del daño muestra la línea de la tabla y la de crítico; el ataque muestra
  `action-kind-action`.
- `action_type_test.dart` (nuevo): `fromCastingTime` para los seis tiempos del SRD y la reacción
  con condición; colores de los cuatro tipos distintos entre sí en la paleta por defecto.
- `palettes_test.dart`: contraste de los colores de acción en las doce variantes.
- `wizard_test.dart`, `spell_preparation_test.dart`: los tests de la fase 26 siguen pasando con la
  nueva estructura de fila; se añade uno que comprueba que el `DetailInfoButton` y el `Checkbox` de
  una fila del selector comparten el mismo centro vertical (`tester.getCenter`).
- `flutter analyze` sin avisos y `flutter test` en verde. El servidor no cambia.
