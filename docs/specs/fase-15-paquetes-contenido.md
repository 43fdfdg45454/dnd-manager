# Fase 15 — Paquetes de contenido privados

Contrato cerrado. El repositorio solo incluye el SRD 5.1 (CC-BY). Cada instancia puede importar
**paquetes de contenido** JSON (subclases, ítems, hechizos, razas, trasfondos, rasgos) creados por
su administrador a partir de material que posea. Ningún paquete se commitea (`.gitignore`:
`content-packs/`).

## Formato (`docs/content-packs.md`, con ejemplo ficticio "Reinos de Ejemplo")

```json
{
  "id": "reinos-ejemplo",            // [a-z0-9-]{3,40}; prefijo de todos los índices del pack
  "name": "Reinos de Ejemplo",
  "version": "1.0.0",
  "classesExtended": [ { "classIndex": "fighter", "subclasses": [ { "index": "reinos-ejemplo-centinela", "name": "Centinela", "flavor": "Arquetipo marcial", "description": ["..."], "levels": [ { "level": 3, "features": [ { "index": "reinos-ejemplo-vigilia", "name": "Vigilia", "description": ["..."] } ] } ] } ] } ],
  "items": [ { "index": "reinos-ejemplo-espada-del-alba", "name": "Espada del Alba", "category": "Weapon", "subcategory": "Martial Melee", "rarity": "Rare", "requiresAttunement": true, "costCp": null, "weightLb": 3, "damageDice": "1d8", "damageType": "slashing", "versatileDice": "1d10", "properties": ["versatile"], "description": ["..."], "modifiers": [ { "kind": "AttackBonus", "value": 1 }, { "kind": "DamageBonus", "value": 1 } ] } ],
  "spells": [ { "index": "reinos-ejemplo-luz-del-alba", "name": "Luz del Alba", "level": 1, "school": "evocation", "castingTime": "1 action", "range": "60 feet", "components": ["V","S"], "material": null, "duration": "Instantaneous", "concentration": false, "ritual": false, "description": ["..."], "higherLevel": [], "classes": ["cleric","paladin"], "subclasses": [], "attackType": null, "damage": null, "dcAbility": null } ],
  "races": [ { "index": "reinos-ejemplo-aurano", "name": "Aurano", "speed": 30, "size": "Medium", "sizeDescription": "...", "abilityBonuses": [ { "ability": "cha", "bonus": 2 } ], "languages": ["Common"], "age": "...", "alignment": "...", "traits": [ { "index": "reinos-ejemplo-brillo", "name": "Brillo", "description": ["..."] } ], "subraces": [ { "index": "...", "name": "...", "description": "...", "abilityBonuses": [], "traits": [] } ] } ],
  "backgrounds": [ { "index": "reinos-ejemplo-farero", "name": "Farero", "featureName": "...", "featureDescription": ["..."], "skillProficiencies": ["perception","survival"], "startingEquipmentText": "..." } ]
}
```

Reglas: todos los índices empiezan por `<id>-`; nombres ≤ 200; listas ≤ 500 entradas por tipo;
`modifiers` sigue `ItemModifier` (fase 11); `category`/`rarity`/`kind` con los nombres de los
enums; referencias (`classIndex`, `classes`, `subclasses`, `subraces`) deben existir en el SRD o
en el propio pack. Tamaño máximo del fichero 20 MB.

## Servidor (Opus)

- Columna `Source` (string, índice) en `ClassDefinition` (no: las clases no se amplían),
  `SubclassDefinition`, `SubclassLevel`, `FeatureDefinition`, `SpellDefinition`, `RaceDefinition`,
  `SubraceDefinition`, `TraitDefinition`, `BackgroundDefinition` e `ItemTemplate` (SRD: `"srd"`;
  pack: su `id`). Migración `AddCatalogSource` con valor por defecto `srd` para lo existente.
  `CatalogImport.Ruleset = "pack:<id>"` por pack, con `DatasetVersion = version` y `CountsJson`.
- `ContentPackImporter` (`Dnd.Infrastructure/Catalog/ContentPackImporter.cs`): deserializa
  (`JsonSerializerOptions` estricto, `UnmappedMemberHandling.Disallow` para detectar typos),
  valida con mensajes en español que incluyen la ruta (`items[3].modifiers[0].kind`), y en una
  transacción: borra las definiciones con `Source == id` (ítems: `UpsertItemsAsync` para
  conservar ids referenciados por inventarios; el resto se reemplaza) e inserta las nuevas; graba
  `CatalogImport`. Idempotente: reimportar la misma versión reemplaza igualmente.
- `SrdSeeder` deja intactas las definiciones con `Source != "srd"` al reimportar el SRD.
- Endpoints Admin (`/api/v1/admin/content-packs`): `GET` lista `[{ id, name, version, importedAt, counts }]`;
  `POST` multipart (`file`) o JSON directo → `201 { id, counts }` / `400` con lista de errores;
  `DELETE /{id}` elimina las definiciones del pack (los personajes conservan índices; `GetCharacter`
  marca `catalogMissing: true` en clases/subclases/hechizos/razas que no resuelvan, en vez de fallar).
- Consultas de catálogo: incluyen todas las fuentes; `ItemSummaryDto.Source` pasa a devolver
  `"srd"`, `"homebrew"` o el id del pack; `SpellSummaryDto`, `ClassDetailDto.Subclasses[]`,
  `RaceSummaryDto`, `BackgroundDto` ganan `source`.
- Tests: importar el ejemplo ficticio (fichero de test en `server/tests/Dnd.Api.Tests/Fixtures/content-pack-example.json`)
  → aparece la subclase en `GET /catalog/classes/fighter`, el ítem en `/catalog/items`, el hechizo
  en `/catalog/spells`; reimportar con cambio de nombre lo actualiza conservando el id del ítem;
  `DELETE` lo quita y un personaje con esa subclase sigue cargando con `catalogMissing`; errores de
  validación con ruta; usuario no admin → 403; reseed del SRD no borra el pack.

## Cliente (Sonnet)

- Pantalla admin "Contenido" (`/admin/content`, enlace desde la pantalla de administración): lista
  de packs (nombre, versión, fecha, recuentos), botón "Importar paquete" (`file_picker` JSON,
  subida multipart con progreso), borrar con confirmación, errores de validación listados.
- Compendio y selectores (clase/subclase, hechizos, ítems, razas, trasfondos): chip de fuente
  cuando no es SRD ("Reinos de Ejemplo"); `classThemeOf` usa icono genérico para subclases de pack.
- Hoja: badge "Contenido no disponible" si `catalogMissing`.
- Tests: pantalla lista e importa (fake), chip de fuente en el compendio.

## Documentación

- `docs/ADR/0007-paquetes-de-contenido-privados.md`: el repo solo SRD; el contenido con copyright
  se importa por instancia y nunca se commitea; formato versionado.
- `deploy/README.md`: sección "Paquetes de contenido" (cómo importar desde la app; copia de
  seguridad incluye la base de datos, así que los packs se restauran con ella).
- `CLAUDE.md`: mención en "Decisiones fijadas".

## Verificación

`cd server && dotnet build && dotnet test`; `cd app && flutter analyze && flutter test`. Manual:
importar el ejemplo ficticio y elegir la subclase en un guerrero de nivel 3.
