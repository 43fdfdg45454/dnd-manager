# Fase 3 — Catálogo SRD 5.1

Contrato cerrado. Importa el SRD 5.1 (reglas 2014) desde el dataset JSON de `5e-database`
(`server/seed/srd/`, licencia MIT; contenido CC-BY 4.0) y lo expone para el cliente.

## Fuente

Ficheros en `server/seed/srd/` (inglés, `src/2014/en` del dataset): Classes, Subclasses, Levels,
Features, Races, Subraces, Traits, Spells, Equipment, Magic-Items, Conditions, Skills,
Backgrounds, Weapon-Properties, Equipment-Categories, Damage-Types, Magic-Schools, Languages,
Alignments. Se incluyen como **EmbeddedResource** del proyecto `Dnd.Infrastructure` para que
viajen dentro de la imagen Docker sin copiar carpetas.

## Entidades (Dnd.Domain/Catalog)

Todas con `Index` (slug del dataset, clave natural, único) y `Name`. Las listas se guardan como
JSON en columnas de texto mediante `ValueConverter` (portable a SQLite y Postgres).

```
ClassDefinition     Index, Name, HitDie, SavingThrows[] (ability index), ProficiencyNames[],
                    SpellcastingAbility? ("int"|"wis"|"cha"), IsSpellcaster, SpellcastingLevel (1, 2 o 3; 0 si no), 
                    SubclassFlavor (p. ej. "Primal Path"), StartingEquipmentText
ClassLevel          ClassIndex, Level (1-20), ProfBonus, AbilityScoreBonuses, FeatureIndexes[],
                    ClassSpecificJson (string), CantripsKnown?, SpellsKnown?, SpellSlots[9] (int[], nivel 1..9)
SubclassDefinition  Index, ClassIndex, Name, Flavor, Description[]
SubclassLevel       SubclassIndex, Level, FeatureIndexes[]
FeatureDefinition   Index, Name, ClassIndex, SubclassIndex?, Level, Description[]
RaceDefinition      Index, Name, Speed, Size, AbilityBonusesJson ([{ability, bonus}]), TraitIndexes[],
                    Languages[], Age, Alignment, SizeDescription, SubraceIndexes[]
SubraceDefinition   Index, RaceIndex, Name, Description, AbilityBonusesJson, TraitIndexes[]
TraitDefinition     Index, Name, Description[], RaceIndexes[], SubraceIndexes[]
SpellDefinition     Index, Name, Level (0-9), School, CastingTime, Range, Components[] (V/S/M),
                    Material?, Duration, Concentration, Ritual, Description[], HigherLevel[],
                    ClassIndexes[], SubclassIndexes[], AttackType?, DamageJson?, DcAbility?
ItemTemplate        Id (Guid), CampaignId? (null = SRD), Index? (null = homebrew), Name,
                    Category (Weapon|Armor|Shield|AdventuringGear|Tool|Mount|Consumable|MagicItem|Other),
                    Subcategory (p. ej. "Simple Melee", "Light Armor", "Potion"), Rarity?
                    (Common|Uncommon|Rare|VeryRare|Legendary|Artifact|Varies), RequiresAttunement,
                    CostCp (int, en piezas de cobre; null si desconocido), WeightLb (decimal?),
                    DamageDice?, DamageType?, VersatileDice?, Properties[] (índices), RangeNormal?, RangeLong?,
                    ArmorClassBase?, AddDexModifier?, MaxDexBonus?, StrengthMinimum?, StealthDisadvantage,
                    Description[], IsSrd (computed = CampaignId == null), CreatedAt
ConditionDefinition Index, Name, Description[]
SkillDefinition     Index, Name, AbilityIndex, Description[]
BackgroundDefinition Index, Name, FeatureName, FeatureDescription[], SkillProficiencies[], StartingEquipmentText
CatalogImport       Id, Ruleset ("srd-5.1"), DatasetVersion (sha/fecha del dataset), ImportedAt, Counts JSON
```

Conversión de coste: `cp`=1, `sp`=10, `ep`=50, `gp`=100, `pp`=1000.

