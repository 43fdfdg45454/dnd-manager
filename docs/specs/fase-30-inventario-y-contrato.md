# Fase 30 — Inventario y contrato

Fase de **documentación**: no cambia código. Parte del [ADR 0009](../ADR/0009-framework-y-sistemas-de-juego.md)
y de la [hoja de ruta](../framework-roadmap.md). Clasifica cada fichero del servidor y de la app como
**Núcleo**, **5e** o **Mixto** y fija sobre papel los dos contratos (`IGameSystem` en el servidor y
`GameSystemUi` en la app) con lo que el módulo 5e necesita hoy. Las fases 31–33 aplican estas tablas
de forma mecánica: mover, no reescribir.

Medido sobre `master` en `b740b99` (2026-10-10), contando ficheros `.cs`/`.dart` sin `obj/` ni `bin/`.

Leyenda:

- **Núcleo**: no depende de ninguna regla de 5e; se queda en `OpenTrpg.Core.*` (servidor) o
  `packages/core` (app).
- **5e**: reglas, catálogo o pantallas de D&D 5e; se mueve entero a `OpenTrpg.Systems.Dnd5e.*` /
  `packages/dnd5e`.
- **Mixto**: el fichero tiene una parte genérica y otra de 5e; la columna "Reparto" dice qué miembros
  se quedan en el núcleo, cuáles van al módulo y qué interfaz del contrato los une.

## 0. Totales medidos

| Capa | Ficheros | Núcleo | 5e | Mixto |
|---|---|---|---|---|
| `Dnd.Domain` | 126 | 43 | 68 | 15 |
| `Dnd.Application` | 165 | 101 | 40 | 24 |
| `Dnd.Infrastructure` (sin migraciones) | 97 | 50 | 34 | 13 |
| `Dnd.Infrastructure/Persistence/Migrations` | 65 (32 migraciones, 32 `Designer`, 1 snapshot) | 9 | 12 | 11 (por migración, §1.3.2) |
| `Dnd.Api` | 45 (+3 páginas en `wwwroot`) | 36 | 3 | 6 |
| App `lib/core` | 77 (10 022 líneas) | 73 | 2 | 2 |
| App `lib/features` | 208 (50 783 líneas) | 93 | 79 | 36 |

Diferencias con la tabla del ADR 0009:

- `Dnd.Domain/Catalog` tiene **34** ficheros, no 33: la fase 29 añadió `HeightWeightTable.cs`.
- `Dnd.Domain/Rules` **no es genérico**: su único fichero, `AbilityRules`, es aritmética de 5e
  (modificador por puntuación y bonificador de competencia por nivel). Los dados genéricos
  (`IDiceRoller`, `RandomDiceRoller`) están en `Characters/Dice.cs`.
- `Dnd.Domain/Items` no es "5e con plantilla": inventario, tienda, alijo y transacciones son núcleo;
  la plantilla con daño, CA, rareza y sintonía es `Catalog/ItemTemplate` (Mixto).
- App: `core` 10 022 líneas, `characters` 23 600, `catalog` 4 084, `items` 5 635, `dice` 1 187, como
  decía el ADR. `core` no es del todo genérico: `ui/action_type.dart` (acción, acción adicional,
  reacción) y `ui/spell_category.dart` son 5e.

## 1. Inventario del servidor

### 1.1 `Dnd.Domain` (126 ficheros)

| Carpeta | Ficheros | N / 5e / M | Resumen |
|---|---|---|---|
| `Campaigns` | 5 | 4 / 0 / 1 | Núcleo; `Campaign` guarda el dinero del alijo en pc. |
| `Catalog` | 34 | 0 / 29 / 5 | Catálogo 5e; registro de paquetes y plantilla de objeto mixtos. |
| `Characters` | 47 | 6 / 37 / 4 | Reglas de la hoja; identidad, peticiones y dados al núcleo. |
| `Common` | 3 | 3 / 0 / 0 | |
| `Files` | 2 | 2 / 0 / 0 | |
| `Items` | 10 | 4 / 1 / 5 | Inventario, tienda y alijo genéricos; campos de objeto 5e. |
| `Library` | 3 | 3 / 0 / 0 | |
| `Lore` | 4 | 4 / 0 / 0 | |
| `Maps` | 3 | 3 / 0 / 0 | |
| `Messages` | 1 | 1 / 0 / 0 | |
| `Releases` | 1 | 1 / 0 / 0 | |
| `Rules` | 1 | 0 / 1 / 0 | `AbilityRules`. |
| `Sessions` | 6 | 6 / 0 / 0 | |
| `Users` | 6 | 6 / 0 / 0 | |

#### `Campaigns` (5)

| Fichero | Qué hace | Clas. | Reparto |
|---|---|---|---|
| `Campaign` | Agregado: miembros, roles, invitaciones, traspaso, zona horaria, recordatorios, ajustes del alijo | Mixto | Todo núcleo. `StashCopperPieces` es dinero en la unidad mínima de 5e: se queda como entero sin unidad y la moneda la define el sistema (`ICurrencySystem`, §4). Fase 31 añade `SystemId`. |
| `CampaignInvitation` | Invitación pendiente de un usuario | Núcleo | |
| `CampaignMember` | Usuario y rol en la campaña | Núcleo | |
| `CampaignRole` | `Owner > DM > Player` y comparación por rango | Núcleo | |
| `OwnershipTransfer` | Traspaso de propiedad pendiente | Núcleo | |

#### `Catalog` (34)

| Fichero | Qué hace | Clas. | Reparto |
|---|---|---|---|
| `AbilityBonus` | Bonificador racial a una característica | 5e | |
| `BackgroundDefinition` | Trasfondo: competencias, idiomas, equipo, rasgo | 5e | |
| `BackgroundPersonality` | Rasgos, ideales, vínculos, defectos y tabla del trasfondo | 5e | |
| `CatalogImport` | Registro de importación (`srd-5.1` o `pack:<id>`), idempotencia del seed | Mixto | Al núcleo como registro de paquetes (id, nombre, versión, recuentos). `SrdRuleset` al módulo. Fase 34 le añade `SystemId` (§6). |
| `CatalogSources` | Valores de la columna `Source` (`srd`, `homebrew`, id de paquete) | Mixto | `Homebrew`, `PackRulesetPrefix`, `PackRuleset`, `IsReserved`, `MaxLength` al núcleo; `Srd` pasa a ser el id del paquete base del módulo 5e. |
| `ClassDefinition`, `ClassLevel` | Clase y su tabla por nivel | 5e | |
| `CompanionRule` | Compañero animal de un rasgo de subclase | 5e | |
| `ConditionDefinition` | Condición del SRD | 5e | Cada sistema trae su lista de condiciones. |
| `ExpandedSpell` | Conjuro que una subclase añade a la lista de su clase | 5e | |
| `FeatureDefinition` | Rasgo de clase/subclase con recurso y modificadores | 5e | |
| `HeightWeightTable` | Tabla de altura y peso de raza (PHB cap. 4) | 5e | La tirada es 5e; el resultado se guarda en el núcleo (§3). |
| `ItemCategory` | Arma, armadura, escudo, equipo, herramienta, montura, consumible, mágico, otro | Mixto | Se queda en el núcleo tal cual en la fase 32 (la tienda y el inventario filtran por categoría); la etiqueta y el significado de cada una los da el sistema (`IItemSystem`). |
| `ItemRarity` | Rareza | 5e | |
| `ItemTemplate` | Plantilla de objeto (SRD, paquete u homebrew de campaña) | Mixto | Núcleo: `Id`, `CampaignId`, `Index`, `Source`, `Name`, `Category`, `Subcategory`, `CostCp`, `WeightLb`, `Description`. 5e: `Rarity`, `RequiresAttunement`, `DamageDice`, `DamageType`, `VersatileDice`, `Properties`, `RangeNormal`, `RangeLong`, `ArmorClassBase`, `AddDexModifier`, `MaxDexBonus`, `StrengthMinimum`, `StealthDisadvantage`, `Effects`, `Modifiers`. Ver §7, pregunta 3. |
| `ItemTemplateData` | Datos para crear o editar una plantilla | Mixto | Mismo reparto que `ItemTemplate`. |
| `LevelChoiceJson` | Esquema JSON de elecciones por nivel, `OptionPrerequisites`, `ProficiencyKeys`, `ChoiceModifier` | 5e | |
| `LevelChoiceOrder`, `LevelChoiceRule` | Orden y reglas de elección por nivel, conjuntos de opciones | 5e | |
| `RaceChoices`, `RaceDefinition`, `RaceExtensionDefinition`, `SubraceDefinition`, `TraitDefinition` | Razas, subrazas, extensiones de paquete, rasgos | 5e | |
| `ResourceFormula` | Fórmula del máximo de un recurso | 5e | |
| `RollTable` | Tabla de tirada de un paquete (oleada de magia salvaje…) | 5e | Concepto genérico, pero hoy solo la alimentan paquetes 5e; se mueve con el catálogo. Candidata a núcleo cuando haya un segundo sistema. |
| `SkillDefinition` | Habilidad y su característica | 5e | |
| `SpellCategory`, `SpellDefinition` | Conjuros y su categoría | 5e | |
| `StartingEquipment` | Equipo inicial de clase/trasfondo, `EquipmentCategory` | 5e | |
| `SubclassDefinition`, `SubclassLevel`, `SubclassSpellcasting` | Subclases | 5e | |
| `TrinketEntry` | Baratija d100 (solo por paquete) | 5e | |

#### `Characters` (47)

| Fichero | Qué hace | Clas. | Reparto |
|---|---|---|---|
| `Abilities`, `AbilityScores` | Claves y valores de las seis características | 5e | |
| `ChangeRequest` | Petición de cambio de un jugador que el DM aprueba (payload JSON opaco, `BeforeJson`) | Núcleo | El flujo es del núcleo; el payload lo interpreta quien registra el tipo (§3.3). |
| `ChangeRequestStatus` | Pendiente, aprobada, rechazada, cancelada | Núcleo | |
| `ChangeRequestType` | `Activate`, `EditSheet`, `AddItem`, `RemoveItem`, `CustomItem`, `AdjustMoney`, `Other`, `Companion` | Mixto | Núcleo: `Activate`, `AddItem`, `RemoveItem`, `CustomItem`, `AdjustMoney`, `Other`. Sistema: `EditSheet`, `Companion`. Pasa de enum a cadena registrada; los valores en base de datos no cambian (ya se guardan como texto). |
| `Character` (1 477 líneas) | Agregado: identidad, hoja guardada, estado de combate, inventario | Mixto | Ver §3: identidad, dueño, estado, retrato, textos, altura y peso, dinero e inventario al núcleo; el resto a `Dnd5eCharacter`. |
| `Character.ClassActions` | Furia, imposición de manos, castigo divino, recuperación arcana y natural | 5e | |
| `Character.LevelUp` | Concesión y aplicación de nivel, elecciones activas | 5e | El núcleo no conoce "nivel" (§3.3). |
| `Character.OriginChoices` | Elecciones de origen y su efecto | 5e | |
| `Character.SpellPreparation` | Preparación de conjuros pendiente | 5e | |
| `CharacterChoice`, `CharacterClassLevel`, `CharacterCompanion`, `CharacterProficiency`, `CharacterResource`, `CharacterSpell` | Colecciones hijas de la hoja | 5e | |
| `CharacterCondition` | Condición activa (índice y nota) | 5e | |
| `CharacterOverride`, `OverrideFields` | Valor calculado sustituido a mano y lista de campos | 5e | La idea (sobrescribir y marcar) la muestra el núcleo de UI (`OverrideMark`); los campos los define el sistema. |
| `CharacterSheet` | Hoja calculada | 5e | |
| `CharacterStatus` | `Draft` / `Active` | Núcleo | |
| `ChoiceEffects`, `ChoiceValidity` | Efectos de una elección y elecciones que dejaron de ser válidas | 5e | |
| `ClassResourceRules`, `ResourceRecharge`, `RollOnRest` | Recursos automáticos por clase, recarga, tiradas tras descanso | 5e | |
| `CombatCalculator`, `CombatUpdate`, `DamageResult` | Ataques con desglose, seguimiento de combate, daño y concentración | 5e | |
| `CompanionCalculator` | Estadísticas del compañero | 5e | |
| `Dice` | `IDiceRoller`, `RandomDiceRoller` | Núcleo | Se mueve a `Core.Domain/Rules`. |
| `HpMode`, `MulticlassRules`, `ProficiencySource`, `ProficiencyType` | PG fijos/tirados, multiclase, competencias | 5e | |
| `RestKind` | Corto / largo | 5e | |
| `RestRequest` | Descanso pedido por el jugador y aprobado por el DM | Mixto | Núcleo: el flujo (`CampaignId`, `CharacterId`, `RequestedByUserId`, estado, resolución, una pendiente por personaje). 5e: `Kind` (pasa a cadena del sistema) y los dados de golpe (pasan a `PayloadJson`). |
| `RestRequestStatus` | Estados de la petición | Núcleo | |
| `SheetCalculator`, `SheetInput` | Cálculo puro de la hoja SRD 5.1 y su entrada | 5e | |
| `SheetEdit` | Edición de hoja (clases, competencias, conjuros, overrides) | 5e | |
| `SheetEditMode` | Directa, por petición o prohibida | Núcleo | |
| `ShortRestResult`, `SpellPreparationReason`, `SpellSlotState`, `SpellSlotTables` | Descanso corto, preparación, espacios de conjuro | 5e | |
| `ValueBreakdown` | `BreakdownPart`, `ValueBreakdown`, `BreakdownSources`, etiquetas | Mixto | `BreakdownPart` y `ValueBreakdown` al núcleo (contrato de "origen de cada punto"); `BreakdownSources` y las etiquetas en español al módulo. |

#### Resto del dominio

