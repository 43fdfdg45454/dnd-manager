# Fase 35 — Valda's Spire of Secrets completo como paquete privado del sistema 5e

Contrato de implementación. Parte de `docs/framework-roadmap.md` (fila 35), del formato de paquete
v3 de la fase 34 (`docs/content-packs.md`) y de la decisión del usuario del 2026-10-10: **se importa
el libro entero**, no una selección.

Todo el contenido de Valda's es Product Identity de Mage Hand Press (créditos, p. 5 del PDF): el
paquete, los scripts que lo construyen, los informes con texto del libro y las copias del PDF viven
**solo** en `/mnt/project-files/dnd-manager/` (y en `content-packs/`, ignorado por git). En el
repositorio público solo entran cambios de código, formato y tests con datos **ficticios**.

## 0. Fuente

- PDF con capa de texto: `/mnt/project-files/dnd-manager/valdas-spire-of-secrets.pdf` (385 páginas).
- Texto extraído (`pdftotext -layout`, `\f` separa páginas, página N del PDF = bloque N):
  `/mnt/project-files/dnd-manager/valda-texto/valdas-spire-of-secrets.txt`.
- Índice del libro (páginas del PDF):

| Capítulo | Páginas | Contenido |
|---|---|---|
| 1 Races | 7–20 | Geppettin, Mandrake, Mousefolk, Spirithost y la raza variante Near-Human (16–20) |
| 2 Classes | 21–190 | Diez clases nuevas con sus subclases y listas propias: Alchemist 27 (Bomb Formulae, Discoveries, Fields of Study), Captain 45 (Banners, Cohorts), Craftsman 63 (Artisans' Guilds, Masterwork Properties 78–90), Gunslinger 91 (Deeds, Creeds), Investigator 103 (Occult Specializations), Martyr 115 (Mortal Burdens), Necromancer 127 (Grave Ambitions, Undead Thralls 140), Warden 145 (Champion's Call), Warmage 157 (Tricks, Houses), Witch 173 (Hexes, Grand Hexes, Crafts) |
| 3 Subclasses | 191–270 | Subclases (y algún rasgo nuevo) para las doce clases del SRD |
| 4 Customization | 271–290 | Multiclasing de las clases nuevas (272), **Auxiliary Levels** (275), Feats (282), **Starter Feats** (286) |
| 5 Equipment | 291–320 | Armas (292, incluidas armas de fuego por épocas), armaduras (302), objetos mágicos (302–320) |
| 6 Spells | 321–358 | Etiquetas (322), listas por clase (322–328), descripciones (329–358) |
| 7 Appendices | 359–385 | A Variant Rules (360), B Familiars (364), C Monstrous Grafts (369), D Siegeball (374) |

## 1. Resultado

Un paquete v3 `id: "valdas-spire"`, `name: "Valda's Spire of Secrets"`, `system: "dnd5e"`,
`requires: []` (si alguna subclase del capítulo 3 amplía una clase que solo existe en el PHB
privado, esa subclase va en un **segundo** paquete `valdas-spire-phb` con `requires:
["phb-2014"]`, para que el principal se active en campañas sin PHB), en
`/mnt/project-files/dnd-manager/valdas-spire.json`, construido de forma reproducible por scripts en
`/mnt/project-files/dnd-manager/valdapack-src/` (misma técnica que `phbpack-src/`: Python, lectura
del texto por páginas, salida JSON v3). Debe importar sin errores de validación, activarse en una
campaña y permitir crear y subir al nivel 20 por la API un personaje de **cada** clase nueva y de
cada raza nueva.

Secciones del paquete y lo que contienen:

