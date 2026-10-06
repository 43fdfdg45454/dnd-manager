using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Campaigns;
using Dnd.Application.Common;
using Dnd.Domain.Campaigns;
using Dnd.Domain.Sessions;
using Microsoft.Extensions.Logging;

namespace Dnd.Application.Sessions;

/// <summary>
/// Sessions of a campaign ordered by start. By default only the ones that have not finished; with
/// <c>from</c> the lower bound is explicit, and with <c>includePast</c> there is none.
/// </summary>
public sealed class ListSessionsHandler(ICampaignAccess access, ISessionRepository sessions, SessionViewBuilder views, IDateTimeProvider clock)
{
    public async Task<IReadOnlyList<SessionDto>> HandleAsync(Guid currentUserId, Guid campaignId, ListSessionsQuery query, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.Player, cancellationToken);

        var now = clock.UtcNow;
        var all = await sessions.ListByCampaignsAsync([campaignId], cancellationToken);
        var filtered = all.Where(s =>
            (query.From is { } from ? s.StartsAt >= from : query.IncludePast == true || s.IsUpcomingOrInProgress(now))
            && (query.To is not { } to || s.StartsAt < to)).ToList();
        return await views.BuildAsync(filtered, currentUserId, cancellationToken);
    }
}

/// <summary>A DM schedules a session: it gets the next number of the campaign and its reminders.</summary>
/// <remarks>
/// <c>Number</c> is the highest existing number plus one, read in the same unit of work as the insert.
/// SQLite (tests) has no contention; on PostgreSQL two simultaneous creations could read the same
/// maximum, in which case the unique index (CampaignId, Number) rejects the second insert and the API
/// answers 409 so the client retries.
/// </remarks>
public sealed class CreateSessionHandler(
    ICampaignAccess access,
    ICampaignRepository campaigns,
    ISessionRepository sessions,
    SessionViewBuilder views,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task<SessionDto> HandleAsync(Guid currentUserId, Guid campaignId, CreateSessionRequest request, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.DM, cancellationToken);
        var campaign = await campaigns.GetByIdAsync(campaignId, cancellationToken) ?? throw CampaignErrors.CampaignNotFound();

        var now = clock.UtcNow;
        var number = await sessions.NextNumberAsync(campaignId, cancellationToken);
        var session = GameSession.Create(
            campaignId,
            number,
            request.Title,
            request.StartsAt,
            request.DurationMinutes,
            request.Location,
            request.Notes,
            currentUserId,
            now);
        session.SyncReminders(campaign.ReminderOffsetsMinutes, now);

        sessions.Add(session);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.SessionUpdatedAsync(session, now, cancellationToken);
        return await views.BuildOneAsync(session, currentUserId, cancellationToken);
    }
}

/// <summary>A session with its answers; reminders only for DMs. Non-members get 404.</summary>
public sealed class GetSessionHandler(SessionLoader loader, SessionViewBuilder views)
{
    public async Task<SessionDto> HandleAsync(Guid currentUserId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var loaded = await loader.LoadAsync(sessionId, currentUserId, cancellationToken);
        return await views.BuildOneAsync(loaded.Session, currentUserId, cancellationToken);
    }
}