| Carpeta / fichero | Qué hace | Clas. | Reparto |
|---|---|---|---|
| `Common/ContentVisibility`, `DomainException`, `EntityBase` | Visibilidad, errores, base de entidad | Núcleo | |
| `Files/FileKind`, `StoredFile` | Metadatos de ficheros | Núcleo | |
| `Items/CharacterItem` | Entrada de inventario | Mixto | Núcleo: `CharacterId`, `CampaignId`, `TemplateId`, `Quantity`, `Equipped`, `Charges`, `ChargesMax`, `SortOrder`, `Notes`, fechas. 5e: `Attuned` y las columnas 5e de `Overrides`. |
| `Items/EffectiveItem` | Plantilla + overrides resuelta | Mixto | Núcleo: nombre, categoría, peso, coste, descripción. 5e: daño, CA, propiedades, rareza, sintonía, modificadores. |
| `Items/ItemLimits` | Longitudes y máximos | Mixto | Longitudes al núcleo; `DiceMaxLength`, `DamageTypeMaxLength`, `MaxRange`, `MaxArmorClass`, `MaxDexBonus` al módulo. |
| `Items/ItemModifier` | Modificadores de objeto (característica, salvación, CA, ataque, daño, velocidad, PG, iniciativa…) | 5e | |
| `Items/ItemOverrides` | Campos que sustituyen a los de la plantilla | Mixto | `Name`, `Description`, `Category`, `WeightLb` al núcleo; el resto 5e. |
| `Items/ItemUpdate` | Cambios de juego de una entrada | Mixto | `Attuned` 5e; lo demás núcleo. |
| `Items/PartyStashItem`, `Shop`, `ShopItem`, `Transaction` | Alijo, tiendas, stock, registro de compras | Núcleo | Usan `ItemOverrides` como `CharacterItem`. |
| `Library/*` (3), `Lore/*` (4), `Maps/*` (3), `Messages/DirectMessage`, `Releases/AppRelease` | Biblioteca, lore, mapas, mensajes, versiones del APK | Núcleo | |
| `Rules/AbilityRules` | Modificador y bonificador de competencia | 5e | |
| `Sessions/*` (6), `Users/*` (6) | Sesiones, asistencia, recordatorios; usuarios y tokens | Núcleo | |

### 1.2 `Dnd.Application` (165 ficheros)

| Carpeta | Ficheros | N / 5e / M |
|---|---|---|
| `DependencyInjection.cs` | 1 | 0 / 0 / 1 |
| `Abstractions` (16 + 22 en `Persistence`) | 38 | 29 / 4 / 5 |
| `Auth` | 10 | 10 / 0 / 0 |
| `Campaigns` | 14 | 14 / 0 / 0 |
| `Catalog` | 19 | 0 / 19 / 0 |
| `ChangeRequests` | 2 | 1 / 0 / 1 |
| `Characters` | 27 | 3 / 15 / 9 |
| `Common` | 4 | 4 / 0 / 0 |
| `ContentPacks` | 2 | 0 / 0 / 2 |
| `Files` | 8 | 8 / 0 / 0 |
| `Items` | 9 | 3 / 1 / 5 |
| `Library`, `Lore`, `Maps` | 9 | 9 / 0 / 0 |
| `Messages` | 1 | 1 / 0 / 0 |
| `Party` | 2 | 0 / 1 / 1 |
| `Releases` | 3 | 3 / 0 / 0 |
| `Sessions` | 7 | 7 / 0 / 0 |
| `Setup` | 2 | 2 / 0 / 0 |
| `Users` | 7 | 7 / 0 / 0 |

#### `Abstractions` (38)

| Fichero | Qué hace | Clas. | Reparto |
|---|---|---|---|
| `IAccountEmailService`, `IEmailSender`, `ISessionEmailService`, `ISessionLinkTokens`, `IPublicUrlProvider` | Correo y enlaces públicos | Núcleo | |
| `ICampaignAccess`, `ICampaignDefaults` | Rol del usuario en una campaña; valores por defecto | Núcleo | |
| `ICampaignNotifier` | `CampaignEvent`, `CampaignEventTypes`, extensiones por evento | Mixto | Núcleo: `message.received`, `character.updated`, `shop.updated`, `changeRequest.updated`, `changeRequest.resolved`, `session.updated`, `party.stash.updated`, `membership.removed`, `invitation.received`, `members.updated`, `restRequest.updated`. Declarados por 5e: `party.rest`, `levelUp.granted`, `levelUp.completed` (§4.4). |
| `IContentPackImporter` | Importar, listar, borrar paquetes | Mixto | Firma del núcleo (con sistema); implementación del módulo (§6). |
| `IDateTimeProvider`, `IFileStorage`, `IPasswordHasher`, `ITokenService`, `IRealtimeConnections` | Infraestructura genérica | Núcleo | |
| `IEquippedGearProvider` | Armadura y escudo equipados para la CA | 5e | |
| `ISrdSeeder` | Importa el SRD | 5e | Pasa a ser "cargar el paquete base" del módulo (§6). |
| `Persistence/ICatalogRepository` | 39 consultas al catálogo | 5e | |
| `Persistence/ICharacterCompanionRepository` | Compañeros | 5e | |
| `Persistence/ICharacterRepository` | Personajes por campaña, con detalles, propiedad | Mixto | Listar, obtener y `CharacterOwnership` núcleo; `GetWithDetailsAsync` (carga las colecciones 5e) recibe los `Include` del módulo. |
| `Persistence/IRestRequestRepository`, `IItemTemplateRepository` | Peticiones de descanso; plantillas | Mixto | Como sus entidades. |
| `Persistence/` (17 más): `IChangeRequestRepository`, `ICampaignRepository`, `IUserRepository`, `IRefreshTokenRepository`, `IPasswordTokenRepository`, `IFileRepository`, `ILoreRepository`, `IMapRepository`, `ILibraryRepository`, `ISessionRepository`, `IReleaseRepository`, `IInstanceStatsRepository`, `IDirectMessageRepository`, `IShopRepository`, `ITransactionRepository`, `IPartyStashRepository`, `IUnitOfWork` | Repositorios genéricos | Núcleo | |

#### `Catalog` (19) — todo 5e

| Fichero | Qué hace |
|---|---|
| `Beasts` | Búsqueda y detalle de bestias (`IBeastCatalog`) |
| `CatalogDtos` | DTO de clases, razas, rasgos, conjuros, objetos, atribución |
| `CatalogErrors`, `CatalogFilters`, `CatalogJson` | Errores, filtros, columnas JSON |
| `GetAttribution` | Atribución CC-BY del SRD (el núcleo la agrega por sistema, §4) |
| `GetClass`, `GetFeature`, `GetItem`, `GetRace`, `GetSpell` | Detalle |
| `ListClasses`, `ListRaces`, `ListReferenceTables` (condiciones, habilidades, trasfondos), `ListRollTables`, `ListTrinkets` | Listas |
| `SearchItems`, `SearchSpells` | Búsqueda paginada |
| `StartingEquipmentDtos` | Equipo inicial resuelto, `GetEquipmentCategoryHandler` |

#### `Characters` (27)

| Fichero | Qué hace | Clas. | Reparto |
|---|---|---|---|
| `CharacterDtos` | `CharacterSummaryDto`, `CharacterDetailDto` y otros 42 DTO de la hoja | Mixto | §3.2. |
| `CharacterLifecycle` | `SubmitCharacterHandler`, `ActivateCharacterHandler`, `DeleteCharacterHandler` | Mixto | Envío, borrado y cambio de estado al núcleo; antes de activar, el núcleo llama a `ICreationSystem.PrepareActivationAsync` (orígenes completos, PG máximos, preparación inicial de conjuros). |
| `CharacterLoader` | `CharacterErrors`, `LoadedCharacter`, carga con permisos | Núcleo | |
| `CharacterOwner` | Cambiar el dueño o convertir en PNJ | Núcleo | |
| `CharacterSheetService` | `ICharacterSheetService`: cálculo, recálculo, detalle, grupo, resúmenes | 5e | Implementa `ISheetSystem` (§4). |
| `ChoiceGrants`, `InvalidChoices`, `OptionCosts` | Lo que concede una elección, elecciones inválidas, coste de opciones | 5e | |
| `ClassActions` | Acciones de clase | 5e | |
| `CombatSummaryBuilder` | Resumen de combate (ataques, paneles de clase) | 5e | |
| `CombatTracking` | `CharacterTracker`, `UpdateCombatHandler`, `ApplyDamageHandler`, `SetConcentrationHandler`, `SpellSlotHandler`, `ResourceHandler`, `RestHandler` | 5e | `CharacterTracker` (cargar, comprobar seguimiento, guardar, notificar) se generaliza al núcleo como `CharacterWriter`. |
| `Companions` | Compañero animal | 5e | |
| `CreateCharacter` | Crear borrador o PNJ rápido | Mixto | Nombre, dueño y campaña al núcleo; raza, clase, características y equipo inicial a `ICreationSystem.InitializeAsync`. |
| `GetCharacter`, `ListCharacters` | Detalle y lista | Mixto | Permisos y carga en el núcleo; cuerpo armado por el sistema. |
| `LevelUpDtos`, `LevelUpHandlers`, `LevelUpPlanner` (1 008 líneas) | Plan y aplicación de subida de nivel | 5e | |
| `OriginChoices`, `OriginOptionDescriptions` | Elecciones de origen | 5e | |
| `RestRequests` | Crear, cancelar, listar, ver, aprobar, rechazar | Mixto | Ciclo de la petición al núcleo; validar el payload y aplicarlo al aprobar, `IRestSystem`. |
| `SetPortrait` | Retrato | Núcleo | |
| `SheetCatalog` | Catálogo cargado para calcular | 5e | |
| `SheetPatch`, `SheetPatchSnapshot` | Parche de hoja y su "antes" | Mixto | `Name`, `Notes`, `Backstory`, `PersonalityTraits`, `Ideals`, `Bonds`, `Flaws`, altura y peso pasan a un `CharacterProfilePatch` del núcleo; razas, clases, características, competencias, conjuros, overrides, alineamiento, `HpMode`, `BackgroundDetail` al módulo. |
| `UpdateSheet` | Aplica directo o crea `ChangeRequest` | Mixto | La decisión (`SheetEditMode`) y la creación de la petición son núcleo; aplicar el parche, sistema. |
| `SpellPreparation` | Preparar conjuros | 5e | |

#### Resto de `Dnd.Application`

| Carpeta / fichero | Qué hace | Clas. | Reparto |
|---|---|---|---|
| `DependencyInjection` | Registra handlers y servicios | Mixto | Se parte en `AddCoreApplication()` y `AddDnd5eApplication()`. |
| `Auth/*` (10), `Campaigns/*` (14), `Common/*` (4), `Files/*` (8), `Library/*`, `Lore/*`, `Maps/*` (9), `Messages/MessageHandlers`, `Releases/*` (3), `Sessions/*` (7), `Setup/*` (2), `Users/*` (7) | Todo lo genérico | Núcleo | Fase 31: `CreateCampaignRequest.SystemId`, `CampaignDto.SystemId`, `CampaignSummaryDto.SystemId`. |
| `ChangeRequests/ChangeRequestDto` | DTO de la petición | Núcleo | |
| `ChangeRequests/ChangeRequestHandlers` | Listar, ver, aprobar, rechazar, cancelar | Mixto | Despacho por tipo: `Activate` y los de inventario los aplica el núcleo; `EditSheet` y `Companion` se delegan en `IGameSystem.ChangeRequests` (§3.3). |
| `ContentPacks/ContentPackDtos`, `ContentPackHandlers` | Paquetes y fuentes del catálogo | Mixto | Handlers de administración en el núcleo, que delegan en el sistema del paquete; `ListCatalogSourcesHandler` al módulo. |
| `Items/CampaignItems` | Homebrew de campaña | Mixto | CRUD núcleo; validación y campos de plantilla, `IItemSystem`. |
| `Items/Inventory` | Añadir, editar, usar, quitar, dinero; peticiones de inventario | Mixto | Operaciones núcleo; tras equipar, sintonizar o quitar llama a `ISheetSystem.RecalculateAsync`; el límite de sintonía es 5e. |
| `Items/InventoryEquippedGearProvider` | Armadura y escudo para la CA | 5e | |
| `Items/InventoryView` | Inventario con objetos efectivos | Mixto | Lectura núcleo; extras del objeto, sistema. |
| `Items/ItemDtos` | Objetos, inventario, tiendas, transacciones | Mixto | `DamageDto`, `RangeDto`, `ArmorDto`, `ItemModifierDto` y esos campos de `EffectiveItemDto`/`ItemOverridesDto` a 5e. |
| `Items/ItemValidation` | Reglas de plantilla y overrides | Mixto | Longitudes núcleo; dados, CA, alcance, modificadores 5e. |
| `Items/PartyStash`, `Shops`, `Trading` | Alijo, tiendas, compra y venta | Núcleo | Gancho de recálculo de hoja tras recibir objetos. |
| `Party/PartyDtos` | `PartyMemberDto` (CA, iniciativa, espacios…) | 5e | |
| `Party/PartyHandlers` | `PartyLoader`, `GetPartyHandler`, `PartyRestHandler`, `PartyAdjustHandler`, `PartyLevelHandler` | Mixto | `PartyLoader` (permiso de DM y filtro por ids) al núcleo; el resto al módulo. |

### 1.3 `Dnd.Infrastructure` (97 ficheros sin migraciones)

| Área | Ficheros | N / 5e / M |
|---|---|---|
| `Persistence` (contexto, configuraciones, repositorios) | 57 | 31 / 17 / 9 |
| `Catalog` (seed del SRD y paquetes) | 19 | 0 / 17 / 2 |
| `Email` | 8 | 8 / 0 / 0 |
| `Files` | 3 | 2 / 0 / 1 |
| `Auth`, `Campaigns`, `Options`, `Sessions`, `Time` | 9 | 9 / 0 / 0 |
| `DependencyInjection.cs` | 1 | 0 / 0 / 1 |

#### 1.3.1 Persistencia (57)

