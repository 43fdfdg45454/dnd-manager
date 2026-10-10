using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.ContentPacks;
using OpenTrpg.Core.Domain.Catalog;

namespace OpenTrpg.Core.Application.Catalog;

/// <summary>Sources of the catalog for every user: the SRD first, then the imported packs by name (to label pack content).</summary>
public sealed class ListCatalogSourcesHandler(IContentPackImporter importer)
{
    public const string SrdName = "SRD 5.1";

    public async Task<IReadOnlyList<CatalogSourceDto>> HandleAsync(CancellationToken cancellationToken = default) =>
        [
            new CatalogSourceDto(Dnd5eCatalogSources.Srd, SrdName, null),
            .. (await importer.ListAsync(cancellationToken)).Select(p => new CatalogSourceDto(p.Id, p.Name, p.Version)),
        ];
}
