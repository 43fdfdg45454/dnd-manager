# Fase 28 — Fichas uniformes y vista propia de habilidades

Problema: en la pestaña Combate la CA, la iniciativa, la velocidad, la percepción pasiva y la
competencia se muestran en un `Wrap` de tarjetas de distinto ancho y alto (la de iniciativa lleva un
botón "Tirar" debajo, "30 pies" es más ancha, "Inspiración" es un chip metido en la misma fila), así
que nada queda alineado. Las habilidades viven dentro de esa misma tarjeta como ocho botones más un
enlace a una hoja inferior: no son solo de combate, no caben todas y ya existe una pestaña
"Habilidades" en la ficha que las lista enteras pero sin ventaja/desventaja.

Auditoría de grupos de datos con tamaños dispares (lo que esta fase corrige):

| Dónde | Qué pasa | Corrección |
|---|---|---|
| Combate › `StatsCard` (`vitals_section.dart`) | `Wrap` de `StoneCard` de ancho variable; iniciativa más alta por el botón "Tirar"; chip Inspiración en la misma fila | Rejilla fija de fichas iguales (sección 1) |
| Combate › `StatsCard` | Habilidades rápidas + "Todas las habilidades" dentro de la tarjeta de estadísticas | Fuera de Combate; la pestaña Habilidades es la vista (sección 2) |
| Resumen › segunda fila (`tile-hp-current`, `tile-temp-hp`, `tile-inspiration`) | `Wrap` de `_StatTile` sin rejilla: anchos distintos a los de la rejilla de arriba | Misma rejilla de fichas iguales (sección 3) |
| Resumen › Compañero (`companion_section.dart`) | CA y Percepción pasiva en `titleMedium`; salvaciones y habilidades sin estilo (`bodyMedium`) y con otro espaciado | Mismo estilo y espaciado en todos los `_Fact` (sección 4) |
| Combate › Espacios de conjuro (`resources_section.dart`) | Etiqueta en `SizedBox(width: 92)`: "Pacto (niv. 5)" no cabe y se recorta | Ancho 112 y elipsis (sección 4) |

Lo demás revisado queda como está: salvaciones de muerte (etiqueta fija de 72 px, tres círculos
iguales), condiciones (chips iguales), recursos (pips/barra), "Una vez por descanso largo" (chips),
roster del DM (texto en línea), tarjetas de ataque (bono en cabecera, daño en cuerpo, igual en todas).

Reglas: nada de reglas caseras; los textos visibles en español; colores solo de los tokens; todo
número con desglose sigue abriéndolo con `StatValue`; no se cambia la API ni el servidor.

## 1. Fichas iguales compartidas (`app/lib/core/ui/stat_tiles.dart`, nuevo)

Sacar de `character_tabs.dart` `_EqualGrid` y `_StatTile` a un fichero público reutilizable:

- `StatTileGrid({required int columnsWide, required List<Widget> children, Key? key})`: la
  rejilla actual (3 columnas, o `columnsWide` desde 600 px; `childAspectRatio` 1.15, o 1.3 cuando es
  ancha con más de 3 columnas; sin scroll propio). Mismo comportamiento que `_EqualGrid`.
- `StatTile({required String statKey, required String label, required String value, Breakdown?
  breakdown, String? totalText, Widget? mark, VoidCallback? onTap, Widget? corner, Key? key})`:
  la ficha actual (`StoneCard` con clave `tile-<statKey>`, etiqueta `labelMedium` centrada a dos
  líneas con elipsis, valor en `titleLarge` numérico dentro de `FittedBox`, y `mark` a la derecha
  del valor, que en la ficha es el `OverrideMark`). `StatValue` sigue abriendo el desglose al tocar
  el número cuando hay `breakdown`; si no lo hay, el número es texto plano.
  - `onTap`: pulsación sobre el resto de la ficha (el `StoneCard`), p. ej. alternar inspiración.
  - `corner`: widget pequeño colocado en la esquina superior derecha con `Stack`/`Positioned`
    (p. ej. un icono de dado para tirar). **No cambia el alto ni el ancho de la ficha**: todas las
    fichas de una rejilla miden lo mismo tenga o no `corner`.
