using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Domain.Sessions;
using Microsoft.Extensions.Logging;

namespace OpenTrpg.Core.Application.Sessions;

/// <summary>
/// Sends the due session reminders. Called periodically by the <c>ReminderDispatcher</c> and
/// directly by tests. A reminder is due when it is pending, its send time has passed and, after a
/// failure, five minutes have elapsed since the last attempt. Each reminder is emailed to every
/// member of the campaign with notifications enabled and then marked as sent. If the sending throws,
/// the failure is recorded (<see cref="Reminder.Attempts"/>, <see cref="Reminder.LastError"/>) and the
/// reminder is abandoned (<see cref="Reminder.FailedAt"/>) at the third failure.
/// </summary>
/// <remarks>
/// Failures are per reminder, not per recipient: when only some deliveries fail the reminder counts as
/// sent (the failed addresses are logged and kept in <see cref="Reminder.LastError"/>), because retrying
/// would email everyone else again. It is a failure only when no recipient received it.
/// </remarks>
public sealed class ReminderProcessor(
    ISessionRepository sessions,
    ICampaignRepository campaigns,
    ISessionEmailService emails,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    ILogger<ReminderProcessor> logger)
{
    /// <summary>Processes the reminders due now.</summary>
    public Task<int> ProcessDueAsync(CancellationToken cancellationToken = default) => ProcessDueAsync(clock.UtcNow, cancellationToken);

    /// <summary>Processes the reminders due at <paramref name="now"/> and returns how many were examined.</summary>
    public async Task<int> ProcessDueAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var due = await sessions.ListDueRemindersAsync(now, cancellationToken);
        if (due.Count == 0)
        {
            return 0;
        }

        var members = (await campaigns.ListMemberContactsAsync(due.Select(d => d.CampaignId).Distinct().ToList(), cancellationToken))
            .GroupBy(m => m.CampaignId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var item in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessOneAsync(item, members.GetValueOrDefault(item.CampaignId) ?? [], now, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return due.Count;
    }

    private async Task ProcessOneAsync(DueReminder item, IReadOnlyList<MemberContact> members, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var reminder = item.Reminder;
        var session = item.Session;
        if (session.Status != SessionStatus.Scheduled)
        {
            reminder.MarkSent(now, "Omitido: la sesión ya no está programada.");
            return;
        }

        var context = SessionEmails.ContextOf(session, item.CampaignName, item.TimeZoneId);
        var recipients = members.Where(m => m.WantsEmails).ToList();
        var result = await EmailFanOut.SendAsync(
            recipients,
            m => emails.SendReminderAsync(context, SessionEmails.RecipientOf(m), session.FindRsvp(m.UserId)?.Status, cancellationToken),
            logger,
            cancellationToken);

        if (result.AllFailed)
        {
            var error = result.Failures[0].Message;
            reminder.RegisterFailure(now, error);
            logger.LogWarning(
                "Reminder {ReminderId} of session {SessionId} failed (attempt {Attempts}): {Error}",
                reminder.Id,
                session.Id,
                reminder.Attempts,
                error);
            return;
        }

        reminder.MarkSent(
            now,
            result.Failures.Count > 0 ? $"{result.Failures.Count} de {recipients.Count} envíos fallaron: {result.Failures[0].Message}" : null);
        logger.LogInformation("Reminder {ReminderId} of session {SessionId} sent to {Count} recipient(s)", reminder.Id, session.Id, result.Delivered);
    }
}