| Fichero | Qué hace | Clas. | Reparto |
|---|---|---|---|
| `AppDbContext` | 56 `DbSet` | Mixto | Los del núcleo se quedan; los de catálogo y hoja los aporta el módulo con `IGameSystemModule.ConfigureModel` (§4.5). |
| `JsonListConversion` | Listas como JSON | Núcleo | |
| `Configurations/` raíz (7): `Campaign`, `CampaignInvitation`, `CampaignMember`, `OwnershipTransfer`, `PasswordToken`, `RefreshToken`, `User` | Tablas del núcleo | Núcleo | |
| `Configurations/Catalog/` (14): `CatalogColumns`, `ClassDefinition`, `ClassLevel`, `FeatureDefinition`, `LevelChoiceConfigurations`, `RaceDefinition`, `RaceExtensionDefinition`, `RollTable`, `SpellDefinition`, `SubclassDefinition`, `SubclassLevel`, `SubraceDefinition`, `TraitDefinition`, `TrinketEntry` | Tablas `Catalog*` | 5e | |
| `Configurations/Catalog/SimpleDefinitionConfigurations` | Condiciones, habilidades, trasfondos, categorías de equipo y `CatalogImports` | Mixto | `CatalogImportConfiguration` al núcleo; las otras cuatro al módulo. |
| `Configurations/Catalog/ItemTemplateConfiguration` | `ItemTemplates` | Mixto | Como `ItemTemplate`. |
| `Configurations/Characters/CharacterConfiguration` | `Characters` y 10 tablas hijas: `CharacterClassLevels`, `CharacterChoices`, `CharacterProficiencies`, `CharacterSpells`, `CharacterSpellSlots`, `CharacterResources`, `CharacterOverrides`, `ChangeRequests`, `RestRequests`, `CharacterCompanions` | Mixto | Columnas del núcleo de `Characters`, `ChangeRequests` y `RestRequests` al núcleo; el resto de columnas y tablas hijas al módulo (división de tabla, §3.4). |
| `Configurations/Content/` (5): `AppRelease`, `File`, `Library`, `Lore`, `Map` | Contenido | Núcleo | |
| `Configurations/Items/ItemConfigurations`, `ItemOverridesMapping` | Inventario, tiendas, transacciones, alijo; columnas de overrides | Mixto | Tablas núcleo; las columnas 5e de `ItemOverridesMapping` las mapea el módulo. |
| `Configurations/Messages/DirectMessageConfiguration`, `Configurations/Sessions/SessionConfigurations` | Mensajes, sesiones | Núcleo | |
| `Repositories/CatalogRepository`, `CatalogItemQueries`, `CharacterCompanionRepository` | Catálogo, compañeros | 5e | |
| `Repositories/CharacterRepository`, `ItemTemplateRepository`, `RestRequestRepository` | | Mixto | `CharacterRepository.GetWithDetailsAsync` recibe los `Include` del módulo. |
| `Repositories/` (16 más): campañas, usuarios, tokens (2), ficheros, lore, mapas, biblioteca, sesiones, releases, estadísticas, mensajes, tiendas, transacciones, alijo, peticiones de cambio | Repositorios genéricos | Núcleo | |

#### 1.3.2 Migraciones (32)

Una sola historia (`__EFMigrationsHistory`) sobre un solo `AppDbContext`:

| Migraciones | Clas. |
|---|---|
| `InitialAuth`, `AddCampaigns`, `AddFilesLoreMapsLibrary`, `AddSessionsAndReminders`, `AddAppReleases`, `AddInstanceSettings`, `DropInstanceSettings`, `AddPartyStashAndDirectMessages`, `AddInvitationsAndChangeRequestSnapshots` | Núcleo (9) |
| `AddCatalog`, `AddClassSkillChoices`, `AddLevelChoices`, `AddStartingEquipment`, `AddSpellHealing`, `AddTrinketTable`, `AddRaceGrantsAndExtensions`, `AddSubclassFeatureResources`, `AddSubclassSpellcasting`, `AddSubclassExpandedSpellList`, `AddOptionCostAndChoiceOrder`, `AddSubclassFeatureModifiers` | 5e (12, catálogo) |
| `AddCharacters`, `AddItemsAndShops`, `AddCatalogSource`, `AddItemModifiers`, `AddRestRequestsAndPendingLevelUp`, `AddSpellPreparation`, `AddOriginChoicesAndRestRolls`, `MakeDmCharactersNpcs`, `AddCharacterPersonality`, `AddCharacterCompanions`, `AddCharacterHeightAndWeight` | Mixto (11: tocan tablas del núcleo y de la hoja a la vez) |

Las migraciones aplicadas **no se reescriben ni se reparten**: son historia de instancias reales y 11
de ellas mezclan núcleo y 5e. Propuesta (§4.5, §7 pregunta 1): un solo contexto con modelo compuesto
(núcleo + módulos registrados) y una sola historia; las migraciones existentes se quedan en el
ensamblado de infraestructura del núcleo y las nuevas se generan ahí mismo.

#### 1.3.3 Seed del SRD (8 de `Catalog` + datos)

| Fichero | Qué hace | Clas. |
|---|---|---|
| `SrdDataset` (1 544 líneas) | Lee `server/seed/srd/*.json` (recurso incrustado) y crea las definiciones; añade lo que el dataset no trae (lanzador por clase, riqueza inicial) | 5e |
| `SrdSeeder` | Importa si no hay `CatalogImport` de esa versión | 5e |
| `SrdBeastCatalog` | Bestias del SRD en memoria (sin tabla) | 5e |
| `SrdLevelChoices` | `level-choices.json`, `option-sets.json` | 5e |
| `SrdSpellCategories`, `SrdItemModifiers`, `CatalogItems` | Categorías de conjuro, modificadores de objetos mágicos, objetos por índice | 5e |
| `CatalogOptions` | `SeedOnStartup` | Mixto: pasa a "cargar los paquetes base de los sistemas registrados". |
| `server/seed/srd/` (20 JSON del dataset, `level-choices.json`, `option-sets.json`, `LICENSE-5e-database.md`, `README-level-choices.md`) | Datos del SRD | 5e: paquete base del módulo (§6). |

#### 1.3.4 Paquetes de contenido (11 de `Catalog`)

| Fichero | Qué hace | Clas. | Reparto |
|---|---|---|---|
| `ContentPackImporter` | Valida y reemplaza las definiciones de un paquete en una transacción; lista y borra | Mixto | Transacción, registro (`CatalogImport`) y borrado por `Source` al núcleo como `ContentPackRegistry`; escribir las filas, al módulo (`ICatalogSystem.ImportPackAsync`). |
| `ContentPackModels` (759 líneas) | Modelo JSON del formato 5e | 5e | |
| `ContentPackValidator` y sus 8 parciales (`Companion`, `Costs`, `ExpandedSpells`, `LevelChoices`, `OriginChoices`, `Personality`, `StartingEquipment`, `Trinkets`) | Validación del formato 5e | 5e | La cabecera (`formatVersion`, `id`, `name`, `version`, desde la fase 34 `system`) la valida el núcleo. |

#### 1.3.5 SMTP, ficheros, autenticación, SignalR

| Fichero | Qué hace | Clas. |
|---|---|---|
| `Email/*` (8): `AccountEmailService`, `PublicLinkBuilder`, `SessionEmailService`, `SmtpEmailSender`, `SmtpOptions`, `SmtpSecurityMapper`, `Templates/AccountEmailTemplates`, `Templates/SessionEmailTemplates` | SMTP del operador | Núcleo |
| `Files/DiskFileStorage`, `FileStorageOptions` | Almacenamiento en disco | Núcleo |
| `Files/SystemDocumentSeeder` | Registra `system/SRD_CC_v5.1.pdf` si existe | Mixto: el mecanismo es núcleo; el documento lo declara el módulo (`GameSystemInfo.SystemDocuments`). |
| `Auth/*` (4), `Campaigns/CampaignAccess`, `Options/AppOptions`, `Options/CampaignDefaults`, `Sessions/SessionLinkTokens`, `Time/SystemDateTimeProvider` | JWT, acceso, opciones, enlaces, reloj | Núcleo |
| `DependencyInjection` | Registro | Mixto |
| SignalR | No vive aquí: hub y notificador están en `Dnd.Api/Realtime` (§1.4) | Núcleo |

### 1.4 `Dnd.Api` (45 ficheros + `wwwroot`)

| Fichero | Qué hace | Clas. |
|---|---|---|
| `Program.cs` | Composición, middleware, endpoints y hub | Mixto: registra el núcleo y cada módulo (`AddGameSystem<Dnd5eModule>()`). |
| `Auth/*` (3) | Políticas, JWT, `ClaimsPrincipal` | Núcleo |
| `Errors/AppExceptionHandler`, `Filters/ValidationFilter` | Problemas HTTP, validación | Núcleo |
| `Hosting/*` (7): `ForwardedHeadersSetup`, `LoggingSetup`, `PublicUrlProvider`, `RateLimitingSetup`, `ReminderDispatcher`, `ReminderOptions`, `StartupTasks` | Hosting | Núcleo; `StartupTasks` Mixto (migra, carga paquetes base, registra documentos de sistema). |
| `Realtime/*` (4): `CampaignHub`, `ConnectionTracker`, `RealtimeOptions`, `SignalRCampaignNotifier` | Hub `/hubs/campaign`, método `campaignEvent` | Núcleo |
| `wwwroot/admin.html`, `session.html`, `set-password.html` | Páginas servidas | Núcleo |
| `Endpoints/*` (28) | 21 núcleo, 3 5e, 4 mixtos | ver tabla |

Rutas, todas bajo `/api/v1` salvo páginas y hub. **→ 5e** marca lo que en la fase 32 pasa a
`/api/v1/systems/dnd5e/...` con alias (§4.6).

| Fichero | Prefijo | Rutas | Clas. |
|---|---|---|---|
| `AdminContentPackEndpoints` | `/admin/content-packs` | `GET ""`, `POST ""`, `DELETE /{id}` | Mixto (núcleo; el paquete declara su sistema desde la fase 34) |
| `AdminReleaseEndpoints` | `/admin` | `GET /releases`, `POST /releases`, `DELETE /releases/{id}`, `GET /stats` | Núcleo |
| `AdminUserEndpoints` | `/admin/users` | `GET ""`, `POST ""`, `PATCH /{id}`, `POST /{id}/setup-email` | Núcleo |
| `AppEndpoints` | `/app` | `GET /info`, `GET /latest`, `GET /download/{buildNumber}` | Núcleo |
| `AuthEndpoints` | `/auth` | `POST /login`, `/refresh`, `/logout`, `/password/forgot`, `/password/set`; `GET /me`, `PATCH /me` | Núcleo |
| `CampaignEndpoints` | `/campaigns` | `GET ""`, `POST ""`, `GET /{id}`, `PATCH /{id}`, `PATCH /{id}/settings`, `DELETE /{id}`, `GET /{id}/members`, `POST /{id}/members`, `GET /{id}/invitations`, `DELETE /{id}/invitations/{invitationId}`, `PATCH /{id}/members/{userId}`, `DELETE /{id}/members/{userId}`, `POST /{id}/leave`, `POST /{id}/transfer-ownership` | Núcleo |
| `CatalogEndpoints` | `/catalog` | `GET /attribution`, `/classes`, `/classes/{index}`, `/races`, `/races/{index}`, `/spells`, `/spells/{index}`, `/beasts`, `/beasts/{index}`, `/items`, `/items/{id}`, `/trinkets`, `/roll-tables`, `/conditions`, `/skills`, `/backgrounds`, `/equipment-categories/{index}`, `/sources`, `/features/{index}` | 5e (→ 5e) |
| `ChangeRequestEndpoints` | `/campaigns/{campaignId}/change-requests`, `/change-requests/{id}` | `GET` lista; `GET ""`, `POST /approve`, `POST /reject`, `POST /cancel` | Núcleo |
| `CharacterEndpoints` | `/campaigns/{campaignId}/characters`, `/characters/{id}` | Núcleo: `GET ""` y `POST ""` de la campaña; `GET ""`, `POST /submit`, `POST /activate`, `PATCH /portrait`, `PUT /owner`, `DELETE ""`. **→ 5e**: `PATCH /sheet`, `GET /origin-choices`, `PUT /origin-choices`, `PATCH /combat`, `POST /damage`, `PUT /companion`, `POST /companion/hp`, `DELETE /companion`, `POST /concentration`, `POST /spell-slots/{level}/spend`, `POST /spell-slots/{level}/restore`, `POST /resources`, `POST /resources/{resourceId}/spend`, `/restore`, `/rolls`, `DELETE /resources/{resourceId}`, `POST /rest/short`, `POST /rest/long`, `GET /spell-preparation`, `POST /spell-preparation`, `POST /spell-preparation/keep`, `GET /invalid-choices`, `POST /invalid-choices`, `POST /class-actions/{rage, lay-on-hands, divine-smite, arcane-recovery, natural-recovery}` | Mixto |
| `FileEndpoints` | `/files` | `POST ""`, `GET /{id}` | Núcleo |
| `InventoryEndpoints` | `/characters/{id}` | `GET /inventory`, `POST /inventory`, `PATCH /inventory/{itemId}`, `POST /inventory/{itemId}/use`, `DELETE /inventory/{itemId}`, `POST /money` | Núcleo (con ganchos al sistema) |
| `InvitationEndpoints` | `/` | `GET /me/invitations`, `POST /invitations/{id}/accept`, `POST /invitations/{id}/decline` | Núcleo |
| `ItemEndpoints` | `/campaigns/{campaignId}/items` | `GET ""`, `GET /{templateId}`, `POST ""`, `PATCH /{templateId}`, `DELETE /{templateId}` | Mixto (rutas núcleo; cuerpo validado por el sistema) |
| `LevelUpEndpoints` | `/characters/{id}/level-up` | `GET ""`, `POST ""` | 5e (→ 5e) |
| `LibraryEndpoints` | `/library`, `/campaigns/{campaignId}/library` | `GET ""`, `POST ""`, `PATCH /{id}`, `DELETE /{id}`; `GET ""`, `PUT /{documentId}`, `DELETE /{documentId}` | Núcleo |
| `LoreEndpoints` | `/campaigns/{campaignId}/lore`, `/lore/{id}` | `GET ""`, `POST ""`; `GET ""`, `PATCH ""`, `DELETE ""`, `POST /attachments`, `DELETE /attachments/{attachmentId}` | Núcleo |
| `MapEndpoints` | `/campaigns/{campaignId}/maps`, `/maps/{id}` | `GET ""`, `POST ""`; `GET ""`, `PATCH ""`, `DELETE ""`, `POST /pins`, `PATCH /pins/{pinId}`, `DELETE /pins/{pinId}` | Núcleo |
| `MessageEndpoints` | `/campaigns/{campaignId}/messages`, `/messages/{id}` | `POST ""`, `GET ""`, `GET /unread-count`; `POST /read` | Núcleo |
| `PageEndpoints` | (raíz) | `GET /set-password`, `GET /sessions/{id}`, `GET /admin` | Núcleo |
| `PartyEndpoints` | `/campaigns/{campaignId}/party` | `GET ""`, `POST /rest`, `POST /adjust`, `POST /grant-level`, `DELETE /grant-level` | 5e (→ 5e) |
| `PartyStashEndpoints` | `/campaigns/{campaignId}/stash` | `GET ""`, `POST /items`, `PATCH /items/{itemId}`, `DELETE /items/{itemId}`, `POST /items/{itemId}/take`, `POST /items/return`, `POST /gold`, `POST /gold/split` | Núcleo |
| `PublicSessionEndpoints` | `/public/sessions/{id}` | `GET ""`, `POST /rsvp` | Núcleo |
| `RealtimeEndpoints` | `/realtime` | `GET /status` | Núcleo |
| `RestRequestEndpoints` | `/characters/{id}/rest-requests`, `/campaigns/{campaignId}/rest-requests`, `/rest-requests/{id}` | `POST ""`, `DELETE ""`; `GET ""`; `GET ""`, `POST /approve`, `POST /reject` | Mixto (rutas núcleo; cuerpo y aplicación del sistema) |
| `SessionEndpoints` | `/campaigns/{campaignId}`, `/sessions/{id}`, `/me/sessions` | `GET /sessions`, `POST /sessions`, `GET /journal`; `GET ""`, `PATCH ""`, `DELETE ""`, `PUT /rsvp`, `PUT /summary`, `POST /notify`; `GET ""` | Núcleo |
| `SetupEndpoints` | `/setup` | `GET /status`, `POST /admin` | Núcleo |
| `ShopEndpoints` | `/campaigns/{campaignId}`, `/shops/{id}` | `GET /shops`, `POST /shops`, `GET /transactions`; `GET ""`, `PATCH ""`, `DELETE ""`, `POST /items`, `POST /items/bulk`, `PATCH /items/{shopItemId}`, `DELETE /items/{shopItemId}`, `POST /buy`, `POST /sell` | Núcleo |
| `UserEndpoints` | `/users` | `GET /search` | Núcleo |

