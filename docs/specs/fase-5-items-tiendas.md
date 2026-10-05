# Fase 5 — Ítems, inventario, homebrew y tiendas

Contrato cerrado. Depende de las fases 3 (`ItemTemplate`) y 4 (`Character`, `ChangeRequest`,
`ICampaignAccess`).

## Reglas

- Los ítems de un personaje pertenecen a la campaña: `CharacterItem.CampaignId` siempre igual a
  la del personaje.
- **Operaciones del jugador sin aprobación**: comprar/vender en tienda abierta, equipar/desequipar,
  sintonizar (máx. 3), usar consumible (resta 1 a `Quantity` o 1 carga), reordenar, anotar.
- **Con aprobación (ChangeRequest)**: añadir o quitar ítems a mano (`AddItem`/`RemoveItem`),
  crear un ítem personalizado para el personaje (`CustomItem`), ajustar dinero fuera de una
  compra/venta (`AdjustMoney`). El DM lo hace directo. En `Draft` el dueño también lo hace directo
  (equipo inicial).
- **Homebrew de campaña** (`ItemTemplate.CampaignId = campaña`): solo al menos DM crea/edita/borra.
  Visible para todos los miembros al buscar ítems dentro de la campaña.
- **Creación rápida** = `templateId` sin overrides. **Creación avanzada** = `templateId` +
  overrides, o sin plantilla (todos los campos a mano). Los overrides son campos opcionales que
  sustituyen a los de la plantilla al calcular el ítem efectivo (`EffectiveItem`).
- Dinero en piezas de cobre (`CopperPieces`); la UI muestra pp/gp/ep/sp/cp y acepta entrada en gp.
- **Compra**: atómica. Requiere tienda abierta, stock disponible (si no es ilimitado), dinero
  suficiente. Resta dinero, resta stock, crea/acumula `CharacterItem` (acumula si misma plantilla
  sin overrides y apilable) y registra `Transaction`. **Venta**: la tienda define
  `BuybackPercent` (por defecto 50); el jugador vende un ítem no sintonizado; suma dinero, resta
  cantidad, suma stock si la tienda rastrea stock de ese ítem; registra `Transaction`.
- Peso total y capacidad (Fue × 15) informativos en la hoja.
- CA: el `SheetCalculator` de la fase 4 recibe ahora el equipo equipado (armadura + escudo).

## Entidades (Dnd.Domain/Items)

```
CharacterItem   Id, CharacterId, CampaignId, TemplateId?, Quantity (≥1), Equipped, Attuned, Charges?, ChargesMax?,
                SortOrder, Notes?, Overrides (owned): Name?, Description[]?, Category?, DamageDice?, DamageType?,
                VersatileDice?, Properties[]?, RangeNormal?, RangeLong?, ArmorClassBase?, AddDexModifier?, MaxDexBonus?,
                StrengthMinimum?, StealthDisadvantage?, WeightLb?, Rarity?, RequiresAttunement?, AttackBonus?, DamageBonus?,
                Effects[]? (texto libre: "+1 a ataque y daño", "Luz 20 ft"), CreatedAt, UpdatedAt
Shop            Id, CampaignId, Name, Description?, IsOpen, BuybackPercent (0-100), CreatedAt, UpdatedAt
ShopItem        Id, ShopId, TemplateId?, Overrides (mismo owned type que CharacterItem), PriceCp, Stock? (null = ilimitado), SortOrder
Transaction     Id, CampaignId, ShopId, CharacterId, Type (Purchase|Sale), ItemName, Quantity, TotalCp, At
```

`EffectiveItem` (Domain, puro): plantilla ∪ overrides → nombre, categoría, daño, propiedades, CA,
peso, rareza, bonos, efectos, `IsCustom` (sin plantilla u overrides no vacíos), `StackableHint`
(consumibles, munición y objetos de aventura sin cargas son apilables).

## Endpoints (`/api/v1`, JWT)

### Catálogo de campaña (SRD + homebrew)

| Método | Ruta | Body | Respuesta |
|--------|------|------|-----------|
| GET | `/campaigns/{id}/items?search=&category=&rarity=&source=all\|srd\|homebrew&page&pageSize` | — | `200 Page<ItemSummaryDto>` miembros |
| POST | `/campaigns/{id}/items` | `ItemTemplateInput` | `201 ItemDetailDto` (≥ DM) |
| PATCH | `/campaigns/{id}/items/{templateId}` | `ItemTemplateInput` parcial | `200` (≥ DM; solo homebrew de esa campaña) |
| DELETE | `/campaigns/{id}/items/{templateId}` | — | `204` (≥ DM; `409` si lo usa algún ítem o tienda) |

### Inventario

| Método | Ruta | Body | Respuesta |
|--------|------|------|-----------|
| GET | `/characters/{id}/inventory` | — | `200 InventoryDto { items: CharacterItemDto[], copperPieces, totalWeightLb, carryCapacityLb, attunedCount }` |
| POST | `/characters/{id}/inventory` | `{ templateId?, quantity, overrides? }` | `201 CharacterItemDto` (DM o dueño en Draft) · `202 ChangeRequestDto` (dueño Active → `AddItem`/`CustomItem`) |
| PATCH | `/characters/{id}/inventory/{itemId}` | `{ equipped?, attuned?, notes?, sortOrder?, charges? }` | `200` (dueño o DM) · `400` > 3 sintonizados o no equipable |
| POST | `/characters/{id}/inventory/{itemId}/use` | `{ amount = 1 }` | `200` resta cantidad o cargas; borra si llega a 0 y era consumible |
| DELETE | `/characters/{id}/inventory/{itemId}` | `{ quantity? }` | `204` (DM o Draft) · `202` (`RemoveItem`) |
| POST | `/characters/{id}/money` | `{ deltaCp, reason }` | `200` (DM o Draft) · `202` (`AdjustMoney`) |

