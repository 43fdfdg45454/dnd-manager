# Hoja de ruta: del companion de D&D al framework de tabletop

Decisión de fondo en [ADR 0009](ADR/0009-framework-y-sistemas-de-juego.md). Cada fase lleva su
contrato en `docs/specs/` y se fusiona en `master` con la CI en verde, como hasta ahora.

| Fase | Qué | Resultado visible |
|---|---|---|
| 29 | **Inventario y contrato.** Clasificar cada entidad, endpoint y pantalla como núcleo o 5e; definir `IGameSystem` y `GameSystemUi` sobre papel con lo que 5e necesita hoy. | ADR 0009 aceptado, `docs/specs/fase-29-*.md` con la tabla completa y los dos contratos. Sin cambios de código. |
| 30 | **Sistema por campaña.** `Campaign.SystemId`, `GET /systems`, registro de sistemas en el servidor con 5e como único registrado; la app muestra el sistema al crear la campaña. | Ningún cambio de comportamiento; migración nueva. |
| 31 | **División del servidor.** `Core.Domain/Application/Infrastructure/Api` y `Systems.Dnd5e.*`; los endpoints de 5e quedan bajo `/api/v1/systems/dnd5e/...` con alias temporales de las rutas actuales. Renombrado de la solución y la imagen. | API idéntica para el cliente actual; solución nueva. |
| 32 | **División de la app.** `packages/core` y `packages/dnd5e`; el núcleo monta la ficha, el asistente, el combate y el catálogo del sistema de la campaña a través de `GameSystemUi`. | App idéntica para el usuario. |
| 33 | **SRD como paquete base y formato 5e completo.** El SRD se carga como paquete empaquetado con el módulo; el formato admite clases completas (tabla, recursos, conjuros, opciones por nivel), dotes, condiciones y reglas opcionales. Reimportar el PHB privado con lo que antes no cabía. | Catálogo igual o mayor; `content-packs.md` v3. |
| 34 | **Valda's Spire of Secrets** como paquete privado del sistema 5e (clases, subclases, dotes, conjuros, objetos) con las mecánicas nuevas que necesite el módulo. | Campañas 5e con contenido de Valda's. |
| 35 | **Sistema mínimo "hoja libre"** para validar el contrato con un segundo sistema. | Una campaña de otro juego con hoja definida por paquete, sin automatización. |

Orden alternativo: la fase 33 no depende de la 31 ni de la 32 y es la que desbloquea Valda's; puede
adelantarse si el contenido importa más que la separación.

## Decisiones pendientes del propietario

1. Nombre del producto y del repositorio tras la fase 31.
2. Valda's como paquete del sistema 5e (recomendado: es compatible con 5e) o como sistema aparte.
3. Orden: separación primero (29 → 32 → 33 → 34) o contenido primero (29 → 33 → 34 → 30 → 32).
4. Si el sistema mínimo "hoja libre" (fase 35) interesa o se deja para cuando haya un segundo juego
   concreto.
