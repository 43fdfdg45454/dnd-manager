# Fase 34 — SRD como paquete base, formato 5e v3 y paquetes activables por campaña

Contrato de implementación. Parte de `docs/framework-roadmap.md` (fila 34), de la fase 30 §6
(paquetes y catálogo) con las decisiones del revisor (4 y 6) y de la enmienda sin compatibilidad
hacia atrás. Objetivo: que **todo** el contenido de D&D 5e que no es SRD (PHB privado y, en la
fase 35, Valda's Spire of Secrets **completo**: razas, clases nuevas, subclases, dotes, equipo, objetos
mágicos, conjuros, criaturas y reglas) quepa en un paquete JSON, y que cada campaña elija qué
paquetes usa.

Sin compatibilidad hacia atrás: `CatalogImports` desaparece, el formato 1/2 deja de aceptarse si
estorba (el PHB privado se reexporta a v3 con el script de construcción), y las instancias se recrean.

## 0. Estado de partida (master, 2026-10-10)

- El SRD se carga con `SrdSeeder` desde `server/src/Systems/Dnd5e/seed/srd/*.json` (formato de
  5e-database más `level-choices.json` y `option-sets.json`), registrado en `CatalogImports` como
  `Ruleset = "srd-5.1"`; sus filas llevan `Source = "srd"`.
- Los paquetes (formato 1 y 2, `docs/content-packs.md`) se importan con `ContentPackRegistry` →
  `ICatalogSystem.ImportPackAsync`, registrados como `Ruleset = "pack:<id>"`. Un paquete solo
  puede **extender** clases del SRD (`classesExtended`), y aporta objetos, conjuros, razas,
  trasfondos, conjuntos de opciones (dotes incluidas), baratijas y tablas.
- No existen en el formato: clases completas, condiciones, criaturas, reglas o textos sueltos,
  vocabularios (idiomas, propiedades de arma, categorías de equipo, escuelas, tipos de daño).
- El catálogo no sabe de campañas: todo lo importado está disponible en todas.
- La app: `admin_content_page.dart` (importar/borrar paquetes), `HomebrewTab` en la sección
  "Contenido" de la campaña, compendio global con pestañas del sistema, `catalogSourcesProvider`.

## 1. Modelo de datos (núcleo)

Tabla `ContentPacks` sustituye a `CatalogImports` (migración `ReplaceCatalogImportsWithContentPacks`,
que crea la tabla nueva, copia las filas `pack:*` y `srd-5.1` y borra la antigua):

| Columna | Tipo | Notas |
|---|---|---|
| `Id` | `varchar(60)` PK | `srd`, `phb-2014`, `valdas`… (`[a-z0-9-]{3,40}`) |
| `SystemId` | `varchar(32)` | sistema que lo entiende (`dnd5e`) |
| `Name`, `Version` | `varchar(200)`, `varchar(40)` | |
| `FormatVersion` | `int` | `3` para los importados; `0` para el base cargado por el seeder |
| `IsBase` | `bool` | el paquete base del sistema (SRD): no se borra, no se desactiva, siempre activo |
| `ImportedAt` | `timestamptz` | |
| `CountsJson` | `text` | recuentos por tipo |

Tabla `CampaignContentPacks` (migración `AddCampaignContentPacks`): `CampaignId` FK cascade,
`PackId` FK cascade, `EnabledAt`, `EnabledByUserId`; PK compuesta. El paquete base no se guarda
(siempre activo). **Al importar un paquete nuevo no se activa en ninguna campaña**; al borrar un
paquete se borran sus filas de activación. Las campañas creadas antes de esta fase quedan sin
paquetes activos (sin compatibilidad: el DM los activa).

Núcleo (`OpenTrpg.Core`):

- `ContentPack` (dominio), `IContentPackRepository`, `ContentPackRegistry` adaptado (registra
  `SystemId` del paquete: `"system"` del JSON o el sistema por defecto).
