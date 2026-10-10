using System.Security.Claims;
using OpenTrpg.Core.Api.Auth;
using OpenTrpg.Core.Api.Filters;
using OpenTrpg.Core.Application.Maps;

namespace OpenTrpg.Core.Api.Endpoints;

/// <summary>Campaign maps (an image) with their pins.</summary>
public static class MapEndpoints
{
    public static IEndpointRouteBuilder MapMapEndpoints(this IEndpointRouteBuilder app)
    {
        var campaigns = app.MapGroup("/api/v1/campaigns/{campaignId:guid}/maps")
            .WithTags("Maps")
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        campaigns.MapGet("", async (Guid campaignId, ClaimsPrincipal user, ListMapsHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), campaignId, ct)))
            .WithName("ListMaps")
            .WithSummary("Mapas de la campaña. Los jugadores solo ven los de visibilidad Players.");

        campaigns.MapPost("", async (Guid campaignId, CreateMapRequest request, ClaimsPrincipal user, CreateMapHandler handler, CancellationToken ct) =>
            {
                var map = await handler.HandleAsync(user.GetUserId(), campaignId, request, ct);
                return TypedResults.Created($"/api/v1/maps/{map.Id}", map);
            })
            .WithName("CreateMap")
            .WithSummary("Crea un mapa a partir de una imagen MapImage de la campaña; el servidor toma su ancho y alto. Requiere al menos DM.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        var group = app.MapGroup("/api/v1/maps/{id:guid}")
            .WithTags("Maps")
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("", async (Guid id, ClaimsPrincipal user, GetMapHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, ct)))
            .WithName("GetMap")
            .WithSummary("Mapa con sus pines; los jugadores no reciben los DmOnly (y 404 si el mapa es DmOnly).");

        group.MapPatch("", async (Guid id, UpdateMapRequest request, ClaimsPrincipal user, UpdateMapHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, request, ct)))
            .WithName("UpdateMap")
            .WithSummary("Cambia nombre, visibilidad, orden o imagen. Requiere al menos DM.")
            .ProducesValidationProblem();

        group.MapDelete("", async (Guid id, ClaimsPrincipal user, DeleteMapHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(user.GetUserId(), id, ct);
                return TypedResults.NoContent();
            })
            .WithName("DeleteMap")
            .WithSummary("Borra el mapa, sus pines y su imagen. Requiere al menos DM.");

        group.MapPost("/pins", async (Guid id, CreatePinRequest request, ClaimsPrincipal user, CreatePinHandler handler, CancellationToken ct) =>
            {
                var pin = await handler.HandleAsync(user.GetUserId(), id, request, ct);
                return TypedResults.Created($"/api/v1/maps/{id}", pin);
            })
            .WithName("CreateMapPin")
            .WithSummary("Añade un pin (x, y relativos entre 0 y 1). Requiere al menos DM.")
            .ProducesValidationProblem();

        group.MapPatch("/pins/{pinId:guid}", async (Guid id, Guid pinId, UpdatePinRequest request, ClaimsPrincipal user, UpdatePinHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, pinId, request, ct)))
            .WithName("UpdateMapPin")
            .WithSummary("Edición parcial de un pin (mover, renombrar, cambiar icono o visibilidad). Requiere al menos DM.")
            .ProducesValidationProblem();

        group.MapDelete("/pins/{pinId:guid}", async (Guid id, Guid pinId, ClaimsPrincipal user, DeletePinHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(user.GetUserId(), id, pinId, ct);
                return TypedResults.NoContent();
            })
            .WithName("DeleteMapPin")
            .WithSummary("Borra un pin. Requiere al menos DM.");

        return app;
    }
}