### Tiendas

| Método | Ruta | Body | Respuesta |
|--------|------|------|-----------|
| GET | `/campaigns/{id}/shops` | — | `200 ShopSummaryDto[]` (jugadores solo ven abiertas) |
| POST | `/campaigns/{id}/shops` | `{ name, description?, buybackPercent? }` | `201` (≥ DM) |
| GET | `/shops/{id}` | — | `200 ShopDto` con ítems efectivos y precios · `404` cerrada para jugador |
| PATCH | `/shops/{id}` | `{ name?, description?, isOpen?, buybackPercent? }` | `200` (≥ DM) |
| DELETE | `/shops/{id}` | — | `204` (≥ DM) |
| POST | `/shops/{id}/items` | `{ templateId?, overrides?, priceCp, stock? }` | `201` (≥ DM) |
| PATCH | `/shops/{id}/items/{shopItemId}` | `{ priceCp?, stock?, overrides?, sortOrder? }` | `200` (≥ DM) |
| DELETE | `/shops/{id}/items/{shopItemId}` | — | `204` (≥ DM) |
| POST | `/shops/{id}/buy` | `{ characterId, shopItemId, quantity = 1 }` | `200 { inventory, transaction }` · `400` sin dinero/stock · `403` personaje ajeno · `409` cerrada |
| POST | `/shops/{id}/sell` | `{ characterId, itemId, quantity = 1 }` | `200 { inventory, transaction }` · `400` sintonizado/cantidad |
| GET | `/campaigns/{id}/transactions?characterId=&page` | — | `200 Page<TransactionDto>` (DM todas; jugador las de sus personajes) |

```
ItemTemplateInput  { name, category, subcategory?, rarity?, requiresAttunement, costCp?, weightLb?, damageDice?, damageType?,
                     versatileDice?, properties[], rangeNormal?, rangeLong?, armorClassBase?, addDexModifier?, maxDexBonus?,
                     strengthMinimum?, stealthDisadvantage, description[], effects[] }
CharacterItemDto   { id, templateId?, templateName?, quantity, equipped, attuned, charges?, chargesMax?, notes?, sortOrder,
                     overrides: {…solo los definidos}, effective: EffectiveItemDto, isCustom }
EffectiveItemDto   { name, category, subcategory?, rarity?, requiresAttunement, weightLb?, damage?: { dice, type, versatile? },
                     properties[], range?: { normal, long? }, armor?: { base, addDex, maxDex?, strengthMinimum?, stealthDisadvantage },
                     attackBonus, damageBonus, effects[], description[] }
ShopDto            { id, name, description, isOpen, buybackPercent, items: [{ id, priceCp, stock?, effective: EffectiveItemDto, templateId? }] }
TransactionDto     { id, shopName, characterName, type, itemName, quantity, totalCp, at }
```

## Cliente Flutter

- Pestaña **Inventario** del personaje: dinero (editable → `/money`, con aviso de aprobación),
  peso/capacidad, lista agrupada (equipado, mochila, consumibles) con acciones por ítem: equipar,
  sintonizar, usar, ver detalle efectivo (con sus overrides marcados), notas, quitar.
- **Añadir ítem**: diálogo con dos pestañas: *Rápido* (buscador del catálogo de campaña, cantidad)
  y *Avanzado* (elige plantilla opcional y muestra formulario con todos los campos precargados y
  editables; o "desde cero"). Al guardar, 202 → "Enviado al DM".
- **Homebrew** (≥ DM): en la campaña, sección "Objetos de la campaña" con lista, crear/editar/borrar
  usando el mismo formulario avanzado.
- **Tiendas**: lista en la campaña; detalle con ítems, precio formateado, stock, botón "Comprar"
  (selector de personaje propio y cantidad; confirma total) y "Vender" desde el inventario cuando
  hay una tienda abierta. El DM edita tienda e ítems y abre/cierra con un interruptor.
- Tests de widget: comprar descuenta dinero mostrado; ítem con override muestra el marcador;
  jugador Active al añadir ve el mensaje de aprobación.

## Pruebas del servidor

`EffectiveItem`: overrides sustituyen solo lo definido; `IsCustom`. Compra: descuenta dinero y
stock, acumula apilables, falla sin dinero (400) y sin stock (400), tienda cerrada (409), personaje
de otro jugador (403); dos compras concurrentes del último de stock → solo una pasa (usar
`RowVersion`/concurrencia optimista en `ShopItem` y `Character`). Venta: suma `BuybackPercent`.
Sintonizar un cuarto ítem → 400. CA con cota de malla (16, sin Des) + escudo = 18 en el
`SheetCalculator`. Jugador Active: añadir ítem → 202 y `ChangeRequest` `AddItem`; DM aprueba →
aparece en inventario.
