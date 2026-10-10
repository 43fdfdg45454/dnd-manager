using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions.Persistence;

namespace OpenTrpg.Systems.Dnd5e.Application.Catalog;

public sealed class GetFeatureHandler(ICatalogRepository catalog)
{
    public async Task<FeatureDto> HandleAsync(string index, CancellationToken cancellationToken = default) =>
        FeatureDto.From(await catalog.GetFeatureAsync(index, cancellationToken) ?? throw CatalogErrors.FeatureNotFound());
}
