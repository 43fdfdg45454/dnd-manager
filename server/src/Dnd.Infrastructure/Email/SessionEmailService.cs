using Dnd.Application.Abstractions;
using Dnd.Domain.Sessions;
using Dnd.Infrastructure.Email.Templates;
using Dnd.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Dnd.Infrastructure.Email;

internal sealed class SessionEmailService(IEmailSender sender, ISessionLinkTokens tokens, IOptions<AppOptions> options) : ISessionEmailService
{
    /// <summary>Link of the public session page, carrying the token of the recipient.</summary>
    public string BuildSessionLink(Guid sessionId, Guid userId) =>
        $"{options.Value.PublicUrl.TrimEnd('/')}/sessions/{sessionId}?token={Uri.EscapeDataString(tokens.Create(sessionId, userId))}";

    public Task SendReminderAsync(SessionEmailContext session, EmailRecipient recipient, RsvpStatus? rsvp, CancellationToken cancellationToken = default) =>
        sender.SendAsync(
            SessionEmailTemplates.Reminder(session, recipient, rsvp, BuildSessionLink(session.SessionId, recipient.UserId)),
            cancellationToken);

    public Task SendNoticeAsync(SessionEmailContext session, EmailRecipient recipient, string subject, string message, CancellationToken cancellationToken = default) =>
        sender.SendAsync(
            SessionEmailTemplates.Notice(session, recipient, subject, message, BuildSessionLink(session.SessionId, recipient.UserId)),
            cancellationToken);
}
