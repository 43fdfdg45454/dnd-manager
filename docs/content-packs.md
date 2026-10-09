# Paquetes de contenido

El repositorio solo incluye el SRD 5.1 (CC-BY 4.0). Cada instancia puede ampliar su catálogo con
**paquetes de contenido**: ficheros JSON que el administrador crea a partir de material que posea
(subclases, objetos, conjuros, razas con sus subrazas y rasgos, trasfondos) y que importa en su
servidor. Ver [ADR 0007](ADR/0007-paquetes-de-contenido-privados.md).

> **Nunca se commitean.** Los paquetes suelen contener material con copyright. Guárdalos fuera del
> repositorio o en `content-packs/`, que está en `.gitignore`. El único paquete de este documento,
> "Reinos de Ejemplo", es ficticio.

## Ejemplo ficticio

```json
{
  "formatVersion": 1,
  "id": "reinos-ejemplo",
  "name": "Reinos de Ejemplo",
  "version": "1.0.0",
  "classesExtended": [
    {
      "classIndex": "fighter",
      "subclasses": [
        {
          "index": "reinos-ejemplo-centinela",
          "name": "Centinela",
          "flavor": "Arquetipo marcial",
          "description": ["Guardianes ficticios que vigilan las murallas de los Reinos de Ejemplo."],
          "levels": [
            {
              "level": 3,
              "features": [
                {
                  "index": "reinos-ejemplo-vigilia",
                  "name": "Vigilia",
                  "description": ["Texto de ejemplo: mientras estés consciente, ganas un bonificador de +1 a la iniciativa."]
                }
              ]
            }
          ]
        }
      ]
    }
  ],
  "items": [
    {
      "index": "reinos-ejemplo-espada-del-alba",
      "name": "Espada del Alba",
      "category": "Weapon",
      "subcategory": "Martial Melee",
      "rarity": "Rare",
      "requiresAttunement": true,
      "costCp": null,
      "weightLb": 3,
      "damageDice": "1d8",
      "damageType": "slashing",
      "versatileDice": "1d10",
      "properties": ["versatile"],
      "description": ["Una espada ficticia que brilla con la primera luz del día."],
      "modifiers": [
        { "kind": "AttackBonus", "value": 1 },
        { "kind": "DamageBonus", "value": 1 }
      ]
    }
  ],
  "spells": [
    {
      "index": "reinos-ejemplo-luz-del-alba",
      "name": "Luz del Alba",
      "level": 1,
      "school": "evocation",
      "castingTime": "1 action",
      "range": "60 feet",
      "components": ["V", "S"],
      "material": null,
      "duration": "Instantaneous",
      "concentration": false,
      "ritual": false,
      "description": ["Un destello ficticio de luz cálida golpea a una criatura que puedas ver."],
      "higherLevel": ["El daño aumenta en 1d8 por cada nivel de espacio por encima de 1."],
      "classes": ["cleric", "paladin"],
      "subclasses": [],
      "attackType": null,
      "damage": { "type": "Radiant", "atSlotLevel": { "1": "2d8", "2": "3d8" } },
      "dcAbility": "dex"
    }
  ],
  "races": [
    {
      "index": "reinos-ejemplo-aurano",
      "name": "Aurano",
      "speed": 30,
      "size": "Medium",
      "sizeDescription": "Los auranos ficticios miden entre 1,60 y 1,90 metros.",
      "abilityBonuses": [{ "ability": "cha", "bonus": 2 }],
      "languages": ["Common"],
      "age": "Viven tanto como los humanos.",
      "alignment": "Suelen preferir el orden.",
      "traits": [
        { "index": "reinos-ejemplo-brillo", "name": "Brillo", "description": ["Tu piel emite una luz tenue en un radio de 1,5 metros."] }
      ],
      "subraces": [
        {
          "index": "reinos-ejemplo-aurano-del-alba",
          "name": "Aurano del Alba",
          "description": "Auranos ficticios nacidos al amanecer.",
          "abilityBonuses": [{ "ability": "wis", "bonus": 1 }],
          "traits": [
            { "index": "reinos-ejemplo-mirada-clara", "name": "Mirada clara", "description": ["Tienes ventaja en las tiradas para no quedar cegado."] }
          ]
        }
      ]
    }
  ],
  "backgrounds": [
    {
      "index": "reinos-ejemplo-farero",
      "name": "Farero",
      "featureName": "Luz en la costa",
      "featureDescription": ["Los marineros ficticios de la costa te ofrecen refugio."],
      "skillProficiencies": ["perception", "survival"],
      "startingEquipmentText": "Una linterna, un catalejo barato y 10 po."
    }
  ]
}
```

El mismo fichero se usa en los tests del servidor:
`server/tests/Dnd.Api.Tests/Fixtures/content-pack-example.json`.

## Reglas generales

- **Codificación**: UTF-8. Se admiten comentarios `//` y comas finales; los nombres de propiedad no
  distinguen mayúsculas, pero se recomienda `camelCase` como en el ejemplo.
- **Propiedades desconocidas**: son un error (`items[0].damgeDice: Propiedad desconocida`), para
  detectar erratas.
- **Índices**: todos los `index` del paquete empiezan por `<id>-` (prefijo del paquete), solo
  contienen minúsculas, números y guiones y tienen como máximo 100 caracteres. No pueden repetirse
  dentro del mismo tipo ni coincidir con un índice del SRD o de otro paquete.
- **Textos**: se recortan los espacios de los extremos. Los nombres (`name`, `featureName`,
  `flavor`) tienen como máximo 200 caracteres. Los campos de descripción son listas de párrafos
  (`string[]`): como máximo 200 párrafos de 10 000 caracteres; los párrafos vacíos se descartan.
- **Listas**: como máximo 500 entradas en cada lista del formato (`items`, `spells`,
  `classesExtended[].subclasses`, `levels[].features`, `traits`...), salvo los límites menores que
  se indican.
- **Referencias** (`classIndex`, `classes`, `subclasses` de conjuros): deben existir en el SRD o en el
  propio paquete. Los paquetes **no añaden clases**; las subrazas y los rasgos se declaran dentro de
  su raza. Los objetos de `backgrounds[].startingEquipment` deben existir en el SRD o en el propio
  paquete, y sus categorías en las categorías de equipo del SRD.
- **Tamaño máximo del fichero**: 20 MB.
- El contenido se muestra tal cual: escribe los textos en el idioma que prefieras (el SRD está en
  inglés).

## Campos

Notación: `string?` admite `null` o ausencia; **obligatorio** indica que no puede faltar ni estar vacío.

### Raíz

