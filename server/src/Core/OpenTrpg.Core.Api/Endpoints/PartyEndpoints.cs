using System.Security.Claims;
using OpenTrpg.Core.Api.Auth;
using OpenTrpg.Core.Api.Filters;
using OpenTrpg.Core.Application.Party;
using Microsoft.AspNetCore.Mvc;

namespace OpenTrpg.Core.Api.Endpoints;

/// <summary>DM tools for the table: the party at a glance, forced rests and quick adjustments.</summary>
public static class PartyEndpoints
{
    public static IEndpointRouteBuilder MapPartyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/campaigns/{campaignId:guid}/party")
            .WithTags("Party")
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("", async (Guid campaignId, ClaimsPrincipal user, GetPartyHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), campaignId, ct)))
            .WithName("GetParty")
            .WithSummary("Personajes activos de la campaña con PG, CA, condiciones, salvaciones de muerte y espacios de conjuro. Requiere al menos DM.");

        group.MapPost("/rest", async (Guid campaignId, PartyRestRequest request, ClaimsPrincipal user, PartyRestHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), campaignId, request, ct)))
            .WithName("PartyRest")
            .WithSummary("Descanso corto o largo forzado para todos los personajes activos o los indicados (el corto no gasta dados de golpe). Cancela sus peticiones de descanso pendientes. Requiere al menos DM.")
            .ProducesValidationProblem();

        group.MapPost("/adjust", async (Guid campaignId, List<PartyAdjustment> request, ClaimsPrincipal user, PartyAdjustHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), campaignId, request, ct)))
            .WithName("PartyAdjust")
            .WithSummary("Daño o curación, PG temporales, condiciones y PG máximos (0 quita el valor sobrescrito) de varios personajes a la vez. Requiere al menos DM.")
            .ProducesValidationProblem();

        group.MapPost("/grant-level", async (Guid campaignId, PartyLevelRequest? request, ClaimsPrincipal user, PartyLevelHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.GrantAsync(user.GetUserId(), campaignId, request, ct)))
            .WithName("PartyGrantLevel")
            .WithSummary("Concede el siguiente nivel ({ characterIds? }; sin lista, a todos los activos): pendingLevelUpTo = nivel + 1. No se acumula y no pasa de 20. Requiere al menos DM.")
            .ProducesValidationProblem();

        group.MapDelete("/grant-level", async (Guid campaignId, [FromBody] PartyLevelRequest? request, ClaimsPrincipal user, PartyLevelHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.RevokeAsync(user.GetUserId(), campaignId, request, ct)))
            .WithName("PartyRevokeLevel")
            .WithSummary("Retira el nivel concedido y aún no completado ({ characterIds? } opcional; sin lista, a todos). Requiere al menos DM.")
            .ProducesValidationProblem();

        return app;
    }
}
