using OpenTrpg.Core.Domain.Catalog;

namespace OpenTrpg.Core.Application.ContentPacks;

/// <summary>
/// The catalog definitions a use case can see: the sources (base packs, the packs enabled in the campaign and the
/// campaign's homebrew) by <c>Source</c>. <see cref="CampaignId"/> is null for the global scope (the base packs and
/// every imported pack: the compendium outside a campaign).
/// </summary>
public sealed record CatalogScope(Guid? CampaignId, IReadOnlySet<string> Sources)
{
    public bool IsGlobal => CampaignId is null;

    /// <summary>Whether a definition of <paramref name="source"/> belongs to the scope.</summary>
    public bool Allows(string source) => Sources.Contains(source);

    /// <summary>A scope for a campaign: <paramref name="baseSources"/>, the packs enabled in it and its homebrew.</summary>
    public static CatalogScope ForCampaign(Guid campaignId, IEnumerable<string> baseSources, IEnumerable<string> enabledPacks) =>
        new(campaignId, baseSources.Concat(enabledPacks).Append(CatalogSources.Homebrew).ToHashSet(StringComparer.Ordinal));
}

/// <summary>Builds the <see cref="CatalogScope"/> of a campaign, of a character's campaign or the global one.</summary>
public interface ICatalogScopeResolver
{
    /// <summary>The base packs of the campaign's system, the packs the campaign enabled and its homebrew.</summary>
    Task<CatalogScope> ForCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default);

    /// <summary>The scope of the campaign of a character (global when the character does not exist).</summary>
    Task<CatalogScope> ForCharacterAsync(Guid characterId, CancellationToken cancellationToken = default);

    /// <summary>The base packs and every imported pack (the compendium outside a campaign).</summary>
    Task<CatalogScope> GlobalAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// The catalog scope of the current request (registered per request scope). Game systems filter their catalog queries
/// with <see cref="Current"/>; null means no filter (every definition of the base and imported packs, as the global
/// scope). Set by the catalog endpoints (<c>?campaignId=</c>) and by the use cases of a campaign or a character.
/// </summary>
public sealed class CatalogScopeContext(ICatalogScopeResolver resolver)
{
    public CatalogScope? Current { get; private set; }

    public void Use(CatalogScope scope) => Current = scope;

    /// <summary>Uses the scope of <paramref name="campaignId"/> unless it is already the current one.</summary>
    public async Task<CatalogScope> UseCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        if (Current is { CampaignId: { } current } scope && current == campaignId)
        {
            return scope;
        }

        Current = await resolver.ForCampaignAsync(campaignId, cancellationToken);
        return Current;
    }

    /// <summary>Whether a definition of <paramref name="source"/> is visible now (always without a scope).</summary>
    public bool Allows(string source) => Current?.Allows(source) ?? true;
}
