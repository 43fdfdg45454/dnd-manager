# ADR 0008 — Versionado semántico con GitVersion

**Estado**: aceptado.

## Contexto

Las releases de la CI salían todas como `0.1.0` (con `-build.N`), porque la versión estaba fija en
`app/pubspec.yaml` y la imagen Docker no recibía ninguna. No había forma de saber qué cambió entre
dos versiones ni de que la app y la API reportaran la misma.

## Decisión

- Versión semántica `MAYOR.MENOR.PARCHE` calculada por **GitVersion 6** (`GitVersion.yml`, flujo
  `GitHubFlow/v1`, `master` en modo `ContinuousDeployment`).
- Cada push a `master` sube el parche respecto a la última etiqueta `vX.Y.Z`; `+semver: minor` o
  `+semver: major` en un commit sube la menor o la mayor. La CI crea la etiqueta al publicar la
  release, y es la base del cálculo siguiente. Las ejecuciones de `master` van en fila.
- La misma versión va al APK (`versionName`), a la imagen (`:X.Y.Z` y `GET /api/v1/app/info`) y a la
  release de GitHub. `versionCode` sigue siendo el número de ejecución de la CI.
- Se elimina el workflow manual `release.yml`; su publicación opcional en el servidor pasa a la CI.

## Consecuencias

- Nadie edita versiones a mano; las etiquetas antiguas `v0.1.0-build.N` quedan como historia y la
  primera versión gestionada es `0.2.0` (`next-version`).
- La CI necesita el historial completo (`fetch-depth: 0`).
