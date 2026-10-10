using OpenTrpg.Core.Domain.Characters;

namespace OpenTrpg.Core.Application.Abstractions.Persistence;

/// <summary>Filter of rest request listings; null fields do not filter.</summary>
/// <param name="CharacterOwnerUserId">Only requests of characters owned by this user.</param>
public sealed record RestRequestQuery(
    Guid? Id = null,
    Guid? CampaignId = null,
    Guid? CharacterId = null,
    Guid? CharacterOwnerUserId = null,
    RestRequestStatus? Status = null);

/// <summary>A rest request with the names the API shows next to it.</summary>
public sealed record RestRequestView(
    RestRequest Request,
    string CharacterName,
    Guid? CharacterOwnerUserId,
    string RequestedByDisplayName,
    string? ResolvedByDisplayName);

public interface IRestRequestRepository
{
    /// <summary>Tracked request, ready to be resolved.</summary>
    Task<RestRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Tracked pending request of a character, or null.</summary>
    Task<RestRequest?> GetPendingAsync(Guid characterId, CancellationToken cancellationToken = default);

    /// <summary>Tracked pending requests of the given characters.</summary>
    Task<IReadOnlyList<RestRequest>> ListPendingAsync(IReadOnlyCollection<Guid> characterIds, CancellationToken cancellationToken = default);

    /// <summary>Read-only requests matching the query, newest first.</summary>
    Task<IReadOnlyList<RestRequestView>> ListViewsAsync(RestRequestQuery query, CancellationToken cancellationToken = default);

    void Add(RestRequest request);
}
