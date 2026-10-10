using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Campaigns;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Campaigns;
using OpenTrpg.Core.Domain.Sessions;
using Microsoft.Extensions.Logging;

namespace OpenTrpg.Core.Application.Sessions;

public static class SessionErrors
{
    /// <summary>Also used for users who are not members of the campaign, so its existence is not revealed.</summary>
    public static AppException SessionNotFound() => AppException.NotFound("Sesión no encontrada.");

    public static AppException InvalidLink() => AppException.Unauthorized("El enlace no es válido o ha caducado.");
}

/// <summary>A session loaded for a use case, with its campaign and the role of the acting user in it.</summary>
public sealed record LoadedSession(GameSession Session, Campaign Campaign, CampaignRole Role)
{
    public bool IsDm => Role.IsAtLeast(CampaignRole.DM);
}

/// <summary>Loads sessions (tracked, with RSVPs and reminders) and checks the access of the actor.</summary>
public sealed class SessionLoader(ISessionRepository sessions, ICampaignRepository campaigns, ICampaignAccess access)
{
    /// <summary>404 when the session does not exist or the actor is not a member of its campaign.</summary>
    public async Task<LoadedSession> LoadAsync(Guid sessionId, Guid actorUserId, CancellationToken cancellationToken)
    {
        var session = await sessions.GetWithDetailsAsync(sessionId, cancellationToken) ?? throw SessionErrors.SessionNotFound();
        var role = await access.GetRoleAsync(session.CampaignId, actorUserId, cancellationToken) ?? throw SessionErrors.SessionNotFound();
        var campaign = await campaigns.GetByIdAsync(session.CampaignId, cancellationToken) ?? throw SessionErrors.SessionNotFound();
        return new LoadedSession(session, campaign, role);
    }

    /// <summary>Like <see cref="LoadAsync"/> but only for DMs (403 for players).</summary>
    public async Task<LoadedSession> LoadForDmAsync(Guid sessionId, Guid actorUserId, string forbiddenMessage, CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(sessionId, actorUserId, cancellationToken);
        return loaded.IsDm ? loaded : throw AppException.Forbidden(forbiddenMessage);
    }
}

/// <summary>Builds session DTOs for the current user: local time, RSVPs, counts and (DMs) reminders.</summary>
public sealed class SessionViewBuilder(ICampaignRepository campaigns)
{
    public async Task<IReadOnlyList<SessionDto>> BuildAsync(IReadOnlyCollection<GameSession> sessions, Guid currentUserId, CancellationToken cancellationToken)
    {
        if (sessions.Count == 0)
        {
            return [];
        }

        var campaignIds = sessions.Select(s => s.CampaignId).Distinct().ToList();
        var infos = (await campaigns.ListScheduleInfoAsync(campaignIds, cancellationToken)).ToDictionary(c => c.Id);
        var members = (await campaigns.ListMemberContactsAsync(campaignIds, cancellationToken))
            .GroupBy(m => m.CampaignId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<SessionDto>(sessions.Count);
        foreach (var session in sessions)
        {
            if (!infos.TryGetValue(session.CampaignId, out var info))
            {
                continue;
            }

            var campaignMembers = members.GetValueOrDefault(session.CampaignId) ?? [];
            result.Add(Build(session, info, campaignMembers, currentUserId));
        }

        return result;
    }

    private static SessionDto Build(GameSession session, CampaignScheduleInfo info, IReadOnlyList<MemberContact> members, Guid currentUserId)
    {
        var names = members.ToDictionary(m => m.UserId, m => m.DisplayName);
        var isDm = members.FirstOrDefault(m => m.UserId == currentUserId)?.Role.IsAtLeast(CampaignRole.DM) == true;

        // Answers of people who left the campaign are ignored.
        var answers = session.Rsvps.Where(r => names.ContainsKey(r.UserId)).ToList();
        var rsvps = answers
            .OrderBy(r => names[r.UserId], StringComparer.InvariantCultureIgnoreCase)
            .ThenBy(r => r.UserId)
            .Select(r => new RsvpDto(r.UserId, names[r.UserId], r.Status.ToString(), r.Comment))
            .ToList();
        var counts = new RsvpCountsDto(
            answers.Count(r => r.Status == RsvpStatus.Yes),
            answers.Count(r => r.Status == RsvpStatus.No),
            answers.Count(r => r.Status == RsvpStatus.Maybe),
            Math.Max(0, members.Count - answers.Count));

        var reminders = isDm
            ? session.Reminders
                .OrderBy(r => r.SendAt)
                .Select(r => new ReminderDto(r.OffsetMinutes, r.SendAt, r.SentAt, r.FailedAt))
                .ToList()
            : null;

        return new SessionDto(
            session.Id,
            session.Number,
            session.CampaignId,
            info.Name,
            session.Title,
            session.StartsAt,
            CampaignSchedule.ToLocalIso(session.StartsAt, info.TimeZoneId),
            info.TimeZoneId,
            session.DurationMinutes,
            session.Location,
            session.Notes,
            session.SummaryMarkdown,
            session.SummaryUpdatedAt,
            session.Status.ToString(),
            session.FindRsvp(currentUserId) is { } mine && names.ContainsKey(currentUserId) ? mine.Status.ToString() : null,
            rsvps,
            counts,
            reminders);
    }

    public async Task<SessionDto> BuildOneAsync(GameSession session, Guid currentUserId, CancellationToken cancellationToken) =>
        (await BuildAsync([session], currentUserId, cancellationToken)).Single();
}

/// <summary>Result of sending one email to each recipient: failures do not stop the others.</summary>
public sealed record FanOutResult(int Delivered, IReadOnlyList<Exception> Failures)
{
    public bool AllFailed => Delivered == 0 && Failures.Count > 0;
}

public static class EmailFanOut
{
    /// <summary>Calls <paramref name="send"/> for every recipient, collecting failures (cancellation is rethrown).</summary>
    public static async Task<FanOutResult> SendAsync<T>(IEnumerable<T> recipients, Func<T, Task> send, ILogger logger, CancellationToken cancellationToken)
    {
        var delivered = 0;
        var failures = new List<Exception>();
        foreach (var recipient in recipients)
        {
            try
            {
                await send(recipient);
                delivered++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Could not send a session email to one recipient");
                failures.Add(ex);
            }
        }

        return new FanOutResult(delivered, failures);
    }
}
