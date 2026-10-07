# Fase 22 — Personalidad del trasfondo y tablas de tirada

Contrato cerrado. Regla (skill `phb-2014`, 02-creacion-personaje §4 y §6): cada personaje tiene
**dos rasgos de personalidad, un ideal, un vínculo y un defecto**, que se tiran o eligen en las
tablas de su trasfondo (d8, d6, d6, d6) o se escriben libremente. Algunos trasfondos tienen una
tabla opcional (especialidad, estafa, origen…). El SRD trae las tablas del acólito; el resto solo
llega por paquetes privados (texto del PHB, **nunca** en el repo; ejemplos ficticios).

## Servidor (Opus)

- `Character`: `PersonalityTraits`, `Ideals`, `Bonds`, `Flaws` (texto, ≤ 1000 cada uno) y
  `BackgroundDetail` (≤ 200, el resultado de la tabla opcional, p. ej. "Especialidad: Bibliotecario").
  En `CharacterDetailDto` y en `SheetPatch` (el dueño de un borrador o el DM lo cambian directo; el
  dueño de un personaje activo por `ChangeRequest`, como el resto de la hoja). Migración
  `AddCharacterPersonality`.
- Catálogo de trasfondos: `personality: { traits: [text], ideals: [{ text, alignment }],
  bonds: [text], flaws: [text] }` (dado implícito = número de entradas) y
  `optionalTables: [{ key, name, entries: [text] }]`. Importar desde el SRD
  (`personality_traits`, `ideals` con su alineamiento, `bonds`, `flaws` de `5e-SRD-Backgrounds.json`).
  Paquetes: mismos campos en `backgrounds[]`; validación (listas 1..20, textos ≤ 500).
- **Tablas de tirada genéricas** en paquetes: `rollTables: [{ key, name, dice ("d100", "d20"…),
  entries: [{ from, to, text }], classIndex?, subclassIndex? }]` (cobertura completa del dado sin
  solapes). `GET /api/v1/catalog/roll-tables` (con filtro `subclass=`) → tablas con sus entradas.
  Sirve para la Oleada de magia salvaje del hechicero (d100) y futuras tablas.
- Documentar todo en `docs/content-packs.md` con ejemplos ficticios.
- Tests: acólito del SRD trae 8/6/6/6 con alineamientos; pack con personalidad y tabla opcional
  válida/ inválida; `rollTables` con huecos o solapes → error; parche de personalidad del dueño en
  borrador directo y en activo crea `ChangeRequest`.

## App (Sonnet)

- Asistente: paso **"Personalidad"** tras el trasfondo. Cuatro bloques (Rasgos ×2, Ideal, Vínculo,
  Defecto): botón "Tirar dN" (Key `personality-roll-<kind>`) que elige al azar, o tocar una entrada
  de la lista (tarjetas iguales como `SelectionGrid` en una columna; el ideal muestra su
  alineamiento), o escribir el propio texto. Tabla opcional del trasfondo igual (una entrada).
  Opcional: no bloquea, pero "Siguiente" avisa si falta algo. Sin tablas (trasfondo sin datos):
  solo campos de texto.
- Hoja: sección "Personalidad" (rasgos, ideal, vínculo, defecto, detalle del trasfondo),
  editable con las reglas de la hoja.
- Panel del hechicero (y compendio, sección "Tablas"): si existe una tabla de tirada para su
  subclase, botón "Oleada de magia salvaje" → campo "Tira 1d100" (Key `roll-table-input`) y muestra
  el resultado (Key `roll-table-result`); en el compendio, la tabla completa con buscador.
- Tests: tirar y elegir en el paso de personalidad, texto libre, envío en el parche; tabla de
  tirada muestra el resultado correcto para 1, 50 y 100 (y 00 = 100).

## Verificación

`cd server && dotnet build && dotnet test`; `cd app && flutter analyze && flutter test`.
