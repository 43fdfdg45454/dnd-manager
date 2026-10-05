using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Dnd.Application.Abstractions;

namespace Dnd.Api.Tests;

/// <summary>Captures sent emails instead of talking to SMTP.</summary>
public sealed partial class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _messages = new();

    public IReadOnlyList<EmailMessage> Messages => _messages.ToList();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _messages.Enqueue(message);
        return Task.CompletedTask;
    }

    public IReadOnlyList<EmailMessage> SentTo(string email) =>
        _messages.Where(m => string.Equals(m.To, email, StringComparison.OrdinalIgnoreCase)).ToList();

    public EmailMessage LastSentTo(string email) =>
        SentTo(email).LastOrDefault() ?? throw new InvalidOperationException($"No email was sent to {email}.");

    public string LastTokenSentTo(string email) => ExtractToken(LastSentTo(email));

    public static string ExtractLink(EmailMessage message)
    {
        var match = LinkRegex().Match(message.TextBody ?? message.HtmlBody);
        return match.Success ? match.Value : throw new InvalidOperationException("The email does not contain a set-password link.");
    }

    public static string ExtractToken(EmailMessage message)
    {
        var match = LinkRegex().Match(message.TextBody ?? message.HtmlBody);
        return match.Success
            ? Uri.UnescapeDataString(match.Groups["token"].Value)
            : throw new InvalidOperationException("The email does not contain a set-password link.");
    }

    [GeneratedRegex(@"https?://\S+/set-password\?token=(?<token>[A-Za-z0-9_\-%]+)")]
    private static partial Regex LinkRegex();
}
