using Dnd.Domain.Characters;

namespace Dnd.Application.Abstractions.Persistence;

/// <summary>Filter of change request listings; null fields do not filter.</summary>
public sealed record ChangeRequestQuery(
    Guid? Id = null,
    Guid? CampaignId = null,
    Guid? CharacterId = null,
    Guid? RequestedByUserId = null,
    ChangeRequestStatus? Status = null);

/// <summary>A change request with the names the API shows next to it.</summary>
public sealed record ChangeRequestView(
    ChangeRequest Request,
    string CharacterName,
    Guid? CharacterOwnerUserId,
    string RequestedByDisplayName,
    string? ResolvedByDisplayName);

public interface IChangeRequestRepository
{
    /// <summary>Tracked request, ready to be resolved.</summary>
    Task<ChangeRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Tracked pending requests of a character, optionally of one type.</summary>
    Task<IReadOnlyList<ChangeRequest>> ListPendingAsync(Guid characterId, ChangeRequestType? type, CancellationToken cancellationToken = default);

    /// <summary>Read-only requests matching the query, newest first.</summary>
    Task<IReadOnlyList<ChangeRequestView>> ListViewsAsync(ChangeRequestQuery query, CancellationToken cancellationToken = default);

    void Add(ChangeRequest request);
}
