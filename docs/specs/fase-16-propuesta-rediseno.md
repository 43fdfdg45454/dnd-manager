# Fase 16 — Propuesta de rediseño guiado por el PHB 2014

Estado: **propuesta pendiente de aprobación**. La skill `phb-2014` (fuera del repositorio) es la
fuente de qué puede hacer un personaje y cómo; este documento describe cómo queda cada pantalla y
por qué, y las dudas que hay que resolver antes de implementar.

Regla absoluta: la app acompaña una sesión presencial. Asiste con **lo que tu personaje puede
hacer** y lleva la cuenta de lo que es molesto llevar a mano. No sustituye al DM, no narra, no
decide tiradas.

## Principios de diseño (móvil y tablet)

1. **Una pregunta por pantalla.** Cada vista responde a una sola cosa: "¿qué puedo hacer ahora?",
   "¿cómo está mi grupo?", "¿qué elijo al subir?".
2. **Lo accionable arriba, lo informativo abajo.** En combate: PG, acciones de clase, ataques y
   conjuros caben en la primera pantalla sin desplazar; rasgos y notas, debajo.
3. **Cada número explica su origen** con un toque (ya existe `StatValue`); nada de `+X` mudos.
4. **Estados claros, no iconos ambiguos.** La conexión en vivo tiene una franja con texto, no solo
   un icono.
5. **Animación con significado.** Cada animación confirma una acción del juego (furia, castigo,
   subir de nivel, caer a 0 PG). Nunca decorativa sin motivo, nunca bloqueante, siempre < 1 s salvo
   la de subir de nivel.
6. **Tablet = dos paneles.** Mesa del DM: roster a la izquierda, detalle a la derecha. Mi sesión:
   combate a la izquierda, conjuros/inventario a la derecha. Móvil: lo mismo apilado con pestañas.

## Respuestas a las preguntas

- **Tiempo real.** Sí, todo: las solicitudes de cambio (crear, aprobar, rechazar, cancelar)
  emiten `changeRequest.updated`; el jugador ve el resultado sin refrescar y la hoja se recarga.
  Lo que falta es que *se note*: una tarjeta "Tu cambio fue aprobado" en Mi sesión.
- **SignalR vs gRPC.** Recomendación: **mantener SignalR por el mismo puerto HTTPS**. gRPC
  necesitaría HTTP/2 extremo a extremo (otro `server` de nginx con `grpc_pass`, otro puerto
  expuesto, otro certificado en la app) y un cliente de streaming propio; no aporta seguridad
  extra: la autenticación es el mismo JWT en ambos casos. Lo que sí hay que endurecer, y se hace
  en esta fase: token solo en `/hubs` y con vida corta (ya), límite de tamaño de mensaje, límite
  de conexiones por usuario, rate limit en `negotiate`, cierre de conexión al expulsar a un
  miembro, y cabeceras de nginx. Ver sección "Conexión".
- **La nube tachada.** El icono depende de `connectivity.isOffline`, que se pone a `true` si
  **cualquier** petición HTTP falló recientemente, y además el hub cae a `disconnected` si el
  `negotiate` o el `Upgrade` fallan en el proxy. Resultado: el icono miente. Se sustituye por un
  estado derivado solo del hub, con diagnóstico paso a paso (abajo).
- **Descanso largo.** Pasa a requerir permiso del DM (ver "Descansos").
- **Rejilla de Combate en el Resumen.** Era un `Wrap` de fichas de ancho variable. Pasa a una
  rejilla fija de 3 columnas y 2 filas con fichas iguales (CA, Iniciativa, Velocidad / PG máx,
  Percepción pasiva, Competencia), que en tablet se vuelve de 6 en una fila.
- **Imposición de manos.** Dos botones: "Curarme" y "Curar a otro" (abre la lista del grupo o un
  campo de nombre para PNJ). Sin interruptor.

## Pantallas

### 1. Inicio (mis campañas)

Lista de tarjetas grandes con nombre, mi rol, próxima sesión y un punto de estado en vivo si la
campaña está abierta en otro dispositivo. Un solo botón: "Entrar". Menú de usuario arriba a la
derecha (Compendio, Servidor, Atribuciones, Admin si procede). **Por qué:** es una pantalla de paso;
no debe competir en atención con la campaña.

### 2. Shell de campaña

