# ADR 0001 — Cliente móvil en Flutter

**Estado**: aceptado.

## Contexto

Solo importa Android por ahora. El servidor es .NET. Se valoraron Kotlin/Compose, .NET MAUI, PWA
y Flutter.

## Decisión

Flutter. Buena experiencia de usuario, tooling estable, visor PDF y caché local maduros, y deja
abierta una versión iOS sin reescribir.

## Consecuencias

- Dos lenguajes en el repo (C# y Dart). Los contratos de la API se documentan con OpenAPI y el
  cliente Dart se genera o se escribe a mano a partir de ellos.
- El APK se distribuye directamente (sin Play Store); la API expone la última versión.