Ruta nueva del núcleo en la fase 31: `GET /api/v1/systems` (sistemas registrados: id, nombre, versión,
atribución).

## 2. Inventario de la app

### 2.1 `lib/core` (77 ficheros, 10 022 líneas)

| Carpeta | Ficheros | Qué hace | Clas. | Reparto |
|---|---|---|---|---|
| `auth` | 8 | `AuthController`, interceptor, repositorio, estado, tokens, usuario | Núcleo | |
| `cache` | 6 | Caché `drift` de respuestas, mantenimiento, datos obsoletos | Núcleo | |
| `config` | 1 | `API_BASE_URL` | Núcleo | |
| `content` | 1 | Visibilidad de contenido | Núcleo | |
| `files` | 6 | Imágenes autenticadas, caché en disco, subida, abrir externo | Núcleo | |
| `motion` | 11 | Llamas, destellos, celebración de nivel, descanso (hoguera, luna), sello, viñeta, temblor | Núcleo | Son efectos visuales sin reglas; los dispara el módulo. |
| `network` | 4 | `ApiClient` (`dio`), errores, conectividad, almacén de confianza | Núcleo | |
| `realtime` | 7 | Hub SignalR, banner de conexión, diagnóstico, icono de estado, eventos | Núcleo salvo `realtime_events.dart` (Mixto) | `realtime_events.dart`: `PartyRest` y `LevelUpGranted` son eventos que declara 5e; el núcleo pasa los tipos desconocidos a los módulos (`GameSystemUi.onRealtimeEvent`, §5). |
| `router` | 1 | `app_router.dart`: `AppRoutes` y `GoRouter` | Mixto | Ver §2.3. |
| `server` | 7 | URL del servidor, sonda, huella de certificado, época de sesión | Núcleo | |
| `storage` | 1 | `SharedPreferences` | Núcleo | |
| `theme` | 10 | Tokens, paletas, tipografía, componentes (`StoneCard`, `ParchmentCard`, `RuneDivider`), iconos propios, texturas, contraste | Núcleo | El token "magia" y el icono `d20` son genéricos del tema. |
| `ui` | 10 | `StatTileGrid`/`StatTile`, `StatValue`/`BreakdownSheet`, `SelectionGrid`, `InfiniteScrollList`, `MarkdownView`, `OfflineAware*`, `SourceChip`, `ContentErrorView` | Núcleo salvo dos | `action_type.dart` (acción, acción adicional, reacción) y `spell_category.dart` son **5e** y se mueven a `packages/dnd5e`. |
| `update` | 4 | Versión, diálogo y bloqueo de actualización | Núcleo | |

### 2.2 `lib/features` (208 ficheros, 50 783 líneas)

| Carpeta | Ficheros | Líneas | N / 5e / M |
|---|---|---|---|
| `admin` | 9 | 1 001 | 9 / 0 / 0 |
| `auth` | 4 | 313 | 4 / 0 / 0 |
| `campaigns` | 16 | 2 482 | 16 / 0 / 0 |
| `catalog` | 17 | 4 084 | 1 / 15 / 1 |
| `change_requests` | 1 | 513 | 0 / 0 / 1 |
| `characters` | 74 | 23 600 | 2 / 58 / 14 |
| `dice` | 5 | 1 187 | 3 / 0 / 2 |
| `home` | 7 | 628 | 6 / 0 / 1 |
| `items` | 24 | 5 635 | 10 / 2 / 12 |
| `library` | 6 | 1 069 | 6 / 0 / 0 |
| `lore` | 6 | 1 205 | 6 / 0 / 0 |
| `maps` | 6 | 1 376 | 6 / 0 / 0 |
| `server` | 1 | 400 | 1 / 0 / 0 |
| `session` | 16 | 3 814 | 7 / 4 / 5 |
| `sessions` | 14 | 2 887 | 14 / 0 / 0 |
| `settings` | 2 | 789 | 2 / 0 / 0 |

#### Carpetas del núcleo

| Carpeta | Páginas, widgets y proveedores | Clas. |
|---|---|---|
| `admin` | `AdminUsersPage`, `CreateUserDialog`, `AdminContentPage` (paquetes), `ContentPacksController`, `ContentPacksRepository`, `ContentPack` | Núcleo (desde la fase 34 la importación indica el sistema del paquete) |
| `auth` | `LoginPage`, `ForgotPasswordPage`, `SplashPage`, validadores | Núcleo |
| `campaigns` | `CampaignsPage`, `CampaignShell` (Mesa del DM / Mi sesión / Campaña / Personajes), `CampaignGeneralPage`, `CampaignSectionPage` (secciones: personajes, lore, mapas, sesiones, diario, miembros, tiendas, biblioteca, contenido, ajustes), `CampaignFormDialog`, `MembersSection`, `AddMemberDialog`, `TransferOwnershipDialog`, `CampaignSettingsSection`, `CampaignsController`, `CampaignDetailController`, `CampaignRoleCache` | Núcleo. Fase 31: `CampaignFormDialog` elige sistema y `CampaignDetail.systemId`. Las secciones "Personajes" y "Contenido" montan widgets mixtos (§2.2 `characters`, `items`). |
| `home` | `AppShell`, `HomePage`, `ProfilePage`, `EditNameDialog`, `ServerInfoRepository` | Núcleo |
| `home/attribution_page.dart` | Atribución CC-BY y licencias de fuentes | Mixto: licencias del núcleo + `GameSystemUi.attributions` de cada sistema. |
| `library` | `LibraryPage`, `PdfViewerPage`, controladores y almacenamiento | Núcleo |
| `lore` | `LoreTab`, `LoreEntryPage`, `LoreEditorPage` | Núcleo |
| `maps` | `MapsTab`, `MapViewerPage`, `PinFormDialog` | Núcleo |
| `server` | `ServerPage` (URL, certificados) | Núcleo |
| `sessions` | `SessionsTab`, `SessionPage`, `SessionFormPage`, `SummaryEditorPage`, `JournalTab`, `NextSessionCard`, `CalendarSettingsDialog`, `NotifyDialog` | Núcleo |
| `settings` | `AppearancePage`, `AppearanceController` | Núcleo |

#### `catalog` (17)

| Fichero | Qué hace | Clas. | Reparto |
|---|---|---|---|
| `ui/compendium_page.dart` | Compendio con pestañas Hechizos, Objetos, Clases, Razas, Bestias, Condiciones, Tablas | Mixto | La página (rama de la barra inferior, búsqueda, `TabBar`) es núcleo; las pestañas las da `GameSystemUi.compendiumTabs`. |
| `ui/detail_widgets.dart` | `CatalogAsyncBody`, `DetailList`, `FactRow`, `Paragraphs`, `ExpandableEntry` | Núcleo | Se mueve a `core/ui`. `ModifierLines` (modificadores de objeto) es 5e y va con el módulo. |
| `data/models.dart` (1 517), `beast_models`, `catalog_repository`, `catalog_controllers` | Modelos y acceso al catálogo 5e | 5e | |
| `domain/catalog_format`, `item_modifier_format` | Formatos de conjuros, objetos, modificadores | 5e | |
| `ui/beast_page`, `class_detail_page`, `race_detail_page`, `spell_detail_page`, `feature_detail_page`, `item_detail_page`, `condition_sheet`, `roll_table_widgets`, `catalog_detail_links` (`DetailInfoButton`) | Detalles del compendio | 5e | |

#### `characters` (74)

| Fichero o carpeta | Qué hace | Clas. | Reparto |
|---|---|---|---|
| `data/models.dart` (2 590) | `CharacterSummary`, `CharacterDetail`, `CharacterSheet` y ~60 modelos | Mixto | `CharacterSummary` y la cabecera de `CharacterDetail` (id, campaña, dueño, nombre, estado, retrato, textos, altura y peso, dinero, peticiones pendientes) al núcleo; la hoja al módulo (§3.2). |
| `data/characters_repository.dart` | 43 llamadas HTTP | Mixto | Núcleo: lista, crear, obtener, enviar, activar, retrato, dueño, borrar, peticiones de cambio (listar, ver, aprobar, rechazar, cancelar). 5e: hoja, orígenes, elecciones inválidas, daño, combate, compañero, concentración, espacios, recursos, descansos, acciones de clase, subida de nivel, preparación. |
| `data/characters_controller.dart` | `CampaignCharactersController`, `CharacterController`, `ChangeRequestsController`, `pendingChangeRequestCountProvider`, `spellInfoProvider`, `invalidChoicesProvider` | Mixto | Los cuatro primeros al núcleo; `spellInfoProvider`, `invalidChoicesProvider` a 5e. |
| `data/view_mode_controller.dart` | `CharacterView` (Combate/Detalle), `CharacterTab`, `PlayerSessionTab` | Mixto | `CharacterView` y la memoria de la vista al núcleo; la lista de subpestañas de Detalle la da el módulo (`GameSystemUi.detailTabs`). |
| `data/character_wizard_controller` (1 817), `level_up_controller`, `companion_models` | Asistente, subida de nivel, compañero | 5e | |
| `domain/character_format.dart` | `formatModifier`, abreviaturas, habilidades, alineamientos, compra de puntos, `copperToGoldText`, `CharacterPermissions` | Mixto | `CharacterPermissions` al núcleo; `copperToGoldText` a `ICurrency` del sistema; el resto 5e. |
| `domain/change_details.dart`, `payload_format.dart` | Detalle de una petición de cambio | Mixto | `ItemChangeDetail`, `RemoveItemDetail`, `MoneyChangeDetail`, `PlainChangeDetail` al núcleo; `SheetChangeDetail` y los formatos de clases, competencias, conjuros, overrides al módulo (`GameSystemUi.describeChangeRequest`). |
| `domain/class_theme`, `combat_math`, `height_weight`, `spell_combat` | Acento por clase, matemáticas de combate, tirada de altura y peso, conjuros en combate | 5e | |
| `ui/character_avatar`, `change_owner_dialog` | Avatar, cambio de dueño | Núcleo | |
| `ui/character_page.dart` | Página con pestañas Combate / Detalle, cabecera, acciones de estado | Mixto | Armazón (cabecera con retrato, nombre, estado, enviar/activar/borrar, dos vistas) al núcleo; contenido de cada vista del módulo. |
| `ui/character_detail_tabs.dart` | Subpestañas Resumen, Habilidades, Rasgos, Hechizos, Inventario, Notas (+ Sesión en Mi sesión) | Mixto | El contenedor, Inventario y Notas al núcleo; Resumen, Habilidades, Rasgos, Hechizos del módulo. |
| `ui/character_tabs.dart` (1 008) | `SummaryTab`, `SkillsTab`, `TraitsTab`, `SpellsTab`, `NotesTab`, `OverrideMark` | Mixto | `NotesTab` (notas, historia, personalidad) y `OverrideMark` al núcleo; el resto 5e. |
| `ui/characters_tab.dart` | Lista de personajes de la campaña, `CharacterCard`, "Crear rápido (PNJ)" | Mixto | Lista y tarjeta al núcleo; el subtítulo (raza, clases, nivel) lo da `GameSystemUi.rosterSubtitle`. |
| `ui/new_character_dialog.dart` | Crear personaje (asistente o PNJ rápido) | Mixto | El diálogo es núcleo; "asistente" abre `GameSystemUi.creationWizard`. |
| `ui/height_weight_fields.dart` | Campos de altura y peso con tirada | Mixto | Campos al núcleo; botón de tirada lo aporta 5e (tabla de la raza). |
| `ui/sheet_editor_page.dart` (1 189) | Editor manual de la hoja | Mixto | Nombre, textos y altura/peso al núcleo (editor de perfil); el resto 5e. |
| `ui/invalid_choices_page`, `origin_choices_widgets`, `point_buy_dialog`, `prepare_spells_page`, `rest_rolls_page`, `skill_rolls`, `spell_picker_page` | Flujos 5e | 5e | |
| `ui/combat/` (14) | `CombatView`, `HpCard`, `StatsCard`, `DeathSavesCard`, `ConditionsCard`, `AttacksSection`, `SpellsSection`, `SpellSlotsSection`, `ResourcesSection`, `ConsumablesSection`, `ClassPanelsSection`, `CompanionSection`, `RestSection`, `WildMagicSurgePrompt`, `concentration_flow`, `recovery_reminder`, `rest_celebration`, `combat_state`, `combat_support` | 5e | `combat_support.dart` tiene ayudas genéricas (`runCombat`, `PipRow`, `CombatCard`): se quedan en el módulo en la fase 33 y se suben al núcleo solo si otro sistema las usa. |
| `ui/combat/panels/` (14) | Paneles de bárbaro, bardo, clérigo, druida, guerrero, monje, paladín, explorador, pícaro, hechicero, brujo, mago, `panel_support`, `critical_damage_roll` | 5e | |
| `ui/level_up/` (7) | `LevelUpPage` y sus pasos | 5e | |
| `ui/wizard/` (9) | `CharacterWizardPage` y sus pasos (nombre, raza, clase, características, trasfondo, origen, equipo, conjuros, personalidad, revisión) | 5e | `step_personality` usa textos del núcleo pero con las tablas del trasfondo: queda en 5e. |