- `ICatalogSystem` gana `Task<PackImportResult> ImportPackAsync(Stream, CancellationToken)` (ya
  existe), `Task RegisterBasePackAsync(CancellationToken)` (el seeder registra la fila `IsBase`) y
  `IReadOnlyList<string> BaseCatalogSources` sigue en `Info`.
- **Ámbito de catálogo**: `CatalogScope` = `{ CampaignId?, Sources: IReadOnlySet<string> }` con
  `Sources = base ∪ activos de la campaña ∪ {"homebrew" de esa campaña}`; lo construye
  `ICatalogScopeResolver.ForCampaignAsync(campaignId)` / `ForCharacterAsync(characterId)` /
  `Global()` (= base ∪ todos los importados; para el compendio global, decisión 6 de la fase 30).
- Endpoints núcleo:
  - `GET /api/v1/admin/content-packs` → `[{ id, systemId, name, version, formatVersion, isBase, importedAt, counts }]`.
  - `POST /api/v1/admin/content-packs`, `DELETE /api/v1/admin/content-packs/{id}` como hoy (404/409 `base-pack` si es base).
  - `GET /api/v1/campaigns/{id}/content-packs` (miembros) → `[{ id, name, version, isBase, enabled }]`
    de los paquetes del sistema de la campaña.
  - `PUT /api/v1/campaigns/{id}/content-packs` (Owner/DM) con `{ "packIds": [...] }` → reemplaza la
    lista (ignora el base; 400 `unknown-pack` si no existe o es de otro sistema). Emite el evento
    de tiempo real `campaign.updated` (ya existe) para que la app recargue.
- `ItemTemplateRepository.WhereListed` pasa a filtrar por `CatalogScope` (tienda, alijo, homebrew,
  búsqueda de objetos). Un objeto de un paquete desactivado que ya está en un inventario sigue
  resolviéndose (como hoy al borrar: `CatalogMissing` solo si se borró).

## 2. Formato de paquete v3 (módulo 5e)

`"formatVersion": 3` y `"system": "dnd5e"` (por defecto). Se mantiene todo el formato 2 (secciones,
`levelChoices`, `grants`, `resource`, `modifiers`, `companion`, `optionSets`, `heightWeight`…) y se
añaden las secciones siguientes. `docs/content-packs.md` se reescribe para v3 (los formatos 1 y 2
quedan documentados en una sección "Formatos anteriores" solo si el importador los sigue aceptando;
recomendación: aceptar solo v3 y actualizar el script privado del PHB).

### 2.1 `classes[]` — clases completas

Una clase nueva (Alchemist, Captain, Gunslinger, Witch…) con todo lo que hoy tiene `ClassDefinition`
+ `ClassLevel` del SRD:

```jsonc
{
  "index": "ej-alquimista",          // con prefijo del paquete
  "name": "Alchemist",
  "hitDie": 8,
  "savingThrows": ["con", "int"],
  "proficiencies": { "armor": ["light"], "weapons": ["simple"], "tools": ["alchemists-supplies"] },
  "skillChoices": { "choose": 2, "from": ["arcana", "medicine", "nature"] },
  "startingEquipment": { ... },       // mismo esquema que Background.startingEquipment + clase SRD
  "startingEquipmentText": "...",
  "multiclassing": { "prerequisites": { "int": 13 }, "proficiencies": { ... } },
  "spellcasting": {                   // opcional
    "ability": "int",
    "progression": "full" | "half" | "third" | "pact" | "table",
    "slots": { "1": [2,0,0,0,0,0,0,0,0], ... },  // solo con "table"
    "cantripsKnown": { "1": 2, "4": 3 }, "spellsKnown": { ... },   // opcional; por nivel
    "preparation": "prepared" | "known",
    "ritual": true, "focus": "alchemists-supplies"
  },
  "subclassFlavor": "Field of Study",
  "subclassLevel": 3,
  "description": ["..."],
  "levels": [
    { "level": 1, "profBonus": 2, "features": [Feature], "abilityScoreImprovement": false,
      "classSpecific": { "bombs": 2 } },         // valores de tabla por nivel (como class_specific del SRD)
    ...
  ],
  "resources": [ { "key": "bombs", "name": "Bombs", "max": "classSpecific:bombs", "recharge": "LongRest" } ],
  "subclasses": [Subclass],           // el mismo esquema que classesExtended[].subclasses
  "spellList": ["ej-conjuro-1", "fireball"]   // lista de conjuros de la clase (índices SRD o del paquete)
}
```

