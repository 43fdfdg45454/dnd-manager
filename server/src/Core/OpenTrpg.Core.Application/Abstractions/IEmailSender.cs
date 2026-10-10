namespace OpenTrpg.Core.Application.Abstractions;

public sealed record EmailMessage(string To, string Subject, string HtmlBody, string? TextBody = null);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
