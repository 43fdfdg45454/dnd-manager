# Fase 24 — Lote de correcciones de juego y nueva navegación

Contrato a partir de la lista de problemas que el usuario encontró jugando (2026-10-09). Donde un
punto admitía varias lecturas se fija aquí la elegida; las dudas abiertas quedan al final.

## 1. Dados

1. **Crítico y pifia solo donde existen** (PHB cap. 7): el 20 o el 1 natural solo significan algo
   en tiradas de ataque (crítico/pifia) y en salvaciones contra muerte (20 = recuperas 1 PG,
   1 = dos fallos). `rollAndShow` recibe `RollKind { attack, deathSave, check }` (por defecto
   `check`); en `check` (iniciativa, características, habilidades, salvaciones) no hay chip ni color
   de crítico/pifia. El resultado muestra siempre el d20 natural ("d20: 1") junto al total para que
   un 6 = 1+5 no confunda. El historial guarda el tipo y aplica el mismo criterio.
2. **Dado virtual en toda entrada de tirada**: cada campo donde el jugador escribe una tirada física
   tiene al lado un botón "Tirar" (`RollInputButton`) que tira la expresión con el dado virtual,
   rellena el campo y registra la tirada en el historial. Aplica a: PG al subir de nivel (1dN),
   características (4d6kh3 por fila y "Tirar las seis"), oro inicial, baratija (1d100), tiradas al
   descansar, tablas de tirada (Oleada de magia salvaje, compendio), Intervención divina (1d100).
   Salvaciones contra muerte: "Tirar" aplica el resultado (≥10 éxito, <10 fallo, 1 = dos fallos,
   20 = recupera 1 PG). Concentración: el diálogo ofrece "Tirar salvación" (d20 + salvación de CON)
   y responde solo según la CD.
3. **PG al subir de nivel**: además de tirar, "Usar el valor fijo" (dado/2 + 1: 4, 5, 6, 7 según
   d6, d8, d10, d12; regla del PHB). Se envía como `hitPointsRolled` igual que una tirada; sin cambios
   de API.
4. **Críticos de conjuros y rasgos**: el daño de conjuros (sección nueva de combate), Castigo divino,
   Ataque furtivo, Marca del cazador y Artes marciales tienen la casilla "Crítico" que duplica los
   dados (no los modificadores). Castigo divino añade también "Contra no muerto o infernal" (+1d8,
   respetando el máximo de 6d8).

## 2. Combate

5. **Conjuros en la vista de combate**: sección "Conjuros" con trucos y conjuros preparados/conocidos
   que tengan ataque, salvación o daño/curación (datos de `GET /catalog/spells/{index}`, incluidos
   `damageAtSlotLevel`, `damageAtCharacterLevel` y `healAtSlotLevel`). Cada fila: tirar ataque
   (bonificador de ataque de conjuros de su clase), CD y característica de salvación, tirar daño o
   curación eligiendo el nivel de lanzamiento (escalado), casilla "Crítico" y "Gastar espacio" que usa
   el endpoint de espacios existente.
6. **Habilidades a mano en combate**: la tarjeta de estadísticas de combate gana una fila de tiradas
   rápidas de habilidad (Engaño, Persuasión, Intimidación, Percepción, Sigilo, Atletismo, Acrobacias,
   Perspicacia) junto a Inspiración, y "Todas las habilidades" abre la lista completa. Lectura elegida
   para "Agregar deception, así como está inspiración"; ver dudas.
7. **Recursos de clase que el jugador no puede subir**: los recursos automáticos (`IsAuto`, p. ej.
   Imposición de manos, Canalizar divinidad, Furia, Ki) solo se recuperan con descansos o por el DM.
   Servidor: `POST resources/{id}/restore` sobre un recurso automático devuelve 403 si quien lo pide
   no es DM/Owner, salvo los puntos de hechicería (Fuente de magia convierte espacios en puntos).
   App: sin pulsación larga ni "+" para el jugador en esos recursos.

## 3. Personaje y subida de nivel

8. **Vista del personaje**: "Combate" deja de ser un modo que oculta las pestañas; pasa a ser la
   primera pestaña del mismo `TabBar` (Combate, Resumen, Habilidades, Rasgos, Hechizos, Inventario,
   Notas). La app recuerda la última pestaña por personaje.
9. **Subida de nivel**: el paso de revisión lista los rasgos nuevos del nivel (de `ClassLevelFeature`
   de la clase y la subclase) y, para lanzadores que preparan, avisa de que tras confirmar se abre
   "Preparar conjuros". Se revisan las reglas de elecciones de los niveles que se saltaban (ver
   tests de `LevelUpPlanner` añadidos en esta fase).
10. **Dotes**: el SRD 5.1 solo incluye Grappler. Las dotes del PHB no pueden ir en el repositorio
    (CLAUDE.md); se aportan como **paquete de contenido** privado que el administrador importa en su
    instancia. Cada dote con `abilityIncrease` (las "medias dotes" suben la característica) y
    `prerequisites` estructurados. Requisitos explicados: el motivo de no elegibilidad dice qué falta
    y qué tiene el personaje ("Requiere Fuerza 13; tienes 10"), y se muestra siempre en la tarjeta de
    la opción, no solo como deshabilitada. Prerrequisitos nuevos: `race` (lista de razas),
    `proficiency` (armadura o arma) y `spellcasting` (poder lanzar al menos un conjuro).