#### `dice` (5)

| Fichero | Qué hace | Clas. | Reparto |
|---|---|---|---|
| `domain/dice_expression.dart` | Parser y tirada de expresiones (`2d6+3`, `adv`, `dis`, críticos dobles) | Núcleo | La notación `adv`/`dis` es del motor de dados y vale para cualquier sistema. |
| `ui/dice_page.dart`, `ui/roll_input_button.dart` | Página de dados, botón de tirada libre | Núcleo | |
| `data/dice_controller.dart` | Historial local, natural 20 / natural 1 | Mixto | Historial al núcleo; detectar "crítico" y "pifia" con el d20 lo decide el sistema (`GameSystemUi.classifyRoll`). |
| `ui/dice_sheet.dart` | Hoja de resultado | Mixto | Resultado al núcleo; etiquetas de crítico/pifia del sistema. |

#### `items` (24)

| Fichero | Qué hace | Clas. | Reparto |
|---|---|---|---|
| `data/inventory_repository`, `shops_repository` | HTTP de inventario y tiendas | Núcleo | |
| `data/campaign_items_repository` | Homebrew | Mixto | HTTP núcleo; cuerpo con campos 5e. |
| `data/items_controllers.dart` | `InventoryController`, `CampaignItemsController`, `HomebrewActions`, `ShopsController` | Mixto | Controladores núcleo; ninguno calcula reglas, pero exponen modelos mixtos. |
| `data/models.dart` (764) | `ItemOverrides`, `EffectiveItem`, `CharacterItem`, `Inventory`, tiendas | Mixto | Daño, CA, propiedades, rareza, sintonía y modificadores a un `systemData` interpretado por el módulo. |
| `domain/item_form_data`, `items_format` | Formulario y formatos | Mixto | |
| `domain/combat_usable.dart` | Qué objetos importan en combate | 5e | |
| `ui/attunement_dialog.dart` | Sintonía (máx. 3) | 5e | |
| `ui/add_item_page`, `item_feedback`, `quantity_dialog`, `sell_dialog`, `shop_dialogs`, `shop_page`, `shops_tab`, `transactions_page` | Tiendas, venta, cantidades, transacciones | Núcleo | |
| `ui/inventory_tab.dart` | Inventario de un personaje | Mixto | Lista, cantidades, equipar, usar, dinero al núcleo; sintonía y chips de daño/CA del módulo (`GameSystemUi.itemExtras`). |
| `ui/effective_item_page.dart` | Detalle del objeto con overrides | Mixto | Igual. |
| `ui/item_fields_form.dart` (552), `item_composer`, `homebrew_tab` | Formulario de plantilla y homebrew | Mixto | Nombre, categoría, peso, coste, descripción al núcleo; campos 5e como sección del módulo (`GameSystemUi.itemFormSection`). |
| `ui/item_search_list.dart`, `shop_catalog_page.dart` | Buscar en el catálogo para añadir a inventario o tienda | Mixto | Lista paginada al núcleo; la fuente es el catálogo de objetos del sistema. |

#### `session` (16) y `change_requests` (1)

| Fichero | Qué hace | Clas. | Reparto |
|---|---|---|---|
| `data/messages_repository`, `stash_repository` | Mensajes secretos, alijo | Núcleo | |
| `data/party_repository` | `GET /party`, descanso, ajuste, conceder nivel | 5e | |
| `data/rest_requests_repository` | Peticiones de descanso | Mixto | HTTP núcleo; tipo y payload del sistema. |
| `data/models.dart` | `PartyMember`, `PartyAdjustment`, `StashItem`, `PartyStash`, `DirectMessage` | Mixto | `StashItem`, `PartyStash`, `DirectMessage` al núcleo; `PartyMember`, `PartyAdjustResult`, `PartyAdjustment` a 5e. |
| `data/session_controllers.dart` | `PartyController`, `RestRequestsController`, `StashController`, `MessagesController` | Mixto | `PartyController` a 5e; el resto al núcleo. |
| `ui/dm/dm_session_page.dart` | Mesa del DM: barra de acciones (descanso corto/largo, conceder nivel, mensaje), roster, alijo, tiendas, peticiones | Mixto | Página, mensajes, alijo, tiendas y peticiones al núcleo; barra de acciones de grupo y roster del módulo (`GameSystemUi.partyPanel`). |
| `ui/dm/party_roster.dart`, `dm_character_sheet.dart` | Roster con CA, iniciativa, PG, espacios, condiciones; hoja rápida del DM | 5e | |
| `ui/dm/add_stash_item_page`, `message_composer`, `ui/stash_card`, `ui/player/messages_inbox`, `ui/session_feedback` | Alijo, mensajes, avisos | Núcleo | |
| `ui/player/player_session_page.dart` | Mi sesión: Combate y Detalle con subpestaña Sesión | Mixto | Armazón, selector y subpestaña Sesión (mensajes, alijo, nivel concedido) al núcleo; Combate y Detalle del módulo. |
| `ui/player/combat_items_section.dart` | Consumibles en combate | 5e | |
| `change_requests/ui/change_requests_page.dart` | Lista y detalle de peticiones, aprobar/rechazar | Mixto | Página núcleo; el detalle de `EditSheet` y `Companion` lo pinta el módulo. |

### 2.3 Rutas de `app_router.dart`

| Ruta | Página | Clas. |
|---|---|---|
| `/splash`, `/server`, `/login`, `/forgot-password` | `SplashPage`, `ServerPage`, `LoginPage`, `ForgotPasswordPage` | Núcleo |
| `/` | `HomePage` (rama de `AppShell`) | Núcleo |
| `/compendium` | `CompendiumPage` (rama) | Mixto (pestañas del sistema; ver §7 pregunta 6: con varios sistemas, ¿de cuál?) |
| `/dice` | `DicePage` (rama) | Núcleo |
| `/library` | `LibraryPage` (rama) | Núcleo |
| `/profile` | `ProfilePage` (rama) | Núcleo |
| `/admin/users`, `/admin/content` | `AdminUsersPage`, `AdminContentPage` | Núcleo |
| `/attributions` | `AttributionPage` | Mixto |
| `/settings/appearance` | `AppearancePage` | Núcleo |
| `/campaigns/:id` | redirige según el rol | Núcleo |
| `/campaigns/:id/general` y `/campaigns/:id/general/<sección>` | `CampaignGeneralPage`, `CampaignSectionPage` | Núcleo |
| `/campaigns/:id/dm` | `DmSessionPage` | Mixto |
| `/campaigns/:id/player` | `PlayerSessionPage` | Mixto |
| `/campaigns/:id/characters` | `CampaignCharactersPage` | Núcleo (tarjetas mixtas) |
| `/campaigns/:id/change-requests` | `ChangeRequestsPage` | Mixto |
| `/campaigns/:id/shops/:shopId`, `/campaigns/:id/transactions` | `ShopPage`, `TransactionsPage` | Núcleo |
| `/campaigns/:id/characters/new` | `CharacterWizardPage` | 5e (vía `GameSystemUi.creationWizard`) |
| `/campaigns/:id/lore/new`, `/campaigns/:id/lore/:entryId`, `/campaigns/:id/lore/:entryId/edit` | `LoreEditorPage`, `LoreEntryPage` | Núcleo |
| `/campaigns/:id/maps/:mapId` | `MapViewerPage` | Núcleo |
| `/campaigns/:id/sessions/new`, `/campaigns/:id/sessions/:sessionId`, `.../edit`, `.../summary` | `SessionFormPage`, `SessionPage`, `SummaryEditorPage` | Núcleo |
| `/campaigns/:id/library`, `/library/:docId/view` | `LibraryPage`, `PdfViewerPage` | Núcleo |
| `/characters/:id` | `CharacterPage` | Mixto |
| `/characters/:id/edit` | `SheetEditorPage` | Mixto |
| `/characters/:id/level-up` | `LevelUpPage` | 5e |
| `/characters/:id/prepare-spells`, `/characters/:id/invalid-choices`, `/characters/:id/rest-rolls` | `PrepareSpellsPage`, `InvalidChoicesPage`, `RestRollsPage` | 5e |
| `/compendium/spells/:index`, `/compendium/items/:id`, `/compendium/classes/:index`, `/compendium/races/:index`, `/compendium/beasts/:index` | Detalles del compendio | 5e |

En la fase 33 las rutas 5e las declara el módulo (`GameSystemUi.routes`) y el núcleo las añade al
`GoRouter` sin cambiar los caminos (los enlaces y los tests siguen valiendo).

## 3. `Character` en el núcleo

### 3.1 Campos de la entidad

| Campo de `Character` | Destino | Nota |
|---|---|---|
| `Id`, `CampaignId`, `OwnerUserId` (null = PNJ), `Name`, `Status` (`Draft`/`Active`), `PortraitFileId`, `CreatedAt`, `UpdatedAt`, `Version` (concurrencia) | Núcleo | Identidad, campaña, dueño, ciclo de vida. |
| `Notes`, `Backstory`, `PersonalityTraits`, `Ideals`, `Bonds`, `Flaws` | Núcleo | Textos libres; en 5e los rellena el asistente con las tablas del trasfondo, pero se guardan y editan como texto. |
| `HeightInches`, `WeightPounds` | Núcleo | Datos sin efecto mecánico. La unidad de presentación la da el sistema (5e: pies/pulgadas, libras). |
| `CopperPieces` | Núcleo | Dinero en la unidad mínima; lo mueven tienda, alijo y peticiones de dinero. Denominaciones por sistema (`ICurrencySystem`). |
| `Items` (`CharacterItem`) | Núcleo | Inventario (con los campos 5e de §1.1 en el módulo). |
| `RaceIndex`, `SubraceIndex`, `BackgroundIndex`, `BackgroundDetail`, `Alignment`, `ApplyRacialBonuses`, `HpMode`, `BaseStr`…`BaseCha` | Hoja 5e | |
| `HitPointsCurrent`, `TemporaryHitPoints`, `DeathSaveSuccesses`, `DeathSaveFailures`, `ExhaustionLevel`, `ConditionsJson`, `ConcentratingOnSpellIndex`, `Inspiration`, `HitDiceUsedJson` | Hoja 5e (estado de combate) | |
| `PendingLevelUpTo`, `LevelGrantedByUserId`, `LevelGrantedAt`, `SpellPreparationPending`, `SpellPreparationReason` | Hoja 5e | El núcleo no conoce "nivel": conceder nivel es una acción de grupo del módulo. |
| `Classes`, `Proficiencies`, `Spells`, `SpellSlots`, `Resources`, `Overrides`, `Choices` (`CharacterChoice`), compañero (`CharacterCompanion`, tabla aparte) | Hoja 5e | |

Métodos: `Create`, `IsOwnedBy`, `CanViewSheet`, `ResolveSheetEdit`, `EnsureCanTrack`, `EnsureCanDelete`,
`EnsureCanSubmit`, `SetPortrait`, `Rename`, `ChangeOwner`, `SetHeightAndWeight`, `AdjustMoney` y las
operaciones de inventario (`AddItem`, `ReceiveItem`, `RemoveItem`, `UpdateItem`, `UseItem`, `FindItem`)
quedan en el núcleo. `Activate(maxHp)` se parte: el núcleo cambia el estado y el módulo fija los PG
(`ICreationSystem.PrepareActivationAsync`). Todo lo demás (`ApplySheetEdit`, `Set*`, `Replace*`,
`ApplyCombatUpdate`, `ApplyDamage`, `Heal`, espacios, recursos, descansos, acciones de clase, subida de
nivel, orígenes, preparación) va a `Dnd5eCharacter`.

### 3.2 `CharacterDetailDto` y `CharacterSummaryDto`

| Campos del DTO | Destino |
|---|---|
| `Id`, `CampaignId`, `OwnerUserId`, `OwnerDisplayName`, `Name`, `Status`, `PortraitFileId`, `PortraitUrl`, `Notes`, `Backstory`, `PersonalityTraits`, `Ideals`, `Bonds`, `Flaws`, `HeightInches`, `WeightPounds`, `CopperPieces`, `CreatedAt`, `UpdatedAt`, `PendingChangeRequests`, `Inventory` (parte núcleo de cada objeto) | Núcleo (`CharacterCoreDto`) |
| `RaceIndex/Name`, `SubraceIndex/Name`, `BackgroundIndex/Name`, `BackgroundDetail`, `*CatalogMissing`, `Alignment`, `ApplyRacialBonuses`, `HpMode`, `Base*`, `HitPointsCurrent`, `TemporaryHitPoints`, salvaciones de muerte, `ExhaustionLevel`, `Conditions`, `ConcentratingOnSpellIndex`, `Inspiration`, `HitDiceUsed`, `Classes`, `Proficiencies`, `Spells`, `Overrides`, `Resources`, `SpellSlots`, `Sheet` (con `breakdowns`), `Combat` (`CombatSummaryDto`), `PendingRest`, `PendingLevelUpTo`, `SpellPreparationPending/Reason`, `InvalidChoices`, `RestRollsPending`, `Choices`, `Feats`, `OptionCosts`, `Companion`, `CompanionFeature`, `CompanionPending` | Sistema (`ISheetSystem.BuildDetailAsync`) |
| `CharacterSummaryDto`: `Id`, `CampaignId`, `OwnerUserId`, `OwnerDisplayName`, `Name`, `Status`, `PortraitUrl` | Núcleo |
| `CharacterSummaryDto`: `RaceName`, `Classes`, `Level`, `HitPointsCurrent`, `HitPointsMax` | Sistema (`ISheetSystem.BuildRosterLine`) |