Barra inferior con dos destinos, según rol: "General" y "Mesa del DM" o "Mi sesión". Encima del
contenido, una **franja de conexión** de una línea que solo aparece cuando NO estamos conectados
en vivo: ámbar "Reconectando… (3 s)" o roja "Sin conexión en vivo · datos de hace 2 min ·
Reintentar". Cuando todo va bien no hay franja: solo un pequeño sello rúnico en la barra superior
que **late** suavemente. **Por qué:** el estado normal no debe ocupar espacio ni exigir
interpretación; el anómalo debe ser imposible de ignorar y decir qué hacer.

### 3. General

Rejilla de secciones de dos columnas (tablet: tres) con icono propio, título y un dato vivo
(próxima sesión, nº de entradas de lore, mensajes). Sin cambios de estructura; sí de piel.

### 4. Mesa del DM

Móvil, de arriba abajo:

1. **Grupo**: una fila por personaje, 72 px: retrato, nombre, PG como barra con número, iconos de
   condición, concentración, death saves si está a 0. Tocar = hoja de acciones (daño, curar,
   temporales, condiciones, PG máx, mensaje secreto). Pulsación larga = selección múltiple.
2. **Acciones de mesa** en una tira horizontal: Descanso corto, Descanso largo, Mensaje, Dados,
   Botín, Tiendas, Solicitudes (con contador).
3. **Peticiones pendientes**: descansos largos solicitados por jugadores y cambios de hoja, cada
   uno con "Aprobar" / "Rechazar" en la propia tarjeta.
4. **Subidas de nivel**: "Conceder nivel" al grupo o a uno: el jugador recibe el aviso y entra al
   asistente de subida.

Tablet: roster fijo a la izquierda (40 %), el resto a la derecha. **Por qué:** el DM mira el
grupo todo el rato y actúa sobre uno; nada debe taparle la lista.

### 5. Mi sesión · Combate

Primera pantalla sin desplazar (móvil 360×780):

- Cabecera de clase (acento + icono propio): nombre, nivel, subclase.
- **PG**: barra grande con temporales en otro color; botones −/+ y campo rápido. Al recibir daño
  la barra vibra y se tiñe de rojo un instante; al curar, un pulso verde. A 0 PG la tarjeta se
  convierte en **salvaciones de muerte** con tres pips de éxito y tres de fallo.
- **Fila de estado**: CA, Iniciativa, Velocidad, Percepción pasiva, Concentración (toque = origen).
- **Acciones de clase**: tarjeta por recurso con pips y botón de acción. Furia y Ataque temerario
  del bárbaro con llamas al activarse; Castigo divino con destello dorado; Ki con pips que se
  consumen con un trazo. Cada acción dice qué hace en una línea (del PHB) y cuántos usos quedan.
- **Ataques**: una fila por arma: `+7` y `1d8+4 cortante`, toque = desglose, botón "Tirar".
- **Conjuros de combate**: espacios por nivel como pips; lista de conjuros con tiempo de
  lanzamiento y concentración; "Lanzar" gasta espacio y, si concentra, fija la concentración.
- **Objetos de combate**: consumibles y objetos con cargas, con "Usar".

Debajo (desplazando): condiciones con su efecto resumido (del PHB), rasgos de combate pasivos
(Ataque extra, Ataque furtivo con su dado…), notas rápidas.

**Por qué:** en combate se miran cuatro cosas: cuánta vida, qué puedo hacer ahora, con qué ataco,
qué conjuros me quedan. Todo lo demás es ruido.

### 6. Mi sesión · Fuera de combate

Tarjetas en este orden: **Descansos** (corto directo; largo = "Pedir al DM" con estado
"pendiente" y animación de hoguera/luna al concederse), **Subir de nivel** (solo visible cuando el
DM lo ha concedido, con destello), **Preparar conjuros** (contador N/máx), **Botín del grupo**,
**Inventario**, **Tiendas abiertas**, **Mensajes del DM**, **Rasgos y trasfondo**, **Notas**.

### 7. Subida de nivel (nueva)

Asistente modal a pantalla completa, un paso por elección, generado desde la tabla "Elecciones
por nivel" del PHB para esa clase/subclase y nivel:

1. "Subes a nivel N" con lo que ganas automáticamente (rasgos nuevos con su texto, espacios).
2. PG: tirar en la app (animación de dado) o valor fijo, según ajuste de campaña.
3. Una pantalla por `[ELECCIÓN]`: subclase, estilo de combate, mejora de característica o dote
   (con prerrequisitos validados), pericia, trucos y conjuros nuevos (y sustitución opcional),
   invocaciones, metamagia, maniobras, don del pacto, tótem, enemigo predilecto, terreno,
   disciplinas. Cada opción con su texto del PHB y, si tiene efecto numérico, el desglose
   resultante ("CA 16 → 17").