| Sección v3 | Contenido de Valda's |
|---|---|
| `races[]`, `traits[]` | Las cuatro razas con sus subrazas; Near-Human como raza con sus variantes (`subraces`) y opciones (`optionSets`) según lo describa el libro |
| `classes[]` (completas) | Las diez clases: dado de golpe, salvaciones, competencias, equipo inicial, multiclase (cap. 4 p. 272), lanzamiento (`progression` o `table`), 20 niveles con rasgos, `resources` (bombas, inspiración de capitán, deeds, hexes…), subclases, lista de conjuros propia |
| `classes[]` con `extends` | Capítulo 3: subclases para las clases del SRD; los rasgos nuevos "de clase" que añade el capítulo se expresan como `levelChoices`/`features` de la extensión |
| `optionSets[]` | Bomb Formulae, Discoveries, Banners, Artisans' Guilds, Deeds, Occult Specializations, Mortal Burdens, Grave Ambitions, Warmage Tricks, Hexes, Grand Hexes, Witch's Crafts, etc., con sus prerequisitos y `grants`/`resource`/`modifiers` |
| `feats[]` | Capítulo 4: dotes y **Starter Feats** (ver §2) |
| `items[]` | Capítulo 5: armas (incluidas armas de fuego con `firearm`), armaduras, objetos mágicos; propiedades de arma y armadura nuevas en `reference` (weapon-properties, armor-properties) y las Masterwork Properties del Craftsman (ver §2) |
| `spells[]` | Capítulo 6 completo, con `lists` por clase (SRD y nuevas) y las etiquetas (`tags`) del libro |
| `creatures[]` | Familiares del apéndice B, cohortes del Captain, esbirros no muertos del Necromancer, y cualquier bloque de estadísticas del libro |
| `rules[]` | Apéndice A (reglas variantes), apéndice C (injertos: su regla general; cada injerto como `items[]` si tiene coste y requisitos, o como `rules[]` si es solo texto), apéndice D (Siegeball), las reglas de Auxiliary Levels y Starter Feats, las reglas de armas de fuego, y la introducción de cada capítulo que explique una mecánica |
| `reference` | Vocabularios nuevos: propiedades de arma y armadura, etiquetas de conjuro, categorías de equipo nuevas |

Lo que el libro trae y el formato **no** puede expresar se registra en el inventario de la 35A y
se resuelve en la 35B antes de construir el paquete; no se recorta contenido por comodidad. Lo que
es puro texto de ambientación (cartas de Valda, descripciones de sabor) entra como `description`
del elemento al que acompaña; las ilustraciones no.

## 2. Mecánicas nuevas previstas (a confirmar en la 35A)

Estas mecánicas aparecen en el índice y, según la lectura del texto, pueden requerir cambios en el
formato y en el módulo 5e (servidor y app). La 35A las confirma o descarta y añade las que falten.

| Mecánica | Hipótesis de modelado |
|---|---|
| **Auxiliary Levels** (p. 275): clases de un solo nivel que se toman como un nivel de multiclase | `classes[]` con `auxiliary: true` y `maxLevel: 1`; el asistente de subida las ofrece como opción de multiclase una sola vez; cuentan como nivel de personaje y de multiclase según diga el libro |
| **Starter Feats** (p. 286): dotes que se eligen en el nivel 1 | `feats[].starter: true`; el asistente de creación ofrece la elección en el nivel 1 con la regla exacta del libro (en lugar de qué, y si es opcional de la mesa → regla variante activable, ver más abajo) |
| **Masterwork Properties** (p. 78–90): propiedades que el Craftsman aplica a armas y armaduras | `optionSets` del Craftsman cuyas opciones llevan `modifiers` sobre el objeto; en la app, un ítem "mejorado" muestra la propiedad en `itemExtras`. Si hace falta, `items[].properties` admite propiedades definidas en `reference` del paquete |
| **Cohorts / Undead Thralls / Familiars**: criaturas que acompañan al personaje | Ya existe `companion` en el formato (fase 24); se amplía si el libro permite varios o con progresión por nivel |
| **Armas de fuego** (p. 292): recarga, encasquillado, épocas | `firearm{reload,misfire,era}` informativo (decisión de la 34A); si el libro define una regla general, entra en `rules[]`. Sin contador automático de disparos en esta fase |
| **Reglas variantes** (apéndice A) que una mesa activa o no | `rules[]` con `category: "variant"`; en esta fase son solo texto consultable; no hay interruptores de regla por campaña |
| **Etiquetas de conjuro** (p. 322) | `spells[].tags` y vocabulario en `reference`; el compendio permite filtrar por etiqueta |
| **Multiclase** de las clases nuevas (p. 272) | `classes[].multiclassing` ya existe; se comprueba que cubra lo que pide el libro (prerequisitos compuestos, competencias) |

