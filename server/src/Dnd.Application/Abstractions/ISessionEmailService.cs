using Dnd.Domain.Sessions;

namespace Dnd.Application.Abstractions;

/// <summary>Session facts that go into an email, with the campaign's time zone to render dates.</summary>
public sealed record SessionEmailContext(
    Guid SessionId,
    int Number,
    string CampaignName,
    string Title,
    DateTimeOffset StartsAt,
    string TimeZoneId,
    int? DurationMinutes,
    string? Location,
    string? Notes);

public sealed record EmailRecipient(Guid UserId, string Email, string DisplayName);

/// <summary>Builds (Spanish templates, signed links) and sends the session emails.</summary>
public interface ISessionEmailService
{
    /// <param name="rsvp">Current answer of the recipient, or null when pending.</param>
    Task SendReminderAsync(SessionEmailContext session, EmailRecipient recipient, RsvpStatus? rsvp, CancellationToken cancellationToken = default);

    Task SendNoticeAsync(SessionEmailContext session, EmailRecipient recipient, string subject, string message, CancellationToken cancellationToken = default);
}
