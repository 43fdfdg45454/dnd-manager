# Fase 17 — Equipo inicial estructurado

Contrato cerrado. Hoy el paso "Equipo inicial" del asistente de creación solo muestra el texto del
SRD y el jugador añade cada objeto a mano. El dataset SRD ya trae el equipo fijo y las opciones
(a)/(b) estructuradas por clase y trasfondo; esta fase las usa.

Decisiones: el jugador confirma su equipo en el asistente y entra directo (el personaje es borrador:
las ediciones del dueño no pasan por aprobación). La activación del personaje por el DM no cambia.
Riqueza inicial alternativa (PHB pág. 143, `[SRD]` en la skill): tirada física, el jugador escribe el
resultado. `[DUDA]` de la skill: con la alternativa de oro, opción "conservar el equipo del
trasfondo" (desactivada por defecto, como dice el libro).

## Servidor (Opus)

### Modelo normalizado (`StartingEquipment`, JSON en columna de texto)

```json
{
  "fixed": [ { "item": "explorers-pack", "quantity": 1 } ],
  "choices": [
    {
      "description": "(a) chain mail or (b) leather armor, longbow, and 20 arrows",
      "choose": 1,
      "options": [
        { "label": "Chain Mail", "items": [ { "item": "chain-mail", "quantity": 1 } ] },
        { "label": "Leather Armor, Longbow, 20 Arrows", "items": [ ... ] },
        { "label": "Any martial weapon", "category": "martial-weapons", "categoryChoose": 1, "items": [] }
      ]
    }
  ],
  "gold": { "dice": "5d4", "multiplier": 10 },     // riqueza alternativa de la clase; null en trasfondos
  "fixedGoldCp": 1500                             // oro fijo incluido (trasfondos: starting_gold)
}
```

- Una opción puede mezclar objetos fijos y una o varias elecciones por categoría (`multiple` del
  dataset): `items` + `category`/`categoryChoose`; si hay más de una categoría en la misma opción,
  `categories: [{ category, choose }]` (usar siempre la lista; `category` es azúcar opcional).
- Mapeo desde `5e-SRD-Classes.json` y `5e-SRD-Backgrounds.json`: `starting_equipment` → `fixed`;
  `starting_equipment_options[]` → `choices[]` (`counted_reference` → item, `multiple` → items +
  categories, `choice` con `equipment_category` → category). Índices de objetos = índices SRD de
  `ItemTemplate`; categorías = índices de `5e-SRD-Equipment-Categories.json`. `gold` por clase desde
  la tabla de riqueza inicial (barbarian 2d4×10, bard 5d4×10, cleric 5d4×10, druid 2d4×10, fighter
  5d4×10, monk 5d4×1, paladin 5d4×10, ranger 5d4×10, rogue 4d4×10, sorcerer 3d4×10, warlock 4d4×10,
  wizard 4d4×10). Los packs del dataset (`explorers-pack`…) se mantienen como un objeto; si el
  catálogo tiene su contenido, opción en el DTO `contents` para mostrarlo.
- Entidades: `ClassDefinition.StartingEquipmentJson`, `BackgroundDefinition.StartingEquipmentJson`.
  Catálogo de categorías: `EquipmentCategory { Index, Name, ItemIndexes[] }` (con `Source`) para
  resolver "cualquier arma marcial". Migración `AddStartingEquipment`. Subir `SrdDataset.Version`.
- DTOs: `ClassDetailDto.StartingEquipment`, `BackgroundDto.StartingEquipment` (con nombre del objeto
  resuelto y su `templateId`), `GET /api/v1/catalog/equipment-categories/{index}` → objetos de la
  categoría (id, nombre, índice) para el selector.
- Paquetes de contenido: `backgrounds[].startingEquipment` y `classesExtended[]` sin cambios
  (los packs no añaden clases). Mismo esquema; los objetos pueden ser SRD o del pack; validación
  de referencias. Documentar en `docs/content-packs.md`.
- Tests: guerrero → 4 elecciones y 0 fijos, primera elección con cota de malla; pícaro → 3 fijos;
  acólito → fijos + elección de símbolo sagrado + 15 po; categoría `martial-weapons` lista armas
  marciales; pack con trasfondo con `startingEquipment` válido e inválido.

## App (Sonnet)

Paso "Equipo inicial" del asistente (`app/lib/features/characters/ui/wizard/step_proficiencies.dart`
o fichero propio `step_equipment.dart`):
- Interruptor superior: **"Equipo de clase y trasfondo"** / **"Oro inicial"**.
- Equipo: sección "Incluido" (fijos de clase y trasfondo, marcados, no editables, con cantidades);
  por cada elección, tarjetas (a)/(b)/(c) seleccionables con sus objetos; las opciones con categoría
  abren un selector filtrado (`equipment-category-picker`) y exigen elegir `choose` objetos.
  Contador "Elecciones 2 de 4". Validación: todas las elecciones completas.
- Oro inicial: "Tira 5d4 y escribe el resultado" (campo 5..20 según dados), vista previa "× 10 =
  N po", casilla "Conservar el equipo del trasfondo" (por defecto no). Sin equipo de clase.
- Se mantiene "Añadir otro objeto" (lista libre actual) debajo, por si el DM lo permite.
- Revisión y envío: las líneas de objetos se añaden al inventario como hoy (`inventory.add`); el oro
  (fijo del trasfondo + riqueza) va en `SheetPatch.copperPieces`. Todo en la misma confirmación.
- Tests: elecciones (a)/(b) bloquean hasta completarse; categoría abre selector; oro valida rango y
  calcula; el envío registra los objetos correctos y el oro.

## Paquete privado

Regenerar `phb-2014.json` con `startingEquipment` en los 12 trasfondos del PHB (objetos SRD por
índice, oro fijo) usando el generador de la sesión; nunca se commitea.

## Verificación

`cd server && dotnet build && dotnet test`; `cd app && flutter analyze && flutter test`. Manual:
crear un guerrero eligiendo cota de malla, arma marcial + escudo, ballesta ligera y paquete de
mazmorras; el inventario lo muestra todo al terminar.
