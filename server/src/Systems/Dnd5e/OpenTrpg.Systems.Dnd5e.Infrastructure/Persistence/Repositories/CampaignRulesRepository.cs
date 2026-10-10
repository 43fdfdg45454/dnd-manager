using Microsoft.EntityFrameworkCore;
using OpenTrpg.Core.Infrastructure.Persistence;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions.Persistence;
using OpenTrpg.Systems.Dnd5e.Domain.Rules;

namespace OpenTrpg.Systems.Dnd5e.Infrastructure.Persistence.Repositories;

internal sealed class CampaignRulesRepository(AppDbContext db) : ICampaignRulesRepository
{
    public Task<Dnd5eCampaignRules?> GetAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
        db.Set<Dnd5eCampaignRules>().FirstOrDefaultAsync(x => x.CampaignId == campaignId, cancellationToken);

    public void Add(Dnd5eCampaignRules rules) => db.Set<Dnd5eCampaignRules>().Add(rules);
}
