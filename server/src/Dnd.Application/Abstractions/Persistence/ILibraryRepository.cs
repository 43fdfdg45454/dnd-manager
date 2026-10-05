using Dnd.Domain.Library;

namespace Dnd.Application.Abstractions.Persistence;

/// <summary>A document recommended in a campaign with the DM's note.</summary>
public sealed record RecommendedDocument(LibraryDocument Document, string? Note);

public interface ILibraryRepository
{
    /// <summary>Read-only documents by title; <paramref name="search"/> is lowercase text looked up in title and description.</summary>
    Task<IReadOnlyList<LibraryDocument>> ListAsync(string? search, LibraryCategory? category, CancellationToken cancellationToken = default);

    /// <summary>Tracked document, or null.</summary>
    Task<LibraryDocument?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecommendedDocument>> ListRecommendedAsync(Guid campaignId, CancellationToken cancellationToken = default);

    /// <summary>Tracked recommendation, or null.</summary>
    Task<CampaignDocument?> GetRecommendationAsync(Guid campaignId, Guid documentId, CancellationToken cancellationToken = default);

    void Add(LibraryDocument document);

    /// <summary>Deletes the document and the recommendations that point at it.</summary>
    void Remove(LibraryDocument document);

    void Add(CampaignDocument recommendation);

    void Remove(CampaignDocument recommendation);
}
