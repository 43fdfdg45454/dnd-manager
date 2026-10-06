# Fase 11 — Modificadores estructurados de ítems, HP máximo y escudo real

Contrato cerrado. Hoy la hoja solo lee de los ítems equipados la CA de la armadura y un +2 fijo por
escudo; un ítem "+3 Destreza" no cuenta. Tras esta fase los ítems (SRD, homebrew o con overrides)
modifican la hoja y el combate de forma estructurada.

## Dominio (Opus)

### `ItemModifier` (`server/src/Dnd.Domain/Items/ItemModifier.cs`)

```csharp
public enum ItemModifierKind
{
    AbilityBonus,      // Target = "str".."cha", Value = ±n (se suma)
    AbilitySet,        // Target = ability, Value = puntuación fija; aplica solo si es mayor que la calculada
    SaveBonus,         // Target = ability o null (= todas), Value = ±n
    SkillBonus,        // Target = índice de habilidad o null (= todas), Value = ±n
    ArmorClassBonus,   // Value = ±n (se suma a la CA calculada o fijada por armadura)
    AttackBonus,       // Value = ±n (armas: solo ese arma; otros objetos: todos los ataques)
    DamageBonus,       // ídem
    SpeedBonus,        // Value = ±pies
    HitPointsMaxBonus, // Value = ±n
    InitiativeBonus,   // Value = ±n
}

public sealed record ItemModifier(ItemModifierKind Kind, string? Target, int Value)
```

- Validación en `ItemModifier.Normalize()` / `Validate()`: `Kind` definido; `Target` obligatorio y
  válido para `AbilityBonus`/`AbilitySet` (`Abilities.All`), opcional y válido para `SaveBonus`
  (`Abilities.All`) y `SkillBonus` (índice de habilidad en minúsculas, sin validar contra catálogo);
  nulo para el resto. `Value` entre `ItemLimits.MinModifier = -10` y `MaxModifier = 30`
  (`AbilitySet` entre 1 y 30). Máximo `ItemLimits.MaxModifiers = 10` por ítem.
- Lista `Modifiers` (`IReadOnlyList<ItemModifier>`, vacía por defecto) en `ItemTemplateData`,
  `ItemTemplate` (columna `jsonb` como `Properties`/`Effects`) y `ItemOverrides` (nullable: null =
  conserva los del template; lista vacía = quita todos). `ItemOverrides.IsEmpty/Copy/Normalize`
  actualizados. Migración `AddItemModifiers`.
- `EffectiveItem.Modifiers` = `overrides.Modifiers ?? template?.Modifiers ?? []`.
  `AttackBonus`/`DamageBonus` sueltos de `ItemOverrides` se mantienen por compatibilidad: al
  resolver, se convierten en modificadores `AttackBonus`/`DamageBonus` añadidos a la lista si no es
  nulo (la UI nueva ya no los edita; los existentes siguen funcionando).
- `EffectiveItem.IsEquippable`: `Weapon`, `Armor`, `Shield` **y** `MagicItem` (anillos, capas,
  guanteletes se "llevan puestos"). Los consumibles y el resto no.
- Cuándo aplica un modificador (`EffectiveItem.IsActiveFor(CharacterItem item)`): el ítem está
  `Equipped` y, si `RequiresAttunement`, además `Attuned`.

### Cálculo de hoja

- `EquippedGear` se amplía (o se sustituye por `EquippedGear` + `IReadOnlyList<ActiveModifier>`):
  `ActiveModifier(string ItemName, ItemModifier Modifier, bool FromWeapon)`.
  `EquippedGear.FromEquipped(IEnumerable<(EffectiveItem item, bool attuned)>)` recoge armadura,
  escudo (**su `ArmorClassBase`**, ya no +2 fijo; sin valor → 2 por defecto para escudos sin dato) y
  todos los modificadores activos.
- `InventoryEquippedGearProvider` pasa `Attuned` y el nombre.
- `SheetCalculator`:
  - Características: `computed = base + raciales + Σ AbilityBonus`; luego
    `computed = max(computed, AbilitySet)` por cada set; luego override; clamp 1–30.
  - Salvaciones y habilidades: `+ Σ SaveBonus/SkillBonus` con target igual o nulo (antes del override).
  - CA: `CalculateArmorClass(...) + Σ ArmorClassBonus` (el bonus también se aplica sobre la CA
    sin armadura; el override de `armorClass` sigue mandando).
  - Velocidad, iniciativa y HP máx: `+ Σ` correspondientes (el bonus de HP máx se aplica sobre el
    cálculo medio y **también** sobre el override `hitPointsMax`, porque el override representa los
    HP "propios"; documentarlo en el XML doc).
  - `CharacterSheet.ItemEffects: IReadOnlyList<AppliedItemEffect(string ItemName, ItemModifierKind Kind, string? Target, int Value)>`
    con todo lo aplicado (excluidos AttackBonus/DamageBonus de armas, que van al combate).
- `CombatCalculator`: cada ataque suma `AttackBonus`/`DamageBonus` del propio arma **más** los de
  los demás ítems activos que no sean armas (p. ej. un anillo "+1 a ataques"). El tipo `AttackLine`
  (o equivalente) añade `Breakdown` textual: `"+5 = DES +3, competencia +2"` y
  `"1d8+4 = DES +3, Espada +1"`, para la vista de combate.