public sealed class UpdateSessionHandler(
    SessionLoader loader,
    ISessionRepository sessions,
    SessionViewBuilder views,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task<SessionDto> HandleAsync(Guid currentUserId, Guid sessionId, UpdateSessionRequest request, CancellationToken cancellationToken = default)
    {
        var loaded = await loader.LoadForDmAsync(sessionId, currentUserId, "Solo un DM puede editar las sesiones.", cancellationToken);
        var session = loaded.Session;
        var now = clock.UtcNow;

        var regenerateReminders = false;
        if (request.Title is not null)
        {
            session.Rename(request.Title, now);
        }

        if (request.StartsAt is { } startsAt && startsAt.ToUniversalTime() != session.StartsAt)
        {
            session.Reschedule(startsAt, now);
            regenerateReminders = true;
        }

        if (request.DurationMinutes.IsSet)
        {
            session.SetDuration(request.DurationMinutes.Value, now);
        }

        if (request.Location.IsSet)
        {
            session.SetLocation(request.Location.Value, now);
        }

        if (request.Notes.IsSet)
        {
            session.SetNotes(request.Notes.Value, now);
        }

        if (request.Status is not null)
        {
            var status = EnumNames.Parse<SessionStatus>(request.Status);
            if (status != session.Status)
            {
                session.SetStatus(status, now);
                regenerateReminders = true;
            }
        }

        if (regenerateReminders)
        {
            var (removed, added) = session.SyncReminders(loaded.Campaign.ReminderOffsetsMinutes, now);
            sessions.RemoveReminders(removed);
            sessions.AddReminders(added);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.SessionUpdatedAsync(session, now, cancellationToken);
        return await views.BuildOneAsync(session, currentUserId, cancellationToken);
    }
}

/// <summary>Deletes the session with its answers and pending reminders. Requires at least DM.</summary>
public sealed class DeleteSessionHandler(SessionLoader loader, ISessionRepository sessions, IUnitOfWork unitOfWork, ICampaignNotifier notifier, IDateTimeProvider clock)
{
    public async Task HandleAsync(Guid currentUserId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var loaded = await loader.LoadForDmAsync(sessionId, currentUserId, "Solo un DM puede borrar las sesiones.", cancellationToken);
        sessions.Remove(loaded.Session);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.SessionUpdatedAsync(loaded.Session, clock.UtcNow, cancellationToken);
    }
}

/// <summary>A member sets or changes their own answer to a session.</summary>
public sealed class RespondToSessionHandler(SessionLoader loader, SessionViewBuilder views, IUnitOfWork unitOfWork, ICampaignNotifier notifier, IDateTimeProvider clock)
{
    public async Task<SessionDto> HandleAsync(Guid currentUserId, Guid sessionId, RsvpRequest request, CancellationToken cancellationToken = default)
    {
        var loaded = await loader.LoadAsync(sessionId, currentUserId, cancellationToken);
        loaded.Session.Respond(currentUserId, EnumNames.Parse<RsvpStatus>(request.Status), request.Comment, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.SessionUpdatedAsync(loaded.Session, clock.UtcNow, cancellationToken);
        return await views.BuildOneAsync(loaded.Session, currentUserId, cancellationToken);
    }
}

/// <summary>Sets the journal summary of a session (blank removes it). Requires at least DM.</summary>
public sealed class SetSessionSummaryHandler(SessionLoader loader, SessionViewBuilder views, IUnitOfWork unitOfWork, ICampaignNotifier notifier, IDateTimeProvider clock)
{
    public async Task<SessionDto> HandleAsync(Guid currentUserId, Guid sessionId, SetSessionSummaryRequest request, CancellationToken cancellationToken = default)
    {
        var loaded = await loader.LoadForDmAsync(sessionId, currentUserId, "Solo un DM puede escribir el resumen de la sesión.", cancellationToken);
        loaded.Session.SetSummary(request.SummaryMarkdown, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.SessionUpdatedAsync(loaded.Session, clock.UtcNow, cancellationToken);
        return await views.BuildOneAsync(loaded.Session, currentUserId, cancellationToken);
    }
}

/// <summary>
/// Summaries of the campaign in chronological order (oldest first). Cancelled sessions are only
/// visible to DMs.
/// </summary>
public sealed class GetJournalHandler(ICampaignAccess access, ISessionRepository sessions, ICampaignRepository campaigns)
{
    public async Task<PagedResult<SessionSummaryDto>> HandleAsync(Guid currentUserId, Guid campaignId, JournalQuery query, CancellationToken cancellationToken = default)
    {
        var role = await access.RequireAsync(campaignId, currentUserId, CampaignRole.Player, cancellationToken);
        var campaign = await campaigns.GetByIdAsync(campaignId, cancellationToken) ?? throw CampaignErrors.CampaignNotFound();

        var page = query.Page ?? 1;
        var pageSize = query.PageSize ?? SessionRules.DefaultPageSize;
        var all = await sessions.ListWithSummaryAsync(campaignId, includeCancelled: role.IsAtLeast(CampaignRole.DM), cancellationToken);

        var items = all
            .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue))
            .Take(pageSize)
            .Select(s => new SessionSummaryDto(
                s.Id,
                s.Number,
                s.Title,
                s.StartsAt,
                CampaignSchedule.ToLocalIso(s.StartsAt, campaign.TimeZoneId),
                s.Status.ToString(),
                s.SummaryMarkdown!,
                s.SummaryUpdatedAt))
            .ToList();
        return new PagedResult<SessionSummaryDto>(items, all.Count, page, pageSize);
    }
}

/// <summary>Scheduled sessions that have not finished, in all the campaigns of the current user.</summary>
public sealed class ListMySessionsHandler(ICampaignRepository campaigns, ISessionRepository sessions, SessionViewBuilder views, IDateTimeProvider clock)
{
    public async Task<IReadOnlyList<SessionDto>> HandleAsync(Guid currentUserId, MySessionsQuery query, CancellationToken cancellationToken = default)
    {
        var campaignIds = await campaigns.ListCampaignIdsOfUserAsync(currentUserId, cancellationToken);
        if (campaignIds.Count == 0)
        {
            return [];
        }

        var now = clock.UtcNow;
        var all = await sessions.ListByCampaignsAsync(campaignIds, cancellationToken);
        var filtered = all.Where(s =>
            s.Status == SessionStatus.Scheduled
            && (query.From is { } from ? s.StartsAt >= from : s.IsUpcomingOrInProgress(now))
            && (query.To is not { } to || s.StartsAt < to)).ToList();
        return await views.BuildAsync(filtered, currentUserId, cancellationToken);
    }
}

/// <summary>
/// Immediate email of a DM to the members of the campaign who have notifications enabled. Fails only
/// when every delivery failed.
/// </summary>
public sealed class NotifySessionHandler(
    SessionLoader loader,
    ICampaignRepository campaigns,
    ISessionEmailService emails,
    ILogger<NotifySessionHandler> logger)
{
    public async Task HandleAsync(Guid currentUserId, Guid sessionId, NotifySessionRequest request, CancellationToken cancellationToken = default)
    {
        var loaded = await loader.LoadForDmAsync(sessionId, currentUserId, "Solo un DM puede enviar avisos.", cancellationToken);
        var context = SessionEmails.ContextOf(loaded.Session, loaded.Campaign.Name, loaded.Campaign.TimeZoneId);

        var recipients = (await campaigns.ListMemberContactsAsync([loaded.Campaign.Id], cancellationToken))
            .Where(m => m.WantsEmails)
            .ToList();
        var result = await EmailFanOut.SendAsync(
            recipients,
            m => emails.SendNoticeAsync(context, SessionEmails.RecipientOf(m), request.Subject.Trim(), request.Message, cancellationToken),
            logger,
            cancellationToken);

        if (result.AllFailed)
        {
            throw result.Failures[0];
        }
    }
}

public static class SessionEmails
{
    public static SessionEmailContext ContextOf(GameSession session, string campaignName, string timeZoneId) => new(
        session.Id,
        session.Number,
        campaignName,
        session.Title,
        session.StartsAt,
        timeZoneId,
        session.DurationMinutes,
        session.Location,
        session.Notes);

    public static EmailRecipient RecipientOf(MemberContact member) => new(member.UserId, member.Email, member.DisplayName);
}
