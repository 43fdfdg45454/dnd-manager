using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FluentValidation;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Application.Systems;
using OpenTrpg.Core.Domain.Campaigns;
using OpenTrpg.Core.Domain.Characters;

namespace OpenTrpg.Core.Application.Characters;

// Rests approved by the DM (phase 16b): the owner asks for a rest of one of the game system's kinds, a DM approves
// it (the system applies the rest), rejects it, or makes it moot by resting the character directly.

/// <summary>
/// A rest request with the names of the people involved. The fields of the system's payload (in D&amp;D 5e,
/// <c>hitDice</c>) are written at the same level.
/// </summary>
/// <param name="Kind">One of the system's rest kinds ("Short" or "Long" in D&amp;D 5e).</param>
/// <param name="Status">Pending, Approved, Rejected or Cancelled.</param>
public sealed record RestRequestDto(
    Guid Id,
    Guid CampaignId,
    Guid CharacterId,
    string CharacterName,
    Guid RequestedByUserId,
    string RequestedByDisplayName,
    string Kind,
    string Status,
    DateTimeOffset RequestedAt,
    Guid? ResolvedByUserId,
    string? ResolvedByDisplayName,
    DateTimeOffset? ResolvedAt,
    string? Comment)
{
    /// <summary>The fields of the system's payload.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Payload { get; init; }

    public static RestRequestDto From(RestRequestView view)
    {
        var request = view.Request;
        return new RestRequestDto(
            request.Id,
            request.CampaignId,
            request.CharacterId,
            view.CharacterName,
            request.RequestedByUserId,
            view.RequestedByDisplayName,
            request.Kind,
            request.Status.ToString(),
            request.RequestedAt,
            request.ResolvedByUserId,
            view.ResolvedByDisplayName,
            request.ResolvedAt,
            request.Comment)
        {
            Payload = JsonFields.From(RestPayloads.Parse(request.PayloadJson)),
        };
    }
}

/// <summary>Body of a rest request: the kind and, at the same level, the fields of the system's payload.</summary>
/// <param name="Kind">One of the system's rest kinds (any case; "short" or "long" in D&amp;D 5e).</param>
public sealed record CreateRestRequestRequest(string Kind)
{
    /// <summary>The fields of the system's payload (in D&amp;D 5e, <c>hitDice</c>).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Payload { get; init; }
}

internal static class RestPayloads
{
    public static JsonObject? Parse(string json)
    {
        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

}

/// <summary>Body of approve and reject: an optional comment for the player.</summary>
public sealed record ResolveRestRequestRequest(string? Comment);

public sealed class ResolveRestRequestRequestValidator : AbstractValidator<ResolveRestRequestRequest>
{
    public ResolveRestRequestRequestValidator()
    {
        RuleFor(x => x.Comment).MaximumLength(RestRequest.CommentMaxLength)
            .WithMessage($"El comentario no puede superar los {RestRequest.CommentMaxLength} caracteres.");
    }
}

public static class RestRequestErrors
{
    public const string AskTheDm = "Pide el descanso al DM.";

    public static AppException NotFound() => AppException.NotFound("Petición de descanso no encontrada.");

    public static AppException NoPending() => AppException.NotFound("No hay ningún descanso pendiente para este personaje.");
}

/// <summary>Loads rest requests with the actor's role in their campaign (404 for non-members).</summary>
public sealed class RestRequestLoader(IRestRequestRepository requests, ICampaignAccess access)
{
    /// <summary>Tracked request, for resolving it.</summary>
    public async Task<(RestRequest Request, CampaignRole Role)> LoadAsync(Guid requestId, Guid actorUserId, CancellationToken cancellationToken)
    {
        var request = await requests.GetByIdAsync(requestId, cancellationToken) ?? throw RestRequestErrors.NotFound();
        var role = await access.GetRoleAsync(request.CampaignId, actorUserId, cancellationToken) ?? throw RestRequestErrors.NotFound();
        return (request, role);
    }

    public async Task<RestRequestDto> ToDtoAsync(Guid requestId, CancellationToken cancellationToken) =>
        RestRequestDto.From((await requests.ListViewsAsync(new RestRequestQuery(Id: requestId), cancellationToken)).Single());

