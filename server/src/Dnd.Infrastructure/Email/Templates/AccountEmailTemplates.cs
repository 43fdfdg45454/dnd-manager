using System.Net;
using Dnd.Application.Abstractions;

namespace Dnd.Infrastructure.Email.Templates;

/// <summary>Spanish account emails (plain text + simple HTML).</summary>
public static class AccountEmailTemplates
{
    public const string SetupSubject = "Tu cuenta en D&D Companion";
    public const string ResetSubject = "Restablecer contraseña";

    public static EmailMessage Setup(string to, string displayName, string link) => Build(
        to,
        SetupSubject,
        displayName,
        intro: "Se ha creado una cuenta para ti en D&D Companion. Para empezar, establece tu contraseña desde este enlace:",
        buttonText: "Establecer contraseña",
        link,
        expiry: "El enlace caduca en 48 horas. Si caduca, pide al administrador que te lo reenvíe.");

    public static EmailMessage Reset(string to, string displayName, string link) => Build(
        to,
        ResetSubject,
        displayName,
        intro: "Hemos recibido una solicitud para restablecer tu contraseña de D&D Companion. Elige una nueva desde este enlace:",
        buttonText: "Restablecer contraseña",
        link,
        expiry: "El enlace caduca en 1 hora. Si no has sido tú, ignora este correo: tu contraseña no cambiará.");

    private static EmailMessage Build(string to, string subject, string displayName, string intro, string buttonText, string link, string expiry)
    {
        var text = $"""
            Hola, {displayName}:

            {intro}

            {link}

            {expiry}

            — D&D Companion
            """;

        var name = WebUtility.HtmlEncode(displayName);
        var href = WebUtility.HtmlEncode(link);
        var html = $"""
            <!DOCTYPE html>
            <html lang="es">
            <body style="font-family: Arial, Helvetica, sans-serif; color: #222; line-height: 1.5;">
              <p>Hola, {name}:</p>
              <p>{WebUtility.HtmlEncode(intro)}</p>
              <p><a href="{href}" style="display: inline-block; padding: 10px 18px; background: #8b1e1e; color: #fff; text-decoration: none; border-radius: 4px;">{WebUtility.HtmlEncode(buttonText)}</a></p>
              <p style="font-size: 13px; color: #555;">Si el botón no funciona, copia este enlace en el navegador:<br><a href="{href}">{href}</a></p>
              <p style="font-size: 13px; color: #555;">{WebUtility.HtmlEncode(expiry)}</p>
              <p>— D&amp;D Companion</p>
            </body>
            </html>
            """;

        return new EmailMessage(to, subject, html, text);
    }
}
