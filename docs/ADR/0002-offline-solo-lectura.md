# ADR 0002 — Offline solo lectura

**Estado**: aceptado.

## Contexto

Las sesiones son presenciales y la mesa puede quedarse sin red. Un offline-first completo con
cola de cambios y resolución de conflictos añade mucha complejidad.

## Decisión

La app cachea localmente (drift/sqlite) el último estado recibido de cada pantalla y lo muestra
sin red con un aviso de "sin conexión". Toda escritura requiere red y se deshabilita sin ella.

## Consecuencias

- El servidor es la única fuente de verdad; no hay sincronización bidireccional.
- Los PDF de la biblioteca pueden descargarse al dispositivo para consulta sin red.
