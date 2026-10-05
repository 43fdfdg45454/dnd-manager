using Dnd.Application.Abstractions;
using Dnd.Domain.Sessions;
using Dnd.Infrastructure.Email.Templates;

namespace Dnd.Infrastructure.Email;

internal sealed class SessionEmailService(IEmailSender sender, ISessionLinkTokens tokens, PublicLinkBuilder links) : ISessionEmailService
{
    /// <summary>Link of the public session page, carrying the token of the recipient.</summary>
    private Task<string> BuildSessionLinkAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken) =>
        links.BuildAsync($"/sessions/{sessionId}?token={Uri.EscapeDataString(tokens.Create(sessionId, userId))}", cancellationToken);

    public async Task SendReminderAsync(SessionEmailContext session, EmailRecipient recipient, RsvpStatus? rsvp, CancellationToken cancellationToken = default) =>
        await sender.SendAsync(
            SessionEmailTemplates.Reminder(session, recipient, rsvp, await BuildSessionLinkAsync(session.SessionId, recipient.UserId, cancellationToken)),
            cancellationToken);

    public async Task SendNoticeAsync(SessionEmailContext session, EmailRecipient recipient, string subject, string message, CancellationToken cancellationToken = default) =>
        await sender.SendAsync(
            SessionEmailTemplates.Notice(session, recipient, subject, message, await BuildSessionLinkAsync(session.SessionId, recipient.UserId, cancellationToken)),
            cancellationToken);
}
