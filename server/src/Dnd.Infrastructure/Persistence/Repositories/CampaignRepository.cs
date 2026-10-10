using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Campaigns;
using Dnd.Domain.Campaigns;
using Dnd.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Dnd.Infrastructure.Persistence.Repositories;

internal sealed class CampaignRepository(AppDbContext db) : ICampaignRepository
{
    public Task<Campaign?> GetWithMembersAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Campaigns.Include(x => x.Members).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Campaign?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Campaigns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CampaignSummaryDto>> ListSummariesForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var rows = await (
                from membership in db.CampaignMembers
                where membership.UserId == userId
                join campaign in db.Campaigns on membership.CampaignId equals campaign.Id
                join owner in db.Users on campaign.OwnerId equals owner.Id
                select new
                {
                    campaign.Id,
                    campaign.Name,
                    campaign.Description,
                    campaign.OwnerId,
                    OwnerDisplayName = owner.DisplayName,
                    MyRole = membership.Role,
                    MemberCount = db.CampaignMembers.Count(m => m.CampaignId == campaign.Id),
                    campaign.CreatedAt,
                    campaign.SystemId,
                })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Sorted in memory so the order does not depend on the database collation.
        return rows
            .OrderBy(r => r.Name, StringComparer.InvariantCultureIgnoreCase)
            .ThenBy(r => r.Id)
            .Select(r => new CampaignSummaryDto(
                r.Id, r.Name, r.Description, r.OwnerId, r.OwnerDisplayName, r.MyRole.ToString(), r.MemberCount, r.CreatedAt, r.SystemId))
            .ToList();
    }

    public async Task<IReadOnlyList<MemberDto>> ListMembersAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        var rows = await (
                from member in db.CampaignMembers
                where member.CampaignId == campaignId
                join user in db.Users on member.UserId equals user.Id
                select new { user.Id, user.DisplayName, user.Email, member.Role, member.JoinedAt })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows
            .OrderByDescending(r => r.Role)
            .ThenBy(r => r.DisplayName, StringComparer.InvariantCultureIgnoreCase)
            .ThenBy(r => r.Email, StringComparer.Ordinal)
            .Select(r => new MemberDto(r.Id, r.DisplayName, r.Email, r.Role.ToString(), r.JoinedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<MemberContact>> ListMemberContactsAsync(IReadOnlyCollection<Guid> campaignIds, CancellationToken cancellationToken = default)
    {
        var rows = await (
                from member in db.CampaignMembers
                where campaignIds.Contains(member.CampaignId)
                join user in db.Users on member.UserId equals user.Id
                select new { member.CampaignId, user.Id, member.Role, user.DisplayName, user.Email, user.NotificationsEnabled, user.IsActive })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows
            .OrderBy(r => r.DisplayName, StringComparer.InvariantCultureIgnoreCase)
            .ThenBy(r => r.Email, StringComparer.Ordinal)
            .Select(r => new MemberContact(r.CampaignId, r.Id, r.Role, r.DisplayName, r.Email, r.NotificationsEnabled, r.IsActive))
            .ToList();
    }

    public async Task<IReadOnlyList<CampaignScheduleInfo>> ListScheduleInfoAsync(IReadOnlyCollection<Guid> campaignIds, CancellationToken cancellationToken = default)
    {
        var rows = await db.Campaigns.AsNoTracking()
            .Where(x => campaignIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Name, x.TimeZoneId, x.ReminderOffsetsMinutesJson })
            .ToListAsync(cancellationToken);
        return rows
            .Select(r => new CampaignScheduleInfo(r.Id, r.Name, r.TimeZoneId, CampaignSchedule.ParseOffsets(r.ReminderOffsetsMinutesJson)))
            .ToList();
    }

    public async Task<IReadOnlyList<Guid>> ListCampaignIdsOfUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await db.CampaignMembers.AsNoTracking().Where(m => m.UserId == userId).Select(m => m.CampaignId).ToListAsync(cancellationToken);

    public void Add(Campaign campaign) => db.Campaigns.Add(campaign);

    public void Remove(Campaign campaign) => db.Campaigns.Remove(campaign);

    public void AddOwnershipTransfer(OwnershipTransfer transfer) => db.OwnershipTransfers.Add(transfer);

    public Task<CampaignInvitation?> GetInvitationAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.CampaignInvitations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<CampaignInvitation?> FindInvitationAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken = default) =>
        db.CampaignInvitations.AsNoTracking().FirstOrDefaultAsync(x => x.CampaignId == campaignId && x.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<CampaignInvitationDto>> ListInvitationsForCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        var rows = await (
                from invitation in db.CampaignInvitations
                where invitation.CampaignId == campaignId
                join user in db.Users on invitation.UserId equals user.Id
                join inviter in db.Users on invitation.InvitedByUserId equals inviter.Id
                select new { invitation.Id, UserId = user.Id, user.DisplayName, user.Email, invitation.Role, Inviter = inviter.DisplayName, invitation.CreatedAt })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Sorted in memory: SQLite cannot order by DateTimeOffset.
        return rows
            .OrderBy(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .Select(r => new CampaignInvitationDto(r.Id, r.UserId, r.DisplayName, r.Email, r.Role.ToString(), r.Inviter, r.CreatedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<MyInvitationDto>> ListInvitationsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var rows = await (
                from invitation in db.CampaignInvitations
                where invitation.UserId == userId
                join campaign in db.Campaigns on invitation.CampaignId equals campaign.Id
                join inviter in db.Users on invitation.InvitedByUserId equals inviter.Id
                select new { invitation.Id, invitation.CampaignId, campaign.Name, invitation.Role, Inviter = inviter.DisplayName, invitation.CreatedAt })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows
            .OrderByDescending(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .Select(r => new MyInvitationDto(r.Id, r.CampaignId, r.Name, r.Role.ToString(), r.Inviter, r.CreatedAt))
            .ToList();
    }

    public void AddInvitation(CampaignInvitation invitation) => db.CampaignInvitations.Add(invitation);

    public void RemoveInvitation(CampaignInvitation invitation) => db.CampaignInvitations.Remove(invitation);
}