- `character_tabs.dart` pasa a usar estos dos widgets; las claves `tile-<statKey>`,
  `combat-grid` y `abilities-grid` se mantienen (los tests de `characters_test.dart` siguen
  pasando sin tocarlos).

## 2. Combate: `StatsCard` con rejilla y sin habilidades (`vitals_section.dart`)

- Sustituir el `Wrap` por `StatTileGrid(key: Key('combat-stats-grid'), columnsWide: 6)` con seis
  fichas `StatTile` en este orden y con estas claves (las de los tests actuales):
  1. `combat-ac` "CA", `sheet.armorClass`, desglose `armorClass`.
  2. `combat-initiative` "Iniciativa", `formatModifier(sheet.initiative)`, desglose `initiative`,
     y `corner` = `IconButton` compacto (`AppIcons.d20`, 20 px, `visualDensity: compact`,
     tooltip "Tirar iniciativa") con clave `roll-initiative` que hace
     `rollAndShow(context, d20Expression(sheet.initiative), label: 'Iniciativa')`. Desaparece el
     `TextButton` "Tirar".
  3. `combat-speed` "Velocidad", `'${sheet.speed} pies'`, desglose `speed`,
     `totalText: '${sheet.speed} pies'`.
  4. `combat-perception` "Percepción pasiva", desglose `passivePerception`.
  5. `combat-proficiency` "Competencia", `formatModifier(sheet.proficiencyBonus)`, desglose
     `proficiencyBonus`.
  6. `combat-inspiration` "Inspiración", valor "Sí"/"No", sin desglose; `corner` =
     `AppIcon(AppIcons.sparkles, 18)` en `tokens.oldGold` cuando hay inspiración y en
     `boneMuted` cuando no; la ficha entera (`onTap`, solo con `canEdit`) alterna la inspiración
     con `patchCombat(CombatPatch(inspiration: !c.inspiration))` dentro de `runCombat`. La clave
     `inspiration` que usa `combat_test.dart` se pone en el `StoneCard` de esta ficha (es decir,
     `StatTile` acepta la `key` y la aplica al `StoneCard`; aquí se pasa `Key('inspiration')` y
     `statKey: 'combat-inspiration'` genera… **no**: para no tener dos claves en un widget, el
     `StoneCard` lleva `Key('tile-combat-inspiration')` y el `InkWell` de `onTap` que lo envuelve
     lleva `Key('inspiration')`). Tocar `inspiration` en el test debe alternar el valor igual que
     el chip de antes.
- Quitar `QuickSkillRolls` de la tarjeta (ver sección 2b). La fila de concentración
  (chip + "Perder") se queda debajo de la rejilla tal cual, con `SizedBox(height: 8)` de
  separación.
- Todas las fichas de esa rejilla miden lo mismo (ancho y alto) a 400 px y a 800 px de ancho:
  test nuevo en `combat_test.dart`, grupo "fichas de combate (fase 28)".

### 2b. Habilidades fuera de Combate

- Mover `skill_rolls.dart` de `ui/combat/` a `ui/skill_rolls.dart` (mismo contenido salvo lo que
  sigue) y actualizar los imports (`spells_section.dart` usa `pickAdvantageMode`).
- Borrar `quickCombatSkills`, `QuickSkillRolls` y `showAllSkillsSheet`. Se mantienen
  `pickAdvantageMode`, `rollSkill` y `rollSkillWithMode`.
- En Combate no queda ninguna habilidad: ni botones, ni enlace. (La pestaña "Habilidades" está a
  dos toques en la misma barra.)
- Tests: borrar el grupo "habilidades en combate" de `combat_test.dart` y cubrir lo mismo en la
  pestaña Habilidades (sección 2c).

### 2c. Pestaña Habilidades como vista propia (`character_tabs.dart`, `SkillsTab`)

- Debajo del título "Habilidades", una línea `bodySmall`: "Toca para tirar; mantén pulsado para
  ventaja o desventaja." (clave `skills-hint`).
