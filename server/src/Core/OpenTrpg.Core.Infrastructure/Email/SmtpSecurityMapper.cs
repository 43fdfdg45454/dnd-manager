using MailKit.Security;

namespace OpenTrpg.Core.Infrastructure.Email;

public static class SmtpSecurityMapper
{
    /// <summary>
    /// Maps the configured security to MailKit's <see cref="SecureSocketOptions"/>. <c>Auto</c> stays
    /// <see cref="SecureSocketOptions.Auto"/> (MailKit: 465 is implicit TLS, otherwise STARTTLS when announced),
    /// unless the legacy <paramref name="useStartTls"/> flag asks for STARTTLS.
    /// </summary>
    public static SecureSocketOptions ToSecureSocketOptions(SmtpSecurity security, bool useStartTls = false) => security switch
    {
        SmtpSecurity.None => SecureSocketOptions.None,
        SmtpSecurity.StartTls => SecureSocketOptions.StartTls,
        SmtpSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
        _ => useStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto,
    };
}
