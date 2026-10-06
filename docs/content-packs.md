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
  su raza.
- **Tamaño máximo del fichero**: 20 MB.
- El contenido se muestra tal cual: escribe los textos en el idioma que prefieras (el SRD está en
  inglés).

## Campos

Notación: `string?` admite `null` o ausencia; **obligatorio** indica que no puede faltar ni estar vacío.

### Raíz

| Campo | Tipo | Reglas |
| --- | --- | --- |
| `formatVersion` | `int?` | Versión del formato. Opcional; si se indica, debe ser `1`. |
| `id` | `string` | **Obligatorio**. `[a-z0-9-]{3,40}`. Identifica el paquete y es el prefijo de sus índices. `srd` y `homebrew` están reservados. |
| `name` | `string` | **Obligatorio**, ≤ 200. Nombre visible ("Reinos de Ejemplo"). |
| `version` | `string` | **Obligatorio**, ≤ 40. Versión libre del paquete (`1.0.0`). |
| `classesExtended` | `ClassExtension[]?` | Subclases nuevas para clases existentes. |
| `items` | `Item[]?` | Objetos. |
| `spells` | `Spell[]?` | Conjuros. |
| `races` | `Race[]?` | Razas con sus rasgos y subrazas. |
| `backgrounds` | `Background[]?` | Trasfondos. |

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

**`Subrace`**: `index` (**obligatorio**, con prefijo), `name` (**obligatorio**, ≤ 200), `description`
(`string?`, un solo texto de ≤ 10 000), `abilityBonuses` (`AbilityBonus[]?`) y `traits` (`Trait[]?`).

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
`traits` y `backgrounds` importados.

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
