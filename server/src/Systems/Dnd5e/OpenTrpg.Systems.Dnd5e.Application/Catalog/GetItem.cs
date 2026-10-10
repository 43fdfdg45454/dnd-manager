using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Items;
using OpenTrpg.Core.Application;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions.Persistence;
using OpenTrpg.Systems.Dnd5e.Application.Items;

namespace OpenTrpg.Systems.Dnd5e.Application.Catalog;

/// <summary>
/// Detail of an item template: SRD items for any authenticated user; homebrew items only for members
/// of their campaign (404 otherwise, so their existence is not revealed).
/// </summary>
public sealed class GetItemHandler(IItemTemplateRepository templates, ICampaignAccess access)
{
    public async Task<ItemDetailDto> HandleAsync(Guid currentUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var template = (await templates.ListByIdsAsync([id], cancellationToken)).SingleOrDefault() ?? throw CatalogErrors.ItemNotFound();
        if (template.CampaignId is { } campaignId && await access.GetRoleAsync(campaignId, currentUserId, cancellationToken) is null)
        {
            throw CatalogErrors.ItemNotFound();
        }

        return ItemDetailDto.From(template);
    }
}