| Campo | Tipo | Reglas |
| --- | --- | --- |
| `formatVersion` | `int?` | Versión del formato: `1` (por defecto) o `2`. El formato 2 añade `optionSets`, `levelChoices` y `grants` ([ver abajo](#formato-2-elecciones-por-nivel)). |
| `id` | `string` | **Obligatorio**. `[a-z0-9-]{3,40}`. Identifica el paquete y es el prefijo de sus índices. `srd` y `homebrew` están reservados. |
| `name` | `string` | **Obligatorio**, ≤ 200. Nombre visible ("Reinos de Ejemplo"). |
| `version` | `string` | **Obligatorio**, ≤ 40. Versión libre del paquete (`1.0.0`). |
| `classesExtended` | `ClassExtension[]?` | Subclases nuevas para clases existentes. |
| `items` | `Item[]?` | Objetos. |
| `spells` | `Spell[]?` | Conjuros. |
| `races` | `Race[]?` | Razas con sus rasgos y subrazas. |
| `backgrounds` | `Background[]?` | Trasfondos. |
| `optionSets` | `OptionSet[]?` | Formato 2. Conjuntos de opciones (dotes, estilos de combate, invocaciones...). |
| `trinkets` | `Trinket[]?` | Tabla de baratijas (d100) del asistente de creación ([ver abajo](#trinket)). Formatos 1 y 2. |
| `rollTables` | `RollTable[]?` | Tablas de tirada genéricas (oleada de magia salvaje...), [ver abajo](#rolltable). Formatos 1 y 2. |

### `ClassExtension`

| Campo | Tipo | Reglas |
| --- | --- | --- |
| `classIndex` | `string` | **Obligatorio**. Clase del SRD (`barbarian`, `bard`, `cleric`, `druid`, `fighter`, `monk`, `paladin`, `ranger`, `rogue`, `sorcerer`, `warlock`, `wizard`). |
| `subclasses` | `Subclass[]?` | Subclases de esa clase. |

**`Subclass`**

| Campo | Tipo | Reglas |
| --- | --- | --- |
| `index` | `string` | **Obligatorio**, con prefijo. |
| `name` | `string` | **Obligatorio**, ≤ 200. |
| `flavor` | `string?` | ≤ 200. Nombre de la elección ("Martial Archetype"); por defecto, el de la clase. |
| `description` | `string[]?` | Párrafos. |
| `levels` | `SubclassLevel[]?` | Rasgos por nivel. |
| `spellcasting` | `SubclassSpellcasting?` | Con `"formatVersion": 2`: la subclase convierte en lanzadora a una clase que no lanza conjuros (ver [`subclasses[].spellcasting`](#subclassesspellcasting)). |
| `expandedSpellList` | `ExpandedSpell[]?` | Con `"formatVersion": 2`: conjuros que se añaden a la lista de la clase para los personajes con esta subclase, sin concederlos (ver [`subclasses[].expandedSpellList`](#subclassesexpandedspelllist)). |

**`SubclassLevel`**: `level` (`int`, **obligatorio**, 1–20, sin repetir dentro de la subclase) y
`features` (`Feature[]?`).

**`Feature`**: `index` (**obligatorio**, con prefijo), `name` (**obligatorio**, ≤ 200),
`description` (`string[]?`) y, con `"formatVersion": 2`, `resource` (`Resource?`, ver
[`levels[].features[].resource`](#levelsfeaturesresource)), `companion` (`Companion?`, ver
[`levels[].features[].companion`](#levelsfeaturescompanion)) y `modifiers` (`Modifier[]?`, ver
[`levels[].features[].modifiers`](#levelsfeaturesmodifiers)).

### `Item`

Mismos campos que los objetos homebrew de una campaña.

| Campo | Tipo | Reglas |
| --- | --- | --- |
| `index` | `string` | **Obligatorio**, con prefijo. |
| `name` | `string` | **Obligatorio**, ≤ 200. |
| `category` | `string` | **Obligatorio**. `Weapon`, `Armor`, `Shield`, `AdventuringGear`, `Tool`, `Mount`, `Consumable`, `MagicItem` u `Other` (exactamente así). |
| `subcategory` | `string?` | ≤ 100 ("Martial Melee", "Light Armor", "Potion"). |
| `rarity` | `string?` | `Common`, `Uncommon`, `Rare`, `VeryRare`, `Legendary`, `Artifact` o `Varies`. |
| `requiresAttunement` | `bool?` | Por defecto `false`. |
| `costCp` | `int?` | Precio en piezas de cobre, 0–1 000 000 000. |
| `weightLb` | `number?` | Peso en libras, 0–100 000. |
| `damageDice` | `string?` | ≤ 32 (`1d8`). |
| `damageType` | `string?` | ≤ 32 (`slashing`). |
| `versatileDice` | `string?` | ≤ 32. Daño a dos manos de las armas versátiles. |
| `properties` | `string[]?` | ≤ 50 entradas de ≤ 100 (`finesse`, `versatile`...). |
| `rangeNormal`, `rangeLong` | `int?` | Alcance en pies, 0–10 000. |
| `armorClassBase` | `int?` | CA base de armaduras y escudos, 0–30. |
| `addDexModifier` | `bool?` | Si la armadura suma el modificador de Destreza. |
| `maxDexBonus` | `int?` | Máximo de Destreza sumado, 0–10. |
| `strengthMinimum` | `int?` | Fuerza mínima, 0–30. |
| `stealthDisadvantage` | `bool?` | Desventaja en Sigilo. Por defecto `false`. |
| `description` | `string[]?` | Párrafos. |
| `effects` | `string[]?` | ≤ 50 efectos en texto libre de ≤ 500. |
| `modifiers` | `Modifier[]?` | ≤ 10 efectos estructurados sobre la hoja (ver abajo). |

**`Modifier`** (igual que `ItemModifier` de la fase 11, activo mientras el objeto está equipado y,
si lo requiere, sintonizado):

| Campo | Tipo | Reglas |
| --- | --- | --- |
| `kind` | `string` | **Obligatorio**: `AbilityBonus`, `AbilitySet`, `SaveBonus`, `SkillBonus`, `ArmorClassBonus`, `AttackBonus`, `DamageBonus`, `SpeedBonus`, `HitPointsMaxBonus` o `InitiativeBonus`. |
| `target` | `string?` | Característica (`str`...`cha`) obligatoria en `AbilityBonus` y `AbilitySet`; característica o `null` (todas) en `SaveBonus`; índice de habilidad o `null` (todas) en `SkillBonus`, ≤ 64; `null` en el resto. |
| `value` | `int` | **Obligatorio**. −10 a 30; en `AbilitySet`, 1 a 30. |

### `Spell`

| Campo | Tipo | Reglas |
| --- | --- | --- |
| `index` | `string` | **Obligatorio**, con prefijo. |
| `name` | `string` | **Obligatorio**, ≤ 200. |
| `level` | `int` | **Obligatorio**, 0 (truco) a 9. |
| `school` | `string` | **Obligatorio**: `abjuration`, `conjuration`, `divination`, `enchantment`, `evocation`, `illusion`, `necromancy` o `transmutation`. |
| `castingTime`, `range`, `duration` | `string` | **Obligatorios**, ≤ 100 ("1 action", "60 feet", "Instantaneous"). |
| `components` | `string[]?` | `V`, `S` y/o `M`. |
| `material` | `string?` | ≤ 10 000. |
| `concentration`, `ritual` | `bool?` | Por defecto `false`. |
| `description`, `higherLevel` | `string[]?` | Párrafos. |
| `classes` | `string[]?` | Clases que pueden aprenderlo (índices del SRD). |
| `subclasses` | `string[]?` | Subclases del SRD o del propio paquete. |
| `attackType` | `string?` | `melee`, `ranged` o `null`. |
| `damage` | `SpellDamage?` | `null` si no hace daño. |
| `dcAbility` | `string?` | Característica de la salvación (`dex`, `wis`...). |
| `category` | `string?` | Opcional: `Healing`, `Damage`, `Control`, `Buff`, `Defense`, `Utility` o `Summoning` (sin distinguir mayúsculas). La app la muestra como icono junto al nombre. Si falta se deduce: con `damage` → `Damage`; con `dcAbility` y sin daño → `Control`; si no, `Utility`. |

**`SpellDamage`**: `type` (`string?`, ≤ 32, p. ej. `Radiant`), `atSlotLevel` (objeto con claves `"1"`
a `"9"` y dados como valor) y/o `atCharacterLevel` (claves `"1"` a `"20"`, para trucos). Al menos uno
de los dos mapas.

### `Race`

| Campo | Tipo | Reglas |
| --- | --- | --- |
| `index` | `string` | **Obligatorio**, con prefijo. |
| `name` | `string` | **Obligatorio**, ≤ 200. |
| `speed` | `int` | **Obligatorio**, 0–200 pies. |
| `size` | `string` | **Obligatorio**: `Tiny`, `Small`, `Medium`, `Large`, `Huge` o `Gargantuan`. |
| `sizeDescription`, `age`, `alignment` | `string?` | ≤ 10 000. |
| `abilityBonuses` | `AbilityBonus[]?` | `{ "ability": "str".."cha", "bonus": -10..10 }`, ambos obligatorios. |
| `languages` | `string[]?` | ≤ 50 entradas de ≤ 100 ("Common"). |
| `traits` | `Trait[]?` | Rasgos de la raza. |
| `subraces` | `Subrace[]?` | Subrazas. |
| `choices` | `OriginChoices?` | Decisiones que la raza pide al crear el personaje (ver abajo). |
| `resistances` | `string[]?` | Tipos de daño que la raza resiste siempre (`fire`, `poison`...). |
| `grants` | `Grants?` | Formato 2. Competencias, idiomas y conjuros fijos de la raza ([ver abajo](#grants-de-raza-y-subraza)). |
| `extends` | `string?` | Formato 2. Índice de una raza del SRD o de otro paquete ya importado: la entrada **amplía** esa raza en lugar de definir una nueva ([ver abajo](#ampliar-una-raza-existente)). |

**`Subrace`**: `index` (**obligatorio**, con prefijo), `name` (**obligatorio**, ≤ 200), `description`
(`string?`, un solo texto de ≤ 10 000), `abilityBonuses` (`AbilityBonus[]?`), `traits` (`Trait[]?`),
`choices` (`OriginChoices?`), `resistances` (`string[]?`), `speed` (`int?`, 0–200: velocidad que
**sustituye** a la de la raza; la hoja la desglosa como "Raza 30" + "<subraza> +5") y `grants`
(`Grants?`, formato 2, como los de la raza).

#### Ampliar una raza existente

Con `extends` solo se leen `subraces`, `traits` y `grants`; el resto de campos de una raza nueva no
aplica (`index` y `name` se ignoran y pueden servir de comentario). `speed`, `size`, `abilityBonuses` y
`languages` dan error: los define la raza base. Reglas:

- La raza base debe existir en el catálogo (SRD u otro paquete ya importado) y no puede ser del propio
  paquete (sus subrazas van en su definición). Cada raza se amplía una sola vez por paquete.
- Las subrazas se registran con la raza base y con el paquete como origen: aparecen en el catálogo y en
  el asistente junto a las del SRD, y al desinstalar el paquete desaparecen. Los personajes que las
  tenían conservan el índice y la hoja avisa de "Contenido no disponible"; sus competencias y conjuros
  de subraza se retiran en el siguiente recálculo de la hoja.
- `traits` y `grants` se añaden a los de la raza base (los rasgos al detalle de la raza; las
  concesiones a todos los personajes de esa raza) mientras el paquete esté instalado.
- Reimportar el SRD o el paquete dueño de la raza base no borra las subrazas añadidas.

```json
"races": [
  {
    "extends": "dwarf",
    "subraces": [
      {
        "index": "reinos-ejemplo-deep-folk", "name": "Deep Folk", "speed": 30,
        "abilityBonuses": [{ "ability": "cha", "bonus": 1 }],
        "grants": {
          "weapons": ["warpicks"],
          "cantrips": ["dancing-lights"],
          "spells": [{ "index": "faerie-fire", "minLevel": 3, "usesPerLongRest": 1 }],
          "spellcastingAbility": "cha"
        }
      }
    ]
  }
]
```

#### `Grants` de raza y subraza

Mismo formato que los [`Grants` de las opciones](#option), con estas diferencias:

- Se aplican al crear o cambiar la raza (o la subraza) con origen "Raza" y se retiran al cambiarla. Si
  el personaje ya tenía la competencia por otra vía, la conserva con su origen.
- `spells[].minLevel` es el **nivel total** del personaje (no el de una clase).
- Los conjuros y trucos se conceden siempre preparados y sin clase (en la API, `classIndex` = `race`).
  La hoja muestra la sección "Raza" en Hechizos con la CD y el ataque calculados con
  `spellcastingAbility` (`str`..`cha`), **obligatoria** cuando hay `spells` o `cantrips`. Si raza y
  subraza la indican, manda la de la subraza.
- `spells[].usesPerLongRest` (`int?`, 1–20) crea un recurso automático con el nombre del conjuro, ese
  máximo y recarga en descanso largo, desde el nivel en que se concede (clave `race.<conjuro>`).

Las razas del SRD importan así sus competencias fijas (las `proficiencies` de sus rasgos: armas del
enano, Keen Senses del elfo, entrenamiento con armas del alto elfo, herramientas del gnomo de las rocas…).

**`Trait`**: `index` (**obligatorio**, con prefijo, único en todo el paquete), `name` (**obligatorio**,
≤ 200) y `description` (`string[]?`).

### `Background`

| Campo | Tipo | Reglas |
| --- | --- | --- |
| `index` | `string` | **Obligatorio**, con prefijo. |
| `name` | `string` | **Obligatorio**, ≤ 200. |
| `featureName` | `string?` | ≤ 200. |
| `featureDescription` | `string[]?` | Párrafos. |
| `skillProficiencies` | `string[]?` | Índices de habilidad del SRD (`perception`, `animal-handling`, `sleight-of-hand`...). |
| `startingEquipmentText` | `string?` | ≤ 10 000. |
| `startingEquipment` | `StartingEquipment?` | Equipo inicial estructurado (formatos 1 y 2). Sin él, el asistente de creación solo muestra `startingEquipmentText` y el jugador añade los objetos a mano. |
| `choices` | `OriginChoices?` | Decisiones que el trasfondo pide al crear el personaje (idiomas, herramientas...). |
| `personality` | `Personality?` | Tablas de rasgos de personalidad, ideales, vínculos y defectos ([ver abajo](#personality)). Formatos 1 y 2. |
| `optionalTables` | `BackgroundTable[]?` | Tablas opcionales del trasfondo (especialidad, origen...), ≤ 20 ([ver abajo](#backgroundtable)). |

#### `Personality`

Cada personaje tiene dos rasgos, un ideal, un vínculo y un defecto, que el asistente tira o deja elegir
en estas tablas (o escribir a mano). El dado es implícito: el número de entradas (8 rasgos → d8). El
SRD trae las del acólito; las demás solo llegan por paquetes.

| Campo | Tipo | Reglas |
| --- | --- | --- |
| `traits` | `string[]?` | 1..20 entradas, cada una ≤ 500. |
| `ideals` | `Ideal[]?` | 1..20 entradas: `{ "text": string (obligatorio, ≤ 500), "alignment": string? (≤ 100) }`. |
| `bonds` | `string[]?` | 1..20 entradas, cada una ≤ 500. |
| `flaws` | `string[]?` | 1..20 entradas, cada una ≤ 500. |

Hay que dar al menos una de las cuatro listas; una lista ausente es una tabla que el trasfondo no
tiene (el asistente pide escribir el texto). `alignment` es un texto libre que la app muestra junto al
ideal; el SRD usa `Lawful`, `Chaotic`, `Good`, `Evil`, `Neutral` y `Any` (la app los traduce), y
cualquier otro texto se muestra tal cual.

#### `BackgroundTable`

| Campo | Tipo | Reglas |
| --- | --- | --- |
| `key` | `string` | **Obligatorio**, minúsculas, números y guiones, ≤ 100, único en el trasfondo. |
| `name` | `string` | **Obligatorio**, ≤ 200. Es el prefijo del resultado: "Especialidad". |
| `entries` | `string[]` | **Obligatorio**, 1..20 entradas, cada una ≤ 500. Dado implícito = número de entradas. |

El jugador se queda con una entrada, que se guarda en la hoja como `backgroundDetail`
(`"<name>: <entrada>"`, ≤ 200).

```json
"backgrounds": [
  {
    "index": "reinos-ejemplo-cartografo", "name": "Cartógrafo de ejemplo",
    "personality": {
      "traits": ["Dibujo mapas de todo lo que veo (texto ficticio).", "Nunca me pierdo, o eso digo."],
      "ideals": [
        { "text": "Precisión. Un mapa mal hecho mata (texto ficticio).", "alignment": "Lawful" },
        { "text": "Curiosidad. Siempre hay otro camino.", "alignment": "Any" }
      ],
      "bonds": ["Busco el mapa que perdió mi maestra (texto ficticio)."],
      "flaws": ["No sé decir que no a una ruta sin explorar."]
    },
    "optionalTables": [
      { "key": "specialty", "name": "Especialidad", "entries": ["Costas", "Cuevas", "Ciudades"] }
    ]
  }
]
```

`GET /api/v1/catalog/backgrounds` devuelve `personality` (`{ traits, ideals: [{ text, alignment }],
bonds, flaws }` o `null`) y `optionalTables` (`[{ key, name, entries }]`, vacío si no hay).

#### `OriginChoices`

Decisiones de una raza, subraza o trasfondo (todas opcionales; formatos 1 y 2). El SRD las importa de
`ability_bonus_options`, `language_options`, las elecciones de competencias de los rasgos y
`trait_specific` (linaje dracónico, truco del alto elfo). La API las devuelve normalizadas en
`GET /api/v1/catalog/races/{index}` (`choices` de la raza y de cada subraza) y en
`GET /api/v1/catalog/backgrounds`; cada personaje las responde con
`GET`/`PUT /api/v1/characters/{id}/origin-choices` y no se puede activar un borrador con alguna
obligatoria sin responder (400, `code: origin-choices-incomplete`). Los idiomas son opcionales (el
asistente ya los pide en su propio paso).

| Campo | Forma | Efecto |
| --- | --- | --- |
| `abilityBonuses` | `{ "choose": 1-6, "amount": 1-2, "from": ["str", "dex"] }` | +`amount` a `choose` características distintas de `from` (sin `from`, las seis). Desglose con origen "race"/"subrace" ("Raza (elección)"). |
| `skills` | `{ "choose": 1-10, "from": ["perception"] }` | Competencias en habilidades (índices del SRD); sin `from`, cualquiera. Origen "Raza" o "Trasfondo". |
| `languages` | `{ "choose": 1-10, "from": ["Elvish"] }` | Idiomas (nombres, como las competencias de idioma); sin `from`, cualquiera. |
| `tools` | `{ "choose": 1-10, "from": ["Catalejo de ejemplo"] }` | Competencias en herramientas; sin `from`, texto libre. |
| `cantrip` | `{ "choose": 1, "spellList": "wizard", "from": ["light"] }` | Truco de la lista de esa clase (`any`: cualquiera), siempre preparado (clase `race` en la lista de conjuros). |
| `feats` | `{ "choose": 1 }` | Una dote del conjunto `feats`, con sus efectos (humano variante). |
| `traitOptions` | `[{ "key": "linaje", "name": "Linaje", "choose": 1, "options": [{ "index": "linaje-escarcha", "name": "Escarcha", "description": ["..."], "damageType": "cold" }] }]` | Opciones de un rasgo; `damageType` da la resistencia a ese daño. |

Ejemplo ficticio de raza al estilo del humano variante:

```json
{
  "index": "reinos-ejemplo-viajero",
  "name": "Viajero de ejemplo",
  "speed": 30,
  "size": "Medium",
  "languages": ["Common"],
  "choices": {
    "abilityBonuses": { "choose": 2, "amount": 1 },
    "skills": { "choose": 1 },
    "feats": { "choose": 1 }
  }
}
```

#### `StartingEquipment`

Mismo esquema que el equipo inicial del catálogo (clases y trasfondos del SRD), sin `gold`: la riqueza
inicial alternativa es solo de las clases y los paquetes no añaden clases.

```json
"startingEquipment": {
  "fixed": [
    { "item": "quarterstaff", "quantity": 1 },
    { "item": "reinos-ejemplo-catalejo-barato" },
    { "item": "explorers-pack" }
  ],
  "choices": [
    {
      "description": "(a) un arma marcial o (b) dos dagas",
      "choose": 1,
      "options": [
        { "label": "Cualquier arma marcial", "category": "martial-weapons", "categoryChoose": 1 },
        { "label": "Dos dagas", "items": [{ "item": "dagger", "quantity": 2 }] },
        {
          "label": "Escudo y dos armas sencillas",
          "items": [{ "item": "shield" }],
          "categories": [{ "category": "simple-weapons", "choose": 2 }]
        }
      ]
    }
  ],
  "fixedGoldCp": 1000
}
```

| Campo | Tipo | Reglas |
| --- | --- | --- |
| `fixed` | `StartingItem[]?` | Objetos que recibe todo personaje con el trasfondo. |
| `choices` | `Choice[]?` | Elecciones (a)/(b)/(c), en el orden del libro. |
| `fixedGoldCp` | `int?` | Dinero incluido, en piezas de cobre (15 po = `1500`), 0-10 000 000. |
| `gold` | — | No se admite en trasfondos (error con su ruta). |

**`StartingItem`**: `item` (**obligatorio**: índice de un objeto **del SRD** —`chain-mail`,
`explorers-pack`— **o del propio paquete**) y `quantity` (`int?`, 1-1000, por defecto 1). Los
paquetes de equipo del SRD (`explorers-pack`...) se mantienen como un objeto; la API añade su
contenido (`contents`) para mostrarlo.

**`Choice`**: `description` (`string?`, ≤ 10 000; si falta se componen las etiquetas de las
opciones), `choose` (`int?`, opciones a elegir, 1 hasta el número de opciones; por defecto 1) y
`options` (**obligatorio**, al menos una).

**`Option`**: `label` (**obligatorio**, ≤ 200), `items` (`StartingItem[]?`, objetos fijos de la
opción) y elecciones por categoría: `categories` (`[{ "category", "choose" }]`, `choose` 1-20, por
defecto 1) o, como atajo para una sola categoría, `category` + `categoryChoose`. Cada opción debe
incluir objetos o al menos una categoría. Las categorías son índices de las categorías de equipo del
SRD (`simple-weapons`, `martial-weapons`, `martial-melee-weapons`, `holy-symbols`,
`musical-instruments`, `artisans-tools`, `arcane-foci`, `druidic-foci`, `equipment-packs`...);
`GET /api/v1/catalog/equipment-categories/{index}` lista sus objetos.

La API devuelve el equipo resuelto en `GET /api/v1/catalog/backgrounds` (`startingEquipment`, o
`null` si el trasfondo no lo define): cada objeto con `item`, `templateId`, `name` y `quantity`, y
cada categoría con `category`, `name` y `choose`.

### `Trinket`

Tabla de baratijas que el jugador tira (1d100) al crear el personaje. La tabla no forma parte del SRD,
así que solo existe si algún paquete la define; sin ella el asistente pide describir la baratija y la
añade como objeto personalizado "Baratija".

| Campo | Tipo | Reglas |
| --- | --- | --- |
| `roll` | `int` | **Obligatorio**, 1..100, sin repetir dentro del paquete. Los números sin entrada se describen a mano. |
| `item` | `string` | **Obligatorio**. Índice de un objeto del SRD o del propio paquete. |

Lo habitual es que cada baratija sea un objeto del paquete con `"category": "Other"`,
`"subcategory": "Trinket"` y sin `costCp` ni `weightLb`:

```json
"items": [
  { "index": "reinos-ejemplo-canica-azul", "name": "Canica azul de ejemplo",
    "category": "Other", "subcategory": "Trinket",
    "description": ["Una canica de cristal que nunca rueda cuesta abajo (texto ficticio)."] }
],
"trinkets": [
  { "roll": 1, "item": "reinos-ejemplo-canica-azul" },
  { "roll": 2, "item": "dagger" }
]
```

Si varios paquetes definen el mismo `roll`, gana el **último importado** (reimportar un paquete lo
vuelve a poner por delante). `GET /api/v1/catalog/trinkets` devuelve la tabla efectiva ordenada por
tirada: `[{ roll, templateId, index, name, description }]` (vacía con solo el SRD).

### `RollTable`

Tabla de tirada genérica: la app la muestra en el compendio (sección "Tablas") y, si tiene subclase,
en el panel de combate de los personajes de esa subclase (p. ej. la oleada de magia salvaje del
hechicero, d100). No forma parte del SRD.

| Campo | Tipo | Reglas |
| --- | --- | --- |
| `key` | `string` | **Obligatorio**, minúsculas, números y guiones, ≤ 100, único en el paquete. |
| `name` | `string` | **Obligatorio**, ≤ 200. |
| `dice` | `string` | **Obligatorio**: `d2`, `d3`, `d4`, `d6`, `d8`, `d10`, `d12`, `d20` o `d100`. |
| `entries` | `Entry[]` | **Obligatorio**: `{ "from": int, "to": int?, "text": string }`. `to` ausente = `from`; `text` obligatorio, ≤ 2000. |
| `classIndex` | `string?` | Clase del catálogo. Si se da `subclassIndex`, se deduce de ella. |
| `subclassIndex` | `string?` | Subclase del SRD, del propio paquete o de otro paquete ya importado. |

Las entradas deben cubrir **todos** los resultados del dado (1..N) **sin huecos ni solapes**; el
error indica el rango que falta o la entrada que se solapa.

```json
"classesExtended": [
  { "classIndex": "sorcerer", "subclasses": [ { "index": "reinos-ejemplo-chispa", "name": "Chispa de ejemplo" } ] }
],
"rollTables": [
  {
    "key": "reinos-ejemplo-chispa-surge", "name": "Oleada de ejemplo", "dice": "d100",
    "subclassIndex": "reinos-ejemplo-chispa",
    "entries": [
      { "from": 1, "to": 50, "text": "Te salen chispas de los dedos (texto ficticio)." },
      { "from": 51, "to": 99, "text": "Nada ocurre." },
      { "from": 100, "text": "Todo el mundo estornuda (texto ficticio)." }
    ]
  }
]
```

**Oleada de magia salvaje.** Si la subclase de un personaje tiene una tabla cuya `key` es
`wild-magic-surge` o termina en `-wild-magic-surge` (`reinos-ejemplo-wild-magic-surge`), al gastar un
espacio de nivel 1 o superior con "Gastar espacio" en la sección "Conjuros" de la pestaña Combate la
tarjeta del conjuro muestra "Oleada de magia salvaje: tira 1d20" con "Tirar d20" (dado virtual; con un
1 se abre la tabla para tirar en ella) y "Tirar oleada" (abre la tabla directamente). Si además el
personaje tiene un recurso automático cuyo `key` termina en `tides-of-chaos` (un `resource` de rasgo,
p. ej. `reinos-ejemplo-tides-of-chaos`), junto al aviso aparece "Recuperar Mareas del caos", que
devuelve un uso sin aprobación del DM: es la regla tras una oleada. Por eso
`POST /api/v1/characters/{id}/resources/{resourceId}/restore` admite a los jugadores, además de los
puntos de hechicería, los recursos automáticos cuyo `key` termina en `tides-of-chaos` (el resto de
recursos automáticos sigue dando 403 a quien no es DM).

Si varios paquetes definen la misma `key`, gana el **último importado**. `GET
/api/v1/catalog/roll-tables` (filtros opcionales `subclass=` y `class=`) devuelve las tablas efectivas
ordenadas por nombre: `[{ key, name, dice, classIndex, subclassIndex, source, entries: [{ from, to,
text }] }]` (vacía con solo el SRD).

## Errores de validación

El paquete se valida entero antes de escribir nada. Si tiene errores, la API responde `400` con un
ProblemDetails cuyo campo `errors` es una **lista** de textos `"ruta: mensaje"`, con la ruta en el
JSON (como máximo 100; el resto se resume en una línea):

```json
{
  "status": 400,
  "title": "El paquete de contenido no es válido.",
  "detail": "El paquete de contenido tiene 3 errores.",
  "errors": [
    "items[3].modifiers[0].kind: Tipo de modificador desconocido. Valores admitidos: AbilityBonus, ...",
    "spells[0].index: Debe empezar por \"reinos-ejemplo-\" (el id del paquete).",
    "classesExtended[0].classIndex: La clase 'artificer' no existe en el catálogo (los paquetes no añaden clases)."
  ]
}
```

Los errores de sintaxis JSON o de tipo (texto donde se espera un número) se informan uno a uno, con
la línea y la posición.

## Importar, reimportar y borrar

Solo el administrador de la instancia.

- **Desde la app**: Administración → Contenido → "Importar paquete" y elige el fichero `.json`.
- **Con la API** (`URL` y credenciales ficticias; usa las de tu instancia):

  ```bash
  URL=https://dnd.example.com
  TOKEN=$(curl -fsS -X POST "$URL/api/v1/auth/login" \
    -H 'Content-Type: application/json' \
    -d '{"email":"admin@example.com","password":"change-me"}' | jq -r .accessToken)

  # Multipart con el campo "file"...
  curl --fail-with-body -X POST "$URL/api/v1/admin/content-packs" \
    -H "Authorization: Bearer $TOKEN" -F "file=@content-packs/reinos-ejemplo.json;type=application/json"

  # ...o el JSON como cuerpo
  curl --fail-with-body -X POST "$URL/api/v1/admin/content-packs" \
    -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
    --data-binary @content-packs/reinos-ejemplo.json

  curl -fsS "$URL/api/v1/admin/content-packs" -H "Authorization: Bearer $TOKEN"     # lista
  curl -fsS -X DELETE "$URL/api/v1/admin/content-packs/reinos-ejemplo" -H "Authorization: Bearer $TOKEN"
  ```

| Petición | Respuesta |
| --- | --- |
| `GET /api/v1/admin/content-packs` | `200 [{ id, name, version, importedAt, counts }]` |
| `POST /api/v1/admin/content-packs` | `201 { id, name, version, counts }`, `400` (errores), `413` (> 20 MB) |
| `DELETE /api/v1/admin/content-packs/{id}` | `204`, o `404` si no existe |
| `GET /api/v1/catalog/sources` (cualquier usuario) | `200 [{ id, name, version }]`: `srd` y los paquetes, para etiquetar el contenido |

`counts` tiene el número de `subclasses`, `features`, `items`, `spells`, `races`, `subraces`,
`raceExtensions` (razas ampliadas con `extends`), `traits`, `backgrounds`, `optionSets`, `options`, `levelChoices`, `trinkets` y `rollTables` importados.

**Reimportar** (mismo `id`, misma u otra `version`) reemplaza todo el contenido del paquete en una
transacción. Los objetos se actualizan por `index` y **conservan su identificador**, así que los
inventarios, tiendas y alijos que los usan siguen funcionando; un objeto que desaparece del paquete
se borra, salvo que esté en uso.

**Borrar** quita del catálogo todo el contenido del paquete. Los personajes conservan los índices
que usaban y siguen cargando: la ficha marca `catalogMissing` en las clases, subclases y conjuros que
ya no existen (y `raceCatalogMissing`/`backgroundCatalogMissing`), y una clase desconocida cuenta como
d8 sin rasgos. Los objetos del paquete que estén en uso se conservan para esas entradas, pero dejan
de aparecer en el catálogo. Reimportar el paquete lo restaura todo.

El contenido del catálogo indica su origen en `source` (`srd`, `homebrew` para objetos de campaña o
el `id` del paquete). Actualizar el SRD (nueva versión del servidor) no toca los paquetes, y las
copias de seguridad de la base de datos los incluyen.

## Formato 2: elecciones por nivel

Con `"formatVersion": 2` un paquete puede ampliar el **asistente de subida de nivel**: añadir opciones
a los conjuntos del SRD (dotes a `feats`, estilos a `fighting-styles`, invocaciones a
`eldritch-invocations`...), crear conjuntos nuevos, declarar las elecciones de sus subclases (y de las
clases base) y lo que conceden. Sin `formatVersion: 2`, los campos de esta sección son un error. El
formato 1 sigue aceptándose tal cual.

### Ejemplo ficticio

Añade una dote al conjunto `feats` del SRD y una subclase de guerrero con una elección de nivel 3 de
un conjunto nuevo:

```json
{
  "formatVersion": 2,
  "id": "tierras-ejemplo",
  "name": "Tierras de Ejemplo",
  "version": "2.0.0",
  "optionSets": [
    {
      "setId": "feats",
      "options": [
        {
          "index": "tierras-ejemplo-vigia-incansable",
          "name": "Vigía incansable",
          "description": ["Texto de ejemplo: nunca bajas la guardia. Ganas +1 a Sabiduría y +2 a la iniciativa."],
          "prerequisitesText": "Prerrequisito: Sabiduría 13 o más",
          "prerequisites": { "abilities": { "wis": 13 } },
          "modifiers": [{ "kind": "InitiativeBonus", "value": 2 }],
          "abilityIncrease": { "amount": 1, "from": ["wis"] }
        }
      ]
    },
    {
      "setId": "tierras-ejemplo-juramentos",
      "name": "Juramentos de la Guardia",
      "options": [
        {
          "index": "tierras-ejemplo-juramento-del-muro",
          "name": "Juramento del Muro",
          "description": ["Texto de ejemplo: mientras lleves armadura, ganas +1 a la CA."],
          "modifiers": [{ "kind": "ArmorClassBonus", "value": 1, "condition": "wearingArmor" }]
        },
        {
          "index": "tierras-ejemplo-juramento-del-faro",
          "name": "Juramento del Faro",
          "description": ["Texto de ejemplo: conoces el truco luz y puedes lanzar un destello tantas veces como tu bonificador por competencia."],
          "grants": { "cantrips": ["light"] },
          "resource": { "key": "tierras-ejemplo-destello", "name": "Destello", "max": "proficiencyBonus", "recharge": "LongRest" }
        }
      ]
    }
  ],
  "classesExtended": [
    {
      "classIndex": "fighter",
      "subclasses": [
        {
          "index": "tierras-ejemplo-guardia",
          "name": "Guardia de las Tierras",
          "flavor": "Arquetipo marcial",
          "description": ["Guardianes ficticios que juran proteger los caminos de las Tierras de Ejemplo."],
          "levels": [
            {
              "level": 3,
              "features": [
                { "index": "tierras-ejemplo-juramento", "name": "Juramento", "description": ["Texto de ejemplo: al entrar en la Guardia pronuncias un juramento."] }
              ],
              "grants": { "skills": ["perception"] }
            }
          ],
          "levelChoices": [
            {
              "level": 3,
              "key": "juramento",
              "name": "Juramento",
              "kind": "OptionSet",
              "setId": "tierras-ejemplo-juramentos",
              "choose": 1,
              "note": "Elige el juramento de tu Guardia."
            }
          ]
        }
      ]
    }
  ]
}
```

El mismo fichero se usa en los tests: `server/tests/Dnd.Api.Tests/Fixtures/content-pack-v2-example.json`.
Al subir a guerrero 3, el asistente ofrece la subclase "Guardia de las Tierras" junto a las del SRD y,
si se elige, también el "Juramento"; al subir a nivel 4, la dote aparece junto a Grappler.

### `OptionSet`

| Campo | Tipo | Reglas |
| --- | --- | --- |
| `setId` | `string` | **Obligatorio**. Si ya existe en el SRD o en otro paquete (`feats`, `fighting-styles`, `eldritch-invocations`, `metamagic`, `pact-boons`, `favored-enemies`...), las opciones se **añaden** a ese conjunto. Si no, es un conjunto nuevo y su id lleva el prefijo del paquete. |
| `name` | `string` | **Obligatorio** en los conjuntos nuevos (≤ 200); se ignora en los existentes. |
| `options` | `Option[]?` | Opciones del conjunto. |

### `Option`

| Campo | Tipo | Reglas |
| --- | --- | --- |
| `index` | `string` | **Obligatorio**, con prefijo (también al añadir a conjuntos del SRD). |
| `name` | `string` | **Obligatorio**, ≤ 200. |
| `description` | `string[]?` | Párrafos. |
| `prerequisitesText` | `string?` | ≤ 2000. Texto literal del prerrequisito. |
| `prerequisites` | `Prerequisites?` | Versión que la app comprueba (ver abajo). |
| `modifiers` | `Modifier[]?` | ≤ 10 efectos numéricos: los de los objetos más `condition`. |
| `abilityIncrease` | `AbilityIncrease?` | Dotes: `{ "amount": 1-2, "from": ["str", "dex"] }`. `from` vacío = cualquier característica; con una sola, se aplica sin preguntar; con varias, el jugador elige una al tomar la dote. El tope de 20 se respeta y el desglose de la característica nombra la dote ("Atleta (nivel 4)"). |
| `grants` | `Grants?` | Competencias y conjuros que concede. |
| `resource` | `Resource?` | Recurso de usos limitados que aparece como recurso automático del personaje. |
| `cost` | `Cost?` | Lo que cuesta **usar** la opción: `{ "resource": "ki", "amount": 2 }` (ver abajo). |

**`Prerequisites`** (todas las condiciones dadas deben cumplirse):

| Campo | Significado |
| --- | --- |
| `minLevel` | 1–20, nivel en la clase de la elección. |
| `pactBoon` | Índice del don del pacto elegido (`pact-of-the-blade`). |
| `cantrip` | Índice de un truco que el personaje conozca. |
| `abilities` | Puntuaciones mínimas: `{ "str": 13 }` (1–30). Varias características = todas. |
| `races` | Índices de raza (del SRD o de un paquete); basta con ser **una** de ellas: `["elf", "half-elf"]`. |
| `proficiency` | `{ "armor": ["heavy"], "weapon": ["martial"] }`. Competencias que el personaje debe tener, **todas**. Armadura: `light`, `medium`, `heavy` o `shields` (`all-armor` del guerrero y el paladín cubre las tres armaduras, no los escudos). Arma: `simple`, `martial` o el índice de un arma (`longswords`). |
| `spellcasting` | `true`: poder lanzar al menos un conjuro (una clase lanzadora con espacios o un conjuro de cualquier origen, trucos raciales incluidos). |

Las opciones sin cumplir aparecen en el asistente como no elegibles, con el motivo, que dice qué
falta y qué tiene el personaje ("Requiere Fuerza 13; tienes 10", "Requiere ser Elf o Half-Elf; eres
Human", "Requiere competencia con armadura pesada; no la tienes"). Si la opción no trae
`prerequisitesText`, el asistente muestra uno generado a partir de estos campos ("Fuerza 13,
competencia con armadura pesada").

**`Modifier`**: `kind`, `target` y `value` como en los objetos, más `condition` opcional:

| `condition` | Se aplica cuando… | Cálculo |
| --- | --- | --- |
| *(ausente)* | siempre | hoja y ataques |
| `wearingArmor` | lleva armadura | hoja (CA, salvaciones...) |
| `rangedWeapon` | ataca con un arma a distancia (munición o subcategoría "Ranged") | ataques |
| `oneHandedMeleeNoOtherWeapon` | arma cuerpo a cuerpo sin la propiedad "two-handed" y ninguna otra arma equipada (no se suma al daño a dos manos de una versátil) | ataques |
| `twoHandedMelee` | arma cuerpo a cuerpo "two-handed", o el daño a dos manos de una versátil | ataques |
| `twoWeaponFighting` | segunda arma al combatir con dos armas | solo texto (no hay línea de ataque de la segunda arma) |

Los bonos aparecen en los desgloses con el nombre de la opción y el nivel: "Juramento del Muro (nivel 3)".

**`Grants`**: `skills` (índices de habilidad), `armor`, `weapons`, `tools`, `languages` (textos ≤ 100),
`savingThrows` (características), `cantrips` (índices de conjuro) y `spells`
(`[{ "index": "hold-person", "minLevel": 3 }]`, `minLevel` opcional: nivel de la clase desde el que se
concede). Las competencias se añaden con origen "Clase"; los conjuros, siempre preparados. Los
conjuros y trucos deben existir en el SRD o en el paquete. En razas y subrazas (`races[].grants`,
`subraces[].grants`) se admiten además `spellcastingAbility` y `spells[].usesPerLongRest`, y
`minLevel` es el nivel total ([ver Race](#grants-de-raza-y-subraza)); fuera de ellas esos dos campos
dan error.

**`Resource`**: recurso de usos limitados que la app crea como recurso automático del personaje.

| Campo | Tipo | Reglas |
| --- | --- | --- |
| `key` | `string` | **Obligatorio**. Índice, sin prefijo obligatorio. Si coincide con otro recurso automático del personaje, se queda el de máximo mayor. |
| `name` | `string` | **Obligatorio**, ≤ 100. |
| `max` | `int`, `string` u objeto | Máximo de usos (ver abajo). |
| `recharge` | `string?` | `ShortRest`, `LongRest` (por defecto), `Dawn` o `Manual`. |
| `dice` | `string?` | Dado que se tira con cada uso: `d4`, `d6`, `d8`, `d10`, `d12`, `d20` o `d100`. |
| `diceByLevel` | `object?` | Dado por nivel de la clase: `{ "3": "d8", "10": "d10", "18": "d12" }`. Vale la entrada más alta no superior al nivel; por debajo de la primera, `dice` (o ninguno). |
| `rollOnRest` | `object?` | Dados que se tiran al descansar (ver abajo). |

**`max`** admite cuatro formas:

| Forma | Ejemplo | Valor |
| --- | --- | --- |
| Entero | `2` | 1–999. |
| Fórmula | `"proficiencyBonus"`, `"2*classLevel+mod:int"` | Suma de los términos; mínimo 1. |
| Fórmula con mínimo | `{ "formula": "mod:wis", "min": 0 }` | Suma de los términos; mínimo `min` (0–999). |
| Tabla por nivel | `{ "byLevel": { "3": 4, "7": 5, "15": 6 } }` | La entrada más alta no superior al nivel de la clase (0–999). Por debajo de la primera entrada, el personaje aún no tiene el recurso. |

Gramática de las fórmulas: términos separados por `+` (hasta 10, con espacios opcionales); cada término
es un entero (0–999) o `[n*]símbolo`, con `n` entero 1–99 y `símbolo` uno de:

| Símbolo | Valor |
| --- | --- |
| `proficiencyBonus` | Bonificador de competencia del personaje. |
| `classLevel` | Nivel en la clase de la elección o del rasgo (las elecciones de origen usan el nivel total). |
| `halfClassLevel` | Mitad de ese nivel, redondeando hacia abajo. |
| `mod:str` … `mod:cha` | Modificador de la característica. |

No hay resta ni paréntesis: `"classLevel-1"` o `"classLevel*2"` son errores (se escribe `"2*classLevel"`).
Una fórmula solo con constantes debe sumar 1–999 (0–999 con `min: 0`). El máximo nunca pasa de 999.

La hoja explica el máximo término a término: `"2*classLevel+mod:int"` a nivel 6 con Inteligencia 16
se muestra como "2 × Nivel de clase 12" + "Inteligencia 3"; las constantes y la entrada de la tabla
llevan el nombre del rasgo u opción y su nivel ("Ventaja táctica (nivel 3), tabla desde el nivel 7");
si se aplica el mínimo, aparece la línea "Mínimo 1". El recurso del personaje incluye `source` (rasgo u
opción y nivel), `dice` (el dado al nivel actual) y `breakdown` (el desglose); la pestaña Combate
ofrece **Tirar** con ese dado, que gasta un uso y tira. La inspiración bárdica del SRD usa el mismo
mecanismo (d6, d8 al 5, d10 al 10, d12 al 15).

**`rollOnRest`**: `{ "dice": "d20", "count": 2, "rest": "long" }` para rasgos cuyos valores se tiran al
descansar (al estilo de un presagio). Tras ese descanso (`long`: solo el largo; `short`: corto y largo) el
recurso queda pendiente (`rollsPending`) hasta que el jugador escribe sus tiradas físicas con
`POST /api/v1/characters/{id}/resources/{resourceId}/rolls` (`{ "values": [14, 3] }`); los valores se
guardan en el recurso (`rolls`). `dice`: `d4`, `d6`, `d8`, `d10`, `d12`, `d20` o `d100`; `count` 1–20.

Los errores llevan la ruta exacta: `...resource.max`, `...resource.max.formula`, `...resource.max.min`,
`...resource.max.byLevel.3`, `...resource.dice`, `...resource.diceByLevel.10`.

**`Cost`**: usos de un recurso del personaje que se gastan **cada vez** que se usa la opción (técnicas
que cuestan ki, metamagia que cuesta puntos de hechicería...).

```json
"cost": { "resource": "ki", "amount": 2 }
```

| Campo | Tipo | Reglas |
| --- | --- | --- |
| `resource` | `string` | **Obligatorio**. Key de un recurso de clase del SRD (`rage`, `bardic-inspiration`, `channel-divinity`, `wild-shape`, `second-wind`, `action-surge`, `indomitable`, `ki`, `lay-on-hands`, `sorcery-points`, `arcane-recovery`, `natural-recovery`) o el `key` de un `resource` de una opción o de un rasgo de subclase (del paquete o del catálogo). |
| `amount` | `int` | **Obligatorio**, 1–20. |

Validación al importar: si el conjunto de la opción lo usan elecciones de clases concretas (del paquete o
del catálogo), un recurso de clase del SRD debe ser de una de esas clases ("`rage` es de barbarian") y el
recurso de un rasgo de subclase, de una subclase de esas clases. Los recursos de las opciones del
paquete valen siempre. Si el conjunto no está ligado a una clase (`feats`, o un conjunto que aún no usa
ninguna elección), basta con que el key exista: un recurso de clase del SRD o un `resource` del paquete
o del catálogo. Errores: `...cost.resource` (falta, no es un índice válido, no existe o es de otra
clase) y `...cost.amount` (falta o fuera de 1–20).

En la app:

- **Asistente de subida**: la tarjeta de la opción muestra "Coste: 2 Ki" (`cost` en las opciones del
  plan: `{ resource, resourceName, amount, label }`; el nombre es el del recurso del personaje, el del
  SRD o el del `resource` que lo declara).
- **Hoja**: `optionCosts` de la ficha lista las opciones elegidas con coste
  (`[{ index, name, resource, resourceName, amount, label }]`) y la sección "Elecciones" las muestra
  como "Golpe sereno (2 Ki)".
- **Combate**: cada recurso lleva `options` (las opciones elegidas que lo gastan); la pestaña Combate
  las lista bajo el recurso con su coste y un botón **Usar** que gasta `amount` usos con
  `POST /api/v1/characters/{id}/resources/{resourceId}/spend` (`{ "amount": 2 }`), sin aprobación,
  como cualquier uso de recurso. El botón se desactiva si no quedan usos suficientes.

### `levelChoices`

En `classesExtended[].levelChoices` (elecciones de la clase base) y en
`classesExtended[].subclasses[].levelChoices` (de la subclase). Cada una:

| Campo | Tipo | Reglas |
| --- | --- | --- |
| `level` | `int` | **Obligatorio**, 1–20: nivel de la clase en que se elige. |
| `key` | `string` | **Obligatorio**, minúsculas, números y guiones, ≤ 100. Única por clase, subclase y nivel; no puede coincidir con una elección del SRD o de otro paquete (p. ej. `asi` del guerrero 4). |
| `name` | `string` | **Obligatorio**, ≤ 200. |
| `kind` | `string` | **Obligatorio**: `Subclass`, `OptionSet`, `AsiOrFeat`, `Expertise`, `Skill`, `Language`, `Tool`, `CantripsKnown`, `SpellsKnown`, `SpellbookSpells` o `Custom`. |
| `setId` | `string?` | Conjunto de donde salen las opciones; **obligatorio** en `OptionSet`. Puede ser del SRD, de otro paquete o del propio paquete. |
| `choose` | `int` | **Obligatorio**, 0–20: cuántas elecciones da este nivel. En `SpellsKnown`/`CantripsKnown` de una subclase con `spellcasting` puede omitirse: se toma el aumento de su tabla `spellsKnown`/`cantripsKnown` respecto al nivel anterior. |
| `from` | `string[]?` | Subconjunto permitido del conjunto (índices de opciones que deben existir en él) o, en `Language`/`Tool`, los valores posibles. Sin `from`, todo el conjunto (o texto libre). |
| `replaces` | `bool?` | Desde este nivel, en cada nivel de la clase se puede sustituir una elección ya hecha. |
| `cumulative` | `bool?` | Se suma a lo elegido en niveles anteriores. |
| `note` | `string?` | ≤ 2000. Aclaración para la interfaz. |
| `filter` | `Filter?` | Conjuros: `spellList` (clase o `any`), `spellLevels` (`[1, 2]`), `maxSpellLevelBySlots`, `source` (`list`, `spellbook` o `known`), `cantripsOnly`, `schools`, `schoolsExceptAt`. |
| `after` | `string?` | Key de otra elección de la misma clase y nivel (del paquete, de la clase base o de la misma subclase, o del catálogo) que se resuelve **antes** que esta. Ver "Orden de las elecciones". |

`AsiOrFeat` ofrece siempre la mejora de característica y las dotes del conjunto `feats`. Las
elecciones `Custom` se validan solo por número. En `Expertise`, `from` limita las habilidades que se
pueden elegir (`["arcana", "nature"]`).

**Orden de las elecciones.** El asistente presenta y el servidor resuelve las elecciones de un nivel en
orden de dependencia: las que pueden dar competencias en habilidades (`Skill`, `Subclass`, `OptionSet`,
`Custom` y `AsiOrFeat`, porque sus opciones pueden traer `grants.skills`) van antes que `Expertise`, y
cada elección con `after` va detrás de la que nombra. Así, la pericia ofrece también las habilidades
ganadas en ese mismo nivel:

- las elegidas en una elección `Skill` del nivel;
- las de `levels[].grants.skills` de la subclase (la que ya tiene el personaje, o la que elige en ese
  nivel: un dominio de clérigo de nivel 1 que concede dos habilidades y pide pericia en ellas, que el
  personaje creado sin subclase recibe al elegirla en la siguiente subida);
- las de `grants.skills` de las opciones y dotes elegidas en el nivel.

En el plan (`GET .../level-up`) esas habilidades llevan `requires: { "choiceKey", "index" }`: la opción
solo vale si se elige `index` en la elección `choiceKey` del mismo nivel, y la app la oculta hasta
entonces. Al aplicar, el servidor recalcula las opciones con todas las respuestas juntas y aplica las
competencias antes que la pericia; una pericia en una habilidad que no se eligió da 400.

`after` sirve para fijar el orden cuando no es el implícito (un idioma después de una habilidad, una
pericia antes de otra elección...). Errores en `...levelChoices[0].after`: key no válida, la propia
elección, ninguna elección con esa key en la misma clase y nivel, o un ciclo entre elecciones del paquete.

```json
"levelChoices": [
  { "level": 3, "key": "idioma-erudito", "name": "Idioma", "kind": "Language", "choose": 1, "after": "habilidad-erudito" },
  { "level": 3, "key": "pericia-erudito", "name": "Pericia", "kind": "Expertise", "choose": 1 },
  { "level": 3, "key": "habilidad-erudito", "name": "Habilidad", "kind": "Skill", "choose": 1, "from": ["arcana", "history"] }
]
```

El asistente las muestra en el orden habilidad → idioma → pericia, y la pericia ofrece la habilidad
elegida en el primer paso.

**Filtro por escuela.** `filter.schools` (`["abjuration", "evocation"]`, índices de escuela del SRD en
minúsculas: `abjuration`, `conjuration`, `divination`, `enchantment`, `evocation`, `illusion`,
`necromancy`, `transmutation`) limita los conjuros a esas escuelas; `filter.schoolsExceptAt`
(`[3, 8, 14, 20]`, niveles 1–20 de la clase) son los niveles en que se puede elegir un conjuro de
cualquier escuela, y solo vale junto con `schools`. El asistente muestra los demás conjuros como no
elegibles con el motivo ("Solo abjuración o evocación salvo en los niveles 3, 8, 14 y 20"). En un nivel
de excepción todos los conjuros de la lista son elegibles: el asistente no cuenta cuántos de los
elegidos son de otra escuela. Errores: `...filter.schools[0]` (escuela desconocida),
`...filter.schoolsExceptAt[0]` (fuera de 1–20), `...filter.schoolsExceptAt` (sin `schools`).

### `subclasses[].spellcasting`

Una subclase de una clase que **no** lanza conjuros (guerrero, pícaro, bárbaro, monje) puede darle
lanzamiento de conjuros:

```json
"spellcasting": {
  "progression": "third",
  "ability": "int",
  "fromLevel": 3,
  "spellList": "wizard",
  "cantripsKnown": { "3": 2, "10": 3 },
  "spellsKnown": { "3": 3, "4": 4, "7": 5, "8": 6 }
}
```

| Campo | Tipo | Reglas |
| --- | --- | --- |
| `progression` | `string` | **Obligatorio**: `third` (tercio), `half` (medio) o `full` (completo). |
| `ability` | `string` | **Obligatorio**: `str`, `dex`, `con`, `int`, `wis` o `cha`. Característica de la CD y del ataque de conjuros. |
| `fromLevel` | `int?` | 1–20, por defecto 1: nivel de la clase desde el que lanza conjuros. |
| `spellList` | `string` | **Obligatorio**: clase del catálogo cuya lista de conjuros usa (`wizard`). |
| `cantripsKnown` | `{nivel: int}?` | Trucos conocidos por nivel de la clase (0–10); vale la entrada más alta no superior al nivel. |
| `spellsKnown` | `{nivel: int}?` | Conjuros conocidos por nivel de la clase (0–30), igual. |

Con esa subclase, la clase pasa a ser lanzadora para el personaje desde `fromLevel`:

- **Espacios**: en clase única, la tabla de la progresión (tercio: 2 espacios de nivel 1 al 3, 3 al 4,
  4 al 7; de nivel 2, 2 al 7 y 3 al 10; de nivel 3, 2 al 13 y 3 al 16; de nivel 4, 1 al 19; medio y
  completo, las tablas del paladín y del mago). En multiclase suma `floor(nivel / 3)` (o `/ 2`, `/ 1`)
  al nivel de lanzador compartido, como las demás clases.
- **Hoja**: el bloque de conjuros lista la clase con su CD y su ataque calculados con `ability` (y su
  desglose) y los máximos de conocidos (`spellsKnownMax`, `cantripsKnownMax` en `sheet.spellcasting`).
- **Asistente**: las elecciones `SpellsKnown`/`CantripsKnown` de la subclase sin `choose` piden el
  aumento de la tabla; `spellList` es la lista por defecto de sus filtros y `maxSpellLevelBySlots` usa
  los espacios de la progresión.

Errores: `...spellcasting` (la clase base ya lanza conjuros), `...spellcasting.progression`,
`...spellcasting.ability`, `...spellcasting.fromLevel`, `...spellcasting.spellList`,
`...spellcasting.spellsKnown.3` (clave que no es un nivel 1–20 o valor fuera de rango) y
`...levelChoices[0].choose` cuando falta y la subclase no tiene la tabla correspondiente.

### `subclasses[].expandedSpellList`

Una subclase puede **ampliar la lista de conjuros de su clase** (patrones de brujo): los conjuros
listados cuentan como de la lista de la clase **solo para los personajes con esa subclase**.

```json
"expandedSpellList": [
  { "index": "faerie-fire", "level": 1 },
  { "index": "sleep", "level": 1 }
]
```

| Campo | Tipo | Reglas |
| --- | --- | --- |
| `index` | `string` | **Obligatorio**: conjuro del SRD, de otro paquete o de este mismo paquete. Sin repetir dentro de la lista. |
| `level` | `int` | **Obligatorio**, 0–9: debe coincidir con el nivel del conjuro (0 para trucos). |

- **No se conceden**: a diferencia de `levels[].grants.spells` (conjuros de dominio siempre
  preparados), el personaje solo puede elegirlos.
- **Asistente de subida**: aparecen como candidatos de `SpellsKnown`, `SpellbookSpells` y, si son de
  nivel 0, `CantripsKnown`, cuando la elección usa la lista de la clase (sin `filter.spellList` o con
  la de la propia clase, o la de `spellcasting.spellList` de la subclase). Se aplican los demás
  filtros (niveles con espacios, escuelas). Cuenta la subclase que el personaje ya tiene: si la elige
  en el mismo nivel, la lista ampliada se aplica desde la siguiente subida.
- **Preparar conjuros**: las clases que preparan de su lista (clérigo, druida, paladín) los ofrecen
  como candidatos; el mago sigue preparando solo de su libro.
- **Compendio**: el conjuro lleva `expandedBy` en la lista y el detalle del catálogo
  (`[{ "subclassIndex", "subclassName", "classIndex", "source" }]`) y la app muestra la etiqueta
  "Lista ampliada: <nombre de la subclase>".

Errores: `...expandedSpellList[0].index` (falta, índice no válido, el conjuro no existe en el
catálogo ni en el paquete, o repetido), `...expandedSpellList[0].level` (falta, fuera de 0–9 o
distinto del nivel del conjuro) y `...expandedSpellList` sin `"formatVersion": 2`.

### `levels[].grants`

Cada nivel de una subclase puede llevar `grants` (mismo formato): se aplican al alcanzar ese nivel de
la clase con la subclase elegida (dominios con conjuros siempre preparados, competencias extra...).

### `levels[].features[].resource`

Cada rasgo de un nivel de subclase puede llevar un `resource` (el mismo `Resource` de las opciones):
el personaje lo recibe como recurso automático al alcanzar ese nivel de la clase con esa subclase, sin
elección en el asistente, y lo pierde si cambia de subclase o baja de nivel. `classLevel` es el nivel
en la clase de la subclase. El origen del recurso es el rasgo con su nivel ("Ventaja táctica (nivel 3)").

```json
{
  "level": 3,
  "features": [
    {
      "index": "tacticos-ejemplo-ventaja",
      "name": "Ventaja táctica",
      "description": ["Texto de ejemplo: gastas un dado de táctica para sumar al ataque."],
      "resource": {
        "key": "tacticos-ejemplo-dados",
        "name": "Dados de táctica",
        "max": { "byLevel": { "3": 4, "7": 5, "15": 6 } },
        "recharge": "ShortRest",
        "dice": "d8",
        "diceByLevel": { "10": "d10", "18": "d12" }
      }
    },
    {
      "index": "tacticos-ejemplo-escudo",
      "name": "Escudo de ejemplo",
      "resource": { "key": "tacticos-ejemplo-escudo", "name": "Escudo", "max": "2*classLevel+mod:int" }
    }
  ]
}
```

### `levels[].features[].modifiers`

Con `"formatVersion": 2`, cada rasgo de un nivel de subclase puede llevar `modifiers`: hasta 10
`Modifier` con la misma forma y validación que los de las opciones (`kind`, `target`, `value` y
`condition` opcional, ver [`Option`](#option)). Se aplican a la hoja mientras el personaje tenga esa
subclase y su nivel en la clase alcance el del rasgo, y se pierden al cambiar de subclase o bajar de
nivel. En los desgloses aparecen con el nombre del rasgo y su nivel ("Pies ligeros (nivel 3)"), igual
que los de las opciones elegidas.

```json
{
  "level": 3,
  "features": [
    {
      "index": "reinos-ejemplo-pies-ligeros",
      "name": "Pies ligeros",
      "description": ["Texto de ejemplo: te mueves antes que nadie."],
      "modifiers": [
        { "kind": "InitiativeBonus", "value": 1 },
        { "kind": "SpeedBonus", "value": 10 },
        { "kind": "ArmorClassBonus", "value": 1, "condition": "wearingArmor" }
      ]
    }
  ]
}
```

Sirve para los rasgos pasivos que suman números (CA, iniciativa, velocidad, salvaciones,
habilidades, PG máximos). **Límite conocido:** lo que exige una decisión del jugador en el momento
(reacciones, ventaja situacional, efectos de un solo uso) sigue en la descripción del rasgo.

Errores con ruta: `...features[0].modifiers` (más de 10, o sin `"formatVersion": 2`),
`...features[0].modifiers[0].kind`, `...modifiers[0].value`, `...modifiers[0].condition` y
`...modifiers[0]` (objetivo no válido para el tipo).

### `levels[].features[].companion`

Un rasgo de subclase puede conceder un **compañero animal**: al alcanzar ese nivel de la clase con esa
subclase, la hoja pide elegir una bestia del catálogo de bestias (las del SRD, pestaña "Bestias" del
compendio) que cumpla el filtro, y la guarda con su nombre y sus PG actuales.

```json
{
  "index": "compas-ejemplo-vinculo",
  "name": "Vínculo de ejemplo",
  "description": ["Texto de ejemplo: una bestia te acompaña."],
  "companion": {
    "beastFilter": { "maxChallengeRating": 0.25, "sizes": ["Medium", "Small"] },
    "hitPoints": "max(beast, 4*classLevel)",
    "proficiencyBonusFromCharacter": true,
    "attackBonusFromCharacter": true
  }
}
```

| Campo | Tipo | Descripción |
| --- | --- | --- |
| `beastFilter.maxChallengeRating` | `number` | **Obligatorio**. VD máximo de la bestia, entre 0 y 30 (`0.25` para 1/4). |
| `beastFilter.sizes` | `string[]?` | Tamaños permitidos: `Tiny`, `Small`, `Medium`, `Large`, `Huge`, `Gargantuan`. Vacío o ausente: cualquiera. |
| `hitPoints` | `string?` | `"beast"` (los PG de la bestia, por defecto) o `"max(beast, N*classLevel)"` con N entre 1 y 20: el mayor entre los PG de la bestia y N × el nivel en la clase del rasgo. |
| `proficiencyBonusFromCharacter` | `bool?` | Suma el bonificador de competencia del personaje a la CA y a las salvaciones y habilidades en que la bestia ya es competente. |
| `attackBonusFromCharacter` | `bool?` | Suma el bonificador de competencia del personaje a las tiradas de ataque y al daño (una vez por ataque, en el primer dado de daño). |

Errores con ruta: `...companion.beastFilter`, `...companion.beastFilter.maxChallengeRating`,
`...companion.beastFilter.sizes[0]`, `...companion.hitPoints`; sin `"formatVersion": 2`,
`...companion`.

En la API:

- El detalle del personaje lleva `companionFeature` (el rasgo alcanzado: `featureIndex`, `featureName`,
  `classIndex`, `classLevel`, `maxChallengeRating`, `maxChallengeRatingText`, `sizes`, `hitPoints`,
  `proficiencyBonusFromCharacter`, `attackBonusFromCharacter`), `companionPending` (rasgo alcanzado y
  sin compañero) y `companion`: `beastIndex`, `beastName`, `name`, `hitPointsCurrent`,
  `hitPointsMax`, `armorClass`, `savingThrows`, `skills`, `attacks[]` (`attackBonus`,
  `attackBreakdown`, `damage[]` con el bonificador ya sumado al primer dado, `damageBreakdown`) y
  `breakdowns` (`armorClass`, `hitPointsMax`, `save.<característica>`, `skill.<habilidad>`).
- `PUT /api/v1/characters/{id}/companion` (`{ "beastIndex": "wolf", "name": "Ceniza" }`): el dueño o un
  DM. El primer compañero y los cambios de nombre se aplican directamente; si un jugador cambia la
  bestia de un personaje activo se crea una solicitud `Companion` (202, con `before`) que el DM
  aprueba; el DM/Owner la cambia directamente. Una bestia fuera del filtro da 400 en `beastIndex`; sin
  el rasgo, 409.
- `POST /api/v1/characters/{id}/companion/hp` (`{ "delta": -5 }` o `{ "current": 7 }`): auto-seguimiento
  sin aprobación, entre 0 y el máximo. Un descanso largo lo devuelve al máximo.
- `DELETE /api/v1/characters/{id}/companion`: solo DM/Owner.
- Cada cambio emite `character.updated` por el hub de la campaña.

La pestaña Combate (y la hoja) ofrece "Elegir compañero" con las bestias que cumplen el filtro y
muestra el bloque del compañero con CA, PG (con controles de daño y curación), salvaciones,
habilidades y ataques con botones de tirada; cada valor abre su desglose.
