using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Common;
using Dnd.Domain.Sessions;

namespace Dnd.Application.Sessions;

/// <summary>
/// Loads the session and the user a link token was issued for. Every problem with the token (missing,
/// manipulated, issued for another session, user no longer a member or active) is a 401, so the
/// endpoint reveals nothing about sessions or users.
/// </summary>
public sealed class PublicSessionLoader(
    ISessionLinkTokens tokens,
    ISessionRepository sessions,
    ICampaignRepository campaigns,
    IUserRepository users)
{
    public async Task<(GameSession Session, string CampaignName, string TimeZoneId, string UserDisplayName, Guid UserId)> LoadAsync(
        Guid sessionId,
        string? token,
        CancellationToken cancellationToken)
    {
        if (!tokens.TryValidate(sessionId, token, out var userId))
        {
            throw SessionErrors.InvalidLink();
        }

        var session = await sessions.GetWithDetailsAsync(sessionId, cancellationToken) ?? throw SessionErrors.SessionNotFound();
        var user = await users.GetByIdAsync(userId, cancellationToken);
        var info = (await campaigns.ListScheduleInfoAsync([session.CampaignId], cancellationToken)).FirstOrDefault();
        var isMember = (await campaigns.ListMemberContactsAsync([session.CampaignId], cancellationToken)).Any(m => m.UserId == userId);
        if (user is null || !user.IsActive || info is null || !isMember)
        {
            throw SessionErrors.InvalidLink();
        }

        return (session, info.Name, info.TimeZoneId, user.DisplayName, userId);
    }

    public static PublicSessionDto ToDto(GameSession session, string campaignName, string timeZoneId, string userDisplayName, Guid userId)
    {
        var rsvp = session.FindRsvp(userId);
        return new PublicSessionDto(
            session.Id,
            session.Number,
            campaignName,
            session.Title,
            session.StartsAt,
            CampaignSchedule.ToLocalIso(session.StartsAt, timeZoneId),
            timeZoneId,
            session.DurationMinutes,
            session.Location,
            session.Notes,
            session.Status.ToString(),
            userDisplayName,
            rsvp?.Status.ToString(),
            rsvp?.Comment);
    }
}

/// <summary>The session shown by the page linked in the emails (no sign-in; the token identifies the member).</summary>
public sealed class GetPublicSessionHandler(PublicSessionLoader loader)
{
    public async Task<PublicSessionDto> HandleAsync(Guid sessionId, string? token, CancellationToken cancellationToken = default)
    {
        var (session, campaignName, zone, name, userId) = await loader.LoadAsync(sessionId, token, cancellationToken);
        return PublicSessionLoader.ToDto(session, campaignName, zone, name, userId);
    }
}

/// <summary>Answers attendance on behalf of the member the link token was issued to.</summary>
public sealed class PublicRsvpHandler(PublicSessionLoader loader, IUnitOfWork unitOfWork, IDateTimeProvider clock)
{
    public async Task<PublicSessionDto> HandleAsync(Guid sessionId, string? token, PublicRsvpRequest request, CancellationToken cancellationToken = default)
    {
        var (session, campaignName, zone, name, userId) = await loader.LoadAsync(sessionId, token, cancellationToken);

        // The comment written in the app is kept when the answer is changed from the link.
        session.Respond(userId, EnumNames.Parse<RsvpStatus>(request.Status), session.FindRsvp(userId)?.Comment, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return PublicSessionLoader.ToDto(session, campaignName, zone, name, userId);
    }
}
