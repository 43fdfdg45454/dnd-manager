using Microsoft.EntityFrameworkCore;
using OpenTrpg.Core.Application.ContentPacks;
using OpenTrpg.Core.Application.Systems;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Infrastructure.Persistence;

namespace OpenTrpg.Core.Infrastructure.Catalog;

/// <summary>
/// <see cref="ICatalogScopeResolver"/> over <c>ContentPacks</c> and <c>CampaignContentPacks</c>. The base sources of a
/// system are its registered base packs plus <see cref="GameSystemInfo.BaseCatalogSources"/> (so an instance whose base
/// pack is not loaded yet still sees its own base content).
/// </summary>
internal sealed class CatalogScopeResolver(AppDbContext db, IGameSystemRegistry systems) : ICatalogScopeResolver
{
    public async Task<CatalogScope> ForCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        var systemId = await db.Campaigns.AsNoTracking().Where(x => x.Id == campaignId).Select(x => x.SystemId).FirstOrDefaultAsync(cancellationToken);
        if (systemId is null)
        {
            return await GlobalAsync(cancellationToken);
        }

        var baseSources = await BaseSourcesAsync(systemId, cancellationToken);
        var enabled = await db.CampaignContentPacks.AsNoTracking()
            .Where(x => x.CampaignId == campaignId)
            .Select(x => x.PackId)
            .ToListAsync(cancellationToken);
        return CatalogScope.ForCampaign(campaignId, baseSources, enabled);
    }

    public async Task<CatalogScope> ForCharacterAsync(Guid characterId, CancellationToken cancellationToken = default)
    {
        var campaignId = await db.Characters.AsNoTracking().Where(x => x.Id == characterId).Select(x => (Guid?)x.CampaignId).FirstOrDefaultAsync(cancellationToken);
        return campaignId is { } id ? await ForCampaignAsync(id, cancellationToken) : await GlobalAsync(cancellationToken);
    }

    public async Task<CatalogScope> GlobalAsync(CancellationToken cancellationToken = default)
    {
        var packs = await db.ContentPacks.AsNoTracking().Select(x => x.Id).ToListAsync(cancellationToken);
        return new CatalogScope(null, packs.Concat(systems.All.SelectMany(s => s.Info.BaseCatalogSources)).ToHashSet(StringComparer.Ordinal));
    }

    private async Task<List<string>> BaseSourcesAsync(string systemId, CancellationToken cancellationToken)
    {
        var packs = await db.ContentPacks.AsNoTracking().Where(x => x.SystemId == systemId && x.IsBase).Select(x => x.Id).ToListAsync(cancellationToken);
        packs.AddRange(systems.Find(systemId)?.Info.BaseCatalogSources ?? []);
        return packs;
    }
}
