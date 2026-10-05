# Fase 4 — Personajes: hoja semiautomática, overrides y aprobación del DM

Contrato cerrado. Depende de las fases 1-3. La vista de combate (UI) es la fase 6, pero los
endpoints de seguimiento de combate se implementan aquí.

## Ciclo de vida y permisos

- Estados: `Draft` → `Active`. En `Draft` el dueño edita libremente. Al pasar a `Active`, las
  ediciones de hoja del dueño generan un `ChangeRequest` que el DM aprueba. El DM (al menos DM en la
  campaña) edita directamente en cualquier estado.
- Crear: cualquier miembro crea un personaje propio. Un DM puede crear uno para otro miembro
  (`ownerUserId`) o sin dueño (PNJ, `ownerUserId = null`).
- Ver: resumen de todos los personajes para todos los miembros; hoja completa solo dueño y DMs.
- Borrar: DM siempre; dueño solo en `Draft`.
- Activar: DM directo (`/activate`) o el dueño envía `/submit` → `ChangeRequest` tipo `Activate`.
- **Seguimiento de combate sin aprobación** (dueño y DM): HP, HP temporales, death saves,
  condiciones, agotamiento, concentración, inspiración, slots, recursos, descansos, dados de golpe.
- Un `ChangeRequest` pendiente se aplica al aprobar con la misma lógica que la edición directa.

## Entidades (Dnd.Domain/Characters)

```
Character            Id, CampaignId, OwnerUserId?, Name, Status (Draft|Active), RaceIndex?, SubraceIndex?,
                     BackgroundIndex?, Alignment?, ApplyRacialBonuses (bool, default true), HpMode (Average|Manual),
                     BaseStr, BaseDex, BaseCon, BaseInt, BaseWis, BaseCha (1-30),
                     HitPointsCurrent, TemporaryHitPoints, DeathSaveSuccesses, DeathSaveFailures (0-3),
                     ExhaustionLevel (0-6), ConditionsJson ([{index, note?}]), ConcentratingOnSpellIndex?,
                     Inspiration (bool), CopperPieces (int, dinero total en pc), HitDiceUsedJson ({classIndex: n}),
                     Notes, Backstory, PortraitFileId?, CreatedAt, UpdatedAt
CharacterClassLevel  Id, CharacterId, ClassIndex, SubclassIndex?, Level (1-20), Order (0 = clase principal)
CharacterProficiency Id, CharacterId, Type (Skill|SavingThrow|Armor|Weapon|Tool|Language), Key (índice o texto),
                     Expertise (bool), Source (Class|Race|Background|Manual)
CharacterSpell       Id, CharacterId, SpellIndex, ClassIndex, IsPrepared, AlwaysPrepared
SpellSlotState       Id, CharacterId, Level (1-9), Used; PactSlotsUsed se guarda como Level = 0
CharacterResource    Id, CharacterId, Key? (p. ej. "rage"), Name, Max, Used, Recharge (ShortRest|LongRest|Dawn|Manual),
                     IsAuto (bool: derivado de clase/nivel; se regenera al cambiar clases)
CharacterOverride    Id, CharacterId, Field (string), Value (int), Note?   — único (CharacterId, Field)
ChangeRequest        Id, CampaignId, CharacterId, RequestedByUserId, Type (Activate|EditSheet|AddItem|RemoveItem|
                     CustomItem|AdjustMoney|Other), PayloadJson, Status (Pending|Approved|Rejected|Cancelled),
                     ResolvedByUserId?, ResolvedAt?, Comment?, CreatedAt
```

Campos de override admitidos: `hitPointsMax`, `armorClass`, `speed`, `initiative`,
`proficiencyBonus`, `passivePerception`, `spellSaveDc`, `spellAttackBonus`, `ability.<str|dex|con|int|wis|cha>`
(sustituye la puntuación final), `save.<ability>`, `skill.<skillIndex>`.

## Cálculo de la hoja (Dnd.Domain/Characters/SheetCalculator, puro y testeable)

Entrada: `Character` + clases + competencias + overrides + datos del catálogo (raza, subraza,
clases con niveles) + equipo equipado (interfaz `IEquippedGear` con `ArmorClassBase`,
`AddDexModifier`, `MaxDexBonus`, `HasShield`; en esta fase se pasa vacío). Salida `CharacterSheet`:

- Puntuaciones finales = base + bonos raciales (raza + subraza) si `ApplyRacialBonuses`; override
  `ability.x` sustituye. Modificador = `AbilityRules.Modifier`.
- Nivel total = suma de niveles de clase. Bonificador de competencia = `AbilityRules.ProficiencyBonus`.
- Salvaciones = mod + PB si hay `SavingThrow` competente. Habilidades = mod de su característica
  + PB (×2 con `Expertise`). Percepción pasiva = 10 + Percepción. Iniciativa = mod Des.
