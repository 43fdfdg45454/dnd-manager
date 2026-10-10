using System.Security.Claims;
using OpenTrpg.Core.Api.Auth;
using OpenTrpg.Core.Api.Filters;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Api.Endpoints;
using OpenTrpg.Systems.Dnd5e.Application.Characters;

namespace OpenTrpg.Systems.Dnd5e.Api.Endpoints;

/// <summary>Level-up wizard (phase 16c): the plan of the next level and its application.</summary>
public static class LevelUpEndpoints
{
    public static IEndpointRouteBuilder MapLevelUpEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/characters/{id:guid}/level-up")
            .WithTags("LevelUp")
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("", async (Guid id, string? classIndex, ClaimsPrincipal user, GetLevelUpPlanHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, classIndex, ct)))
            .WithName("GetLevelUpPlan")
            .WithSummary("Plan de la subida de nivel en una clase (?classIndex=, por defecto la principal): clases posibles con los requisitos de multiclase, dado de golpe, rasgos automáticos, elecciones con sus opciones y límites de conjuros. Dueño con nivel concedido (409 si no) o DM.")
            .ProducesValidationProblem();

        group.MapPost("", async (Guid id, LevelUpRequest request, ClaimsPrincipal user, ApplyLevelUpHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, request, ct)))
            .WithName("ApplyLevelUp")
            .WithSummary("Aplica la subida de nivel ({ classIndex, hitPointsRolled, choices: [{ key, selected, replaced? }] }) y devuelve la hoja. 400 si la tirada o las elecciones no son válidas; 409 sin nivel concedido.")
            .ProducesValidationProblem();

        return app;
    }
}
