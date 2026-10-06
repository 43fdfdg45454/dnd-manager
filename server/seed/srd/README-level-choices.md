# Elecciones por nivel (SRD 5.1)

Dos ficheros escritos a mano a partir del SRD 5.1 que alimentan el asistente de subida de nivel
(fase 16c, `docs/specs/fase-16-rediseno-phb.md`). Solo contienen material del SRD 5.1
(CC-BY 4.0). Todo lo exclusivo del Player's Handbook (subclases no SRD, dotes salvo Grappler,
maniobras, espíritus tótem, disciplinas elementales, la tierra Underdark del explorador y del
Círculo de la Tierra…) llega mediante **paquetes de contenido formato v2**
(`docs/content-packs.md`, `optionSets[]` y `levelChoices[]`), que viven solo en la base de datos de
cada instancia.

> This work includes material taken from the System Reference Document 5.1 ("SRD 5.1") by
> Wizards of the Coast LLC, licensed under the Creative Commons Attribution 4.0 International
> License.

## `option-sets.json`

`{ "sets": [ { "setId", "name", "options": [Option] } ] }`

| Campo de `Option` | Tipo | Significado |
| --- | --- | --- |
| `index` | `string` | Único en todo el fichero. Si el rasgo existe en `5e-SRD-Features.json` se usa su índice tal cual. |
| `name` | `string` | Nombre corto en inglés del SRD ("Defense", "Colossus Slayer"). |
| `description` | `string[]` | Párrafos del texto SRD en inglés. |
| `prerequisitesText` | `string?` | Prerrequisito literal del SRD ("Prerequisite: 5th level, Pact of the Blade feature"). |
| `prerequisites` | `object?` | Versión estructurada: `minLevel` (nivel de la clase), `pactBoon` (índice del don), `cantrip` (índice del truco), `abilities` (`{ "str": 13 }`). |
| `modifiers` | `Modifier[]` | Solo efectos numéricos; `[]` si la opción es solo texto. |
| `abilityIncrease` | `object?` | Aumento de característica (ninguna opción SRD lo usa; previsto para dotes de paquetes). |
| `grants` | `object?` | `{ "skills": ["deception"], "cantrips": ["..."], "spells": [{ "index": "hold-person", "minLevel": 3 }] }`. `minLevel` (opcional) es el nivel de clase a partir del cual se concede. |
| `resource` | `object?` | Recurso de usos limitados: `{ "key", "name", "max": 1, "recharge": "ShortRest" \| "LongRest" }`. `max` puede ser un entero o una fórmula en texto. |

`Modifier`: `{ "kind", "target", "value", "condition"? }`. `kind` ∈ `AbilityBonus`, `AbilitySet`,
`SaveBonus`, `SkillBonus`, `ArmorClassBonus`, `AttackBonus`, `DamageBonus`, `SpeedBonus`,
`HitPointsMaxBonus`, `InitiativeBonus` (los mismos que los modificadores de objetos).
`condition` se omite si el bono es incondicional; vocabulario cerrado:

| `condition` | Se aplica cuando… |
| --- | --- |
| `wearingArmor` | lleva armadura (Defense). |
| `rangedWeapon` | ataca con un arma a distancia (Archery). |
| `oneHandedMeleeNoOtherWeapon` | empuña un arma cuerpo a cuerpo con una mano y ninguna otra arma (Dueling). |
| `twoHandedMelee` | ataca con un arma cuerpo a cuerpo a dos manos. |
| `twoWeaponFighting` | ataca con la segunda arma al combatir con dos armas. |

Conjuntos: `fighting-styles` (6), `eldritch-invocations` (32), `metamagic` (8), `pact-boons` (3),
`favored-enemies` (14), `natural-explorer-terrains` (7), `druid-lands` (7), `hunter-3` (3),
`hunter-7` (3), `hunter-11` (2), `hunter-15` (3), `dragon-ancestors` (10), `feats` (1, `grappler`).

**Estilos de combate:** el dataset duplica cada estilo por clase. El conjunto común usa los índices
del paladín (`fighting-style-defense`, `-dueling`, `-great-weapon-fighting`, `-protection`) y añade
`fighting-style-archery` y `fighting-style-two-weapon-fighting`. Los rasgos
`fighter-fighting-style-<x>` y `ranger-fighting-style-<x>` del dataset equivalen a
`fighting-style-<x>`.

## `level-choices.json`

`{ "rules": [Rule] }`, una regla por (clase, subclase, nivel, `key`).

| Campo | Significado |
| --- | --- |
| `classIndex`, `subclassIndex` | Clase; subclase SRD que da la elección o `null` para la clase base. |
| `level` | Nivel de la clase en el que se elige. |
| `key`, `name` | Identificador estable y nombre en inglés del rasgo. |
| `kind` | `Subclass`, `OptionSet`, `AsiOrFeat`, `Expertise`, `Skill`, `Language`, `Tool`, `CantripsKnown`, `SpellsKnown`, `SpellbookSpells`, `Custom`. |
| `setId` | Conjunto de `option-sets.json` (en `OptionSet` y en `Custom` con conjunto). |
| `choose` | Cuántas elecciones nuevas da este nivel. |
| `from` | Subconjunto permitido de índices del conjunto, o `null` (todo el conjunto). |
| `replaces` | `true`: **desde este nivel, en cada nivel de la clase se puede sustituir uno** de lo ya conocido (conjuros de bardo, hechicero, brujo y explorador; invocaciones). |
| `cumulative` | `true`: se suma a lo elegido en niveles anteriores (invocaciones, metamagia, enemigos y terrenos). |
| `note` | Aclaración en español para la interfaz. |
| `filter` | Opcional, para conjuros: `spellList` (`bard`, `warlock`, `druid`… o `any`), `spellLevels` (`[6]`), `maxSpellLevelBySlots` (nivel con espacios disponibles), `source` (`spellbook`, `known`, `list`), `cantripsOnly`. |

- `CantripsKnown` y `SpellsKnown` son **incrementos** respecto al nivel anterior, derivados de
  `5e-SRD-Levels.json`. Las elecciones del nivel 1 (habilidades, equipo, trucos y conjuros
  iniciales) son de la creación y no están aquí.
- Bardo 10, 14 y 18: el +2 de conjuros conocidos son los Secretos mágicos (`magical-secrets`,
  cualquier lista); no hay otra regla de conjuros conocidos en esos niveles.
- `Custom`: `circle-land` (druida Tierra, conjunto `druid-lands`, se pide en el nivel 2),
  `spell-mastery` y `signature-spells` (mago, desde el libro de conjuros).
- No automatizado (se indica en `note`): los tres trucos del Pacto del tomo, los rituales de Book
  of Ancient Secrets y el idioma de cada enemigo predilecto.

## Discrepancias con el dataset

Se sigue el texto del SRD, no las tablas de `5e-SRD-Levels.json`:

- Invocaciones del brujo: el dataset suma la 3.ª en el nivel 4 y la 4.ª en el 6; el texto las da
  en 5, 7, 9, 12, 15 y 18.
- Metamagia: el dataset marca la 4.ª opción en el nivel 16; el texto la da en el 17.
- Las mejoras de característica del pícaro en el dataset no son monótonas; se usan 4, 8, 10, 12,
  16 y 19.
- Los textos por tierra de `circle-of-the-land-<tierra>` en el dataset repiten la introducción del
  círculo; aquí la descripción es la de Circle Spells más la lista de conjuros de esa tierra.