## 3. Entregas

### 35A — Inventario y huecos del formato (lectura, sin código de producto)

1. Leer el libro completo y escribir `/mnt/project-files/dnd-manager/valdas-inventario.md`: por
   capítulo, cada elemento con su página, su sección v3 de destino y, si no cabe en el formato o el
   módulo no lo sabe aplicar, una línea en la tabla "Huecos" con la propuesta mínima (campo nuevo,
   cambio del asistente, cambio de la hoja). Recuentos por tipo.
2. Confirmar o corregir §2 con la regla exacta del libro (citando página), incluidas las
   condiciones de Auxiliary Levels y Starter Feats.
3. Nada del inventario entra en el repositorio (contiene texto del libro).

### 35B — Cambios de formato y módulo

1. Para cada hueco aceptado de la 35A: campo en `ContentPackModels`, validación, importador,
   persistencia, cálculo de hoja o asistente, DTO y app, con tests con datos **ficticios** en
   `ContentPackV3Tests` y en `app/test`. `docs/content-packs.md` documenta cada campo nuevo.
2. Si la 35A no encuentra huecos, la 35B no existe.

### 35C — Construcción del paquete

1. Scripts en `/mnt/project-files/dnd-manager/valdapack-src/` (`build.py` y un módulo por
   capítulo) que generan `/mnt/project-files/dnd-manager/valdas-spire.json` (y
   `valdas-spire-phb.json` si aplica) a partir del texto. Deterministas: misma entrada, misma salida.
2. Informe `/mnt/project-files/dnd-manager/valdas-informe.md`: recuentos por sección, elementos
   que se cargaron solo como texto (sin mecánica automática) y por qué, y avisos del validador.
3. Test `PrivateValdasPackTests` en `OpenTrpg.Systems.Dnd5e.Api.Tests` (se omite si falta
   `content-packs/valdas-spire.json`): importa, activa en una campaña, crea un personaje de cada
   clase nueva y de cada raza nueva, los sube al nivel 20 por la API eligiendo siempre la primera
   opción válida, y comprueba que la hoja no tiene elecciones inválidas; comprueba también que con
   el paquete desactivado un personaje SRD no cambia.
4. Copia del paquete en `content-packs/` del repositorio local para ejecutar el test; nunca en un
   commit.

## 4. Lo que no cambia

- El SRD y el PHB privado siguen cargando igual; Valda's es un paquete más, activable por campaña.
- Ninguna mecánica de Valda's se aplica a personajes de campañas que no lo activan.
- Nada con copyright en el repositorio público, ni en tests, ni en fixtures, ni en documentación.

## 5. Riesgos

| Riesgo | Mitigación |
|---|---|
| Extracción de texto con columnas mezcladas o tablas rotas (`-layout`) | Los scripts leen por página y por bloque; los casos difíciles (tablas de clase, listas de conjuros) se transcriben a mano en el módulo del capítulo con la página citada |
| Diez clases nuevas con listas propias saturan el asistente y la subida de nivel | El test de 35C sube cada clase al 20 por la API; lo que falle se corrige en el módulo, no se recorta del paquete |
| Auxiliary Levels y Starter Feats tocan el multiclase y la creación | Se modelan en la 35B con tests ficticios antes de construir el paquete |
| Tamaño del paquete | El PHB privado pesa poco; el SRD v3 pesa 1,6 MB; el límite de listas del validador (1000) se revisa si hace falta |