- Tests en `Dnd.Domain.Tests`: AbilityBonus suma; AbilitySet 19 sobre 12 sube, sobre 20 no; ítem
  con sintonía requerida sin sintonizar no aplica; escudo con `ArmorClassBase = 3` (+1) da +3;
  ArmorClassBonus con y sin armadura; SaveBonus con target nulo aplica a las seis; HP máx bonus
  sobre override; CombatCalculator con arma +1 y anillo +1.

### Seed SRD (`server/src/Dnd.Infrastructure/Catalog/SrdItemModifiers.cs`)

Tabla estática `índice → modificadores` aplicada en `SrdDataset.MapMagicItem`/`MapEquipment`
(solo si el índice existe en el dataset; no fallar si falta). Incluir al menos:
`gauntlets-of-ogre-power` (AbilitySet str 19), `headband-of-intellect` (int 19),
`amulet-of-health` (con 19), `belt-of-hill-giant-strength` (21), `belt-of-stone-giant-strength` y
`belt-of-frost-giant-strength` (23), `belt-of-fire-giant-strength` (25), `belt-of-cloud-giant-strength` (27),
`belt-of-storm-giant-strength` (29), `cloak-of-protection` (ArmorClassBonus +1, SaveBonus null +1),
`ring-of-protection` (ídem), `bracers-of-defense` (ArmorClassBonus +2), `ioun-stone-*` no (texto),
`boots-of-striding-and-springing` (SpeedBonus... no: velocidad mínima 30; omitir), `weapon-+1/+2/+3`
(`AttackBonus` y `DamageBonus`; comprobar índices reales del dataset, p. ej. `weapon-1`),
`armor-+1/+2/+3` y `shield-+1/+2/+3` (`ArmorClassBonus`). Subir `SrdDataset.Version`.
Test de seed: `gauntlets-of-ogre-power` tiene un `AbilitySet str 19`.

## Aplicación y API

- `ItemTemplateDto`, `EffectiveItemDto`, `ItemOverridesDto` (crear/editar homebrew, añadir ítem
  avanzado, tienda) ganan `modifiers: [{ kind, target, value }]` (kind como string del enum).
  Validación FluentValidation delega en `ItemModifier.Validate`.
- `CharacterSheetDto` gana `itemEffects: [{ itemName, kind, target, value }]`.
- `AttackDto` del resumen de combate gana `attackBreakdown` y `damageBreakdown` (string).
- Cambios de equipar/sintonizar ya recalculan la hoja (comprobar que `RecalculateAsync` se invoca
  tras `PATCH /inventory/{itemId}`; si no, añadirlo).
- Tests de integración: homebrew con `AbilityBonus dex +3` equipado sube DES en
  `GET /characters/{id}`; al desequipar baja; ítem con sintonía requerida solo aplica sintonizado.

## Cliente (Sonnet)

- Modelos Dart: `ItemModifier {kind, target, value}` en `ItemTemplate`, `EffectiveItem`,
  `ItemOverrides` (serialización null/lista vacía respetada); `CharacterSheet.itemEffects`;
  `Attack.attackBreakdown/damageBreakdown`.
- `item_fields_form.dart`: sección "Modificadores" con filas (tipo ▾, objetivo ▾ cuando aplica,
  valor) y botón "Añadir modificador"; textos en español (`Bonus a característica`, `Fijar
  característica`, `Bonus a salvación`, `Bonus a habilidad`, `Bonus a CA`, `Bonus de ataque`,
  `Bonus de daño`, `Velocidad`, `PG máximos`, `Iniciativa`). Se usa en homebrew, añadir avanzado y
  tienda. Los campos sueltos "Bono de ataque/daño" desaparecen del formulario (se muestran como
  modificadores si vienen del servidor).
- `effective_item_page.dart` y `item_detail_page.dart`: lista "Efectos" con los modificadores
  legibles ("+3 Destreza", "Fuerza 19", "+1 CA", "+1 a todas las salvaciones").
- Hoja: `OverrideMark` se generaliza a `ValueMark` con dos variantes: override (como hoy) y
  "afectado por objeto" (icono distinto, tooltip con el nombre del ítem y el efecto); se aplica en
  características, salvaciones, habilidades, CA, velocidad, iniciativa y HP máx usando
  `sheet.itemEffects`.
- Combate: cada ataque muestra el desglose bajo el bonus/daño.
- HP máximo de primera clase: en el editor, sección "Puntos de golpe", campo numérico "PG máximos"
  (vacío = calculado; valor = override `hitPointsMax`) con texto de ayuda "Calculado: N"; en
  `HpCard` (vista de combate) tocar "/ máx" abre un diálogo para fijarlo o volver al cálculo
  (`Key('hp-max-edit')`), que guarda vía `saveSheet` con solo ese override. Para DM/Owner es
  directo; para el jugador en personaje activo crea solicitud (comportamiento ya existente).
- Tests: parseo de `itemEffects` y `modifiers`; formulario añade/quita filas y serializa;
  marca "afectado por objeto" visible en DES; `hp-max-edit` envía el override.

## Verificación

`cd server && dotnet build && dotnet test`; `cd app && flutter analyze && flutter test`. Manual:
crear homebrew "Guantes ágiles" con `AbilityBonus dex +3`, añadirlo, equipar → DES +3 en la hoja
con la marca; desequipar → vuelve.
