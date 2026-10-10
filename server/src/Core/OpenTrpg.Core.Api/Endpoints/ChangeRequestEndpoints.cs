using System.Security.Claims;
using OpenTrpg.Core.Api.Auth;
using OpenTrpg.Core.Api.Filters;
using OpenTrpg.Core.Application.ChangeRequests;

namespace OpenTrpg.Core.Api.Endpoints;

public static class ChangeRequestEndpoints
{
    public static IEndpointRouteBuilder MapChangeRequestEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/campaigns/{campaignId:guid}/change-requests", async (
                Guid campaignId, string? status, ClaimsPrincipal user, ListChangeRequestsHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), campaignId, status, ct)))
            .WithTags("ChangeRequests")
            .RequireAuthorization()
            .WithName("ListChangeRequests")
            .WithSummary("Solicitudes de cambio de la campaña (DM: todas; jugador: las suyas). Filtro opcional ?status=Pending.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var group = app.MapGroup("/api/v1/change-requests/{id:guid}")
            .WithTags("ChangeRequests")
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("", async (Guid id, ClaimsPrincipal user, GetChangeRequestHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, ct)))
            .WithName("GetChangeRequest")
            .WithSummary("Detalle de una solicitud: DMs, quien la hizo y el dueño del personaje.");

        group.MapPost("/approve", async (Guid id, ApproveChangeRequestRequest? request, ClaimsPrincipal user, ApproveChangeRequestHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, request, ct)))
            .WithName("ApproveChangeRequest")
            .WithSummary("Un DM aprueba la solicitud y se aplica su contenido. 409 si ya no está pendiente.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/reject", async (Guid id, RejectChangeRequestRequest request, ClaimsPrincipal user, RejectChangeRequestHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, request, ct)))
            .WithName("RejectChangeRequest")
            .WithSummary("Un DM rechaza la solicitud indicando el motivo.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/cancel", async (Guid id, ClaimsPrincipal user, CancelChangeRequestHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, ct)))
            .WithName("CancelChangeRequest")
            .WithSummary("Quien hizo la solicitud la retira mientras está pendiente.")
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
