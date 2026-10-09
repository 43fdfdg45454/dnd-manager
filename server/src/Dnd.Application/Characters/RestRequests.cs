using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Common;
using Dnd.Domain.Campaigns;
using Dnd.Domain.Characters;
using FluentValidation;

namespace Dnd.Application.Characters;

// Rests approved by the DM (phase 16b): the owner asks for a short or long rest, a DM approves it
// (the rest is applied with the PHB rules), rejects it, or makes it moot by resting the character directly.

/// <summary>A rest request with the names of the people involved.</summary>
/// <param name="Kind">"Short" or "Long".</param>
/// <param name="HitDice">Hit dice the player wants to spend per class index (short rest only).</param>
/// <param name="Status">Pending, Approved, Rejected or Cancelled.</param>
public sealed record RestRequestDto(
    Guid Id,
    Guid CampaignId,
    Guid CharacterId,
    string CharacterName,
    Guid RequestedByUserId,
    string RequestedByDisplayName,
    string Kind,
    IReadOnlyDictionary<string, int> HitDice,
    string Status,
    DateTimeOffset RequestedAt,
    Guid? ResolvedByUserId,
    string? ResolvedByDisplayName,
    DateTimeOffset? ResolvedAt,
    string? Comment)
{
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
            request.Kind.ToString(),
            request.HitDice,
            request.Status.ToString(),
            request.RequestedAt,
            request.ResolvedByUserId,
            view.ResolvedByDisplayName,
            request.ResolvedAt,
            request.Comment);
    }
}

/// <summary>The pending rest of a character, as shown on the sheet and at the DM's table.</summary>
/// <param name="Kind">"Short" or "Long".</param>
public sealed record PendingRestDto(Guid Id, string Kind, IReadOnlyDictionary<string, int> HitDice, DateTimeOffset RequestedAt)
{
    public static PendingRestDto From(RestRequest request) =>
        new(request.Id, request.Kind.ToString(), request.HitDice, request.RequestedAt);
}

/// <param name="Kind">"short" or "long" (any case).</param>
/// <param name="HitDice">Short rest: hit dice to spend per class index (absent: none).</param>
public sealed record CreateRestRequestRequest(string Kind, IReadOnlyDictionary<string, int>? HitDice);

public static class RestKinds
{
    /// <summary>Parses "short"/"long" in any case; null when it is neither.</summary>
    public static RestKind? TryParse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "short" => RestKind.Short,
        "long" => RestKind.Long,
        _ => null,
    };
}

public sealed class CreateRestRequestRequestValidator : AbstractValidator<CreateRestRequestRequest>
{
    public const int MaxClasses = 20;

    public CreateRestRequestRequestValidator()
    {
        RuleFor(x => x.Kind)
            .Must(k => RestKinds.TryParse(k) is not null)
            .WithMessage("El descanso debe ser short o long.");
        RuleFor(x => x.HitDice!)
            .Must(d => d.Count <= MaxClasses).WithMessage($"No se admiten más de {MaxClasses} clases.")
            .Must(d => d.All(e => !string.IsNullOrWhiteSpace(e.Key) && e.Value is >= 0 and <= RestRequest.MaxHitDicePerClass))
            .WithMessage($"Cada clase debe indicar entre 0 y {RestRequest.MaxHitDicePerClass} dados de golpe.")
            .When(x => x.HitDice is not null)
            .OverridePropertyName("hitDice");
        RuleFor(x => x.HitDice!)
            .Must(d => d.Values.All(v => v == 0))
            .WithMessage("El descanso largo no gasta dados de golpe.")
            .When(x => x.HitDice is not null && RestKinds.TryParse(x.Kind) == RestKind.Long)
            .OverridePropertyName("hitDice");
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
/// The owner of an active character asks the DM for a rest. A short rest names the hit dice to spend,
/// which must not exceed the remaining ones now (approval spends what remains then). One pending
/// request per character (409).
/// </summary>
public sealed class CreateRestRequestHandler(
    CharacterLoader characters,
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

        var kind = RestKinds.TryParse(body.Kind) ?? throw AppException.Validation("kind", "El descanso debe ser short o long.");
        var hitDice = kind == RestKind.Short ? body.HitDice ?? new Dictionary<string, int>() : new Dictionary<string, int>();
        foreach (var (classIndex, count) in hitDice)
        {
            var index = classIndex.Trim();
            if (count > 0 && !character.Classes.Any(c => c.ClassIndex == index))
            {
                throw AppException.Validation("hitDice", $"El personaje no tiene la clase '{index}'.");
            }

            if (count > character.HitDiceRemaining(index))
            {
                throw AppException.Validation("hitDice", $"No quedan suficientes dados de golpe de '{index}'.");
            }
        }

        if (await requests.GetPendingAsync(character.Id, cancellationToken) is not null)
        {
            throw AppException.Conflict("Ya hay un descanso pendiente para este personaje.");
        }

        var now = clock.UtcNow;
        var request = RestRequest.Create(character.CampaignId, character.Id, currentUserId, kind, hitDice, now);
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

/// <summary>
/// A DM approves a pending rest and it is applied in the same transaction with the PHB rules: a short
/// rest spends the requested hit dice (capped at the remaining ones now), a long rest restores hit
/// points, slots and resources, recovers half the hit dice and lowers exhaustion.
/// </summary>
public sealed class ApproveRestRequestHandler(
    RestRequestLoader loader,
    ICharacterRepository characters,
    ICharacterSheetService sheets,
    IDiceRoller dice,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    CompanionPlanner companions,
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
        var sheet = await sheets.CalculateAsync(character, cancellationToken);
        var now = clock.UtcNow;
        if (request.Kind == RestKind.Short)
        {
            character.ShortRest(character.ClampHitDiceToRemaining(request.HitDice), sheet, dice, now);
        }
        else
        {
            character.LongRest(sheet, now);
            await companions.RestoreAfterLongRestAsync([character], now, cancellationToken);
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
