# Fase 25 — Paquetes de contenido completos

Contrato para que todo lo que un manual de reglas describe con mecánica pueda expresarse en un
paquete de contenido (ADR 0007, `docs/content-packs.md`) y la app lo calcule, en lugar de dejarlo en
texto. Nace de la lista de limitaciones detectadas al generar el paquete privado `phb-2014`
(2026-10-09). Se implementa por bloques, cada uno con su PR a `master`, su sección en
`docs/content-packs.md`, sus tests (dominio, API con SQLite, widgets) y el paquete privado regenerado
para usar el mecanismo nuevo. El formato sigue siendo el 2: todos los campos son opcionales y los
paquetes anteriores siguen importando igual.

Regla general: **nada se muestra sin desglose**. Cada valor nuevo que entra en la hoja (velocidad de
subraza, competencia, recurso, espacio de conjuro) lleva su origen con el nombre del rasgo y, si
procede, el nivel ("Wood Elf", "Arcane Ward (nivel 2)").

## Bloque 1 — Razas y subrazas

1. **Subrazas sobre razas existentes.** `races[]` admite `extends` (índice de una raza del SRD o de
   otro paquete) en lugar de los campos de una raza nueva: entonces solo se leen `subraces` (y
   opcionalmente `traits` y `grants`, que se añaden a la raza base). Las subrazas se registran con
   `RaceIndex` = la raza base y `Source` = el paquete; al desinstalar el paquete desaparecen y los
   personajes que las tenían quedan sin subraza (la hoja avisa como ya hace con una opción retirada).
   Validación: la raza base debe existir; una raza con `extends` no puede llevar `speed`, `size`,
   `abilityBonuses` ni `languages`.
2. **Velocidad de subraza.** `subraces[].speed` (`int?`, 0–200) sustituye a la de la raza. La hoja lo
   desglosa como "Raza 30" + "Subraza +5" (`BreakdownSources.Subrace`). `SubraceInfo` gana `Speed`.
3. **Competencias y lenguas fijas de raza y subraza.** `races[].grants` y `subraces[].grants` con el
   mismo `Grants` de las opciones (`skills`, `armor`, `weapons`, `tools`, `languages`, `savingThrows`,
   `cantrips`, `spells`). Se aplican al crear o cambiar la raza con origen `ProficiencySource.Race`
   (clave `race.grant.*` / `race.subrace.grant.*`, como las elecciones de origen) y se retiran al
   cambiar de raza. Las razas del SRD pasan a importar sus `starting_proficiencies` fijas (hacha de
   batalla del enano, armas élficas…), que hoy se pierden.
4. **Conjuros raciales por nivel.** `grants.spells[].minLevel` en razas y subrazas se compara con el
   **nivel total** del personaje (drow: *faerie fire* al 3, *darkness* al 5). Se conceden como
   siempre preparados sin clase (`ClassIndex` nulo) y se lanzan con la característica indicada en
   `grants.spellcastingAbility` (`"cha"`; obligatoria cuando hay `spells` o `cantrips`). El bloque de
   conjuros de la hoja muestra la sección "Raza" con su CD y ataque calculados con esa característica.
   `grants.spells[].usesPerLongRest` (`int?`) crea un recurso automático "Nombre del conjuro" de ese
   máximo con recarga larga.

## Bloque 2 — Recursos en rasgos de subclase

5. **`levels[].features[].resource`** (mismo `Resource` de las opciones) crea el recurso al alcanzar
   el nivel con la subclase, sin elección forzada. Desaparece el patrón "Recurso: …" de 1 entre 1.
6. **Máximos por tabla y fórmulas compuestas.** `max` admite además:
   - `{ "byLevel": { "3": 4, "7": 5, "15": 6 } }`: el valor de la entrada más alta no superior al
     nivel de la clase (dados de superioridad);
   - fórmulas con suma y producto de términos: `"2*classLevel+mod:int"`, `"classLevel+mod:wis"`,
     `"proficiencyBonus"`, `"halfClassLevel"`, `"mod:cha"`, constantes. Gramática: términos separados
     por `+`, cada término `[n*]símbolo` o entero; mínimo 1 salvo `min` explícito
     (`{ "formula": "mod:wis", "min": 1 }`). El desglose del recurso enumera los términos.
   - `max` sigue aceptando el entero y la cadena simple de hoy.
7. **Dado del recurso.** `resource.dice` (`"d8"`) con `diceByLevel` opcional (`{ "3": "d8", "10": "d10",
   "18": "d12" }`): la hoja lo expone (`ResourceValue.Dice`) y la pestaña Combate ofrece "Tirar" con
   ese dado al gastar un uso (dados de superioridad, dado de inspiración bárdica ya existente se
   migra a este mecanismo sin cambiar su comportamiento).
8. **Recursos de rasgos de la clase base del SRD** no cambian: siguen en `ClassResources` del
   dominio.

## Bloque 3 — Lanzadores de tercio y filtros por escuela

9. **Progresión de conjuros de subclase.** `subclasses[].spellcasting`:
   `{ "progression": "third" | "half" | "full", "ability": "int", "fromLevel": 3,
   "spellList": "wizard", "cantripsKnown": { "3": 2, "10": 3 }, "spellsKnown": { "3": 3, "4": 4, … } }`.
   La clase base deja de ser "no lanzadora" para ese personaje: `SheetCalculator` toma la progresión
   de la subclase cuando la clase base no tiene `SpellcastingLevel` (tercio = `floor(nivel/3)` con la
   tabla de tercio del PHB para clase única; en multiclase suma al nivel de lanzador como las demás).
   El bloque de conjuros muestra ataque y CD con la característica indicada y "Preparar/Conocidos"
   según `spellsKnown`.