Para que la API siga **idéntica** (ADR 0009: "API idéntica para el cliente actual"), en la fase 32 el
JSON no cambia de forma: el núcleo construye su parte y el módulo 5e añade sus campos **al mismo
nivel** (objeto plano). La separación en `{ core…, system: {…} }` queda para cuando exista un segundo
sistema (§7 pregunta 2).

### 3.3 Peticiones de cambio, descansos y acciones de grupo

- **`ChangeRequest`** (núcleo). El núcleo decide si una escritura del jugador va directa o por
  petición (`SheetEditMode`: el DM/Owner aplica directo; el dueño de un `Draft` edita libre; el dueño
  de un `Active` pide). Cada tipo de petición tiene un aplicador registrado:
  `Activate`, `AddItem`, `RemoveItem`, `CustomItem`, `AdjustMoney`, `Other` → núcleo;
  `EditSheet`, `Companion` → `IChangeRequestSystem` del módulo. Aprobar = cargar, aplicar con el
  aplicador del tipo, `ISheetSystem.RecalculateAsync`, guardar, notificar `changeRequest.updated`,
  `character.updated`, `changeRequest.resolved`. El "antes" (`BeforeJson`) lo calcula el mismo
  aplicador. `ChangeRequestType` deja de ser enum y pasa a cadena (`HasMaxLength(16)` ya se guarda
  como texto; ningún dato cambia).
- **`RestRequest`** (núcleo con payload del sistema). El jugador pide; el DM aprueba o rechaza; una
  pendiente por personaje. El módulo declara los tipos (`short`, `long`), valida el payload (dados de
  golpe por clase) y lo aplica al aprobar (`IRestSystem.ApplyAsync`), que hoy hace `RestHandler` y
  `ApproveRestRequestHandler`. Migración (fase 32): `RestRequests.Kind` sigue siendo texto;
  `HitDiceJson` pasa a llamarse `PayloadJson` (renombrado de columna, sin pérdida). Si se prefiere no
  tocar la tabla en la fase 32, el renombrado espera a la 34.
- **Acciones de grupo** (`/party/*`). Son del módulo: el roster (`PartyMemberDto`), descanso de grupo,
  ajustes rápidos (PG, PG temporales, condiciones, PG máximos) y conceder/retirar nivel. El núcleo
  aporta `PartyLoader` (DM de la campaña, filtro por ids, personajes activos) y la notificación. El
  alijo (`/stash`) y los mensajes secretos son núcleo.
- **Seguimiento sin aprobación** (PG, salvaciones de muerte, espacios, recursos, condiciones,
  concentración, descansos de DM): el permiso (`EnsureCanTrack`: dueño o DM) es núcleo; qué se
  sigue, del módulo.

### 3.4 Persistencia propuesta

**División de tabla** (EF Core *table splitting*): `Characters` se mapea a dos tipos que comparten
clave, `Character` (núcleo, columnas de §3.1 "Núcleo") y `Dnd5eCharacter` (módulo, el resto de
columnas), relación 1:1 obligatoria. Las tablas hijas (`CharacterClassLevels`, …,
`CharacterCompanions`) cuelgan de `Dnd5eCharacter`. **No hay migración de datos**: la tabla y las
columnas no cambian, solo el mapeo. Un sistema futuro sin tablas propias usaría una tabla genérica
`CharacterSheetDocuments(CharacterId PK/FK, SystemId, Json, Version)`; no se crea hasta que haga falta.

## 4. Contrato `IGameSystem` (servidor)

### 4.1 Esquema

```csharp
namespace OpenTrpg.Core.Application.Systems;

/// <summary>Un sistema de juego registrado (5e es el primero). Sin dependencias de ASP.NET ni EF.</summary>
public interface IGameSystem
{
    string Id { get; }                                  // "dnd5e" (Campaign.SystemId)
    GameSystemInfo Info { get; }                        // nombre, versión, atribuciones, documentos de sistema

    ISheetSystem Sheets { get; }
    ICreationSystem Creation { get; }
    IProgressionSystem Progression { get; }
    ICombatSystem Combat { get; }
    IRestSystem Rests { get; }
    ICatalogSystem Catalog { get; }
    IChoiceSystem Choices { get; }
    IPartySystem Party { get; }
    IItemSystem Items { get; }
    ICurrencySystem Currency { get; }
    IChangeRequestSystem ChangeRequests { get; }

    /// <summary>Tipos de evento de tiempo real propios (además de los del núcleo).</summary>
    IReadOnlyList<string> RealtimeEventKinds { get; }
}

public sealed record GameSystemInfo(
    string Name, string Version,
    IReadOnlyList<AttributionInfo> Attributions,          // SRD 5.1 CC-BY 4.0
    IReadOnlyList<SystemDocumentInfo> SystemDocuments);   // SRD_CC_v5.1.pdf

/// <summary>Hoja: qué guarda un personaje y cómo se calcula (con desglose de cada valor).</summary>
public interface ISheetSystem
{
    SheetSchema Schema { get; }                           // campos editables y sobrescribibles
    Task<SystemSheet> CalculateAsync(CharacterRef character, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, SystemSheet>> CalculateManyAsync(IReadOnlyList<CharacterRef> characters, CancellationToken ct);
    Task<SystemSheet> RecalculateAsync(CharacterRef character, CancellationToken ct);   // tras editar o cambiar el equipo
    Task ValidateEditAsync(CharacterRef character, JsonElement patch, CancellationToken ct);
    Task ApplyEditAsync(CharacterRef character, JsonElement patch, DateTimeOffset now, CancellationToken ct);
    Task<JsonObject> SnapshotAsync(CharacterRef character, JsonElement patch, CancellationToken ct); // "antes"
    Task<JsonObject> BuildDetailAsync(CharacterRef character, CancellationToken ct);               // campos del sistema
    RosterLine BuildRosterLine(CharacterRef character, SystemSheet sheet, bool showHitPoints);
}

public interface ICreationSystem
{
    Task InitializeAsync(CharacterRef character, JsonElement? creation, CancellationToken ct);
    Task<JsonObject> GetOriginChoicesAsync(CharacterRef character, CancellationToken ct);
    Task SaveOriginChoicesAsync(CharacterRef character, JsonElement answers, CancellationToken ct);
    Task PrepareActivationAsync(CharacterRef character, DateTimeOffset now, CancellationToken ct); // orígenes completos, PG, preparación
}

public interface IProgressionSystem
{
    Task<JsonObject> PlanAsync(CharacterRef character, JsonElement? query, CancellationToken ct);
    Task ApplyAsync(CharacterRef character, JsonElement request, bool actorIsDm, CancellationToken ct);
    Task GrantAsync(IReadOnlyList<CharacterRef> characters, Guid grantedBy, DateTimeOffset now, CancellationToken ct);
    Task RevokeAsync(IReadOnlyList<CharacterRef> characters, DateTimeOffset now, CancellationToken ct);
}

public interface ICombatSystem
{
    Task<JsonObject> BuildSummaryAsync(CharacterRef character, SystemSheet sheet, CancellationToken ct);
    Task<DamageOutcome> ApplyDamageAsync(CharacterRef character, int amount, DateTimeOffset now, CancellationToken ct);
    Task HealAsync(CharacterRef character, int amount, DateTimeOffset now, CancellationToken ct);
    Task SetConditionsAsync(CharacterRef character, IReadOnlyList<string> add, IReadOnlyList<string> remove, CancellationToken ct);
}

public interface IRestSystem
{
    IReadOnlyList<string> Kinds { get; }                  // "short", "long"
    Task ValidateRequestAsync(CharacterRef character, string kind, JsonElement payload, CancellationToken ct);
    Task ApplyAsync(CharacterRef character, string kind, JsonElement payload, DateTimeOffset now, CancellationToken ct);
}

public interface ICatalogSystem
{
    IReadOnlyList<string> DefinitionTypes { get; }        // classes, races, spells, items, beasts, conditions…
    Task LoadBasePackAsync(CancellationToken ct);         // el SRD
    Task<PackImportResult> ImportPackAsync(PackHeader header, JsonDocument pack, CancellationToken ct);
    Task DeletePackAsync(string packId, CancellationToken ct);
}

public interface IChoiceSystem
{
    Task<IReadOnlyList<InvalidChoiceInfo>> FindInvalidAsync(CharacterRef character, CancellationToken ct);
    Task ReplaceInvalidAsync(CharacterRef character, JsonElement replacements, CancellationToken ct);
}

public interface IPartySystem
{
    Task<JsonObject> BuildPartyAsync(IReadOnlyList<CharacterRef> characters, CancellationToken ct);
    Task<JsonObject> RestAsync(IReadOnlyList<CharacterRef> characters, string kind, DateTimeOffset now, CancellationToken ct);
    Task<JsonObject> AdjustAsync(IReadOnlyList<CharacterRef> characters, JsonElement adjustments, DateTimeOffset now, CancellationToken ct);
}

public interface IItemSystem
{
    void ValidateTemplate(JsonElement systemFields, IValidationSink errors);
    JsonObject DescribeEffective(ItemRef item);           // daño, CA, rareza, sintonía, modificadores
    Task OnInventoryChangedAsync(CharacterRef character, InventoryChange change, CancellationToken ct); // sintonía, recálculo
}

public interface ICurrencySystem
{
    IReadOnlyList<Denomination> Denominations { get; }    // 5e: pc, pp, pe, po, ppt en cobre
}

public interface IChangeRequestSystem
{
    IReadOnlyList<string> Types { get; }                  // "EditSheet", "Companion"
    Task<JsonObject?> SnapshotAsync(CharacterRef character, string type, JsonElement payload, CancellationToken ct);
    Task ApplyAsync(CharacterRef character, string type, JsonElement payload, DateTimeOffset now, CancellationToken ct);
}
```

`CharacterRef` es el par `(Character núcleo, objeto del sistema cargado)`; en 5e envuelve el
`Character` actual mientras la entidad no esté partida. Los `JsonObject`/`JsonElement` son la
frontera **pública** (lo que viaja al cliente); dentro del módulo 5e los handlers siguen usando sus DTO
tipados (`LevelUpPlanDto`, `CombatSummaryDto`, …) y solo se serializan en el borde.

En la capa de hosting (no en `Application`) cada módulo aporta además:

```csharp
public interface IGameSystemModule
{
    IGameSystem System { get; }
    void AddServices(IServiceCollection services, IConfiguration configuration);
    void ConfigureModel(ModelBuilder modelBuilder);               // tablas del módulo y división de Characters
    void MapEndpoints(IEndpointRouteBuilder systemGroup);         // bajo /api/v1/systems/{id}
}
```

### 4.2 Qué implementa hoy cada operación (5e)

| Operación | Implementación actual |
|---|---|
| `Sheets.Schema` | `SheetPatch`, `SheetPatchValidator`, `OverrideFields`, `SheetEditMode` |
| `Sheets.CalculateAsync`, `CalculateManyAsync`, `RecalculateAsync` | `CharacterSheetService` (`SheetCalculator`, `SheetInput`, `SheetCatalog`, `ValueBreakdown`, `ClassResourceRules`, `InventoryEquippedGearProvider`) |
| `Sheets.ValidateEditAsync`, `ApplyEditAsync`, `SnapshotAsync` | `SheetPatchValidator`, `CharacterSheetService.EnsureCatalogReferencesAsync`, `UpdateSheetHandler`, `ApproveChangeRequestHandler.ApplySheetPatchAsync`, `SheetPatchSnapshot` |
| `Sheets.BuildDetailAsync`, `BuildRosterLine` | `CharacterSheetService.BuildDetailAsync`, `BuildSummariesAsync` |
| `Creation.InitializeAsync` | `CreateCharacterHandler`, `StartingEquipmentResolver` |
| `Creation.Get/SaveOriginChoicesAsync` | `OriginChoicesHandler`, `OriginChoicesPlanner`, `OriginOptionDescriptions` |
| `Creation.PrepareActivationAsync` | `ActivateCharacterHandler`, rama `Activate` de `ApproveChangeRequestHandler` (`OriginChoicesPlanner.EnsureCompleteAsync`, `Character.Activate`, `SpellPreparationPlanner.RequireInitialPreparationAsync`) |
| `Progression.PlanAsync`, `ApplyAsync` | `GetLevelUpPlanHandler`, `ApplyLevelUpHandler`, `LevelUpPlanner`, `LevelUpRules`, `MulticlassRules`, `OptionCosts`, `SpellPreparationPlanner`/`SpellPreparationHandler` |
| `Progression.GrantAsync`, `RevokeAsync` | `PartyLevelHandler`, `Character.GrantLevelUp`/`RevokeLevelUp` |
| `Combat.BuildSummaryAsync` | `CombatSummaryBuilder`, `CombatCalculator`, `CompanionCalculator` |
| `Combat.ApplyDamageAsync`, `HealAsync`, `SetConditionsAsync` | `ApplyDamageHandler`, `UpdateCombatHandler`, `SetConcentrationHandler`, `PartyAdjustHandler`, `Character.ApplyDamage/Heal/ApplyCombatUpdate` |
| Seguimiento propio del sistema (espacios, recursos, acciones de clase, compañero) | `SpellSlotHandler`, `ResourceHandler`, `ClassActionHandler`, `SetCompanionHandler`, `CompanionTrackingHandler`, `CompanionPlanner` (endpoints del módulo, sin método en el contrato) |
| `Rests.ValidateRequestAsync`, `ApplyAsync` | `CreateRestRequestRequestValidator`, `RestHandler.ShortRestAsync/LongRestAsync`, `ApproveRestRequestHandler`, `CompanionPlanner.RestoreAfterLongRestAsync`, `RollOnRest` |
| `Catalog.LoadBasePackAsync` | `SrdSeeder`, `SrdDataset`, `SrdLevelChoices`, `SrdSpellCategories`, `SrdItemModifiers` (y `SrdBeastCatalog` en memoria) |
| `Catalog.ImportPackAsync`, `DeletePackAsync` | `ContentPackImporter`, `ContentPackValidator.*`, `ContentPackModels` |
| Lectura del catálogo | `ICatalogRepository`/`CatalogRepository`, los 19 handlers de `Application/Catalog` (endpoints del módulo) |
| `Choices.FindInvalidAsync`, `ReplaceInvalidAsync` | `InvalidChoicesPlanner`, `InvalidChoicesHandler`, `ChoiceValidity`, `LevelChoiceRule`, `ChoiceGrants` |
| `Party.BuildPartyAsync`, `RestAsync`, `AdjustAsync` | `GetPartyHandler` + `CharacterSheetService.BuildPartyAsync`, `PartyRestHandler`, `PartyAdjustHandler` |
| `Items.ValidateTemplate`, `DescribeEffective` | `ItemValidation` (`ItemRules`, `ItemTemplateInputValidator`), `EffectiveItem`, `InventoryView` |
| `Items.OnInventoryChangedAsync` | Llamadas a `ICharacterSheetService.RecalculateAsync` en `Inventory`, `PartyStash`, `Trading`, límite de sintonía en `InventoryOperations` |
| `Currency` | Constantes de pc en `Character`, `Campaign`, `Shop`; `copperToGoldText` en la app |
| `ChangeRequests` (`EditSheet`, `Companion`) | `ApproveChangeRequestHandler` (ramas `EditSheet` y `Companion`), `CompanionPlanner.ApplyApprovedAsync` |
| `RealtimeEventKinds` | `party.rest`, `levelUp.granted`, `levelUp.completed` en `CampaignEventTypes` |

