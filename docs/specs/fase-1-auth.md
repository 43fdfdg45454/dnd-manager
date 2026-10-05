# Fase 1 — Autenticación y administración de usuarios

Contrato cerrado. Servidor y cliente se implementan en paralelo a partir de este documento.

## Reglas

- No hay registro público. Solo un `Admin` crea usuarios.
- Al crear un usuario se le envía un correo con un enlace para **establecer contraseña**. El
  enlace abre una página web mínima servida por la API (`/set-password?token=...`), así el usuario
  fija su contraseña desde el navegador del móvil sin tener aún la app.
- Login con email + contraseña. Tokens: **access JWT** (HS256, 15 min) + **refresh token** opaco
  (30 días, rotatorio: cada uso emite uno nuevo y revoca el anterior; reutilizar uno revocado
  revoca toda la familia del usuario).
- Roles globales: `Admin`, `User`. Claim `role` en el JWT. Claims: `sub` (id), `email`, `name`.
- Contraseña: mínimo 10 caracteres. Hash con `PasswordHasher<User>` de
  `Microsoft.Extensions.Identity.Core` (no se usa el resto de ASP.NET Identity).
- Tokens de contraseña (alta y reset): opacos, 32 bytes aleatorios en base64url, se guarda solo el
  hash SHA-256, caducan a las 48 h (alta) o 1 h (reset), de un solo uso. Al usarse se revocan todos
  los refresh tokens del usuario.
- "Olvidé mi contraseña" responde siempre `202` aunque el email no exista.
- Usuario desactivado (`isActive=false`): login y refresh devuelven `401`; su JWT vigente sigue
  valiendo hasta caducar (15 min).
- Admin inicial: al arrancar, si no hay usuarios y `App:InitialAdminEmail` está configurado, se
  crea un `Admin` con ese email y se le envía el correo de alta. El enlace también se escribe en el
  log a nivel Warning para el primer arranque sin SMTP.
- Errores en formato `ProblemDetails` (RFC 9457). Validación con FluentValidation → `400` con
  `errors` por campo.

## Configuración

```json
"Jwt": { "Secret": "cadena de al menos 32 caracteres", "Issuer": "dnd-companion", "Audience": "dnd-companion-app", "AccessTokenMinutes": 15, "RefreshTokenDays": 30 },
"App": { "PublicUrl": "https://dnd.example.com", "InitialAdminEmail": "admin@example.com" }
```

Variables de entorno equivalentes: `Jwt__Secret`, `App__PublicUrl`, `App__InitialAdminEmail`.

## Entidades (Dnd.Domain)

```
User          Id, Email (único, minúsculas), DisplayName, PasswordHash?, Role (Admin|User),
              IsActive, CreatedAt, LastLoginAt?
RefreshToken  Id, UserId, TokenHash, ExpiresAt, CreatedAt, RevokedAt?, ReplacedByTokenHash?
PasswordToken Id, UserId, TokenHash, Purpose (Setup|Reset), ExpiresAt, UsedAt?
```

## Endpoints

Prefijo `/api/v1`. Respuestas JSON en camelCase.

### Auth (anónimos salvo `me` y `logout`)

| Método | Ruta | Body | Respuesta |
|--------|------|------|-----------|
| POST | `/auth/login` | `{ email, password }` | `200 AuthResponse` · `401` credenciales inválidas o usuario inactivo |
| POST | `/auth/refresh` | `{ refreshToken }` | `200 AuthResponse` · `401` token inválido, caducado, revocado o usuario inactivo |
| POST | `/auth/logout` | `{ refreshToken }` | `204` (revoca ese refresh token; requiere JWT) |
| POST | `/auth/password/forgot` | `{ email }` | `202` siempre |
| POST | `/auth/password/set` | `{ token, password }` | `204` · `400` token inválido/caducado/usado o contraseña débil |
| GET | `/auth/me` | — | `200 UserDto` (requiere JWT) |

```
AuthResponse { accessToken, accessTokenExpiresAt (ISO 8601), refreshToken, user: UserDto }
UserDto      { id, email, displayName, role ("Admin"|"User"), isActive, hasPassword, createdAt, lastLoginAt? }
```

### Admin (requieren JWT con rol `Admin`; si no, `403`)

| Método | Ruta | Body | Respuesta |
|--------|------|------|-----------|
| GET | `/admin/users?search=&page=1&pageSize=50` | — | `200 { items: UserDto[], total, page, pageSize }` |
| POST | `/admin/users` | `{ email, displayName, role }` | `201 UserDto` + envía correo de alta · `409` email ya existe |
| PATCH | `/admin/users/{id}` | `{ displayName?, role?, isActive? }` | `200 UserDto` · `400` si un admin intenta quitarse a sí mismo el rol o desactivarse |
| POST | `/admin/users/{id}/setup-email` | — | `202` reenvía el correo de alta (nuevo token, invalida el anterior) |

### Página web de contraseña (servida por la API)

- `GET /set-password?token=...`: HTML estático (archivo en `wwwroot/set-password.html`) con un
  formulario (contraseña + repetir) que llama a `POST /api/v1/auth/password/set` por `fetch` y
  muestra el resultado en español. Sin frameworks. También sirve para el reset.

## Correos (en español, texto + HTML sencillo)

- **Alta**: asunto "Tu cuenta en D&D Companion", enlace `{PublicUrl}/set-password?token=...`,
  caducidad 48 h.
- **Reset**: asunto "Restablecer contraseña", mismo enlace, caducidad 1 h.

## Cliente Flutter

- Almacenar tokens en `flutter_secure_storage`. Interceptor `dio`: añade `Authorization: Bearer`,
  ante `401` intenta un refresh (una sola vez, con lock para no disparar refresh en paralelo) y
  reintenta; si el refresh falla, cierra sesión.
- Estado de sesión global (Riverpod): `unknown` → `signedOut` | `signedIn(user)`. Al arrancar lee
  el refresh token y llama a `/auth/me`.
- Rutas: `/login`, `/forgot-password`, `/` (inicio con nombre del usuario y botón de cerrar sesión),
  `/admin/users` (solo `Admin`). `go_router` redirige según el estado.
- Pantalla admin: lista con búsqueda, botón "Nuevo usuario" (email, nombre, rol), por usuario:
  reenviar correo de alta, activar/desactivar, cambiar rol. Mensajes de error en español.
- Tests de widget con repositorios falsos: login válido navega a inicio; error muestra mensaje;
  admin crea usuario y aparece en la lista.

## Pruebas del servidor (Dnd.Api.Tests)

Base de datos **SQLite en memoria** en `ApiFactory` (sustituye el `DbContext` de Npgsql y usa
`EnsureCreated`), `IEmailSender` falso que captura mensajes. Cubrir: admin inicial creado al
arrancar; crear usuario envía correo y el token del correo permite fijar contraseña; login
correcto/incorrecto; refresh rota y el anterior deja de valer; usuario inactivo no entra; `User`
recibe `403` en admin; admin no puede desactivarse a sí mismo; forgot devuelve 202 con email
inexistente.
