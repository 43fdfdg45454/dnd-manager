# Fase 21 — Baratija inicial y límite de idiomas

Contrato cerrado. Dos huecos del asistente de creación detectados por el usuario.

## 1. Baratija (tabla d100, PHB pág. 160)

Regla (skill `phb-2014`, 07-equipo §10): al crear el personaje se puede tirar una vez en la tabla de
baratijas. La tabla es del PHB (no SRD): su texto **nunca** entra en el repositorio.

- Paquetes de contenido: sección opcional `trinkets: [{ roll: 1..100, item: "<índice de objeto>" }]`
  que referencia objetos del propio pack o del SRD (`category: "Other"`, `subcategory: "Trinket"`,
  sin coste ni peso). Validación: rolls 1..100 únicos, objetos existentes. Varios packs: gana el
  último importado para cada `roll`. Documentar en `docs/content-packs.md` con ejemplo ficticio.
- `GET /api/v1/catalog/trinkets` → `[{ roll, templateId, index, name, description }]` (vacío en SRD
  puro).
- Asistente, paso de equipo: sección **"Baratija"** (Key `equipment-trinket`) bajo "Equipo inicial".
  "Tira 1d100 y escribe el resultado" (campo 1..100, Key `trinket-roll`). Con tabla: muestra el
  objeto resultante con el mismo `EquipmentLineTile` y se añade al inventario al terminar. Sin
  tabla para ese número: campo de texto "Describe tu baratija" (Key `trinket-text`, máx. 200) y se
  añade como objeto personalizado del personaje llamado "Baratija" con esa descripción (mismo
  mecanismo de objetos personalizados que ya exista; en borrador no necesita aprobación). Opcional:
  "Sin baratija" no bloquea.

## 2. Límite de idiomas

Regla: idiomas = fijos de la raza (y subraza) + tantos a elegir como digan las opciones de raza,
subraza y trasfondo (`choices.languages.choose`; p. ej. humano 1, semielfo 1, alto elfo 1,
acólito 2). Las clases del SRD no dan idiomas a elegir (Druídico y Jerga de ladrones son rasgos).

- Paso de idiomas: los fijos aparecen bloqueados con candado y "De la raza"; contador
  "Idiomas a elegir n/m" (Key `wizard-languages-counter`); al llegar a m el resto queda bloqueado
  (`SelectionState.blocked`). Si una opción restringe la lista (`from` no vacío) solo esas son
  elegibles para esa opción. Sin elecciones (m = 0) el texto dice "Tu raza y trasfondo no te dan
  idiomas adicionales". Cambiar raza/subraza/trasfondo recorta la selección sobrante.
- No se avanza con más de m elegidos; menos de m sí (aviso "Te quedan k idiomas por elegir").
- Servidor: al guardar las elecciones de origen (`PUT origin-choices`) los idiomas elegidos se
  guardan como `race.languages`/`race.subrace.languages`/`background.languages`; el asistente
  reparte la selección entre esas claves en orden (raza, subraza, trasfondo) respetando `from`.
  El PATCH de hoja del dueño en un personaje **activo** no puede añadir idiomas (eso ya va por
  `ChangeRequest`); el DM sí.

## Tests

Servidor: pack con `trinkets` válido/inválido; `GET /catalog/trinkets`. App: baratija con tabla
añade el objeto; sin tabla pide texto y añade el personalizado; idiomas: humano 1 extra (bloquea
el segundo), acólito elfo alto 1+2, fijos bloqueados, recorte al cambiar de trasfondo.
