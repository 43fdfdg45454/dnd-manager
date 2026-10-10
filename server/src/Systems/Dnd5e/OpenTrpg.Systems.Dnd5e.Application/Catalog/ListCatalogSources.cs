using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.ContentPacks;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Application.Catalog;

/// <summary>
/// Sources of the catalog for every user: the base pack (SRD) first, then the imported packs of the system by name (to
/// label pack content). With a campaign scope (<c>?campaignId=</c>) every source says whether the campaign enables it.
/// </summary>
public sealed class ListCatalogSourcesHandler(IContentPackImporter importer, CatalogScopeContext scope)
{
    public const string SrdName = Dnd5eCatalogSources.SrdName;

    public async Task<IReadOnlyList<CatalogSourceDto>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var current = scope.Current is { CampaignId: not null } campaignScope ? campaignScope : null;
        var packs = (await importer.ListAsync(cancellationToken))
            .Where(p => p.SystemId == Dnd5eCatalogSources.SystemId)
            .OrderByDescending(p => p.IsBase)
            .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(p => p.Id, StringComparer.Ordinal)
            .Select(p => new CatalogSourceDto(p.Id, p.Name, p.IsBase ? null : p.Version)
            {
                IsBase = p.IsBase,
                Enabled = current is null ? null : p.IsBase || current.Allows(p.Id),
            })
            .ToList();
        if (packs.All(p => p.Id != Dnd5eCatalogSources.Srd))
        {
            packs.Insert(0, new CatalogSourceDto(Dnd5eCatalogSources.Srd, SrdName, null) { IsBase = true, Enabled = current is null ? null : true });
        }

        return packs;
    }
}
