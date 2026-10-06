# Fase 14 — Asistente guiado de creación de personaje

Contrato cerrado. Sustituye el flujo "diálogo con nombre + formulario gigante" por un asistente
paso a paso. El formulario completo (`SheetEditorPage`) se conserva para editar después.

## Servidor (pequeño, Sonnet)

- `ClassDefinition.SkillChoicesJson` (`{"choose": 2, "from": ["acrobatics", ...]}`), importado
  desde `proficiency_choices` del dataset (la opción cuyo `from` son habilidades `skill-*`);
  migración `AddClassSkillChoices`; `ClassDetailDto.SkillChoices { Choose, From[] }`. Subir
  `SrdDataset.Version`. Test: `rogue` elige 4 entre 11; `wizard` 2 entre 6.
- Nada más: creación = `POST /campaigns/{id}/characters` (nombre, dueño) + `PATCH /characters/{id}/sheet`
  (`SheetPatch`) + `POST /characters/{id}/inventory` por línea de equipo (ya existentes).

## Cliente (Sonnet)

### Ruta y estructura

- Ruta `/campaigns/:id/characters/new` (y `/campaigns/:id/characters/new?ownerUserId=` para el
  DM) → `features/characters/ui/wizard/character_wizard_page.dart`. `CharactersTab` abre el
  asistente con el botón "Nuevo personaje" (`characters-new`); el DM conserva "Crear rápido (PNJ)"
  en un menú secundario que abre el `NewCharacterDialog` actual.
- `PageView` sin deslizamiento manual, carril de progreso superior (puntos con `AppIcon` por paso)
  y barra inferior "Atrás" / "Siguiente" (`wizard-back`, `wizard-next`); en el último paso
  "Crear personaje" (`wizard-submit`).
- Estado: `CharacterWizardController extends Notifier<WizardState>` en
  `features/characters/data/character_wizard_controller.dart`.
  `WizardState { int step; String name; MemberSummary? owner; String? raceIndex; String? subraceIndex;
  bool applyRacialBonuses; String? classIndex; String? subclassIndex; AbilityMethod method
  (pointBuy|standardArray|manual); BaseAbilities abilities; String? backgroundIndex; Set<String> skills;
  Set<String> languages; List<(String templateId, String name, int qty)> equipment; List<SpellPatch> spells;
  String? alignment; String notes; }`. `String? validate(int step)` devuelve el error en español o null.
  `SheetPatch toPatch()` construye un único parche.

### Pasos

1. **Nombre** (`step-name`): nombre (obligatorio, ≤ 100), alineamiento opcional (lista SRD), dueño
   (solo DM: yo / miembro / PNJ sin dueño).
2. **Raza** (`step-race`): tarjetas con `AppIcon` genérico, velocidad y bonos; subraza obligatoria
   si la raza tiene; interruptor "Aplicar bonos raciales" (por defecto sí). Datos:
   `catalogRacesProvider` + `raceDetailProvider`.
3. **Clase** (`step-class`): tarjetas con acento e icono de `classThemeOf` (fase 13b); muestra
   dado de golpe, salvaciones y lanzamiento de conjuros; subclase si se desbloquea a nivel 1
   (`subclassUnlockLevel`). Nivel inicial fijo 1.
4. **Características** (`step-abilities`): `SegmentedButton` método: *Compra por puntos* (reutiliza
   la lógica de `point_buy_dialog.dart`, presupuesto 27, mostrando restantes), *Matriz estándar*
   (15/14/13/12/10/8 asignables con menús desplegables, cada valor una sola vez), *Manual* (1–20).
   Vista previa de la puntuación final con bonos raciales y modificador.
5. **Trasfondo y competencias** (`step-background`): trasfondo (lista SRD; muestra habilidades
   que otorga y texto de equipo), selección de habilidades de clase (`SkillChoices`: exactamente
   `choose`, deshabilitando las ya otorgadas por el trasfondo), idiomas (chips libres de la lista
   SRD de idiomas de la raza + "Común").
6. **Equipo inicial** (`step-equipment`): texto SRD de clase y trasfondo (`StartingEquipmentText`)
   y lista editable de líneas (buscador `item_search_list.dart` en modo selección, cantidad).
   Opcional; se puede saltar.
7. **Hechizos** (`step-spells`, solo si la clase lanza a nivel 1): trucos conocidos y hechizos
   conocidos/preparados según `ClassLevelDto` nivel 1 (`CantripsKnown`, `SpellsKnown`; para
   clases que preparan, máximo = `max(1, mod + nivel)`); selector reutilizando `SpellPickerPage`
   embebido con contadores "Trucos 2/3".
8. **Revisión** (`step-review`): resumen por secciones con botón "Editar" que salta al paso;
   "Crear personaje" ejecuta: `create(name, owner)` → `saveSheet(toPatch())` → `inventory.add` por
   línea → navega a `/characters/:id`. Errores: muestra el mensaje del servidor y permanece.

### Validación por paso (mensajes en español)

Nombre vacío → "Introduce un nombre"; raza sin subraza requerida → "Elige una subraza"; clase sin
elegir → "Elige una clase"; subclase requerida a nivel 1 → "Elige una subclase"; compra por puntos
con presupuesto excedido → "Te has pasado de 27 puntos"; matriz con valores sin asignar → "Asigna
las seis puntuaciones"; habilidades distintas de `choose` → "Elige N habilidades"; trucos/hechizos
por encima del máximo → "Como máximo N trucos".

### Tests (`app/test/wizard_test.dart`)

- Validación de cada paso bloquea "Siguiente" y muestra el mensaje.
- Compra por puntos: 27 puntos exactos permitidos; 28 no. Matriz estándar: no se repite un valor.
- Hechizos: el paso se omite para guerrero y aparece para mago con límites de nivel 1.
- Envío: los fakes registran `create` + `saveSheet` (con razas, clases, puntuaciones, competencias,
  hechizos, trasfondo) + `add` por línea de equipo, y la navegación termina en `/characters/:id`.
- `CharactersTab`: "Nuevo personaje" abre el asistente; el DM sigue teniendo "Crear rápido".

## Verificación

`cd server && dotnet build && dotnet test`; `cd app && flutter analyze && flutter test`. Manual:
crear un mago de nivel 1 completo en menos de dos minutos desde el móvil.
