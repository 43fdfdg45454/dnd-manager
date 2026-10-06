using System.Security.Claims;
using Dnd.Api.Auth;
using Dnd.Api.Filters;
using Dnd.Application.Characters;

namespace Dnd.Api.Endpoints;

/// <summary>Rests asked to the DM: the owner asks (or cancels), a DM approves or rejects.</summary>
public static class RestRequestEndpoints
{
    public static IEndpointRouteBuilder MapRestRequestEndpoints(this IEndpointRouteBuilder app)
    {
        var characters = app.MapGroup("/api/v1/characters/{id:guid}/rest-requests")
            .WithTags("RestRequests")
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        characters.MapPost("", async (Guid id, CreateRestRequestRequest request, ClaimsPrincipal user, CreateRestRequestHandler handler, CancellationToken ct) =>
            {
                var created = await handler.HandleAsync(user.GetUserId(), id, request, ct);
                return TypedResults.Created($"/api/v1/rest-requests/{created.Id}", created);
            })
            .WithName("CreateRestRequest")
            .WithSummary("El dueño pide un descanso al DM ({ kind: short|long, hitDice?: { clase: n } }). 409 si ya hay uno pendiente o el personaje no está activo.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        characters.MapDelete("", async (Guid id, ClaimsPrincipal user, CancelRestRequestHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(user.GetUserId(), id, ct);
                return TypedResults.NoContent();
            })
            .WithName("CancelRestRequest")
            .WithSummary("El dueño cancela el descanso pendiente de su personaje (404 si no hay ninguno).");

        app.MapGet("/api/v1/campaigns/{campaignId:guid}/rest-requests", async (
                Guid campaignId, string? status, ClaimsPrincipal user, ListRestRequestsHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), campaignId, status, ct)))
            .WithTags("RestRequests")
            .RequireAuthorization()
            .WithName("ListRestRequests")
            .WithSummary("Peticiones de descanso de la campaña (DM: todas; jugador: las de sus personajes). Filtro opcional ?status=Pending.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var group = app.MapGroup("/api/v1/rest-requests/{id:guid}")
            .WithTags("RestRequests")
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("", async (Guid id, ClaimsPrincipal user, GetRestRequestHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, ct)))
            .WithName("GetRestRequest")
            .WithSummary("Detalle de una petición de descanso: DMs y el dueño del personaje.");

        group.MapPost("/approve", async (Guid id, ResolveRestRequestRequest? request, ClaimsPrincipal user, ApproveRestRequestHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, request, ct)))
            .WithName("ApproveRestRequest")
            .WithSummary("Un DM aprueba el descanso y se aplica con las reglas del PHB (el corto gasta los dados pedidos que queden). 409 si ya no está pendiente.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/reject", async (Guid id, ResolveRestRequestRequest? request, ClaimsPrincipal user, RejectRestRequestHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, request, ct)))
            .WithName("RejectRestRequest")
            .WithSummary("Un DM rechaza el descanso ({ comment? } opcional). 409 si ya no está pendiente.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