4. Resumen y "Confirmar". El botón no se activa hasta completar todo. Al confirmar: animación de
   celebración (runas que ascienden, el número de nivel que crece) y la hoja actualizada.

Las elecciones quedan guardadas por nivel, así la hoja muestra "Estilo de combate: Defensa (nivel
1)" y el desglose de CA cita ese origen. Multiclase: elegir clase al inicio del asistente, con
requisitos del PHB validados.

### 8. Hoja completa

Se mantiene con pestañas, pero el Resumen pasa a: características (6 fichas iguales, 3×2), la
rejilla fija de combate (3×2), salvaciones y habilidades en listas con el valor a la derecha.

### 9. Conexión y servidor (diagnóstico)

En "Servidor": prueba en cuatro pasos con resultado por línea: 1) API `/health`, 2) sesión
`/auth/me`, 3) `negotiate` del hub, 4) WebSocket (o transporte alternativo usado). Cada fallo
dice su causa probable ("el proxy no reenvía Upgrade"). Es la pantalla que resuelve "no sé si
estoy conectado".

## Estética

- **Paleta "rúnica"**, oscura por defecto, sin pergamino beige: fondo obsidiana `#14110F`, piedra
  `#231D19`, hueso `#E7DCC6` para texto, **ascua** `#D9671E` como acción principal, arcano
  `#8A6BD1`, musgo `#5E7A4A` para curación, sangre `#9B2226` para daño, oro viejo `#B8923A` solo
  para bordes y pequeños sellos. Modo claro opcional con hueso/arcilla. Textura: grano sutil en
  fondos y bordes irregulares "a pincel" en tarjetas (pintados con `CustomPainter`, sin imágenes).
- **Iconos propios**: un set de ~45 glifos dibujados a trazo (SVG en el repositorio, licencia del
  proyecto), estilo talla en piedra: clases, acciones, recursos, estados, conexión. Sin
  game-icons ni Material en las vistas de juego.
- **Fuentes**: cuerpo en la fuente del sistema (sin empaquetar nada); solo un título con una
  fuente OFL (ver duda 5).
- **Animaciones** (todas con `CustomPainter`/`AnimationController`, sin librerías): furia y
  temerario (llamas), castigo (destello), ki (trazos), daño (sacudida y tinte), curación (pulso),
  0 PG (viñeta oscura), descanso corto (hoguera) y largo (luna), subir de nivel (celebración),
  conexión (sello que late), mensaje del DM (sello que se rompe).

## Descansos y permisos (cambios de regla)

- Corto: el jugador lo hace cuando quiere (gasta dados de golpe en la app). *(Ver duda 1.)*
- Largo: el jugador **pide**; el DM concede a uno o a todos desde la Mesa (o lo fuerza sin
  petición). Al concederse se aplican las reglas del PHB (PG al máximo, mitad de dados de golpe,
  recargas, −1 agotamiento, preparar conjuros).
- Subir de nivel: el DM **concede** el nivel (a uno o al grupo); el jugador completa el asistente.
  *(Ver duda 2.)*

## Dudas que decides tú

1. ¿El descanso **corto** también requiere permiso del DM, o solo el largo?
2. ¿Quién dispara la subida de nivel: el DM concede "puedes subir a N" (recomendado) o se lleva
   cuenta de PX en la app y sube solo?
3. PG al subir: ¿tirada en la app, valor fijo, o lo elige el DM por campaña (recomendado)?
4. ¿Dotes activadas por campaña (regla opcional del PHB) o siempre?
5. Fuentes: ¿acepto una fuente OFL para títulos (licencia libre, sin derechos restrictivos) o
   todo con la fuente del sistema?
6. Paleta: ¿oscura por defecto como propongo, o quieres mantener la claridad de pergamino?
7. Multiclase en el asistente de subida desde el principio, o en una fase posterior?
8. Para la nube tachada necesito un dato: en el servidor, `docker compose logs api | grep -i
   "hubs/campaign"` mientras abres una campaña, y confirmar que el `map` y las cabeceras
   `Upgrade`/`Connection` están en tu nginx. Con eso te digo la causa exacta.

## Orden de implementación propuesto

1. Correcciones rápidas: rejilla de combate, Imposición de manos, estado de conexión y
   diagnóstico, endurecimiento del hub.
2. Descanso largo con permiso y concesión de nivel (servidor + Mesa del DM + Mi sesión).
3. Catálogo de elecciones por nivel (datos desde la tabla del PHB; las subclases no SRD vienen
   del paquete privado) y asistente de subida de nivel.
4. Nueva piel: paleta, iconos propios, texturas y animaciones.