- CA sin armadura = 10 + Des; Bárbaro: 10 + Des + Con; Monje: 10 + Des + Sab (si no lleva
  armadura ni escudo). Con armadura: `ArmorClassBase` + min(Des, `MaxDexBonus`) si `AddDexModifier`;
  escudo +2. Override `armorClass` sustituye.
- HP máximos (`HpMode = Average`): nivel 1 de la clase principal = dado de golpe máximo + Con; cada
  nivel adicional = (dado/2 + 1) + Con (mínimo 1 por nivel). `Manual` exige override `hitPointsMax`.
- Dados de golpe por clase: `Level` de cada clase menos los usados.
- Velocidad = raza (subraza no cambia) salvo override.
- Lanzamiento de conjuros: por cada clase con `SpellcastingAbility`: CD = 8 + PB + mod; ataque = PB +
  mod. Slots: una sola clase lanzadora → tabla de `ClassLevel.SpellSlots`; multiclase → nivel de
  lanzador = suma de (full casters: nivel; `SpellcastingLevel = 2`: nivel/2; `= 3`: nivel/3, todo
  redondeado hacia abajo) y tabla multiclase del SRD. Brujo (`warlock`) aparte: Pact Magic con los
  slots de su tabla (todos del nivel máximo), `Level = 0` en `SpellSlotState`.
  Preparados máximos (informativo): clérigo/druida = mod + nivel; paladín = mod + nivel/2; mago = Int + nivel.
- Recursos automáticos (`IsAuto`) por clase y nivel, regenerados al cambiar clases (se conserva
  `Used` si la `Key` ya existía):

| Clase | Key | Max | Recarga |
|-------|-----|-----|---------|
| barbarian | rage | `class_specific.rage_count` (999 = ilimitado en nivel 20) | LongRest |
| bard | bardic-inspiration | max(1, mod Car) | LongRest (ShortRest desde nivel 5) |
| cleric | channel-divinity | 1 (2 en nivel 6, 3 en 18) | ShortRest |
| druid | wild-shape | 2 | ShortRest |
| fighter | second-wind | 1 | ShortRest |
| fighter | action-surge | 1 (2 en nivel 17) | ShortRest |
| fighter | indomitable | 1 (nivel 9), 2 (13), 3 (17) | LongRest |
| monk | ki | nivel (desde nivel 2) | ShortRest |
| paladin | lay-on-hands | 5 × nivel | LongRest |
| paladin | channel-divinity | 1 (desde nivel 3) | ShortRest |
| sorcerer | sorcery-points | nivel (desde nivel 2) | LongRest |
| wizard | arcane-recovery | 1 | LongRest |
| warlock | — (los slots de pacto cubren el recurso) | | |
| rogue, ranger | sin recursos automáticos | | |

- Descanso corto: recursos `ShortRest` a 0 usados; brujo recupera slots de pacto; gastar dados de
  golpe (`{classIndex: n}`): cada uno cura dado + Con (mínimo 0), nunca por encima del máximo.
- Descanso largo: HP al máximo, slots a 0 usados, recursos `ShortRest` y `LongRest` repuestos,
  recupera dados de golpe = max(1, nivel total / 2), agotamiento −1, death saves a 0, concentración
  fuera. HP temporales se mantienen.

## Endpoints (`/api/v1`, requieren JWT; autorización vía `ICampaignAccess`)

| Método | Ruta | Body | Respuesta |
|--------|------|------|-----------|
| GET | `/campaigns/{id}/characters` | — | `200 CharacterSummaryDto[]` |
| POST | `/campaigns/{id}/characters` | `{ name, ownerUserId? }` | `201 CharacterDetailDto` (Draft, puntuaciones 10) |
| GET | `/characters/{id}` | — | `200 CharacterDetailDto` · `403` si no es dueño/DM |
| PATCH | `/characters/{id}/sheet` | `SheetPatch` | `200 CharacterDetailDto` (DM o dueño en Draft) · `202 ChangeRequestDto` (dueño en Active) |
| POST | `/characters/{id}/submit` | — | `201 ChangeRequestDto` tipo Activate (dueño, Draft) |
| POST | `/characters/{id}/activate` | — | `200 CharacterDetailDto` (DM) |
| DELETE | `/characters/{id}` | — | `204` |
| PATCH | `/characters/{id}/combat` | `{ hitPointsCurrent?, temporaryHitPoints?, deathSaveSuccesses?, deathSaveFailures?, exhaustionLevel?, conditions?, inspiration? }` | `200 CharacterDetailDto` |
| POST | `/characters/{id}/concentration` | `{ spellIndex: string\|null }` | `200` |
| POST | `/characters/{id}/spell-slots/{level}/spend` · `/restore` | `{ amount = 1 }` | `200` · `400` sin slots |
| POST | `/characters/{id}/resources/{resourceId}/spend` · `/restore` | `{ amount = 1 }` | `200` · `400` |
| POST | `/characters/{id}/resources` | `{ name, max, recharge }` | `201` recurso manual (dueño o DM) |
| DELETE | `/characters/{id}/resources/{resourceId}` | — | `204` solo manuales |
| POST | `/characters/{id}/rest/short` | `{ hitDice: { classIndex: n } }` | `200 CharacterDetailDto` |
| POST | `/characters/{id}/rest/long` | — | `200 CharacterDetailDto` |
| GET | `/campaigns/{id}/change-requests?status=Pending` | — | `200 ChangeRequestDto[]` (DM: todas; jugador: las suyas) |
| GET | `/change-requests/{id}` | — | `200` |
| POST | `/change-requests/{id}/approve` | `{ comment? }` | `200 ChangeRequestDto` (DM) · `409` si no está pendiente |
| POST | `/change-requests/{id}/reject` | `{ comment }` | `200` (DM) |
| POST | `/change-requests/{id}/cancel` | — | `200` (solicitante, pendiente) |