- Las habilidades se agrupan por característica en el orden de la hoja del SRD: Fuerza,
  Destreza, Inteligencia, Sabiduría, Carisma (Constitución no tiene habilidades y no aparece).
  Cada grupo lleva una cabecera `labelLarge` con el nombre de la característica y su modificador
  (`'Fuerza ${formatModifier(mod)}'`, clave `skills-group-<abilityKey>`); dentro, las habilidades
  del grupo en orden alfabético por su etiqueta en español.
- Cada fila sigue siendo el `ListTile` actual (`skill-<index>`, icono de competencia/pericia,
  título, subtítulo, `StatValue` con desglose y `OverrideMark`), con estos cambios:
  - `onLongPress: () => rollSkillWithMode(context, skill)` (ventaja/desventaja).
  - El subtítulo pasa a ser solo "Competente"/"Pericia" (la característica ya la dice el grupo) o
    vacío; cuando está vacío, no hay subtítulo (`subtitle: null`), de modo que todas las filas sin
    competencia tienen la misma altura entre sí y las competentes la suya.
  - Se quita el icono de dado suelto (`Icons.casino_outlined`) del `trailing`: el valor
    `StatValue` ocupa el extremo derecho, alineado en todas las filas. El valor va en
    `titleMedium` numérico (igual en todas las filas).
- Tests nuevos en `characters_test.dart` (grupo "pestaña Habilidades (fase 28)"): (a) existen las
  cabeceras `skills-group-str`, `skills-group-dex`, … y `skills-group-con` no existe; (b) tocar
  `skill-athletics` tira `1d20+5` (como el test actual de `combat_test.dart` línea ~908, que se
  deja como está); (c) pulsación larga en `skill-stealth` abre el selector de modo y
  `mode-disadvantage` produce `dis+2`; (d) `quick-skills` y `all-skills` ya no existen en
  Combate.

## 3. Resumen: segunda fila con la misma rejilla (`character_tabs.dart`, `SummaryTab`)

- Las tres fichas `tile-hp-current`, `tile-temp-hp`, `tile-inspiration` dejan el `Wrap` y pasan a
  `StatTileGrid(key: Key('state-grid'), columnsWide: 3)`: una fila de tres fichas iguales tanto en
  móvil como en ancho. El `SizedBox(height: 8)` entre rejillas se mantiene.
- Test (en el grupo existente "hoja detallada"): las tres fichas tienen el mismo `top` y el
  mismo ancho que entre sí, y ese ancho coincide con el de `tile-armor-class` a 400 px de ancho
  (tres columnas en ambas rejillas).

## 4. Compañero y espacios de conjuro

- `companion_section.dart`: `_Fact` recibe el valor como texto o `StatValue` y lo muestra
  **siempre** en `titleMedium` numérico (`numericStyle(theme.textTheme.titleMedium)`), con la
  etiqueta en `bodySmall` delante; los tres `Wrap` (CA/percepción, salvaciones, habilidades) usan
  `spacing: 12, runSpacing: 4`. Nada más cambia (claves `companion.*` iguales).
- `resources_section.dart`, `SpellSlotsSection`: la etiqueta de cada fila pasa a
  `SizedBox(width: 112, child: Text(label, style: bodyLarge, maxLines: 1, overflow:
  TextOverflow.ellipsis))`. Test: con un brujo con pacto de nivel 5 la fila `combat-pact-slots`
  muestra "Pacto (niv. 5)" sin `RenderFlex`/overflow (el test de widgets falla solo si hay
  excepción de overflow; basta con que `tester.takeException()` sea nulo).

## 5. Fuera de alcance

Cambios en servidor, en el roster del DM, en las tarjetas de ataque y conjuro (fase 27), y
cualquier atajo de habilidades dentro de Combate.

## Verificación

`cd app && flutter analyze && flutter test` sin fallos. Claves y comportamiento de
`characters_test.dart` ("hoja detallada", rejilla 3×2) intactos; `combat_test.dart` sin el grupo
"habilidades en combate" y con el nuevo grupo de fichas; `roll-initiative` e `inspiration` siguen
funcionando en sus tests actuales.
