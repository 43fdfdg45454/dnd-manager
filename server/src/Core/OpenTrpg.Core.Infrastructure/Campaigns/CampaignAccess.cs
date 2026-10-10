using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Campaigns;
using OpenTrpg.Core.Domain.Campaigns;
using OpenTrpg.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace OpenTrpg.Core.Infrastructure.Campaigns;

/// <summary>
/// Membership lookup with a single query on the unique (CampaignId, UserId) index. A missing
/// campaign and a non-member are indistinguishable on purpose (both 404).
/// </summary>
internal sealed class CampaignAccess(AppDbContext db) : ICampaignAccess
{
    public Task<CampaignRole?> GetRoleAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken = default) =>
        db.CampaignMembers
            .AsNoTracking()
            .Where(m => m.CampaignId == campaignId && m.UserId == userId)
            .Select(m => (CampaignRole?)m.Role)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<CampaignRole> RequireAsync(Guid campaignId, Guid userId, CampaignRole minimumRole, CancellationToken cancellationToken = default)
    {
        var role = await GetRoleAsync(campaignId, userId, cancellationToken)
            ?? throw CampaignErrors.CampaignNotFound();

        return role.IsAtLeast(minimumRole)
            ? role
            : throw CampaignErrors.InsufficientRole();
    }
}
