using Dnd.Application.Abstractions.Persistence;

namespace Dnd.Application.Catalog;

/// <summary>Detail of an SRD item. Homebrew items are served by campaign-scoped endpoints (later phase).</summary>
public sealed class GetItemHandler(ICatalogRepository catalog)
{
    public async Task<ItemDetailDto> HandleAsync(Guid id, CancellationToken cancellationToken = default) =>
        ItemDetailDto.From(await catalog.GetSrdItemAsync(id, cancellationToken) ?? throw CatalogErrors.ItemNotFound());
}