    /// <summary>
    /// Cancels the pending requests of the given characters (a DM rested them directly) with
    /// <paramref name="actorUserId"/> as resolver. The caller saves and then publishes the returned requests.
    /// </summary>
    public async Task<IReadOnlyList<RestRequest>> CancelPendingAsync(IReadOnlyCollection<Guid> characterIds, Guid actorUserId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var pending = await requests.ListPendingAsync(characterIds, cancellationToken);
        foreach (var request in pending)
        {
            request.Cancel(actorUserId, now);
        }

        return pending;
    }

    /// <summary>Publishes <see cref="CampaignEventTypes.RestRequestUpdated"/> for each request (call after saving).</summary>
    public static async Task NotifyAsync(ICampaignNotifier notifier, IEnumerable<RestRequest> updated, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var request in updated)
        {
            await notifier.RestRequestUpdatedAsync(request.CampaignId, request.CharacterId, request.Id, now, cancellationToken);
        }
    }
}

/// <summary>
/// The owner of an active character asks the DM for a rest. The game system checks the kind's payload (in D&amp;D 5e,
/// the hit dice to spend, which must not exceed the remaining ones now). One pending request per character (409).
/// </summary>
public sealed class CreateRestRequestHandler(
    CharacterLoader characters,
    CampaignSystems systems,
    IRestRequestRepository requests,
    RestRequestLoader loader,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task<RestRequestDto> HandleAsync(Guid currentUserId, Guid characterId, CreateRestRequestRequest body, CancellationToken cancellationToken = default)
    {
        var character = (await characters.LoadAsync(characterId, currentUserId, cancellationToken)).Character;
        if (!character.IsOwnedBy(currentUserId))
        {
            throw AppException.Forbidden("Solo el dueño del personaje puede pedir un descanso.");
        }

        if (character.Status != CharacterStatus.Active)
        {
            throw AppException.Conflict("Solo un personaje activo puede pedir un descanso.");
        }

        var system = await systems.ForCharacterAsync(character, cancellationToken);
        var kinds = system.Rests.Kinds;
        var kind = kinds.FirstOrDefault(k => string.Equals(k, body.Kind?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw AppException.Validation("kind", $"El descanso debe ser {string.Join(" o ", kinds.Select(k => k.ToLowerInvariant()))}.");
        var payload = await system.Rests.ValidateRequestAsync(
            new CharacterRef(character),
            kind,
            JsonFields.ToElement(body.Payload),
            cancellationToken);

        if (await requests.GetPendingAsync(character.Id, cancellationToken) is not null)
        {
            throw AppException.Conflict("Ya hay un descanso pendiente para este personaje.");
        }

        var now = clock.UtcNow;
        var request = RestRequest.Create(character.CampaignId, character.Id, currentUserId, kind, payload.ToJsonString(), now);
        requests.Add(request);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.RestRequestUpdatedAsync(request.CampaignId, request.CharacterId, request.Id, now, cancellationToken);
        return await loader.ToDtoAsync(request.Id, cancellationToken);
    }
}

/// <summary>The owner withdraws the pending rest request of their character.</summary>
public sealed class CancelRestRequestHandler(
    CharacterLoader characters,
    IRestRequestRepository requests,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task HandleAsync(Guid currentUserId, Guid characterId, CancellationToken cancellationToken = default)
    {
        var character = (await characters.LoadAsync(characterId, currentUserId, cancellationToken)).Character;
        if (!character.IsOwnedBy(currentUserId))
        {
            throw AppException.Forbidden("Solo el dueño del personaje puede cancelar su petición de descanso.");
        }

        var request = await requests.GetPendingAsync(character.Id, cancellationToken) ?? throw RestRequestErrors.NoPending();
        var now = clock.UtcNow;
        request.Cancel(currentUserId, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.RestRequestUpdatedAsync(request.CampaignId, request.CharacterId, request.Id, now, cancellationToken);
    }
}

/// <summary>
/// Rest requests of a campaign, newest first: DMs see all of them, players those of their own
/// characters. <paramref name="status"/> filters by status name (Pending, Approved, Rejected, Cancelled).
/// </summary>
public sealed class ListRestRequestsHandler(ICampaignAccess access, IRestRequestRepository requests)
{
    public async Task<IReadOnlyList<RestRequestDto>> HandleAsync(Guid currentUserId, Guid campaignId, string? status, CancellationToken cancellationToken = default)
    {
        var role = await access.RequireAsync(campaignId, currentUserId, CampaignRole.Player, cancellationToken);

        RestRequestStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(status))
        {
            statusFilter = EnumNames.TryParse<RestRequestStatus>(status, out var parsed)
                ? parsed
                : throw AppException.Validation("status", $"El estado debe ser {EnumNames.Describe<RestRequestStatus>()}.");
        }

        var views = await requests.ListViewsAsync(
            new RestRequestQuery(
                CampaignId: campaignId,
                CharacterOwnerUserId: role.IsAtLeast(CampaignRole.DM) ? null : currentUserId,
                Status: statusFilter),
            cancellationToken);
        return views.Select(RestRequestDto.From).ToList();
    }
}

/// <summary>A rest request: DMs and the owner of the character.</summary>
public sealed class GetRestRequestHandler(IRestRequestRepository requests, ICampaignAccess access)
{
    public async Task<RestRequestDto> HandleAsync(Guid currentUserId, Guid requestId, CancellationToken cancellationToken = default)
    {
        var view = (await requests.ListViewsAsync(new RestRequestQuery(Id: requestId), cancellationToken)).SingleOrDefault()
            ?? throw RestRequestErrors.NotFound();
        var role = await access.GetRoleAsync(view.Request.CampaignId, currentUserId, cancellationToken) ?? throw RestRequestErrors.NotFound();
        if (!role.IsAtLeast(CampaignRole.DM) && view.CharacterOwnerUserId != currentUserId)
        {
            throw AppException.Forbidden("No puedes ver esta petición de descanso.");
        }

        return RestRequestDto.From(view);
    }
}

/// <summary>A DM approves a pending rest and the game system applies it in the same transaction.</summary>
public sealed class ApproveRestRequestHandler(
    RestRequestLoader loader,
    ICharacterRepository characters,
    CampaignSystems systems,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task<RestRequestDto> HandleAsync(Guid currentUserId, Guid requestId, ResolveRestRequestRequest? body, CancellationToken cancellationToken = default)
    {
        var (request, role) = await loader.LoadAsync(requestId, currentUserId, cancellationToken);
        if (!role.IsAtLeast(CampaignRole.DM))
        {
            throw AppException.Forbidden("Solo un DM puede aprobar un descanso.");
        }

        if (!request.IsPending)
        {
            throw AppException.Conflict("La petición de descanso ya no está pendiente.");
        }

        var character = await characters.GetWithDetailsAsync(request.CharacterId, cancellationToken) ?? throw CharacterErrors.CharacterNotFound();
        var system = await systems.ForCharacterAsync(character, cancellationToken);
        var now = clock.UtcNow;
        using (var payload = JsonDocument.Parse(request.PayloadJson))
        {
            await system.Rests.ApplyAsync(new CharacterRef(character), request.Kind, payload.RootElement, now, cancellationToken);
        }

        request.Approve(currentUserId, body?.Comment, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.RestRequestUpdatedAsync(request.CampaignId, request.CharacterId, request.Id, now, cancellationToken);
        await notifier.CharacterUpdatedAsync(request.CampaignId, request.CharacterId, now, cancellationToken);
        return await loader.ToDtoAsync(request.Id, cancellationToken);
    }
}

/// <summary>A DM rejects a pending rest; the comment is optional.</summary>
public sealed class RejectRestRequestHandler(RestRequestLoader loader, IUnitOfWork unitOfWork, ICampaignNotifier notifier, IDateTimeProvider clock)
{
    public async Task<RestRequestDto> HandleAsync(Guid currentUserId, Guid requestId, ResolveRestRequestRequest? body, CancellationToken cancellationToken = default)
    {
        var (request, role) = await loader.LoadAsync(requestId, currentUserId, cancellationToken);
        if (!role.IsAtLeast(CampaignRole.DM))
        {
            throw AppException.Forbidden("Solo un DM puede rechazar un descanso.");
        }

        var now = clock.UtcNow;
        request.Reject(currentUserId, body?.Comment, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.RestRequestUpdatedAsync(request.CampaignId, request.CharacterId, request.Id, now, cancellationToken);
        return await loader.ToDtoAsync(request.Id, cancellationToken);
    }
}
