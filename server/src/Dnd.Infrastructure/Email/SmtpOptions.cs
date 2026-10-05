namespace Dnd.Infrastructure.Email;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 1025;

    /// <summary>Use STARTTLS (true for most providers on port 587; false for local MailHog).</summary>
    public bool UseStartTls { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string FromAddress { get; set; } = "noreply@example.com";

    public string FromName { get; set; } = "D&D Companion";
}
