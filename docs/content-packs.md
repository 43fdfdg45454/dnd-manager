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

**`SubclassLevel`**: `level` (`int`, **obligatorio**, 1–20, sin repetir dentro de la subclase) y
`features` (`Feature[]?`).

**`Feature`**: `index` (**obligatorio**, con prefijo), `name` (**obligatorio**, ≤ 200) y
`description` (`string[]?`).

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

**`Subrace`**: `index` (**obligatorio**, con prefijo), `name` (**obligatorio**, ≤ 200), `description`
(`string?`, un solo texto de ≤ 10 000), `abilityBonuses` (`AbilityBonus[]?`), `traits` (`Trait[]?`),
`choices` (`OriginChoices?`) y `resistances` (`string[]?`).

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
`traits`, `backgrounds`, `optionSets`, `options`, `levelChoices`, `trinkets` y `rollTables` importados.

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
| `abilityIncrease` | `AbilityIncrease?` | Dotes: `{ "amount": 1-2, "from": ["str", "dex"] }`. `from` vacío = cualquier característica; con una sola, se aplica sin preguntar. El tope de 20 se respeta. |
| `grants` | `Grants?` | Competencias y conjuros que concede. |
| `resource` | `Resource?` | Recurso de usos limitados que aparece como recurso automático del personaje. |

**`Prerequisites`** (todas las condiciones dadas deben cumplirse): `minLevel` (1–20, nivel en la
clase de la elección), `pactBoon` (índice del don del pacto elegido, p. ej. `pact-of-the-blade`),
`cantrip` (índice de un truco que el personaje conozca), `abilities` (`{ "str": 13 }`, 1–30). Las
opciones sin cumplir aparecen en el asistente como no elegibles, con el motivo.

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
conjuros y trucos deben existir en el SRD o en el paquete.

**`Resource`**: `key` (índice, sin prefijo obligatorio), `name` (**obligatorio**, ≤ 100), `max` (entero
1–999 o fórmula: `proficiencyBonus`, `classLevel`, `halfClassLevel`, `mod:cha`... mínimo 1),
`recharge` (`ShortRest`, `LongRest` por defecto, `Dawn` o `Manual`) y `rollOnRest` opcional:
`{ "dice": "d20", "count": 2, "rest": "long" }` para rasgos cuyos valores se tiran al descansar (al
estilo de un presagio). Tras ese descanso (`long`: solo el largo; `short`: corto y largo) el recurso
queda pendiente (`rollsPending`) hasta que el jugador escribe sus tiradas físicas con
`POST /api/v1/characters/{id}/resources/{resourceId}/rolls` (`{ "values": [14, 3] }`); los valores se
guardan en el recurso (`rolls`). `dice`: `d4`, `d6`, `d8`, `d10`, `d12`, `d20` o `d100`; `count` 1–20.

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
| `choose` | `int` | **Obligatorio**, 0–20: cuántas elecciones da este nivel. |
| `from` | `string[]?` | Subconjunto permitido del conjunto (índices de opciones que deben existir en él) o, en `Language`/`Tool`, los valores posibles. Sin `from`, todo el conjunto (o texto libre). |
| `replaces` | `bool?` | Desde este nivel, en cada nivel de la clase se puede sustituir una elección ya hecha. |
| `cumulative` | `bool?` | Se suma a lo elegido en niveles anteriores. |
| `note` | `string?` | ≤ 2000. Aclaración para la interfaz. |
| `filter` | `Filter?` | Conjuros: `spellList` (clase o `any`), `spellLevels` (`[1, 2]`), `maxSpellLevelBySlots`, `source` (`list`, `spellbook` o `known`), `cantripsOnly`. |

`AsiOrFeat` ofrece siempre la mejora de característica y las dotes del conjunto `feats`. Las
elecciones `Custom` se validan solo por número.

### `levels[].grants`

Cada nivel de una subclase puede llevar `grants` (mismo formato): se aplican al alcanzar ese nivel de
la clase con la subclase elegida (dominios con conjuros siempre preparados, competencias extra...).
