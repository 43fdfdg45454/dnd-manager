namespace Dnd.Infrastructure.Email;

/// <summary>How the SMTP connection is secured (<c>Smtp:Security</c>).</summary>
public enum SmtpSecurity
{
    /// <summary>MailKit decides from the port: 465 uses implicit TLS, otherwise STARTTLS when the server offers it.</summary>
    Auto,

    /// <summary>No encryption (local test servers such as MailHog only).</summary>
    None,

    /// <summary>Upgrade the connection with STARTTLS (typically port 587); fails if the server does not offer it.</summary>
    StartTls,

    /// <summary>TLS from the first byte, "implicit TLS" (typically port 465).</summary>
    SslOnConnect,
}

/// <remarks>
/// Server certificate validation is never turned off. A server whose certificate is signed by a private CA
/// is trusted by installing that CA for the process, not by skipping the check: .NET on Linux honours the
/// standard OpenSSL variables <c>SSL_CERT_FILE</c> (a PEM bundle) and <c>SSL_CERT_DIR</c> (a hashed
/// directory), so mount the CA certificate in the container and point one of them to it.
/// </remarks>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 1025;

    /// <summary>Connection security. <see cref="SmtpSecurity.Auto"/> by default: port 465 is implicit TLS, 587 is STARTTLS.</summary>
    public SmtpSecurity Security { get; set; } = SmtpSecurity.Auto;

    /// <summary>
    /// Legacy switch, still accepted: when true and <see cref="Security"/> is <see cref="SmtpSecurity.Auto"/>
    /// it behaves as <see cref="SmtpSecurity.StartTls"/>. Prefer <see cref="Security"/>.
    /// </summary>
    public bool UseStartTls { get; set; }

    /// <summary>Check whether the server certificate was revoked (CRL/OCSP). Disable only if the CA publishes no revocation data.</summary>
    public bool CheckCertificateRevocation { get; set; } = true;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string FromAddress { get; set; } = "noreply@example.com";

    public string FromName { get; set; } = "D&D Companion";
}
