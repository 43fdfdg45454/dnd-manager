# ADR 0003 — Contenido base: SRD 5.1 (reglas 2014)

**Estado**: aceptado.

## Contexto

Hace falta un catálogo de razas, clases, hechizos e ítems. Las reglas 2014 son las más usadas y
cuentan con datasets estructurados; el SRD 5.1 está bajo CC-BY 4.0.

## Decisión

Importar el SRD 5.1 desde el dataset JSON del proyecto `5e-database` (licencia MIT, contenido
CC-BY 4.0) incluido en `server/seed/srd/`. El contenido se mantiene en inglés; la UI está en
español y el DM puede renombrar ítems o hechizos en su campaña.

El SRD 5.1 en PDF (CC-BY 4.0) se empaqueta como documento de sistema de la biblioteca. No se
incluye ningún material con copyright de Wizards of the Coast (Player's Handbook, DMG, etc.); el
administrador de cada instancia puede subir sus propios PDF.

## Consecuencias

- Atribución CC-BY visible en la app y en el README.
- Si en el futuro se migra a SRD 5.2 (reglas 2024), el catálogo se versiona por `ruleset`.
