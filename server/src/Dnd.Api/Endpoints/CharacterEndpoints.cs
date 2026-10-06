using System.Security.Claims;
using Dnd.Api.Auth;
using Dnd.Api.Filters;
using Dnd.Application.ChangeRequests;
using Dnd.Application.Characters;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Dnd.Api.Endpoints;

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
            .WithSummary("Crea un personaje en borrador. Un DM puede asignarlo a otro miembro (ownerUserId) o crear un PNJ (ownerUserId: null).")
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
                Guid id, SheetPatch patch, ClaimsPrincipal user, UpdateSheetHandler handler, CancellationToken ct) =>
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
            .WithSummary("El dueño de un borrador pide al DM que lo active (solicitud Activate).")
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/activate", async (Guid id, ClaimsPrincipal user, ActivateCharacterHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, ct)))
            .WithName("ActivateCharacter")
            .WithSummary("Un DM activa el personaje directamente; entra en juego con los PG al máximo.")
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPatch("/portrait", async (Guid id, SetPortraitRequest request, ClaimsPrincipal user, SetPortraitHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, request, ct)))
            .WithName("SetCharacterPortrait")
            .WithSummary("Fija (o quita, con fileId: null) el retrato del personaje con un fichero Portrait de la campaña. Dueño o DM.")
            .ProducesValidationProblem();

        group.MapDelete("", async (Guid id, ClaimsPrincipal user, DeleteCharacterHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(user.GetUserId(), id, ct);
                return TypedResults.NoContent();
            })
            .WithName("DeleteCharacter")
            .WithSummary("Borra el personaje: un DM siempre; el dueño solo en borrador.");

        group.MapPatch("/combat", async (Guid id, CombatUpdateRequest request, ClaimsPrincipal user, UpdateCombatHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, request, ct)))
            .WithName("UpdateCharacterCombat")
            .WithSummary("Seguimiento de combate sin aprobación: PG, PG temporales, salvaciones contra muerte, agotamiento, condiciones, inspiración.")
            .ProducesValidationProblem();

        group.MapPost("/concentration", async (Guid id, ConcentrationRequest request, ClaimsPrincipal user, SetConcentrationHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, request, ct)))
            .WithName("SetCharacterConcentration")
            .WithSummary("Empieza a concentrarse en un conjuro, o deja de hacerlo con spellIndex null.")
            .ProducesValidationProblem();

        group.MapPost("/spell-slots/{level:int}/spend", async (Guid id, int level, AmountRequest? request, ClaimsPrincipal user, SpellSlotHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.SpendAsync(user.GetUserId(), id, level, request, ct)))
            .WithName("SpendSpellSlot")
            .WithSummary("Gasta espacios de conjuro de un nivel (0 = pacto). 400 si no quedan.")
            .ProducesValidationProblem();

        group.MapPost("/spell-slots/{level:int}/restore", async (Guid id, int level, AmountRequest? request, ClaimsPrincipal user, SpellSlotHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.RestoreAsync(user.GetUserId(), id, level, request, ct)))
            .WithName("RestoreSpellSlot")
            .WithSummary("Recupera espacios de conjuro gastados de un nivel (0 = pacto).")
            .ProducesValidationProblem();

        group.MapPost("/resources", async (Guid id, AddResourceRequest request, ClaimsPrincipal user, ResourceHandler handler, CancellationToken ct) =>
                TypedResults.Created((string?)null, await handler.AddAsync(user.GetUserId(), id, request, ct)))
            .WithName("AddCharacterResource")
            .WithSummary("Añade un recurso manual (dueño o DM).")
            .ProducesValidationProblem();

        group.MapPost("/resources/{resourceId:guid}/spend", async (Guid id, Guid resourceId, AmountRequest? request, ClaimsPrincipal user, ResourceHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.SpendAsync(user.GetUserId(), id, resourceId, request, ct)))
            .WithName("SpendCharacterResource")
            .WithSummary("Gasta usos de un recurso. 400 si no quedan.")
            .ProducesValidationProblem();

        group.MapPost("/resources/{resourceId:guid}/restore", async (Guid id, Guid resourceId, AmountRequest? request, ClaimsPrincipal user, ResourceHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.RestoreAsync(user.GetUserId(), id, resourceId, request, ct)))
            .WithName("RestoreCharacterResource")
            .WithSummary("Recupera usos gastados de un recurso.")
            .ProducesValidationProblem();

        group.MapDelete("/resources/{resourceId:guid}", async (Guid id, Guid resourceId, ClaimsPrincipal user, ResourceHandler handler, CancellationToken ct) =>
            {
                await handler.DeleteAsync(user.GetUserId(), id, resourceId, ct);
                return TypedResults.NoContent();
            })
            .WithName("DeleteCharacterResource")
            .WithSummary("Borra un recurso manual (los automáticos de clase no se pueden borrar).")
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/rest/short", async (Guid id, ShortRestRequest? request, ClaimsPrincipal user, RestHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.ShortRestAsync(user.GetUserId(), id, request, ct)))
            .WithName("ShortRest")
            .WithSummary("Descanso corto aplicado por un DM: gasta dados de golpe ({ hitDice: { clase: n } }), repone recursos de descanso corto y slots de pacto. Jugadores: 403 (piden el descanso con rest-requests).")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/rest/long", async (Guid id, ClaimsPrincipal user, RestHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.LongRestAsync(user.GetUserId(), id, ct)))
            .WithName("LongRest")
            .WithSummary("Descanso largo aplicado por un DM: PG al máximo, slots y recursos repuestos, recupera dados de golpe y reduce el agotamiento. Jugadores: 403 (piden el descanso con rest-requests).")
            .ProducesProblem(StatusCodes.Status403Forbidden);

        var classActions = group.MapGroup("/class-actions").ProducesProblem(StatusCodes.Status400BadRequest);

        classActions.MapPost($"/{ClassActionHandler.Rage}", async (Guid id, ClaimsPrincipal user, ClassActionHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.RageAsync(user.GetUserId(), id, ct)))
            .WithName("ClassActionRage")
            .WithSummary("Bárbaro: entra en furia (gasta un uso de Furia). 400 si no es bárbaro o no quedan usos.");

        classActions.MapPost($"/{ClassActionHandler.LayOnHands}", async (Guid id, LayOnHandsRequest request, ClaimsPrincipal user, ClassActionHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.LayOnHandsAsync(user.GetUserId(), id, request, ct)))
            .WithName("ClassActionLayOnHands")
            .WithSummary("Paladín: Imposición de manos ({ amount, targetSelf = true }). Resta del pool y, si targetSelf, cura al personaje sin superar el máximo.")
            .ProducesValidationProblem();

        classActions.MapPost($"/{ClassActionHandler.DivineSmite}", async (Guid id, DivineSmiteRequest request, ClaimsPrincipal user, ClassActionHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.DivineSmiteAsync(user.GetUserId(), id, request, ct)))
            .WithName("ClassActionDivineSmite")
            .WithSummary("Paladín: Castigo divino ({ slotLevel }). Gasta el espacio y devuelve { character, damageDice } (2d8 a nivel 1, +1d8 por nivel, máx. 5d8).")
            .ProducesValidationProblem();

        classActions.MapPost($"/{ClassActionHandler.ArcaneRecovery}", async (Guid id, ArcaneRecoveryRequest request, ClaimsPrincipal user, ClassActionHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.ArcaneRecoveryAsync(user.GetUserId(), id, request, ct)))
            .WithName("ClassActionArcaneRecovery")
            .WithSummary("Mago: Recuperación arcana ({ slotLevels: [..] }). Suma ≤ mitad del nivel de mago (redondeo arriba), ningún espacio > 5; una vez por descanso largo.")
            .ProducesValidationProblem();

        classActions.MapPost("/{action}", IResult (string action) => throw ClassActionHandler.UnknownAction(action))
            .WithName("ClassActionUnknown")
            .WithSummary("Acción de clase desconocida: 404.")
            .ExcludeFromDescription();

        return app;
    }
}