11. **Equipo inicial**: cada línea del equipo incluido y cada elección lleva su origen ("Clase" o
    "Trasfondo") y se agrupan por origen.
12. **Objetos personalizados con modificadores**: se verifica con tests de integración que un objeto
    personalizado con `AbilityBonus` equipado cambia la característica (y lo que depende de ella);
    los objetos sin categoría de equipo se pueden equipar.

## 4. Peticiones de cambio y notificaciones

13. **Detalle por tipo de petición**: `ChangeRequest` guarda `BeforeJson` al crearse (instantánea de
    los campos afectados). `ChangeRequestDto` añade `before` y `details` legibles:
    - `EditSheet`: tabla Campo · Antes · Después por cada campo cambiado (características con su
      modificador, PG, CA, competencias añadidas/quitadas, idiomas, clases y niveles…).
    - `AddItem` / `CustomItem`: tarjeta del objeto (nombre resuelto de la plantilla, cantidad, tipo,
      daño, CA, propiedades, modificadores con su efecto).
    - `RemoveItem`: objeto y cantidad (tenía N, quedará M).
    - `AdjustMoney`: dinero antes, cambio y después en po/pp/pc, con el motivo.
    - `Activate`: resumen del personaje (raza, clase y nivel, características).
14. **Notificaciones**: al resolver una petición se avisa al jugador que la pidió con una
    SnackBar ("El DM aceptó tu personaje Aria" / "…rechazó…") con "Ver", que abre la hoja del
    personaje. El evento `changeRequest.updated` lleva `characterId` y `status`.

## 5. Campañas

15. **Invitaciones**: añadir un miembro crea una `CampaignInvitation` (pendiente) en vez de añadirlo.
    El invitado la ve en Campañas ("Invitaciones") y la acepta o rechaza; el DM ve las pendientes en
    Miembros y puede cancelarlas. Endpoints: `GET /me/invitations`,
    `POST /invitations/{id}/accept|decline`, `GET /campaigns/{id}/invitations`,
    `DELETE /campaigns/{id}/invitations/{invitationId}`. `POST /campaigns/{id}/members` mantiene la
    ruta pero devuelve `202` con la invitación. El administrador global también invita (no añade).
16. **Tienda con plantillas de objetos base**: el DM puede añadir objetos del catálogo en lote:
    "Añadir del catálogo" con selección múltiple y filtros por categoría (Armas sencillas, Armas
    marciales, Armaduras, Equipo de aventurero, Herramientas, Monturas y vehículos, Pociones), con el
    precio de lista del objeto como precio por defecto. Plantillas de tienda predefinidas (Herrería,
    Armería, Tienda general, Alquimista) añaden su lote de golpe.
    Servidor: `POST /shops/{id}/items/bulk { items: [{ templateId, priceCp?, stock? }] }`.
17. **Druidas y bestias**: el catálogo importa las bestias del SRD (`5e-SRD-Monsters.json` filtrado a
    `type = beast`) con sus estadísticas completas. `GET /catalog/beasts?maxCr=&fly=&swim=`. El panel
    de druida lista las formas salvajes permitidas por su nivel (y Círculo de la Luna) y abre la hoja
    de cada bestia (características, CA, PG, velocidades, sentidos, acciones con tirada). El
    compendio gana la sección "Bestias".

## 6. Documentos, listas e iconos

18. **Documentos**: tocar un documento de la biblioteca lo descarga (si no lo está) y lo abre con la
    aplicación del sistema que el usuario elija (`open_filex`, intent `ACTION_VIEW`). El visor
    integrado queda en el menú del documento como "Ver en la app".
19. **Scroll infinito**: ninguna lista tiene "Cargar más"; al acercarse al final se pide la página
    siguiente (widget común `InfiniteScrollList`). Afecta a búsqueda de objetos, transacciones,
    selector de conjuros y usuarios del administrador.
20. **Iconos**: `AppIcon` respeta `IconTheme.size` (antes siempre 24) y se dibuja centrado en una caja
    del tamaño exacto, como `Icon`; se revisan los usos con tamaños fijos que descuadraban filas.

## 7. Navegación

21. **App**: barra inferior global con Campañas, Compendio, Dados, Biblioteca y Perfil. "Perfil"
    sustituye al menú de usuario (nombre, correos, apariencia, servidor, actualizaciones, caché,
    administración, atribuciones, cerrar sesión).
22. **Campaña**: la barra inferior de la campaña pasa a ser Mi sesión/Mesa del DM · Personajes ·
    Campaña (las secciones de la antigua "General"); la campaña abre en Mi sesión o Mesa según el rol.
    Peticiones, transacciones y ajustes siguen en la barra superior.

## Verificación

Servidor y app compilan sin avisos y pasan sus tests; tests nuevos para invitaciones, `BeforeJson`,
alta en lote de tienda, bestias, restauración de recursos automáticos, modificadores de objetos
personalizados, `RollKind` y valor fijo de PG.

## Dudas abiertas

- "Agregar deception, así como está inspiración": se interpreta como acceso rápido a Engaño (y otras
  habilidades sociales) en combate.
- Dotes del PHB: no van en el repositorio; se entregan como paquete privado para importar.
