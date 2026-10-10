# ADR 0009 — Framework de tabletop y sistemas de juego como módulos

**Estado**: propuesto (pendiente de confirmación del propietario).

## Contexto

La aplicación nació como companion de D&D 5e: el dominio de personajes (características, salvaciones,
competencia, espacios de conjuro, salvaciones de muerte, descansos, subida de nivel), el catálogo
(clases, razas, trasfondos, conjuros, objetos) y buena parte de la app Flutter (ficha, asistente,
combate, paneles de clase) asumen las reglas 5e. El resto (usuarios, campañas, roles, sesiones,
diario, lore, mapas, biblioteca, mensajes, tienda y transacciones, peticiones de cambio, dados,
tiempo real, releases) no depende de ningún sistema.

El objetivo pasa a ser un **framework para desarrollar experiencias de mesa** donde D&D 5e (SRD +
PHB) sea el primer sistema y Valda's Spire of Secrets se sume después.

Medido sobre `master` el 2026-10-10:

| Capa | Genérico (núcleo) | Depende de 5e |
|---|---|---|
| `Dnd.Domain` | Users, Campaigns, Sessions, Lore, Maps, Library, Files, Messages, Releases, Common, Rules (dados) | Characters (47 ficheros), Catalog (33), Items (10: plantilla con daño, CA, rareza, atunement) |
| `Dnd.Application` | campañas, sesiones, lore, mapas, biblioteca, mensajes, tienda, peticiones, admin | Characters, Catalog, Party (descansos, nivel), parte de Items |
| `Dnd.Infrastructure` | persistencia, SMTP, ficheros, SignalR | `Catalog/SrdDataset`, seed del SRD, paquetes de contenido, migraciones del catálogo y la ficha |
| App Flutter (~60 k líneas) | `core` (10 k), campaigns, session, sessions, lore, maps, library, admin, auth, home, settings, server (~14 k) | characters (23 k), catalog (4 k), items (5,6 k, parcial), dice (1,2 k, genérico salvo etiquetas) |

Valda's Spire of Secrets es un suplemento **compatible con 5e** (clases nuevas, subclases, dotes,
conjuros, objetos): no es otro sistema, es contenido del sistema 5e. Lo que hoy le falta al formato
de paquetes es admitir clases completas, dotes y mecánicas propias de esas clases.

## Decisión

1. **Un núcleo y sistemas de juego como módulos.** El servidor y la app se dividen en un núcleo
   genérico (todo lo de la columna izquierda) y módulos de sistema. Cada campaña declara su sistema
   (`Campaign.SystemId`); el servidor expone `GET /api/v1/systems` y carga los módulos registrados.
   Un solo despliegue (una API, una app) sirve varios sistemas.
2. **Contrato de sistema en el servidor** (`IGameSystem`): esquema de hoja (qué guarda un
   personaje), cálculo de la hoja con desgloses, creación y subida de nivel, resumen de combate,
   descansos y recuperación, catálogo (tipos de definición, importación de paquetes) y validación de
   elecciones. El núcleo solo conoce `Character` como entidad con identidad, campaña, dueño, estado,
   retrato, notas y un documento de hoja que el módulo interpreta.
3. **Contrato de sistema en la app** (`GameSystemUi`): pestañas de la ficha, asistente de creación,
   flujo de subida de nivel, vista de combate, páginas de catálogo, panel de grupo del DM. El núcleo
   aporta navegación, tema, caché, dados, tiempo real, campañas, sesiones, lore, mapas, biblioteca,
   mensajes, tienda y peticiones de cambio.
4. **D&D 5e es el primer módulo** (`Systems/Dnd5e` en servidor y `packages/dnd5e` en la app) y se
   traslada tal cual: mismas reglas, mismos endpoints bajo el prefijo del sistema, mismas pantallas.
   El SRD 5.1 deja de ser un seed incrustado y pasa a ser el **paquete base** del módulo (CC-BY,
   empaquetado con él); el PHB sigue siendo un paquete privado de la instancia (ADR 0007).
5. **Los paquetes de contenido pertenecen a un sistema.** El formato 5e (ADR 0007,
   `docs/content-packs.md`) crece hasta cubrir todo lo que trae un manual: clases completas con sus
   tablas y recursos, dotes, condiciones, reglas opcionales y tablas. Valda's Spire of Secrets se
   integra como paquete privado del sistema 5e, igual que el PHB.
6. **Sin reglas inventadas.** El módulo 5e implementa lo que dicen el SRD, el PHB y, en su paquete,
   Valda's; lo que un paquete no puede expresar se documenta como límite, no se improvisa.

## Consecuencias

- Reestructuración grande pero mecánica: mover, no reescribir. Cada fase deja `master` desplegable y
  los tests verdes; la app sigue funcionando para 5e en todo momento.
- Nombres: la solución, los paquetes y la imagen dejan de llamarse `Dnd.*` cuando el núcleo quede
  separado (fase 31); el nombre de producto lo decide el propietario.
- Un segundo sistema real valida el contrato; hasta entonces el contrato se diseña a partir de lo que
  5e necesita y de un sistema mínimo de prueba ("hoja libre": atributos, recursos y notas definidos
  en el paquete) que sirve para partidas de otros juegos sin automatización.
- Las migraciones de EF del catálogo y la ficha pasan a ser del módulo 5e; las del núcleo, del núcleo.
