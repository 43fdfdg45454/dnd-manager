# Iconos

Los iconos de esta carpeta son **obra original de este proyecto**, dibujados a mano para la app,
y se distribuyen bajo la misma licencia que el resto del repositorio. No proceden de ninguna
colección de terceros, por lo que no requieren atribución adicional.

Formato común:

- SVG con `viewBox="0 0 24 24"`, solo trazo (`fill="none"`), `stroke="currentColor"`,
  `stroke-width="2"`, extremos y uniones redondeados.
- Estilo "talla en piedra": segmentos rectos y esquinas marcadas, legibles a 20 px.
- El nombre del fichero coincide con el miembro de `AppIcons` (`lib/core/theme/icons.dart`); la
  app los tiñe con `ColorFilter.mode(color, BlendMode.srcIn)`.

Para añadir un icono: dibujarlo con las mismas reglas, guardarlo aquí como `<nombre>.svg` y
añadir el miembro correspondiente a `AppIcons` con un icono Material de respaldo.