10. **Filtro por escuela.** `levelChoices[].filter.schools` (`["abjuration", "evocation"]`) y
    `filter.schoolsExceptAt` (`[3, 8, 14, 20]`: niveles de la clase en que el conjuro puede ser de
    cualquier escuela). El planificador explica el motivo al descartar ("Solo abjuración o evocación
    salvo en los niveles 3, 8, 14 y 20").
11. Las elecciones `SpellsKnown`/`CantripsKnown` de la subclase con `spellcasting` usan su
    `spellsKnown`/`cantripsKnown` por nivel cuando `choose` no se indica, y `maxSpellLevelBySlots`
    se calcula con los espacios de la progresión de la subclase.

## Bloque 4 — Listas de conjuros ampliadas

12. **`subclasses[].expandedSpellList`**: `[{ "index": "faerie-fire", "level": 1 }, …]`. Los conjuros
    se añaden a la lista de la clase **solo para los personajes con esa subclase**: aparecen en el
    planificador (`SpellsKnown`, `SpellbookSpells`), en "Preparar conjuros" y en el compendio con la
    etiqueta "Lista ampliada: Archfey". No se conceden automáticamente (a diferencia de los conjuros
    de dominio). Validación: existen en el SRD o en el paquete y el nivel coincide.

## Bloque 5 — Coste de opciones y pericia temprana

13. **Coste en recursos.** `options[].cost`: `{ "resource": "ki", "amount": 2 }` (recurso de clase o
    de paquete). La app lo muestra en la ficha de la opción ("2 ki") y la pestaña Combate lista las
    opciones con coste junto al recurso con un botón "Usar" que descuenta el importe (sin aprobación,
    como cualquier uso de recurso). Validación: `amount` 1–20; el recurso debe existir para la clase.
14. **Pericia con competencias del mismo nivel.** El planificador resuelve las elecciones de un nivel
    en orden de dependencia: `Skill` y `grants.skills` antes que `Expertise`, de modo que la pericia
    de Knowledge se puede pedir en el nivel 1 y ofrece las habilidades recién concedidas. Se añade
    `levelChoices[].after` (`string?`, clave de otra elección del mismo nivel) para fijar el orden
    cuando no es el implícito.

## Bloque 6 — Compañero animal

15. **`levels[].features[].companion`**: `{ "beastFilter": { "maxChallengeRating": 0.25, "sizes":
    ["Medium", "Small"] }, "hitPoints": "max(beast, 4*classLevel)", "proficiencyBonusFromCharacter":
    true, "attackBonusFromCharacter": true }`. Al alcanzar el nivel, la hoja pide elegir una bestia del
    catálogo (SRD o paquete, filtrada) y la guarda como `CharacterCompanion` (bestia, nombre,
    PG actuales). La pestaña Combate muestra su bloque con CA, PG, ataques y salvaciones
    recalculados: suma el bonificador de competencia del personaje a CA, tiradas de ataque, daño y
    salvaciones/habilidades en que la bestia ya es competente; PG = máximo entre los de la bestia y
    `4 × nivel`. Endpoints: `PUT /characters/{id}/companion` (elegir o cambiar, con `ChangeRequest`
    para jugadores salvo en el momento del nivel), `POST /characters/{id}/companion/hp` (auto-seguimiento).

## Bloque 7 — Baratijas, oleadas y lo que queda en texto

16. **Baratijas** (`trinkets`, formato ya existente): el paquete privado incorpora la tabla d100 del
    manual. La app ya tira en el asistente; sin cambios de código salvo tests.
17. **Oleada de magia salvaje.** La tabla ya se tira desde el panel de hechicero. Se añade
    `options`/`features[].resource` para *Tides of Chaos* (bloque 2) y el panel ofrece "Tirar oleada"
    tras lanzar un conjuro de nivel 1+ (botón junto al gasto de espacio, solo si la subclase tiene una
    tabla con `key` `wild-magic-surge`).
18. **Modificadores condicionales de rasgos.** `features[].modifiers` (mismo `Modifier` con
    `condition`) para rasgos pasivos de subclase que suman números (CA sin armadura, bonificador a
    iniciativa, velocidad). Lo que requiere decisión del jugador en el momento (reacciones, ventaja)
    sigue en texto: se documenta como límite conocido.

## Orden y entrega

| Bloque | PR | Paquete `phb-2014` |
| --- | --- | --- |
| 1 | Razas | v2.1: subrazas sobre las razas del SRD (desaparecen las copias "(PHB)"), Wood Elf como subraza, competencias y conjuros del drow |
| 2 | Recursos | v2.2: 20 recursos como rasgo, dados de superioridad 4/5/6, Arcane Ward `2*classLevel+mod:int` |
| 3 | Tercio | v2.3: Eldritch Knight y Arcane Trickster con progresión y escuelas |
| 4 | Listas | v2.4: Archfey, Great Old One |
| 5 | Coste y pericia | v2.5: disciplinas con ki, Knowledge al nivel 1 |
| 6 | Compañero | v2.6: Beast Master |
| 7 | Resto | v2.7: baratijas, Tides of Chaos, modificadores de rasgos |

Cada PR actualiza `docs/content-packs.md` y la tabla de "Estado" de `docs/PLAN.md` menciona la fase
25 como "en curso (bloques 1–N)". El paquete privado no entra en el repositorio
(`content-packs/` en `.gitignore`); `PrivatePhbPackTests` se amplía por bloque y sigue sin fallar
cuando el fichero no está.

## Fuera de alcance

- Reglas opcionales del manual (dotes de arma, puntos de característica alternativos).
- Compañeros distintos del animal del explorador (familiares, invocaciones): se evaluará tras el
  bloque 6 con el mismo `CharacterCompanion`.
- Automatizar las reacciones y las ventajas situacionales.
