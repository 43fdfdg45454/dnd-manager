# Fase 23 — Personalización: paletas, fuentes y más

Contrato cerrado. Petición del usuario: alternar paletas de colores y fuentes desde
"Personalización", con combinaciones bien pensadas y **al menos una sin naranja, de negro y
gris**. Hoy existen modo oscuro/claro/sistema, tamaño de texto y nivel de animación
(`app/lib/features/settings/`), y los colores salen de `AppTokens` (`app/lib/core/theme/tokens.dart`).

## Paletas

Cada paleta define **las dos variantes** (oscura y clara) de todos los roles de `AppTokens`
(`obsidian`, `stone`, `stoneRaised`, `bone`, `boneMuted`, `ember`, `arcane`, `moss`, `blood`,
`oldGold`, `rune`) y el `ColorScheme` derivado. El selector de modo (oscuro/claro/sistema) se
mantiene y elige la variante.

| Clave | Nombre | Oscura | Clara |
|---|---|---|---|
| `ember` | Obsidiana y brasa (actual, por defecto) | negro piedra, brasa naranja, oro viejo | la clara actual |
| `graphite` | Grafito | **negro con grises**, acento plata/gris claro, sin naranja ni dorado | **gris con negro**: fondos gris claro, texto y botones casi negros |
| `gold` | Oro y fuego | negro cálido, primario dorado, secundario naranja | marfil con dorado y naranja tostado |
| `forest` | Bosque | verde casi negro, musgo y bronce | crema verdosa con verde oscuro |
| `arcane` | Arcano | azul noche, violeta y plata | lavanda pálida con índigo |
| `blood` | Sangre y hueso | negro rojizo, carmesí y hueso | hueso con burdeos |

Reglas de diseño: contraste WCAG AA (≥ 4.5:1 texto normal, ≥ 3:1 texto grande, iconos y bordes de
controles) verificado **en un test** para cada paleta y variante (`bone`/`boneMuted` sobre
`obsidian`, `stone` y `stoneRaised`; texto de botón sobre `ember`). Los colores semánticos
(`moss` = curación, `blood` = daño, `arcane` = magia) conservan su significado en todas las
paletas aunque cambie el tono (en Grafito, desaturados pero distinguibles). Ninguna paleta usa
naranja en Grafito (test: matiz fuera de 15°–45° con saturación > 0.35 para todos sus roles).

- **Colores de clase** (interruptor, por defecto activado): hoy los acentos de clase
  (`classThemeOf`) tiñen tarjetas y paneles; desactivado, se usa el acento de la paleta. En
  Grafito se recomienda desactivado (la vista previa lo muestra).
- **Texturas** (interruptor): activa/desactiva las texturas de piedra (`textures.dart`).

## Fuentes (todas SIL OFL, empaquetadas con su licencia en `app/assets/licenses/`)

- **Títulos**: Almendra (actual), Cinzel, IM Fell English, "Igual que el texto".
- **Texto**: Source Sans 3 (actual), Atkinson Hyperlegible Next (alta legibilidad), Lora (serif),
  Fuente del sistema (sin empaquetar).
- Descargar de `github.com/google/fonts` (`ofl/<familia>`); solo los pesos usados (400/700 o
  variable). Si la red no lo permite, informar sin inventar ficheros.
- Tamaño de texto: Normal, Grande y **Muy grande (1.4)**.

## Pantalla "Personalización"

Secciones Paleta (tarjetas con muestra de colores: fondo, tarjeta, texto, primario y semánticos,
Keys `palette-<clave>`), Modo, Fuente de títulos (`title-font-<clave>`), Fuente de texto
(`body-font-<clave>`), Tamaño de texto, Colores de clase, Texturas, Animaciones. **Vista previa en
vivo** arriba (una tarjeta de personaje en miniatura con PG, un botón y un `StatValue`) que
cambia al tocar cualquier opción. Todo se guarda en `shared_preferences` y se aplica al instante
sin reiniciar. Botón "Restablecer" a los valores por defecto.

## Tests

Contraste de todas las paletas y variantes; Grafito sin naranja; persistencia y restauración de
cada preferencia; la vista previa cambia al elegir paleta y fuente; `MaterialApp` usa la paleta y
la fuente elegidas.

## Verificación

`cd app && flutter analyze && flutter test`. Sin cambios de servidor.