### 4.3 Qué llama el núcleo y qué queda dentro del módulo

El núcleo **llama** a:

- `Sheets.CalculateManyAsync` + `BuildRosterLine` al listar personajes; `Sheets.BuildDetailAsync` en
  `GET /characters/{id}`; `Sheets.RecalculateAsync` tras cualquier operación de inventario, tienda,
  alijo o petición aprobada.
- `Creation.InitializeAsync` al crear y `Creation.PrepareActivationAsync` al activar.
- `ChangeRequests.SnapshotAsync`/`ApplyAsync` para los tipos que el sistema declara.
- `Rests.ValidateRequestAsync` al crear una petición de descanso y `Rests.ApplyAsync` al aprobarla.
- `Items.*` al validar homebrew, pintar inventario/tienda y al cambiar el equipo.
- `Catalog.LoadBasePackAsync` en el arranque e `ImportPackAsync`/`DeletePackAsync` desde
  `/admin/content-packs`.
- `Info` para `GET /systems`, la atribución y los documentos de sistema.

Queda **dentro** del módulo (endpoints propios, sin pasar por el núcleo salvo permisos y
notificación): catálogo y compendio, hoja (`/sheet`), orígenes, subida de nivel, preparación de
conjuros, elecciones inválidas, combate, daño, concentración, espacios, recursos, descansos directos,
acciones de clase, compañero y acciones de grupo. El módulo usa del núcleo: `ICampaignAccess`,
`CharacterLoader`/`CharacterWriter` (antes `CharacterTracker`), `ICampaignNotifier`,
`IDateTimeProvider`, `IDiceRoller`, `IUnitOfWork`, `ValueBreakdown`.

### 4.4 Tiempo real

El hub y `ICampaignNotifier` son núcleo. Los tipos de evento del núcleo son los de §1.2; el módulo
declara los suyos en `RealtimeEventKinds` y los publica con `ICampaignNotifier.PublishAsync(kind, …)`.
Los ids siguen siendo lo único que viaja (ADR 0006). La app recibe los desconocidos como
`Unknown(type, data)` y el núcleo los pasa a `GameSystemUi.onRealtimeEvent`.

### 4.5 Persistencia y migraciones

Un solo `AppDbContext` en el núcleo de infraestructura que, al construir el modelo, aplica las
configuraciones del núcleo y luego `ConfigureModel` de cada módulo registrado. Una sola historia de
migraciones. Las 32 migraciones existentes se quedan donde están (el `Designer` de cada una describe
el modelo completo, no se puede repartir sin reescribir historia). Esto contradice en parte la última
consecuencia del ADR 0009 ("las migraciones del catálogo y la ficha pasan a ser del módulo"): ver
§7 pregunta 1.

### 4.6 Rutas del sistema y alias (fase 32)

Regla: `/api/v1/<ruta>` de 5e → `/api/v1/systems/dnd5e/<ruta>`. Las rutas antiguas siguen
funcionando como **alias** (mismo handler, misma respuesta) con las cabeceras `Deprecation: true` y
`Link: </api/v1/systems/dnd5e/...>; rel="successor-version"`, hasta que la fase 33 publique una app
que use las nuevas y se publique como obligatoria (`AppRelease.IsMandatory`); entonces se borran
(fase 33 o siguiente, §7 pregunta 5).

| Ruta actual | Ruta nueva |
|---|---|
| `GET /api/v1/catalog/attribution` | `GET /api/v1/systems/dnd5e/catalog/attribution` |
| `GET /api/v1/catalog/classes`, `/classes/{index}` | `GET /api/v1/systems/dnd5e/catalog/classes`, `/classes/{index}` |
| `GET /api/v1/catalog/races`, `/races/{index}` | `GET /api/v1/systems/dnd5e/catalog/races`, `/races/{index}` |
| `GET /api/v1/catalog/spells`, `/spells/{index}` | `GET /api/v1/systems/dnd5e/catalog/spells`, `/spells/{index}` |
| `GET /api/v1/catalog/beasts`, `/beasts/{index}` | `GET /api/v1/systems/dnd5e/catalog/beasts`, `/beasts/{index}` |
| `GET /api/v1/catalog/items`, `/items/{id}` | `GET /api/v1/systems/dnd5e/catalog/items`, `/items/{id}` |
| `GET /api/v1/catalog/trinkets`, `/roll-tables`, `/conditions`, `/skills`, `/backgrounds` | `GET /api/v1/systems/dnd5e/catalog/trinkets`, `/roll-tables`, `/conditions`, `/skills`, `/backgrounds` |
| `GET /api/v1/catalog/equipment-categories/{index}`, `/features/{index}`, `/sources` | `GET /api/v1/systems/dnd5e/catalog/equipment-categories/{index}`, `/features/{index}`, `/sources` |
| `PATCH /api/v1/characters/{id}/sheet` | `PATCH /api/v1/systems/dnd5e/characters/{id}/sheet` |
| `GET`, `PUT /api/v1/characters/{id}/origin-choices` | `GET`, `PUT /api/v1/systems/dnd5e/characters/{id}/origin-choices` |
| `PATCH /api/v1/characters/{id}/combat` | `PATCH /api/v1/systems/dnd5e/characters/{id}/combat` |
| `POST /api/v1/characters/{id}/damage` | `POST /api/v1/systems/dnd5e/characters/{id}/damage` |
| `PUT`, `DELETE /api/v1/characters/{id}/companion`, `POST .../companion/hp` | `PUT`, `DELETE /api/v1/systems/dnd5e/characters/{id}/companion`, `POST .../companion/hp` |
| `POST /api/v1/characters/{id}/concentration` | `POST /api/v1/systems/dnd5e/characters/{id}/concentration` |
| `POST /api/v1/characters/{id}/spell-slots/{level}/spend`, `/restore` | `POST /api/v1/systems/dnd5e/characters/{id}/spell-slots/{level}/spend`, `/restore` |
| `POST /api/v1/characters/{id}/resources`, `/resources/{resourceId}/spend`, `/restore`, `/rolls`; `DELETE /resources/{resourceId}` | `/api/v1/systems/dnd5e/characters/{id}/resources…` (mismas subrutas) |
| `POST /api/v1/characters/{id}/rest/short`, `/rest/long` | `POST /api/v1/systems/dnd5e/characters/{id}/rest/short`, `/rest/long` |
| `GET`, `POST /api/v1/characters/{id}/spell-preparation`, `POST .../keep` | `/api/v1/systems/dnd5e/characters/{id}/spell-preparation…` |
| `GET`, `POST /api/v1/characters/{id}/invalid-choices` | `GET`, `POST /api/v1/systems/dnd5e/characters/{id}/invalid-choices` |
| `POST /api/v1/characters/{id}/class-actions/{action}` | `POST /api/v1/systems/dnd5e/characters/{id}/class-actions/{action}` |
| `GET`, `POST /api/v1/characters/{id}/level-up` | `GET`, `POST /api/v1/systems/dnd5e/characters/{id}/level-up` |
| `GET /api/v1/campaigns/{campaignId}/party`, `POST /rest`, `POST /adjust`, `POST`/`DELETE /grant-level` | `/api/v1/systems/dnd5e/campaigns/{campaignId}/party…` (mismas subrutas) |

Se quedan sin cambio (núcleo): personajes (lista, crear, detalle, enviar, activar, retrato, dueño,
borrar), inventario y dinero, peticiones de cambio, peticiones de descanso, homebrew de campaña,
tiendas, alijo, mensajes, sesiones, lore, mapas, biblioteca, ficheros, auth, admin, app, hub. Cada
ruta del sistema comprueba que `Campaign.SystemId` del personaje o la campaña sea `dnd5e`
(404 si no).

## 5. Contrato `GameSystemUi` (app)

### 5.1 Esquema

```dart
/// Lo que un sistema aporta a la app. El núcleo lo elige por `campaign.systemId`.
abstract class GameSystemUi {
  String get id;                                     // 'dnd5e'
  String get name;                                   // 'D&D 5e (SRD 5.1)'
  List<Attribution> get attributions;                // SRD CC-BY 4.0

  /// Rutas propias (subida de nivel, preparar conjuros, detalles del compendio…).
  List<RouteBase> routes(GlobalKey<NavigatorState> rootNavigatorKey);

  // Ficha
  Widget combatView(BuildContext context, SystemCharacter character,
      {required bool canEdit, required bool isDm, Widget? header});
  List<SheetTab> detailTabs(SystemCharacter character); // Resumen, Habilidades, Rasgos, Hechizos
  Widget? sheetEditorSection(SystemCharacter character);
  SystemCharacter parseCharacter(Map<String, dynamic> json); // campos del sistema del detalle

  // Creación y progresión
  Future<String?> openCreationWizard(BuildContext context, String campaignId);
  Widget? pendingActionsCard(SystemCharacter character); // nivel concedido, preparar, elecciones
  void openLevelUp(BuildContext context, String characterId);

  // Catálogo
  List<CompendiumTab> compendiumTabs();               // Hechizos, Objetos, Clases, Razas, Bestias…
  Widget itemExtras(EffectiveItemView item);           // daño, CA, rareza, sintonía
  Widget? itemFormSection(ItemFormController form);    // campos 5e del homebrew

  // Campaña
  Widget partyPanel(BuildContext context, String campaignId); // barra de acciones de grupo + roster
  String rosterSubtitle(CharacterSummaryView summary); // "Elfo · Mago 3"
  ChangeDetail? describeChangeRequest(ChangeRequestView request); // EditSheet, Companion

  // Dados, moneda, tiempo real
  RollClass classifyRoll(DiceResult result);           // crítico, pifia, normal
  String formatMoney(int minorUnits);                  // 5e: "12 po 5 pp"
  void onRealtimeEvent(WidgetRef ref, CampaignEvent event); // party.rest, levelUp.granted
}
```

### 5.2 Correspondencia con los widgets de hoy

| Miembro | Widgets actuales |
|---|---|
| `combatView` | `CombatView` (`HpCard`, `StatsCard`, `DeathSavesCard`, `ConditionsCard`, `AttacksSection`, `SpellsSection`, `SpellSlotsSection`, `ResourcesSection`, `ClassPanelsSection` y 12 paneles, `CompanionSection`, `ConsumablesSection`, `RestSection`) |
| `detailTabs` | `SummaryTab`, `SkillsTab`, `TraitsTab`, `SpellsTab` de `character_tabs.dart`; el núcleo añade Inventario (`InventoryTab`) y Notas (`NotesTab`), y "Sesión" en Mi sesión |
| `sheetEditorSection` | Parte 5e de `SheetEditorPage` (`SheetEditorForm`), `PointBuyDialog`, `SpellPickerPage` |
| `parseCharacter` | `CharacterDetail.fromJson` (campos 5e de `characters/data/models.dart`) |
| `openCreationWizard` | `CharacterWizardPage` y sus 9 pasos, `CharacterWizardController` |
| `pendingActionsCard` | Tarjetas de nivel concedido, preparación pendiente, elecciones inválidas, tiradas tras descanso en `CharacterPage` y `PlayerSessionPage` |
| `openLevelUp` | `LevelUpPage`, `LevelUpController`, pasos de `ui/level_up/` |
| `compendiumTabs` | Pestañas de `CompendiumPage` y páginas `SpellDetailPage`, `ItemDetailPage`, `ClassDetailPage`, `RaceDetailPage`, `BeastPage`, `FeatureDetailPage`, `RollTablePage` |
| `itemExtras` | Chips de daño/CA/rareza de `InventoryTab`, `EffectiveItemPage`, `AttunementDialog`, `isCombatUsable` |
| `itemFormSection` | Campos 5e de `ItemFieldsForm`, `ItemComposer` |
| `partyPanel` | `_ActionBar` de `DmSessionPage`, `PartyRoster`, `PartyMemberRow`, `DmCharacterSheet`, `PartyController`, `PartyRepository` |
| `rosterSubtitle` | Subtítulo de `CharacterCard` (`characters_tab.dart`) |
| `describeChangeRequest` | `SheetChangeDetail`, `payload_format.dart` |
| `classifyRoll` | Natural 20 / natural 1 en `DiceController` y `DiceSheet` |
| `formatMoney` | `copperToGoldText` |
| `onRealtimeEvent` | Ramas `PartyRest` y `LevelUpGranted` de `CampaignRealtime` |

### 5.3 Lo que el núcleo da al módulo

