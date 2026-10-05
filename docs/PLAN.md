# D&D 5e Companion — Plan maestro

Companion app para sesiones **presenciales** de D&D 5e: cada jugador lleva su personaje en Android,
el DM gestiona campaña, lore, mapas, tiendas y calendario, y un administrador del servidor crea los
usuarios. Servidor .NET autohospedado con Docker.

## Decisiones

| Tema | Decisión |
|------|----------|
| Cliente | Flutter (solo Android por ahora; iOS queda abierto) |
| Offline | Solo lectura: la app cachea el último estado; editar requiere red |
| Reglas / contenido | SRD 5.1 (2014), importado desde el dataset JSON de `5e-database` (MIT; contenido CC-BY 4.0 con atribución) |
| Hoja | Semiautomática: clase/raza/nivel → cálculo de modificadores, competencias, slots y rasgos; todo sobreescribible |
| Dados | Privados, sin tiempo real; historial local en el móvil |
| Email | SMTP genérico por variables de entorno |
| Idioma | UI en español, contenido SRD en inglés (el DM puede renombrar) |
| Distribución | APK directo; endpoint de "última versión" para avisar de actualizaciones |
| Mapas | Imagen con zoom + pines con notas enlazadas al lore; pines visibles u ocultos |
| Tiendas | Compra directa: descuenta oro, baja stock, ítem al inventario; DM abre/cierra tienda |
| Hosting | Docker Compose + Nginx (api, postgres, nginx); certificado TLS montado por el operador |
| Documentación | SRD 5.1 en PDF empaquetado (CC-BY); el admin sube otros PDF a la biblioteca de la instancia |

Los ADR en `docs/ADR/` detallan las decisiones con más impacto.

## Modelo de permisos

- Roles globales: `Admin` (crea usuarios, ve todo), `User`.
- Roles por campaña: `Owner` (único; por defecto el DM; transferible), `DM` (co-DMs), `Player`.
- Los ítems de un personaje pertenecen a la campaña; los ítems homebrew viven en la campaña.
- El jugador solo modifica ítems mediante operaciones: comprar/vender en tienda abierta, equipar,
  usar consumible, atunement.
- Auto-seguimiento en combate sin aprobación: HP, HP temporales, death saves, slots, recursos,
  condiciones, concentración, short/long rest.
- Todo lo demás pasa por `ChangeRequest` (pendiente / aprobado / rechazado): subir de nivel, editar
  stats o competencias, ítems a mano, ítems personalizados, oro fuera de una compra. El DM/Owner
  aplica estos cambios directamente.
- El Owner saliente queda como `DM` (o `Player` si elige) al transferir.

## Arquitectura

```
Android (Flutter) ──HTTPS/JSON──▶ Nginx ──▶ API ASP.NET Core (.NET 10 LTS)
                                              ├─ PostgreSQL 17 (EF Core)
                                              ├─ Volumen /data/files (mapas, retratos, PDF, APK)
                                              └─ Worker (recordatorios → SMTP)
```

- Auth: usuarios locales, ASP.NET Identity + JWT (access corto) + refresh token rotatorio. Sin
  registro público. El admin crea el usuario; la API envía email con enlace de "establecer
  contraseña". Reset por email.
- Ficheros: volumen local; la API sirve `/files/{id}` con control de acceso por campaña y soporte
  de `Range` (PDF).
- Catálogo SRD: seed idempotente al arrancar desde `server/seed/srd/`.

## Modelo de dominio

- `User`, `RefreshToken`, `PasswordSetupToken`.
- `Campaign`, `CampaignMember` (role), `OwnershipTransfer`.
- `Character`, `CharacterClass` (multiclase simple), `CharacterOverride`, `CharacterSpell`,
  `CharacterResource`, `SpellSlotState`.
- Catálogo: `Race`, `Class`, `Subclass`, `ClassLevelFeature`, `Feature`, `Spell`, `ItemTemplate`
  (`campaignId` nulo = SRD, no nulo = homebrew), `Condition`.
- Inventario: `CharacterItem` (plantilla + overrides: name, description, damageDice, damageType,
  bonus, properties, weight, charges). Creación rápida = plantilla; avanzada = plantilla + overrides
  o ítem sin plantilla.
- `ChangeRequest`, `Shop`, `ShopItem`, `Transaction`.
- `LoreEntry` (markdown, visibilidad Player/DM, adjuntos), `LibraryDocument`, `Map`, `MapPin`.
- `Session`, `Reminder`, `SessionRsvp`, `AppRelease`.

## Vistas del personaje

- **Detallada**: Resumen / Habilidades y competencias / Rasgos / Hechizos / Inventario / Notas.
- **Combate**: HP, AC, iniciativa, velocidad, death saves, condiciones, ataques con arma equipada,
  spell slots por nivel, recursos por rest, consumibles rápidos, short/long rest, dado rápido.
- **Panel por clase** (widget registrado por `classIndex`): Bárbaro (Rage, Reckless Attack), Mago
  (libro de hechizos, preparados, Arcane Recovery), Paladín (Lay on Hands, Divine Smite, Channel
  Divinity). El resto usa el panel genérico de recursos.

## Otras funcionalidades

- **Recordatorios**: `BackgroundService` que envía por SMTP (MailKit) los `Reminder` vencidos;
  offsets por defecto 24 h y 2 h, editables por campaña; zona horaria por campaña.
- **Biblioteca**: SRD en PDF como documento de sistema; el admin sube PDF (límite configurable);
  visor integrado, descarga para lectura offline, memoria de última página.
- **Dados**: parser (`2d6+3`, `1d20 adv/dis`, `4d6kh3`), tiradas desde la hoja; historial local.
- **Offline**: caché local (drift) por pantalla; aviso "sin conexión"; escrituras deshabilitadas.

## Fases

| Fase | Entregable | Verificación |
|------|------------|--------------|
| 0 | Esqueleto: solución .NET, Flutter, Docker Compose + Nginx, CI, CLAUDE.md, ADRs | `docker compose up` sirve `/health`; CI verde |
| 1 | Auth + admin: crear usuarios, email de alta, login/refresh, pantalla admin | Integración de alta y login; email en MailHog |
| 2 | Campañas: CRUD, miembros, roles, transferir ownership | Tests de autorización por rol |
| 3 | Catálogo SRD: seed + endpoints de consulta; buscador en la app | Conteo de hechizos/ítems tras seed |
| 4 | Personajes: creación semiautomática, cálculos, vista detallada, overrides, `ChangeRequest` | Tests de cálculo |
| 5 | Ítems: inventario, creación rápida/avanzada, homebrew, tiendas y compra | Compra atómica |
| 6 | Vista de combate + paneles Bárbaro/Mago/Paladín + rests + dados | Prueba en emulador |
| 7 | Lore + mapas con pines + biblioteca de documentos | Pin oculto invisible al jugador; PDF offline |
| 8 | Calendario, RSVP, recordatorios SMTP | Reminder en MailHog |
| 9 | Caché offline, aviso de actualización de APK, backups, README de despliegue | Modo avión muestra la hoja |

## Verificación end-to-end

1. `cd server && dotnet build && dotnet test`.
2. `cd app && flutter analyze && flutter test`.
3. `cd deploy && cp .env.example .env && docker compose --profile dev up -d`, abrir `/swagger`,
   crear usuario desde el admin inicial, recibir email en MailHog.
4. `flutter build apk --release`, instalar en emulador, crear personaje, comprar en tienda, modo
   combate, gastar un slot; modo avión y comprobar que la hoja sigue visible.
