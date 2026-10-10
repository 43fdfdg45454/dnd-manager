# Hoja de ruta: del companion de D&D al framework de tabletop

Decisión de fondo en [ADR 0009](ADR/0009-framework-y-sistemas-de-juego.md). Cada fase lleva su
contrato en `docs/specs/` y se fusiona en `master` con la CI en verde, como hasta ahora.

| Fase | Qué | Resultado visible |
|---|---|---|
| 29 | **Correcciones de uso** (próximas sesiones caducadas, descripciones en Elecciones, altura y peso, condiciones, concentración junto a las condiciones, botones de información, navegación Combate/Detalle). | App corregida; sin cambios de arquitectura. |
| 30 | **Inventario y contrato.** Clasificar cada entidad, endpoint y pantalla como núcleo o 5e; definir `IGameSystem` y `GameSystemUi` sobre papel con lo que 5e necesita hoy. | ADR 0009 aceptado, `docs/specs/fase-30-inventario-y-contrato.md` con la tabla completa y los dos contratos. Sin cambios de código. |
| 31 | **Sistema por campaña.** `Campaign.SystemId`, `GET /systems`, registro de sistemas en el servidor con 5e como único registrado; la app muestra el sistema al crear la campaña. | Ningún cambio de comportamiento; migración nueva. |
| 32 | **División del servidor.** `Core.Domain/Application/Infrastructure/Api` y `Systems.Dnd5e.*`; los endpoints de 5e quedan bajo `/api/v1/systems/dnd5e/...` con alias temporales de las rutas actuales. Renombrado de la solución, la imagen y el repositorio a OpenTRPG. | API idéntica para el cliente actual; solución nueva. |
| 33 | **División de la app.** `packages/core` y `packages/dnd5e`; el núcleo monta la ficha, el asistente, el combate y el catálogo del sistema de la campaña a través de `GameSystemUi`. | App idéntica para el usuario. |
| 34 | **SRD como paquete base, formato 5e completo y paquetes activables por campaña.** El SRD se carga como paquete empaquetado con el módulo; el formato admite clases completas (tabla, recursos, conjuros, opciones por nivel), dotes, condiciones y reglas opcionales. Reimportar el PHB privado con lo que antes no cabía; cada campaña elige sus paquetes activos. | Catálogo igual o mayor; `content-packs.md` v3. |
| 35 | **Valda's Spire of Secrets** como paquete privado del sistema 5e (clases, subclases, dotes, conjuros, objetos) con las mecánicas nuevas que necesite el módulo. | Campañas 5e con contenido de Valda's. |

## Decisiones del propietario (2026-10-10)

1. Nombre del producto y del repositorio: **OpenTRPG**.
2. Valda's es un paquete del sistema 5e, **activable por campaña**.
3. Separación primero (30 → 33), después contenido (34 → 35).
4. El sistema mínimo "hoja libre" queda fuera hasta que haya un segundo juego concreto.