```
SheetPatch { name?, raceIndex?, subraceIndex?, backgroundIndex?, alignment?, applyRacialBonuses?, hpMode?,
             baseAbilities? { str, dex, con, int, wis, cha }, classes? [{ classIndex, subclassIndex?, level }],
             proficiencies? [{ type, key, expertise }], spells? [{ spellIndex, classIndex, isPrepared, alwaysPrepared }],
             overrides? [{ field, value, note? }], notes?, backstory?, copperPieces? }
             — cada lista enviada sustituye por completo a la existente; los campos ausentes no cambian.
CharacterSummaryDto { id, campaignId, ownerUserId, ownerDisplayName, name, status, raceName, classes: [{ classIndex, className, subclassName?, level }],
                      level, hitPointsCurrent?, hitPointsMax?, portraitUrl? }   — HP solo para dueño/DM
CharacterDetailDto  { ...todos los campos almacenados en camelCase, classes, proficiencies, spells, overrides, resources,
                      spellSlots: [{ level, max, used }], sheet: CharacterSheetDto, pendingChangeRequests: ChangeRequestDto[] }
CharacterSheetDto   { abilities: { str: { score, modifier, overridden }, ... }, proficiencyBonus, savingThrows: { str: { value, proficient }, ... },
                      skills: [{ index, name, ability, value, proficient, expertise }], passivePerception, initiative, armorClass, speed,
                      hitPointsMax, hitDice: [{ classIndex, die, total, remaining }], spellcasting: [{ classIndex, ability, saveDc, attackBonus,
                      preparedMax? }], overriddenFields: string[] }
ChangeRequestDto    { id, campaignId, characterId, characterName, requestedByUserId, requestedByDisplayName, type, payload (objeto), status,
                      resolvedByDisplayName?, resolvedAt?, comment?, createdAt }
```

## Cliente Flutter

- `features/characters`: lista por campaña (tarjeta: nombre, raza, clases/nivel, estado, HP si
  visible), crear, y **vista detallada** con pestañas Resumen (características con modificador,
  PB, salvaciones, CA, iniciativa, velocidad, HP, inspiración), Habilidades y competencias, Rasgos
  (rasgos de clase por nivel y de raza desde el catálogo), Hechizos (por nivel, preparado/no,
  slots), Inventario (vacío hasta la fase 5), Notas.
- **Editor de hoja** (semiautomático): selectores de raza/subraza/trasfondo/clase/subclase/nivel
  desde el catálogo, puntuaciones base con compra por puntos opcional o entrada manual, casillas de
  competencias en habilidades (muestra las opciones de la clase), selección de hechizos filtrada
  por clase y nivel, lista de overrides con nota. Al guardar: si responde 202, mostrar "Enviado al
  DM para aprobación".
- Icono junto a cada valor con override; toque largo muestra la nota.
- `features/change_requests`: lista para el DM con aprobar/rechazar (comentario), diff legible del
  payload; para el jugador, estado de las suyas y cancelar.
- Tests de widget: hoja muestra modificadores calculados del DTO; guardar en Active muestra el
  mensaje de aprobación; DM aprueba y la solicitud desaparece de pendientes.

## Pruebas del servidor

`SheetCalculator` (Domain.Tests): bárbaro 1 Fue 16 Con 14 → HP 14, CA sin armadura 10+Des+Con;
mago 5 Int 18 → CD 15, ataque +7, slots [4,3,2,…]; multiclase paladín 4 / mago 2 → nivel lanzador 4
→ slots [4,3,0…]; brujo 3 → 2 slots de pacto nivel 2; override `hitPointsMax` sustituye; pícaro
con pericia en Sigilo nivel 1 Des 16 → +7; percepción pasiva.
Integración: jugador en Draft edita → 200; tras activar edita → 202 y el DM aprueba → la hoja
cambia; jugador no DM no puede aprobar (403); descanso largo repone HP y slots; gastar un slot sin
disponibles → 400; miembro que no es dueño ni DM recibe 403 en `/characters/{id}`; recursos
automáticos de bárbaro nivel 3 → rage max 3.
