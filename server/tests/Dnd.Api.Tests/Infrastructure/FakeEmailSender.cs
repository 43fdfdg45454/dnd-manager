using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Dnd.Application.Abstractions;

namespace Dnd.Api.Tests;

/// <summary>Captures sent emails instead of talking to SMTP.</summary>
public sealed partial class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _messages = new();

    public IReadOnlyList<EmailMessage> Messages => _messages.ToList();

    /// <summary>When set, sending a message for which it returns an exception throws it instead of capturing the message.</summary>
    public Func<EmailMessage, Exception?>? FailWhen { get; set; }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (FailWhen?.Invoke(message) is { } failure)
        {
            throw failure;
        }

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

    /// <summary>Link of the public session page in a session email: <c>(sessionId, token, url)</c>.</summary>
    public static (Guid SessionId, string Token, string Url) ExtractSessionLink(EmailMessage message)
    {
        var match = SessionLinkRegex().Match(message.TextBody ?? message.HtmlBody);
        return match.Success
            ? (Guid.Parse(match.Groups["id"].Value), Uri.UnescapeDataString(match.Groups["token"].Value), match.Value)
            : throw new InvalidOperationException("The email does not contain a session link.");
    }

    [GeneratedRegex(@"(?:https?://\S+)?/sessions/(?<id>[0-9a-fA-F\-]{36})\?token=(?<token>[A-Za-z0-9_\-.%]+)")]
    private static partial Regex SessionLinkRegex();

    [GeneratedRegex(@"(?:https?://\S+)?/set-password\?token=(?<token>[A-Za-z0-9_\-%]+)")]
    private static partial Regex LinkRegex();
}
