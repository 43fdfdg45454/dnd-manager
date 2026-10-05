# Fase 6 — Vista de combate, paneles por clase y dados

Contrato cerrado. Casi todo es cliente: usa los endpoints de seguimiento de combate de la fase 4
y el inventario de la fase 5. Cambios de servidor mínimos (abajo).

## Cambios de servidor

- `GET /characters/{id}` incluye `combat: CombatSummaryDto` precalculado para no recomponerlo en el
  cliente:

```
CombatSummaryDto { attacks: AttackDto[], spellSlots: [{ level, max, used }], pactSlots?: { level, max, used },
                   resources: [{ id, key?, name, max, used, recharge, isAuto }], quickConsumables: [{ itemId, name, quantity, charges? }],
                   classPanels: [{ classIndex, level, data: object }], onceSinceLongRest: [{ key, name, used }] }
AttackDto         { itemId?, name, attackBonus, damage: "1d8+3", damageType, versatileDamage?, range?, properties[], notes? }
```

- `attacks`: por cada arma equipada (Fue o Des según `finesse`/a distancia; competencia si el
  personaje tiene la categoría o el arma; `attackBonus` y `damageBonus` de overrides), más "Ataque
  sin armas" (1 + mod Fue). Monje: artes marciales d4/d6/d8/d10 según nivel.
- `classPanels.data` por clase (solo las tres con panel propio; el resto vacío):
  - `barbarian`: `{ rageDamageBonus, rageUses: { max, used }, recklessAttack: true, brutalCriticalDice, unarmoredDefenseAc }`
  - `wizard`: `{ spellbook: [spellIndex], prepared: [spellIndex], preparedMax, arcaneRecovery: { used, slotLevelsRecoverable: ceil(level/2) } }`
  - `paladin`: `{ layOnHands: { pool, used }, divineSmite: { slotsByLevel: [{ level, available, extraDice }] }, channelDivinity: { max, used }, auraRange }`
- Endpoints nuevos (dueño o DM, sin aprobación):
  - `POST /characters/{id}/class-actions/{action}` con `action` ∈ `rage`, `lay-on-hands` (`{ amount }` → cura
    al propio personaje o solo descuenta del pool si `targetSelf=false`), `divine-smite` (`{ slotLevel }` →
    gasta el slot y devuelve `{ damageDice: "3d8" }`), `arcane-recovery` (`{ slotLevels: [..] }` valida
    suma ≤ ceil(nivel/2) y ningún slot > 5, repone esos slots y marca el recurso usado). Responden `200 CharacterDetailDto`.

## Cliente: vista de combate

- Conmutador en la cabecera del personaje: **Detallado** / **Combate**. Se recuerda por personaje
  (`shared_preferences`).
- **Pantalla de combate** (una sola pantalla con scroll, controles grandes para usar con el pulgar):
  1. Barra superior: HP actual / máx. con botones −/+ y campo de daño/curación rápido, HP temporales,
     CA, iniciativa (botón "Tirar"), velocidad, inspiración (toggle), concentración (chip con el
     hechizo y botón de "Perder").
  2. Death saves (3 + 3 círculos) visibles solo si HP = 0. Condiciones como chips (añadir desde la
     lista del catálogo; agotamiento con nivel).
  3. **Ataques**: tarjeta por ataque con botón "Tirar ataque" (1d20 + bono, con ventaja/desventaja
     mediante pulsación larga) y "Tirar daño" (dados + bono; crítico duplica dados).
  4. **Slots de conjuro**: fila por nivel con puntos rellenos/vacíos; toque gasta, pulsación larga
     repone. Pacto aparte.
  5. **Recursos**: lista con puntos o contador (Lay on Hands como barra de pool); toque gasta.
  6. **Panel de clase** (widget registrado por `classIndex`; genérico si no hay):
     - Bárbaro: botón "Furia" (activa estado local con temporizador de 10 asaltos y muestra el
       bono de daño junto a los ataques cuerpo a cuerpo), "Ataque temerario" como recordatorio,
       usos de furia.
     - Mago: pestaña de libro de hechizos con preparados marcados, botón "Recuperación arcana" con
       selector de niveles (descanso corto).
     - Paladín: "Imposición de manos" con deslizador de cantidad, "Castigo divino" con selector de
       slot que muestra los dados extra, "Canalizar divinidad".
  7. **Consumibles rápidos**: pociones y similares con botón "Usar".
  8. **Descansos**: botones "Descanso corto" (diálogo de dados de golpe) y "Descanso largo"
     (confirmación).
- Cada acción llama al endpoint correspondiente y actualiza el estado; si no hay red, muestra
  "Sin conexión" y no cambia nada (fase 9 añade la caché de lectura).

## Dados (local, `features/dice`)

- Parser de expresiones: `NdM`, `+/-k`, `kh<n>`/`kl<n>` (quedarse con los más altos/bajos),
  `adv`/`dis` (equivale a `2d20kh1`/`2d20kl1`), `r<n>` (repetir tirada de 1s una vez, para Great
  Weapon Fighting), varias piezas (`1d8+1d6+3`). Resultado con desglose por dado.
- Pantalla de dados: botones d4-d20/d100, campo de expresión, historial local (últimas 100 tiradas
  en `shared_preferences`), favoritos. Accesible desde cualquier pantalla del personaje (FAB).
- Tiradas desde la hoja: ataques, daño, salvaciones, habilidades e iniciativa con el modificador
  precargado. Animación corta y resultado en una hoja inferior; crítico/pifia resaltados.
- Tests unitarios del parser: `2d6+3` rango 5-15, `4d6kh3` ≤ 18, `adv` devuelve dos d20 y el mayor,
  expresión inválida lanza error controlado.

## Pruebas

Servidor: `attacks` de un guerrero con espada larga Fue 16 nivel 1 competente → +5, "1d8+3", versátil
"1d10+3"; `divine-smite` nivel 1 → "2d8" y gasta el slot; `arcane-recovery` rechaza más niveles de los
permitidos (400); `lay-on-hands` no excede el pool. Cliente: widget de slots gasta al tocar; vista de
combate muestra ataques del DTO; panel de paladín muestra dados de castigo del slot elegido.
