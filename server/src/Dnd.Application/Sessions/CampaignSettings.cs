using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Campaigns;
using Dnd.Domain.Campaigns;
using Dnd.Domain.Sessions;
using FluentValidation;

namespace Dnd.Application.Sessions;

/// <summary>Partial update: absent fields are left unchanged.</summary>
public sealed record UpdateCampaignSettingsRequest(string? TimeZoneId, IReadOnlyList<int>? ReminderOffsetsMinutes);

public sealed class UpdateCampaignSettingsRequestValidator : AbstractValidator<UpdateCampaignSettingsRequest>
{
    public UpdateCampaignSettingsRequestValidator()
    {
        RuleFor(x => x.TimeZoneId).Must(CampaignSchedule.IsValidTimeZone).When(x => x.TimeZoneId is not null)
            .WithMessage(SessionRules.TimeZoneMessage);
        RuleFor(x => x.ReminderOffsetsMinutes).Must(o => CampaignSchedule.AreValidOffsets(o!.ToList())).When(x => x.ReminderOffsetsMinutes is not null)
            .WithMessage(
                $"Los recordatorios deben ser como máximo {CampaignSchedule.MaxOffsets} valores únicos entre {CampaignSchedule.MinOffsetMinutes} y {CampaignSchedule.MaxOffsetMinutes} minutos.");
    }
}

/// <summary>
/// Changes the time zone and reminder offsets of a campaign (at least DM). When the offsets change,
/// the pending reminders of its upcoming scheduled sessions are regenerated.
/// </summary>
public sealed class UpdateCampaignSettingsHandler(
    ICampaignRepository campaigns,
    ISessionRepository sessions,
    ICampaignAccess access,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
{
    public async Task<CampaignDto> HandleAsync(Guid currentUserId, Guid campaignId, UpdateCampaignSettingsRequest request, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.DM, cancellationToken);

        var campaign = await campaigns.GetWithMembersAsync(campaignId, cancellationToken) ?? throw CampaignErrors.CampaignNotFound();
        var now = clock.UtcNow;
        var offsetsChanged = campaign.UpdateSettings(currentUserId, request.TimeZoneId, request.ReminderOffsetsMinutes, now);

        if (offsetsChanged)
        {
            foreach (var session in await sessions.ListUpcomingScheduledAsync(campaignId, now, cancellationToken))
            {
                var (removed, added) = session.SyncReminders(campaign.ReminderOffsetsMinutes, now);
                sessions.RemoveReminders(removed);
                sessions.AddReminders(added);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var members = await campaigns.ListMembersAsync(campaignId, cancellationToken);
        return CampaignDto.From(campaign, members, currentUserId);
    }
}
