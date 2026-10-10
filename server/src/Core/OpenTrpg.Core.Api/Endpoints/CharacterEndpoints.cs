using System.Text.Json;
using System.Security.Claims;
using OpenTrpg.Core.Api.Auth;
using OpenTrpg.Core.Api.Filters;
using OpenTrpg.Core.Application.ChangeRequests;
using OpenTrpg.Core.Application.Characters;
using Microsoft.AspNetCore.Http.HttpResults;
using OpenTrpg.Core.Application.Common;

namespace OpenTrpg.Core.Api.Endpoints;

public static class CharacterEndpoints
{
    public static IEndpointRouteBuilder MapCharacterEndpoints(this IEndpointRouteBuilder app)
    {
        var campaigns = app.MapGroup("/api/v1/campaigns/{campaignId:guid}/characters")
            .WithTags("Characters")
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        campaigns.MapGet("", async (Guid campaignId, ClaimsPrincipal user, ListCharactersHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), campaignId, ct)))
            .WithName("ListCampaignCharacters")
            .WithSummary("Resumen de los personajes de la campaña. Los PG solo se muestran al dueño y a los DMs.");

        campaigns.MapPost("", async (Guid campaignId, CreateCharacterRequest request, ClaimsPrincipal user, CreateCharacterHandler handler, CancellationToken ct) =>
            {
                var character = await handler.HandleAsync(user.GetUserId(), campaignId, request, ct);
                return TypedResults.Created($"/api/v1/characters/{character.Id}", character);
            })
            .WithName("CreateCharacter")
            .WithSummary("Crea un personaje en borrador. Un jugador crea el suyo; un DM no tiene personajes propios y debe indicar ownerUserId: un jugador de la campaña o null para un PNJ (400 si falta o es él mismo u otro DM).")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        var group = app.MapGroup("/api/v1/characters/{id:guid}")
            .WithTags("Characters")
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("", async (Guid id, ClaimsPrincipal user, GetCharacterHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, ct)))
            .WithName("GetCharacter")
            .WithSummary("Hoja completa del personaje. Solo el dueño y los DMs.");

        group.MapPatch("/sheet", async Task<Results<Ok<CharacterDetailDto>, Accepted<ChangeRequestDto>>> (
                Guid id, JsonElement patch, ClaimsPrincipal user, UpdateSheetHandler handler, CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(user.GetUserId(), id, patch, ct);
                return result.ChangeRequest is { } request
                    ? TypedResults.Accepted($"/api/v1/change-requests/{request.Id}", request)
                    : TypedResults.Ok(result.Character!);
            })
            .WithName("UpdateCharacterSheet")
            .WithSummary("Edita la hoja: 200 si se aplica (DM o dueño en borrador); 202 con la solicitud creada si necesita aprobación del DM.")
            .ProducesValidationProblem();

        group.MapPost("/submit", async (Guid id, ClaimsPrincipal user, SubmitCharacterHandler handler, CancellationToken ct) =>
            {
                var request = await handler.HandleAsync(user.GetUserId(), id, ct);
                return TypedResults.Created($"/api/v1/change-requests/{request.Id}", request);
            })
            .WithName("SubmitCharacter")
            .WithSummary("El dueño de un borrador pide al DM que lo active (solicitud Activate). 400 (code origin-choices-incomplete) si faltan elecciones de raza o trasfondo.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/activate", async (Guid id, ClaimsPrincipal user, ActivateCharacterHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, ct)))
            .WithName("ActivateCharacter")
            .WithSummary("Un DM activa el personaje directamente; entra en juego con los PG al máximo. 400 (code origin-choices-incomplete) si faltan elecciones de raza o trasfondo.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPatch("/portrait", async (Guid id, SetPortraitRequest request, ClaimsPrincipal user, SetPortraitHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, request, ct)))
            .WithName("SetCharacterPortrait")
            .WithSummary("Fija (o quita, con fileId: null) el retrato del personaje con un fichero Portrait de la campaña. Dueño o DM.")
            .ProducesValidationProblem();

        group.MapPut("/owner", async (Guid id, SetCharacterOwnerRequest request, ClaimsPrincipal user, SetCharacterOwnerHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, request, ct)))
            .WithName("SetCharacterOwner")
            .WithSummary("Un DM reasigna el personaje a un jugador de la campaña ({ ownerUserId }) o lo convierte en PNJ ({ ownerUserId: null }), sin aprobación.")
            .ProducesValidationProblem();

        group.MapDelete("", async (Guid id, ClaimsPrincipal user, DeleteCharacterHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(user.GetUserId(), id, ct);
                return TypedResults.NoContent();
            })
            .WithName("DeleteCharacter")
            .WithSummary("Borra el personaje: un DM siempre; el dueño solo en borrador.");

        return app;
    }
}