Reglas: `levels` cubre 1–20 sin huecos; `profBonus` por nivel (si falta, el estándar);
`abilityScoreImprovement` por nivel (si falta, los estándar 4/8/12/16/19); los rasgos de nivel usan
`Feature` de v2 (`resource`, `modifiers`, `companion`, `grants`); `spellList` admite también
`{ "class": "wizard" }` para heredar la lista de otra clase. El importador escribe `ClassDefinition`,
`ClassLevel` (con `SpellSlots` calculados por `progression` o copiados de `slots`), `FeatureDefinition`,
`SubclassDefinition`, `SubclassLevel`, recursos y `LevelChoiceRule` como hace ya para subclases.
Las clases nuevas aparecen en el asistente de creación, en el multiclase y en la subida de nivel sin
código específico: todo lo que la hoja necesita (dado de golpe, salvaciones, competencias,
lanzamiento, recursos) sale de la definición.

### 2.2 `feats[]` — dotes de primera clase

Hoy las dotes son `optionSets` con `kind: feat`. v3 añade `feats[]` (`index`, `name`, `prerequisites`,
`prerequisitesText`, `description[]`, `abilityIncrease`, `modifiers`, `grants`, `resource`,
`category?` como "starter" de Valda's) que el importador convierte al mismo `OptionSet` interno
("feats"); `optionSets` sigue aceptado para lo demás (estilos, invocaciones, maniobras…).

### 2.3 `races[]` completas y `subraces`

Ya existe en v2 (`extends` o raza nueva). Se añade `size`, `speed`, `languages`, `age`/`alignment`
texto y `heightWeight` que v2 ya tiene; el importador deja de exigir que una raza nueva tenga algo
del SRD.

### 2.4 `equipment[]` y `items[]`

`items[]` sigue (objetos y objetos mágicos con rareza, sintonía, modificadores). v3 añade a `Item`:
`weapon: { category: "simple"|"martial", range: "melee"|"ranged", damage, damageType, properties[],
rangeNormal, rangeLong, special? }`, `armor: { category, baseAc, dexBonus, maxDexBonus, strMin,
stealthDisadvantage }`, `tool`, `ammunition` y `firearm: { reload, misfire }` (Valda's), de modo que
una arma nueva funciona en ataques y CA como las del SRD (`ItemTemplate` ya tiene estas columnas 5e
mapeadas por el módulo; si falta alguna, se añade con migración).

### 2.5 `spells[]`

Como v2 más `lists: ["ej-alquimista", "wizard"]` (clases cuya lista incluye el conjuro, para
clases nuevas) y `source` implícito.

### 2.6 `creatures[]`

Bloques de estadísticas con el esquema de `5e-SRD-Beasts.json` traducido a camelCase (`index`,
`name`, `size`, `type`, `subtype`, `alignment`, `armorClass`, `hitPoints`, `hitDice`, `speed`,
`abilities`, `savingThrows`, `skills`, `damageVulnerabilities/Resistances/Immunities`,
`conditionImmunities`, `senses`, `languages`, `challengeRating`, `xp`, `traits[]`, `actions[]`,
`reactions[]`, `legendaryActions[]`, `description[]`). Se guardan en una tabla nueva
`Dnd5eCreatures` (migración `AddDnd5eCreatures`); `SrdBeastCatalog` (hoy en memoria desde el JSON)
pasa a leer de esa tabla y el seeder carga los 87 del SRD. Aparecen en el compendio (pestaña
Bestias) y como compañeros (`companion` filtra por tipo y CR como hoy).

### 2.7 `conditions[]`, `rules[]`, `reference`

- `conditions[]`: `index`, `name`, `description[]` → `ConditionDefinition` (pestaña Condiciones y
  selector de condiciones del combate).
- `rules[]`: documentos de reglas (`index`, `title`, `category` ("variant", "multiclassing",
  "equipment", "general"…), `body[]` en Markdown ligero, `tags[]`). Nueva tabla `Dnd5eRules`;
  nueva pestaña **Reglas** en el compendio con búsqueda; sin efecto mecánico. Aquí caben las
  "Auxiliary Levels", reglas de armas de fuego, etc. de Valda's.
- `reference`: vocabularios ampliables: `languages[]`, `weaponProperties[]`, `equipmentCategories[]`,
  `damageTypes[]`, `magicSchools[]`, `tools[]` (todos `index`, `name`, `description[]?`). Tabla
  `Dnd5eReferenceEntries(Kind, Index, Name, DescriptionJson, Source)`; el seeder carga los del SRD.
  Las propiedades de arma nuevas (p. ej. "misfire") se muestran en la ficha con su texto.

### 2.8 Prefijos y referencias

- Todo índice definido por un paquete lleva el prefijo `<id>-` (como hoy); las referencias
  (`spellList`, `grants`, `prerequisites`, `companion`, `startingEquipment`) pueden apuntar a índices
  del SRD o de **otro paquete** declarado en `"requires": ["phb-2014"]` (nuevo campo raíz): el
  importador rechaza referencias a paquetes no importados y el núcleo impide activar un paquete en
  una campaña sin sus `requires` activos (400 `missing-requirement`).
- El validador (`ContentPackValidator.*`) se extiende por sección; cada error con ruta
  (`classes[2].levels[5].features[0].resource.max: ...`).

## 3. Filtrado por campaña (módulo 5e)

Todas las consultas del catálogo del módulo reciben `CatalogScope`:

| Consumidor | Ámbito |
|---|---|
| Asistente de creación, elecciones de origen, subida de nivel, preparación de conjuros, elecciones inválidas | campaña del personaje |
| Hoja (`CharacterSheet`), ataques, recursos, compañero | campaña del personaje; lo que ya está en el personaje se resuelve aunque el paquete se haya desactivado (`invalid-choices` lo señala como "de un paquete desactivado", nuevo motivo `pack-disabled`) |
| Tienda, alijo, homebrew, búsqueda de objetos | campaña |
| Rutas del compendio `GET /api/v1/systems/dnd5e/catalog/*` | `?campaignId=` opcional → ámbito de esa campaña (miembro); sin él, ámbito global |
| `GET .../catalog/sources` | con `?campaignId=` marca `enabled` por paquete |

## 4. App

- **Admin → Contenido**: columna sistema, marca "base", versión de formato; importar/borrar igual.
- **Campaña → Ajustes** (Owner/DM): sección "Paquetes de contenido" con la lista de paquetes del
  sistema (base fijo y marcado; el resto con interruptor), guardado con `PUT`; aviso si un paquete
  requiere otro. Los jugadores ven la lista en solo lectura en la sección "Contenido".
- **Compendio**: selector de fuente (Todo / por paquete) y, dentro de una campaña, el ámbito de la
  campaña (botón "Solo lo activo en la campaña" cuando se abre desde ella); nuevas pestañas
  **Reglas** y las Bestias ya existentes con criaturas de paquetes; detalle de clase para clases
  nuevas (tabla de niveles, recursos, lanzamiento) usando los mismos widgets que las del SRD.
- **Asistente y subida de nivel**: clases, razas, dotes, conjuros y equipo de los paquetes activos
  (la lista llega ya filtrada del servidor; la app no filtra). `SourceChip` muestra el paquete.
- **Ficha**: motivo `pack-disabled` en elecciones inválidas con el texto "Este contenido pertenece a
  un paquete desactivado en la campaña".
- `GameSystemUi` gana `compendiumTabs()` → incluye Reglas; `Dnd5eUi` ya aporta el resto.

## 5. Entregas

Cada una con su PR, servidor y app en verde.

### 34A — Servidor: tablas, formato v3 e importador

1. `ContentPacks` + `CampaignContentPacks`, migraciones, repositorios, `ContentPackRegistry`,
   endpoints de §1, `CatalogScope` y `ICatalogScopeResolver`, `WhereListed` por ámbito, SRD
   registrado como paquete base (`SystemId = dnd5e`, `IsBase`).
2. Formato v3 completo (§2) en `ContentPackModels`, validador e importador; tablas
   `Dnd5eCreatures`, `Dnd5eRules`, `Dnd5eReferenceEntries`; `SrdBeastCatalog` sobre tabla; el
   seeder carga criaturas, condiciones y vocabularios del SRD en las tablas nuevas.
3. Filtrado por ámbito en todas las consultas de §3; `?campaignId=` en el catálogo; `pack-disabled`.
4. Tests: importación de un paquete v3 ficticio con una clase completa (20 niveles, lanzador
   `"table"`, recursos, subclase), una dote, una raza nueva, un arma de fuego, una criatura, una
   regla y un vocabulario; creación de personaje con la clase nueva y subida al nivel 5 por la API;
   activación por campaña (jugador ve solo lo activo; desactivar no rompe la hoja; `requires`);
   `PrivatePhbPackTests` con el PHB reexportado a v3 (script privado actualizado en
   `/mnt/project-files/dnd-manager/`); recuentos del SRD idénticos a los de hoy.
5. `docs/content-packs.md` reescrito para v3 con ejemplo ficticio completo.

### 34B — App

1. Modelos y repositorios: `ContentPack` con `systemId/isBase/formatVersion`, `campaignContentPacks`
   (GET/PUT), `catalogSources` con `enabled`, criaturas/reglas/vocabularios del compendio.
2. Pantallas de §4 y pestaña Reglas; detalle de clase para clases nuevas; `pack-disabled`.
3. Tests de widget de cada pantalla con las fakes; test del flujo "activar paquete → el asistente
   ofrece la clase nueva".

### 34C — SRD como paquete v3

1. Script `server/tools/SrdPack` (proyecto de consola .NET o script Python en `server/tools/`,
   CC-BY) que convierte `seed/srd/*.json` + `level-choices.json` + `option-sets.json` a
   `seed/srd-5.1.pack.json` (formato v3, `id: "srd"`, `isBase` implícito).
2. `SrdSeeder` se sustituye por `ImportPackAsync` del fichero embebido con `IsBase = true`
   (idempotente por versión, como hoy). Test de equivalencia: los recuentos por tabla y una muestra
   de definiciones (una clase con sus 20 niveles, una subclase, una raza con subraza, un conjuro, un
   objeto mágico, un trasfondo) coinciden con los del seeder anterior (comparación guardada en
   `Fixtures/srd-before-34c.json` generada antes del cambio).
3. Se borran `SrdSeeder`, `SrdDataset` y los mapeos específicos que queden sin uso; el formato v3
   queda como el **único** camino de carga del catálogo 5e.

## 6. Lo que no cambia

- El modelo de permisos (Admin importa; Owner/DM activan; los jugadores consultan).
- Los índices del SRD (`fireball`, `wizard`…) y el prefijo de los paquetes.
- Homebrew de campaña (`CampaignId` en `ItemTemplates`) sigue siendo por campaña y siempre activo.

## 7. Riesgos

| Riesgo | Mitigación |
|---|---|
| Una clase completa toca el asistente, la subida de nivel, el multiclase y la hoja | Todo sale de `ClassDefinition`/`ClassLevel`; el test de 34A crea un personaje de la clase nueva y lo sube por la API. Si algún cálculo lee una tabla fija por índice de clase (p. ej. `classSpecific` de SRD), se generaliza. |
| Rendimiento del filtrado por ámbito en consultas calientes (hoja) | `Sources` es un conjunto pequeño (≤ 10); `WHERE Source IN (...)` con índice en `Source`. |
| Conversión del SRD (34C) con pérdidas | Fixture de comparación antes/después; 34C se hace al final y puede desplazarse detrás de la fase 35 si Valda's lo necesita antes. |
| El PHB privado deja de importar | El script privado se actualiza a v3 y `PrivatePhbPackTests` lo cubre; no hay instalaciones que conservar. |
