# Fase 18 — Preparación de conjuros obligatoria y categorías de conjuro

Contrato cerrado. Decisiones del usuario:

- Preparar conjuros se **fuerza** cada vez que hay oportunidad de cambiar (tras cada descanso
  largo, al subir de nivel y la primera vez que la clase puede lanzar), con la opción de **mantener
  la preparación anterior**. No se difiere: la app bloquea "Mi sesión" hasta resolverlo.
- Mismo principio para la **subida de nivel concedida**: si hay nivel pendiente, al abrir la
  campaña se abre el asistente de subida (puede cerrarse solo al terminarlo).
- Cada conjuro muestra un **icono de categoría** (curación, daño, control, apoyo, defensa,
  utilidad, invocación) en todas las listas.

Reglas (skill `phb-2014`, 08-conjuros-reglas y clases): preparan **clérigo, druida, paladín y
mago** (y subclases que lo indiquen). Máximo = mod. de característica + nivel de clase (paladín:
+ mitad del nivel), mínimo 1. Candidatos: lista de la clase (mago: solo su libro de conjuros) de
niveles con espacios disponibles; los trucos no se preparan; los "siempre preparados" (dominio,
juramento, círculo) no cuentan. El paladín lanza desde nivel 2. Bardo, hechicero, brujo y
explorador **conocen** conjuros: no preparan (sus cambios ya los cubre la subida de nivel).

## Servidor (Opus)

- `Character.SpellPreparationPending` (bool) y `SpellPreparationReason` (`Creation` |
  `LongRest` | `LevelUp`). Se activa: al aplicar un descanso largo (aprobado o forzado por el DM) a
  un personaje con alguna clase que prepara; al aplicar una subida de nivel en una clase que prepara
  (incluye el primer nivel con lanzamiento, p. ej. paladín 2); al activar un personaje nuevo que
  prepara si aún no tiene preparación. Migración `AddSpellPreparation`.
- `GET /api/v1/characters/{id}/spell-preparation` → `{ pending, reason, classes: [{ classIndex,
  className, max, alwaysPrepared: [SpellSummary], prepared: [index], candidates: [SpellSummary] }] }`
  donde `SpellSummary = { index, name, level, school, category, concentration, ritual, castingTime,
  source }`. Candidatos según las reglas de arriba (máximo nivel = el mayor con espacios de esa
  clase en la tabla multiclase/propia).
- `POST /api/v1/characters/{id}/spell-preparation { classes: [{ classIndex, spells: [index] }] }`:
  dueño (sin aprobación del DM; es una operación de juego como los recursos) o DM. Valida máximo,
  candidatos y que no incluya trucos ni "siempre preparados"; reemplaza `IsPrepared` de esa clase;
  limpia el pendiente; emite `character.updated`. 409 "No es momento de preparar conjuros" si el
  dueño intenta cambiarla sin pendiente (el DM siempre puede).
- `POST /api/v1/characters/{id}/spell-preparation/keep`: mantiene la preparación actual si sigue
  siendo válida (si el máximo bajó o algún conjuro ya no es candidato → 400 con el motivo); limpia
  el pendiente.
- El parche de hoja (`SheetPatch.Spells`) deja de poder cambiar `IsPrepared` para el dueño (lo
  ignora y conserva el valor); el DM sí.
- **Categoría de conjuro**: `SpellDefinition.Category` (`Healing`, `Damage`, `Control`, `Buff`,
  `Defense`, `Utility`, `Summoning`). Derivación para el SRD: `heal_at_slot_level` → Healing;
  `damage` → Damage; `dc` sin daño → Control; resto → Utility; más una tabla curada
  `SrdSpellCategories.cs` que corrige los obvios (bless, aid, haste, enhance-ability → Buff; shield,
  mage-armor, sanctuary, protection-from-*, stoneskin, globe → Defense; conjure-*, find-familiar,
  animate-dead, summon* → Summoning; cure/healing word/heal → Healing…). Los paquetes pueden indicar
  `category` en sus conjuros (opcional; si falta, misma derivación). DTOs de conjuro (resumen,
  detalle, `CharacterSpellDto`) ganan `category`. Migración incluida en la anterior; subir versión
  del SRD.
- Tests: clérigo nivel 1 con Sab 16 → max 4; descanso largo aprobado activa el pendiente; el dueño
  sin pendiente → 409; DM puede siempre; `keep` válido limpia; `keep` con máximo reducido → 400;
  mago solo candidatos del libro; paladín 1→2 activa pendiente; trucos rechazados; categorías:
  cure-wounds Healing, fireball Damage, hold-person Control, bless Buff, shield Defense,
  find-familiar Summoning.

## App (Sonnet)

- Iconos de categoría (`AppIcons`): healing → `drop`, damage → `flame`, control → `chains`, buff →
  `sparkles`, defense → `shield`, utility → `compass`, summoning → `rune`. Widget
  `SpellCategoryIcon(category)` con color por categoría (moss, blood, arcane, oldGold, bone, boneMuted,
  ember) y tooltip en español. Se muestra delante del nombre en **todas** las listas de conjuros
  (pestaña Hechizos, combate, selector de conjuros, compendio, asistente de subida y de creación).
- Pantalla **"Prepara tus conjuros"** (`/characters/:id/prepare-spells`, Key `prepare-spells`):
  cabecera con el motivo ("Tras el descanso largo", "Nuevo nivel", "Primera preparación"), por
  clase: contador "4 de 5", "Siempre preparados" fijos arriba, buscador por nombre, filtros por
  nivel (chips) y por categoría (chips con icono), lista con casilla. Botones: **"Mantener los de
  ayer"** (`prepare-keep`, solo si hay preparación previa válida) y **"Preparar"**
  (`prepare-confirm`, activo cuando cada clase está en 1..max). Errores del servidor en línea.
- **Forzado**: si el personaje del jugador tiene `spellPreparationPending`, al entrar en "Mi
  sesión" se abre la pantalla sin opción de volver atrás (`PopScope(canPop: false)`); lo mismo con
  el evento en tiempo real `character.updated` que lo active. Si hay nivel pendiente, primero el
  asistente de subida (también forzado), después la preparación.
- El editor de hoja deja de ofrecer la casilla "Preparado" al jugador (sí al DM).
- Creación: el paso de conjuros del asistente, para clases que preparan, elige la preparación
  inicial con el mismo widget (y el mago además su libro).
- Tests: forzado al entrar con pendiente; mantener; buscador y filtros; contador y límite; icono de
  categoría presente en la lista de hechizos.

## Verificación

`cd server && dotnet build && dotnet test`; `cd app && flutter analyze && flutter test`. Manual:
clérigo pide descanso largo, el DM lo aprueba, el móvil del jugador abre "Prepara tus conjuros";
"Mantener los de ayer" cierra; un paladín al subir a 2 prepara por primera vez.
