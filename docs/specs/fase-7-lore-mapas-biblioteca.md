# Fase 7 — Lore, mapas con pines y biblioteca de documentos

Contrato cerrado. Depende de la fase 2 (`ICampaignAccess`) y de un servicio de ficheros común.

## Ficheros (común a mapas, retratos, adjuntos, PDF y APK)

```
StoredFile  Id, CampaignId? (null = global/instancia), OwnerUserId, FileName, ContentType, SizeBytes, Sha256,
            Kind (MapImage|Portrait|LoreAttachment|LibraryDocument|AppRelease), StoragePath, CreatedAt
```

- `IFileStorage` (Infrastructure): guarda en `FileStorage:RootPath/<yyyy>/<MM>/<id><ext>`, borra, abre
  stream. Límite `FileStorage:MaxUploadMegabytes`. Tipos permitidos por `Kind`: imágenes
  (`image/png`, `image/jpeg`, `image/webp`), PDF (`application/pdf`), APK
  (`application/vnd.android.package-archive`).
- `POST /api/v1/files` (multipart: `file`, `kind`, `campaignId?`) → `201 StoredFileDto { id, fileName, contentType, sizeBytes, url }`.
  Autorización: `MapImage`/`LoreAttachment` ≥ DM de la campaña; `Portrait` dueño del personaje o DM;
  `LibraryDocument`/`AppRelease` solo `Admin`.
- `GET /api/v1/files/{id}` → stream con `Content-Type`, `ETag` y soporte de **`Range`**
  (`enableRangeProcessing: true`). Autorización: fichero de campaña → miembro; global → cualquier
  usuario autenticado. Cache-Control privado, 1 día.
- Las imágenes se sirven tal cual; el cliente las cachea (`cached_network_image` con cabecera Bearer).

## Lore

```
LoreEntry  Id, CampaignId, Title, Slug (único por campaña), Category (World|Region|Place|Npc|Faction|Event|Quest|Note|Other),
           ContentMarkdown, Visibility (Players|DmOnly), ParentId?, SortOrder, CoverFileId?, CreatedByUserId, CreatedAt, UpdatedAt
LoreAttachment  Id, LoreEntryId, FileId, Caption?
```

| Método | Ruta | Body | Respuesta |
|--------|------|------|-----------|
| GET | `/campaigns/{id}/lore?category=&search=&parentId=` | — | `200 LoreSummaryDto[]` (jugadores: solo `Players`) árbol plano con `parentId` |
| POST | `/campaigns/{id}/lore` | `{ title, category, contentMarkdown, visibility, parentId?, coverFileId? }` | `201 LoreEntryDto` (≥ DM) |
| GET | `/lore/{id}` | — | `200 LoreEntryDto` · `404` si `DmOnly` y es jugador |
| PATCH | `/lore/{id}` | parcial + `sortOrder?` | `200` (≥ DM) |
| DELETE | `/lore/{id}` | — | `204` (≥ DM; los hijos pasan a raíz) |
| POST | `/lore/{id}/attachments` | `{ fileId, caption? }` | `201` (≥ DM) |
| DELETE | `/lore/{id}/attachments/{attachmentId}` | — | `204` |

Enlaces internos en markdown: `[[slug]]` se renderiza como enlace a la entrada (el cliente lo
resuelve con el listado). El DM ve una insignia "Solo DM" en las ocultas.

## Mapas

```
Map     Id, CampaignId, Name, FileId, WidthPx, HeightPx, Visibility (Players|DmOnly), SortOrder, CreatedAt
MapPin  Id, MapId, X, Y (0..1 relativos), Title, Note (markdown), Icon (string: place|city|dungeon|quest|npc|danger|custom),
        Color?, LoreEntryId?, Visibility (Players|DmOnly), CreatedAt
```

