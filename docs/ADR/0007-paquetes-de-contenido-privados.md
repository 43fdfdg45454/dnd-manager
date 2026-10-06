# ADR 0007 — Paquetes de contenido privados por instancia

**Estado**: aceptado.

## Contexto

El SRD 5.1 incluye una sola subclase por clase y una fracción de las opciones publicadas. Los
usuarios que poseen los manuales oficiales quieren usar su contenido (subclases, objetos, conjuros,
razas, trasfondos), pero ese material tiene copyright de Wizards of the Coast y no puede entrar en
este repositorio público (ADR 0003, `CLAUDE.md`).

## Decisión

- El repositorio y la imagen Docker solo contienen el SRD 5.1 (CC-BY 4.0).
- La API acepta **paquetes de contenido** en JSON (`docs/content-packs.md`) que el administrador
  importa en su instancia desde la app (`/api/v1/admin/content-packs`). Cada definición del
  catálogo lleva una columna `Source` (`srd` o el id del paquete); reimportar un paquete reemplaza
  su contenido y borrarlo lo elimina sin tocar el SRD ni los personajes (que marcan "contenido no
  disponible").
- Los paquetes se crean fuera del repositorio, a partir de material que el usuario posea, y nunca
  se commitean (`content-packs/` en `.gitignore`). El ejemplo de la documentación es ficticio.

## Consecuencias

- Las consultas de catálogo son multi-fuente; los índices de los paquetes llevan el prefijo del
  id del paquete para no chocar con el SRD.
- Las copias de seguridad de la base de datos incluyen el contenido importado.