Mapeo de Equipment a `Category`: `equipment_category.index` = weapon → Weapon; armor → Armor (si
`armor_category` = Shield → Shield); adventuring-gear → AdventuringGear (si `gear_category` =
"Ammunition" o "Potion"… no hay pociones no mágicas, mantener AdventuringGear); tools → Tool;
mounts-and-vehicles → Mount. Magic-Items: Category = MagicItem salvo que `equipment_category` sea
weapon/armor, en cuyo caso se mantiene Weapon/Armor con `Rarity` y descripción, y variantes
(`variant: true`) se importan como ítems independientes.

## Seed

`SrdSeeder` (Infrastructure) ejecutado al arrancar tras las migraciones, antes de
`InitialAdminSeeder`. Idempotente: si existe `CatalogImport` con el mismo `Ruleset` y
`DatasetVersion`, no hace nada. Importa en una transacción. Debe tardar pocos segundos (usar
`AddRange` y un solo `SaveChanges`). Desactivable con `Catalog:SeedOnStartup=false`. Exponer también
`ISrdSeeder.SeedAsync()` para los tests.

## Endpoints (`/api/v1/catalog`, requieren JWT)

| Método | Ruta | Parámetros | Respuesta |
|--------|------|-----------|-----------|
| GET | `/attribution` | — | `200 { ruleset, license, text }` texto CC-BY obligatorio |
| GET | `/classes` | — | `200 ClassSummaryDto[]` |
| GET | `/classes/{index}` | — | `200 ClassDetailDto` (con levels, subclasses, features por nivel) |
| GET | `/races` | — | `200 RaceSummaryDto[]` |
| GET | `/races/{index}` | — | `200 RaceDetailDto` (traits, subraces con sus traits) |
| GET | `/spells` | `search, level, class, school, ritual, concentration, page=1, pageSize=50` | `200 Page<SpellSummaryDto>` |
| GET | `/spells/{index}` | — | `200 SpellDetailDto` |
| GET | `/items` | `search, category, rarity, page, pageSize` | `200 Page<ItemSummaryDto>` solo SRD (CampaignId null) |
| GET | `/items/{id}` | — | `200 ItemDetailDto` |
| GET | `/conditions` | — | `200 ConditionDto[]` |
| GET | `/skills` | — | `200 SkillDto[]` |
| GET | `/backgrounds` | — | `200 BackgroundDto[]` |
| GET | `/features/{index}` | — | `200 FeatureDto` |

`Page<T> { items, total, page, pageSize }`. Búsqueda `search` insensible a mayúsculas sobre `Name`
(`ILIKE`/`LIKE` con `ToLower`). Orden por nombre; hechizos por nivel y nombre.

DTOs en camelCase con los mismos campos que las entidades (sin `*Json` crudos: exponer
`abilityBonuses: [{ability, bonus}]`, `damage: {dice, type}`, `spellSlots: [..9]`, etc.).

## Cliente Flutter

- `features/catalog`: pantalla "Compendio" accesible desde el inicio, con pestañas Hechizos,
  Objetos, Clases, Razas, Condiciones. Búsqueda con *debounce* (300 ms) y filtros básicos
  (nivel y clase en hechizos; categoría en objetos). Paginación con scroll infinito.
- Páginas de detalle: hechizo (todos los campos), objeto (daño, propiedades, AC, coste en
  formato "1 gp / 5 sp"), clase (dado de golpe, salvaciones, tabla de niveles con slots y rasgos
  expandibles), raza (bonos, rasgos, subrazas), condición.
- Pie de página de atribución CC-BY en la pantalla de Compendio (texto de `/attribution`).
- Modelos Dart en `features/catalog/data/models.dart` escritos a mano (sin generación).
- Tests de widget: lista de hechizos con repositorio falso; filtro por nivel; detalle muestra
  descripción.

## Pruebas del servidor

Seed sobre SQLite en memoria: conteos (12 clases, 12 subclases, 290 niveles, 9 razas, 4
subrazas, 319 hechizos, 237 equipo + 362 objetos mágicos ≥ 590 `ItemTemplate` SRD, 15
condiciones, 18 habilidades, 1 trasfondo); segunda ejecución no duplica; `/spells?level=3&class=wizard`
solo devuelve hechizos de nivel 3 del mago; `/items?search=sword` incluye "Longsword"; `/classes/wizard`
trae 20 niveles con slots en el nivel 1 = [2,0,0,0,0,0,0,0,0]; `/attribution` contiene "Creative Commons".
