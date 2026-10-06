# Fase 19 — Elecciones obligatorias que faltan y recordatorios

Contrato cerrado. Principio (decisión del usuario): **cuando las reglas exigen una decisión, la app
la pide en ese momento y no deja seguir sin resolverla**; las opciones opcionales se ofrecen como
recordatorio, sin bloquear. Fuente de reglas: skill `phb-2014`.

## Forzadas

1. **Tirada de características en la creación** (PHB cap. 1). Tercer método en el paso de
   características del asistente: "Tirada (4d6, descarta el menor)". El jugador tira en físico y
   escribe seis totales (3..18, Keys `roll-score-<i>`); la app los ordena y los asigna como la matriz
   estándar (cada valor una vez); vista previa con bonos raciales y modificador. Sin cambios de API
   (las puntuaciones base van en el mismo `SheetPatch`).
2. **Elecciones de raza y trasfondo en la creación**.
   - Servidor: importar del SRD `ability_bonus_options`, `starting_proficiency_options`,
     `language_options` de razas y subrazas y las opciones de rasgos (`trait_specific`, p. ej.
     linaje dracónico y truco del alto elfo); trasfondos: `language_options` y opciones de
     herramientas. Modelo normalizado `RaceChoices { abilityBonuses: { choose, amount, from[] },
     skills: { choose, from[] }, languages: { choose, from[] }, cantrip: { choose, spellList },
     traitOptions: [{ key, name, choose, options: [{ index, name, description, damageType? }] }] }`
     en `RaceDetailDto`/`SubraceDto`/`BackgroundDto`; paquetes de contenido pueden declararlo
     (humano variante: +1 a dos, una habilidad y una dote del set `feats`). Guardar las elecciones
     como `CharacterChoice` (nivel 0, `classIndex: null`, claves `race.*`, `background.*`) con sus
     efectos en la hoja (bonos de característica como parte de desglose `race`, competencias,
     idiomas, truco siempre preparado, resistencia y arma de aliento del dracónido como rasgo).
   - App: el asistente añade, tras raza y trasfondo, las elecciones que correspondan con el mismo
     componente de tarjetas que la subida de nivel; no se avanza sin completarlas.
3. **Subida de nivel completa**: habilidad al entrar en multiclase como bardo, explorador o pícaro;
   revalidación de invocaciones y dotes cuyos prerrequisitos ya no se cumplen (la subida exige
   sustituirlas; también se marca en la hoja `invalidChoices` y, si existe, el jugador ve la
   sustitución forzada al entrar en "Mi sesión").
4. **Concentración**.
   - Al aplicar daño a un personaje concentrado (jugador en su tarjeta de PG o DM en su mesa), la
     app pregunta "¿Superaste la salvación de Constitución (CD N)?" con N = max(10, daño/2); "No"
     termina la concentración. Servidor: `ApplyDamage` devuelve `concentrationCheckDc` cuando
     procede; el cliente lo pide; endpoint existente para terminar concentración.
   - Lanzar otro conjuro de concentración pide confirmar que se pierde el anterior.
   - A 0 PG la concentración termina sola (servidor) y se avisa.
5. **Sintonización**: sintonizar con 3 objetos ya sintonizados abre "Elige cuál dejar"; tras un
   descanso corto aprobado se ofrece sintonizar los objetos pendientes (el PHB exige un descanso
   corto para sintonizar).
6. **Tiradas al descansar**: rasgos con recarga que piden tirada (Presagio de Adivinación: dos d20
   tras descanso largo) se piden en la misma secuencia forzada tras el descanso, después de preparar
   conjuros; los valores se guardan en el recurso y se muestran en el panel de clase.

Orden de la secuencia forzada al entrar en "Mi sesión": subida de nivel pendiente → sustituciones
inválidas → preparación de conjuros → tiradas de descanso.

## Recordatorios (no bloquean)

7. Tras un descanso corto aprobado, si quedan usos: "¿Usar Recuperación arcana / natural?" con el
   selector de espacios.
8. A 0 PG: banner persistente "Anota tu salvación contra muerte" sobre la tarjeta de salvaciones.

## Verificación

Servidor y app en verde; manual: crear un semielfo (pide +1 a dos y dos habilidades), un dracónido
(pide linaje) y un personaje con tirada de características; daño a un clérigo concentrado pide la
salvación con la CD correcta.
