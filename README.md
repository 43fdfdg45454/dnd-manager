# D&D 5e Companion

Companion app para campañas de Dungeons & Dragons 5e en sesiones presenciales. Cada jugador
lleva su personaje en Android; el DM gestiona la campaña, el lore, los mapas, las tiendas y el
calendario. El servidor es ASP.NET Core y se autohospeda con Docker.

- Plan maestro: [docs/PLAN.md](docs/PLAN.md)
- Decisiones de arquitectura: [docs/ADR](docs/ADR)
- Lineamientos para contribuir: [CLAUDE.md](CLAUDE.md)

## Estructura

| Carpeta | Contenido |
|---------|-----------|
| `server/` | Solución .NET 10: `Dnd.Domain`, `Dnd.Application`, `Dnd.Infrastructure`, `Dnd.Api` y tests |
| `app/` | Cliente Flutter (Android) |
| `deploy/` | Docker Compose con `api` y `postgres`; reverse proxy y SMTP a cargo del operador |
| `docs/` | Plan y ADR |

## Puesta en marcha rápida

```bash
# Servidor en local (requiere .NET 10 SDK y una instancia de PostgreSQL)
cd server
dotnet build
dotnet test
dotnet run --project src/Dnd.Api      # Swagger en http://localhost:8080/swagger

# Despliegue con Docker
cd deploy
cp .env.sample .env                  # editar valores
docker compose up -d --build
curl http://127.0.0.1:8080/health/ready

# Cliente
cd app
flutter pub get
flutter run --dart-define=API_BASE_URL=http://10.0.2.2:8080
```

## Licencias y atribución

El código de este repositorio se publica bajo la licencia que indique `LICENSE`. El contenido de
reglas procede del **System Reference Document 5.1** de Wizards of the Coast, licenciado bajo
[Creative Commons Attribution 4.0](https://creativecommons.org/licenses/by/4.0/legalcode). Este
proyecto no está afiliado a Wizards of the Coast y no incluye material con copyright fuera del SRD.
