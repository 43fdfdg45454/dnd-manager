using OpenTrpg.Core.Domain.Catalog;

namespace OpenTrpg.Core.Application.Abstractions.Persistence;

/// <summary>The content packs of the instance and the packs each campaign enables.</summary>
public interface IContentPackRepository
{
    /// <summary>Packs of a game system (base and imported), untracked.</summary>
    Task<IReadOnlyList<ContentPack>> ListBySystemAsync(string systemId, CancellationToken cancellationToken = default);

    /// <summary>Ids of the packs enabled in the campaign (the base pack is never stored).</summary>
    Task<IReadOnlyList<string>> ListEnabledAsync(Guid campaignId, CancellationToken cancellationToken = default);

    /// <summary>Replaces the packs enabled in the campaign (saved with the unit of work).</summary>
    Task ReplaceEnabledAsync(Guid campaignId, IReadOnlyCollection<string> packIds, Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default);
}