| Método | Ruta | Body | Respuesta |
|--------|------|------|-----------|
| GET | `/campaigns/{id}/maps` | — | `200 MapSummaryDto[]` (jugadores: `Players`) |
| POST | `/campaigns/{id}/maps` | `{ name, fileId, visibility }` | `201 MapDto` (≥ DM; el servidor lee ancho/alto de la imagen) |
| GET | `/maps/{id}` | — | `200 MapDto` con `pins` filtrados por visibilidad |
| PATCH | `/maps/{id}` | `{ name?, visibility?, sortOrder?, fileId? }` | `200` |
| DELETE | `/maps/{id}` | — | `204` |
| POST | `/maps/{id}/pins` | `{ x, y, title, note?, icon, color?, loreEntryId?, visibility }` | `201 MapPinDto` (≥ DM) |
| PATCH | `/maps/{id}/pins/{pinId}` | parcial | `200` |
| DELETE | `/maps/{id}/pins/{pinId}` | — | `204` |

## Biblioteca de documentos

```
LibraryDocument  Id, Title, Description?, Category (Rules|Adventure|Supplement|Homebrew|Other), FileId, PageCount?,
                 IsSystem (bool), UploadedByUserId, CreatedAt
CampaignDocument Id, CampaignId, DocumentId, Note?   — "recomendado" en la campaña (≥ DM)
```

- El SRD 5.1 en PDF (CC-BY 4.0) **no se puede descargar desde el entorno de desarrollo**; se
  contempla como documento de sistema solo si el operador lo coloca en
  `FileStorage:RootPath/system/SRD_CC_v5.1.pdf` antes de arrancar (`SystemDocumentSeeder` lo
  registra con `IsSystem = true` si existe). En caso contrario el admin lo sube como cualquier otro.
- No se incluye en el repo ningún material con copyright.

| Método | Ruta | Body | Respuesta |
|--------|------|------|-----------|
| GET | `/library?search=&category=` | — | `200 LibraryDocumentDto[]` (cualquier usuario) |
| POST | `/library` | `{ title, description?, category, fileId }` | `201` (Admin) |
| PATCH | `/library/{id}` | parcial | `200` (Admin) |
| DELETE | `/library/{id}` | — | `204` (Admin; `IsSystem` no se borra) |
| GET | `/campaigns/{id}/library` | — | `200` recomendados de la campaña |
| PUT | `/campaigns/{id}/library/{documentId}` | `{ note? }` | `204` (≥ DM) |
| DELETE | `/campaigns/{id}/library/{documentId}` | — | `204` |

## Cliente Flutter

- `features/lore`: árbol/lista por categoría con búsqueda, lectura markdown
  (`flutter_markdown` o equivalente mantenido), navegación por `[[slug]]`, galería de adjuntos;
  edición para DM (editor de texto con vista previa, selector de visibilidad y padre).
- `features/maps`: lista; visor con zoom y desplazamiento (`InteractiveViewer`), pines como
  widgets posicionados; toque en pin → hoja inferior con nota y enlace al lore; DM: pulsación larga
  para crear pin, arrastrar para mover, editar y borrar; subir mapa desde galería (`image_picker`).
- `features/library`: lista con búsqueda y categoría; visor PDF (`pdfrx` o `pdfx`) con descarga al
  almacenamiento de la app para lectura sin red (`path_provider`), progreso de descarga, recuerdo
  de la última página (`shared_preferences`); admin: subir PDF (`file_picker`).
- Tests de widget: jugador no ve entradas `DmOnly`; pin oculto no se renderiza para jugador; lista
  de biblioteca muestra documentos.

## Pruebas del servidor

Subida rechaza tipos no permitidos y tamaño excesivo (413). `GET /files/{id}` responde `206` a
una petición con `Range`. Jugador: `404` en lore `DmOnly`, no recibe pines `DmOnly`, no puede
subir mapa (403). `IsSystem` no se borra (400). Hijos de una entrada borrada pasan a raíz.