| Servicio | Proveedor o widget del núcleo |
|---|---|
| HTTP | `ApiClient` (`dio`, auth, servidor configurado, conectividad), `getCached` |
| Caché y offline | `ResponseCache`/`DriftResponseCache`, `staleDataProvider`, `OfflineAware`, `OfflineBannerLayout` (escrituras deshabilitadas sin red) |
| Dados | `DiceExpression`, `DiceController` (historial local), `rollAndShow`, `RollInputButton` |
| Tiempo real | `campaignRealtimeProvider` (invalidación por id) y `onRealtimeEvent` |
| Tema | `AppTokens` (sin colores fijos fuera de los tokens), `AppIcon`/`AppIcons`, `StoneCard`, `ParchmentCard`, `SectionHeader`, `RuneDivider`, tipografía, `motion/*` |
| Valores con desglose | `StatValue`, `BreakdownSheet`, `StatTileGrid`, `StatTile`, `OverrideMark` |
| Navegación | `GoRouter` (con las rutas del módulo), `AppRoutes`, `CampaignShell` |
| Contexto | Campaña (`CampaignDetail` con `systemId` y rol), personaje núcleo, `CharacterPermissions` |
| Inventario, tienda, alijo, peticiones de cambio y de descanso | Pantallas del núcleo con huecos para `itemExtras`, `describeChangeRequest` |

## 6. Paquetes y catálogo

1. **SRD como paquete base.** Hoy el SRD es un seed incrustado en `Dnd.Infrastructure`
   (`server/seed/srd/*.json` como `EmbeddedResource`, `SrdDataset`, `SrdSeeder`, registro
   `CatalogImport` con `Ruleset = "srd-5.1"`) y sus filas llevan `Source = "srd"`. En la fase 32 los
   JSON y el código se mueven al ensamblado del módulo sin cambiar de formato (`LoadBasePackAsync`
   hace lo que hace `SrdSeeder`). En la fase 34 el SRD se reescribe al formato de paquete 5e (v3) y se
   carga con el mismo importador que el PHB, con id `srd`, marcado como **base** (no se puede borrar
   ni desactivar). Las filas no cambian de `Source`, así que no hay migración de datos.
2. **`Campaign.SystemId`** (fase 31). Columna `SystemId varchar(32) NOT NULL DEFAULT 'dnd5e'` en
   `Campaigns`; migración `AddCampaignSystemId`; `CreateCampaignRequest.SystemId` opcional (por
   defecto `dnd5e`), validado contra los sistemas registrados; no se puede cambiar después de crear la
   campaña. `CampaignDto`/`CampaignSummaryDto` lo devuelven. `GET /api/v1/systems`.
3. **Paquetes por sistema** (fase 34). El registro de paquetes deja de ser `CatalogImports` con prefijo
   `pack:`: tabla `ContentPacks(Id varchar(60) PK, SystemId varchar(32), Name, Version, FormatVersion,
   IsBase bool, ImportedAt, CountsJson)`; migración `AddContentPacks` que copia las filas `pack:*` y
   `srd-5.1` de `CatalogImports` (y `CatalogImports` desaparece o queda solo para el control de versión
   del SRD; ver §7 pregunta 4). El JSON de paquete gana `"system": "dnd5e"` (por defecto `dnd5e`).
4. **Paquetes activos por campaña** (fase 34). Tabla `CampaignContentPacks(CampaignId FK, PackId FK,
   EnabledAt, EnabledByUserId, PK(CampaignId, PackId))`; migración `AddCampaignContentPacks` que
   activa en todas las campañas existentes todos los paquetes importados (comportamiento actual). El
   paquete base siempre cuenta como activo y no se guarda. Solo el Owner/DM cambia la lista
   (`GET/PUT /api/v1/campaigns/{id}/content-packs`, núcleo).
5. **Consultas filtradas.** Las consultas del catálogo del módulo reciben el conjunto de fuentes
   activas: `Source IN (base, activos de la campaña, 'homebrew' de esa campaña)`. Necesita contexto de
   campaña en las rutas del catálogo (§7 pregunta 6). Desactivar un paquete no borra nada: lo que ya
   usan personajes o inventarios sigue resolviéndose (como hoy al borrar un paquete, que marca
   `CatalogMissing`).

## 7. Riesgos y preguntas abiertas

### Riesgos

| Riesgo | Mitigación |
|---|---|
| `Character` (1 477 líneas + 4 parciales) mezcla núcleo y 5e en métodos que tocan ambos (p. ej. `Activate`, `LongRest` que recupera PG y recursos, `AdjustMoney` dentro de operaciones de inventario) | Fase 32 primero mueve ficheros y namespaces; la división de tabla y de clase es un paso propio y con tests verdes antes de seguir. |
| `CharacterDetailDto` plano: cualquier cambio de forma rompe la app instalada | Mantener el JSON idéntico en la 32 (§3.2); el cambio de forma solo con una app obligatoria. |
| Las 11 migraciones mixtas impiden historias separadas por módulo | Un solo contexto y una sola historia (§4.5). |
| `ItemTemplate`/`ItemOverrides` con 15 columnas 5e en tablas del núcleo | Fase 32 no toca columnas: el módulo mapea sus propiedades sobre la misma tabla (división de tabla también en `ItemTemplates`, y propiedades de entidad poseída en `CharacterItems`, `ShopItems`, `PartyStashItems`). |
| Tests: `Dnd.Domain.Tests` (35 ficheros, 21 de `Characters`) y `Dnd.Api.Tests` (73) asumen namespaces `Dnd.*` | Renombrado mecánico en el mismo commit que el código; los tests de 5e se mueven a `Systems.Dnd5e.*.Tests`. |
| Alias que se quedan para siempre | Fecha de retirada ligada a una release obligatoria (§4.6). |

### Preguntas abiertas

1. **Migraciones.** ¿Se acepta un único `AppDbContext` con modelo compuesto y una sola historia, en
   lugar de "migraciones del módulo" como dice el ADR 0009? Recomendación: sí, y enmendar la última
   consecuencia del ADR; separar historias solo si llega un sistema que se despliegue aparte.
2. **Forma de `CharacterDetailDto`.** ¿Plano para siempre (núcleo + campos del sistema al mismo
   nivel) o `{ ...núcleo, system: { ... } }` cuando haya un segundo sistema? Recomendación: plano en
   la 32 y la 33; decidir con el segundo sistema.
3. **Objetos.** ¿Las columnas 5e de `ItemTemplates` y de los overrides se quedan como columnas
   mapeadas por el módulo (recomendado para la 32) o pasan a un `SystemDataJson` (migración de datos,
   fase 34 o posterior)? ¿Puede un objeto de un sistema venderse en una campaña de otro? (No: la tienda
   filtra por el sistema de la campaña.)
4. **`CatalogImports`.** ¿Se sustituye por `ContentPacks` o se mantiene para la versión del SRD?
   Recomendación: sustituir en la 34, con el SRD como fila `IsBase`.
5. **Retirada de alias.** ¿En la misma fase 33 (con release obligatoria) o una fase después?
   Recomendación: en la release siguiente a la 33, marcada obligatoria.
6. **Compendio con varios sistemas.** El compendio es una rama global de la barra inferior y hoy no
   depende de la campaña. Con paquetes activos por campaña, ¿el compendio global muestra todo lo
   importado del sistema (como hoy) y el filtrado por campaña solo aplica a la ficha y al asistente?
   Recomendación: sí; el compendio global muestra el sistema por defecto de la instancia y un selector
   si hay más de uno.
7. **Dinero.** ¿Se renombra `CopperPieces`/`StashCopperPieces`/`PriceCp`/`CostCp` a nombres neutros
   (`Money`, `PriceMinor`) en la 32 o se deja el nombre y solo cambia el significado? Recomendación:
   dejar las columnas y renombrar solo las propiedades C#.
8. **`RestRequests.HitDiceJson` → `PayloadJson`.** ¿En la 32 o en la 34? (Rename de columna simple.)
9. **`HeightInches`/`WeightPounds` en el núcleo** con unidades imperiales: ¿se acepta y la unidad la
   presenta el sistema, o se pasa a cm/kg? Recomendación: se acepta; cambiarlo no aporta nada a 5e.

### Decisiones del revisor (2026-10-10)

Sobre las preguntas anteriores, en orden:

1. Sí: un único `AppDbContext` con modelo compuesto y una sola historia de migraciones. Se enmienda la
   última consecuencia del ADR 0009 en este mismo cambio.
2. `CharacterDetailDto` plano en las fases 32 y 33; la forma anidada se decide con el segundo sistema.
3. Las columnas 5e de objetos se quedan como columnas mapeadas por el módulo (fase 32); un
   `SystemDataJson` solo si la fase 34 lo necesita. La tienda filtra por el sistema de la campaña.
4. `CatalogImports` se sustituye por `ContentPacks` en la fase 34, con el SRD como fila base.
5. Los alias de rutas se retiran en la release siguiente a la fase 33, marcada obligatoria.
6. El compendio global muestra todo lo importado del sistema por defecto de la instancia (selector si
   hay más de uno); el filtro por paquetes activos aplica a la ficha, al asistente y a la tienda.
7. Dinero: las columnas conservan su nombre; se renombran solo las propiedades C# a nombres neutros
   en la fase 32.
8. `RestRequests.HitDiceJson` → `PayloadJson` en la fase 32, junto con el resto de renombrados.
9. Altura y peso siguen en pulgadas y libras en el núcleo; la unidad la presenta el sistema.

### Lista de comprobación para las fases 31–33

**Fase 31 — Sistema por campaña** (sin mover código)

1. `Campaign.SystemId` (dominio, configuración EF `HasMaxLength(32)`, valor por defecto `dnd5e`) y
   migración `AddCampaignSystemId`.
2. `IGameSystem` mínimo (`Id`, `Info`) en `Dnd.Application/Systems`, `Dnd5eSystem` que lo implementa
   con la atribución actual, registro en DI (`AddGameSystem<Dnd5eSystem>()`).
3. `GET /api/v1/systems`; `CreateCampaignRequest.SystemId` validado; `CampaignDto`,
   `CampaignSummaryDto` con `systemId`.
4. App: `CampaignSummary`/`CampaignDetail.systemId`; `CampaignFormDialog` muestra el sistema (un solo
   valor, sin selector si solo hay uno); `GetAttributionHandler` pasa a leer `Info.Attributions`.
5. Tests: campaña nueva con y sin `systemId`, sistema desconocido → 400, `GET /systems`.

**Fase 32 — División del servidor** (en este orden; cada paso compila y pasa los tests)

1. Crear proyectos `OpenTrpg.Core.Domain/Application/Infrastructure/Api` y
   `OpenTrpg.Systems.Dnd5e.Domain/Application/Infrastructure/Api` (más los de tests).
2. Mover los ficheros **Núcleo** de §1.1–§1.4 tal cual (cambio de namespace).
3. Mover los ficheros **5e** de §1.1–§1.4 y `server/seed/srd/` al módulo.
4. Partir los **Mixtos** según la columna "Reparto", en este orden: `Dice`/`AbilityRules`,
   `ValueBreakdown`, `SheetEditMode`, `ChangeRequestType` (enum → cadena), `CatalogSources`,
   `CatalogImport`, `ICampaignNotifier` (tipos registrables), `Character` (división de tabla §3.4),
   `CharacterDetailDto`/`CharacterSummaryDto` (§3.2), `SheetPatch` (`CharacterProfilePatch`),
   `RestRequest`, `ItemTemplate`/`ItemOverrides`/`CharacterItem`/`EffectiveItem`/`ItemDtos`/
   `ItemValidation`, `Inventory`/`PartyStash`/`Trading` (gancho `IItemSystem.OnInventoryChangedAsync`),
   `ChangeRequestHandlers` (despacho por tipo), `CharacterLifecycle`/`CreateCharacter`,
   `PartyHandlers`, `ContentPackImporter`, `SystemDocumentSeeder`, `StartupTasks`, `AppDbContext`
   (`ConfigureModel`), `DependencyInjection` de cada capa, `Program.cs`.
5. Completar `IGameSystem` (§4.1) delegando en los servicios actuales (§4.2).
6. Mapear los endpoints 5e bajo `/api/v1/systems/dnd5e` y los alias de §4.6; test que recorre la
   tabla de alias y compara respuestas.
7. Comprobar que `dotnet ef migrations add Comprobacion` no genera cambios (modelo idéntico) y
   borrarla.
8. Renombrar solución, imagen Docker y repositorio a OpenTRPG (ADR 0009).

**Fase 33 — División de la app** (en este orden)

1. Crear `packages/core` y `packages/dnd5e` (paquetes Dart locales) y que `app` dependa de ambos.
2. Mover `lib/core` salvo `ui/action_type.dart` y `ui/spell_category.dart` (a `dnd5e`).
3. Mover las carpetas **Núcleo** de §2.2 (`admin`, `auth`, `campaigns`, `home`, `library`, `lore`,
   `maps`, `server`, `sessions`, `settings`) y los ficheros núcleo de `dice`, `items`, `session`,
   `characters`, `catalog/ui/detail_widgets.dart`.
4. Mover a `dnd5e` los ficheros **5e** de §2.2 (`catalog` salvo los dos indicados, `characters` 5e,
   `items/domain/combat_usable.dart`, `items/ui/attunement_dialog.dart`, `session` 5e).
5. Partir los **Mixtos** de §2.2 según su reparto: `characters/data/models.dart`,
   `characters_repository`, `characters_controller`, `view_mode_controller`, `character_format`,
   `change_details`/`payload_format`, `character_page`, `character_detail_tabs`, `character_tabs`,
   `characters_tab`, `new_character_dialog`, `height_weight_fields`, `sheet_editor_page`,
   `items/data/models.dart` y los formularios, `inventory_tab`, `effective_item_page`,
   `session/data/models.dart`, `session_controllers`, `dm_session_page`, `player_session_page`,
   `change_requests_page`, `dice_controller`, `dice_sheet`, `compendium_page`, `attribution_page`,
   `realtime_events.dart`, `app_router.dart`.
6. Definir `GameSystemUi` (§5.1) e implementarlo en `Dnd5eUi` con los widgets de §5.2; el núcleo lo
   obtiene con `gameSystemUiProvider(campaign.systemId)`.
7. Cambiar las llamadas HTTP de 5e a `/api/v1/systems/dnd5e/...` (§4.6).
8. `flutter analyze` y `flutter test` sin cambios de comportamiento; las claves (`Key`) de los tests se
   mantienen.
