using System.Net;
using System.Text;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Domain.Sessions;

namespace OpenTrpg.Core.Infrastructure.Email.Templates;

/// <summary>Spanish session emails (plain text + simple HTML): reminders and DM notices.</summary>
public static class SessionEmailTemplates
{
    private static readonly string[] DayNames = ["domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado"];

    private static readonly string[] MonthNames =
        ["enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"];

    /// <summary>Date and time in the campaign's zone, for example "sábado 10 de octubre de 2026, 20:00".</summary>
    public static string FormatLocal(DateTimeOffset instant, string timeZoneId)
    {
        var local = CampaignSchedule.ToLocal(instant, timeZoneId);
        return $"{DayNames[(int)local.DayOfWeek]} {local.Day} de {MonthNames[local.Month - 1]} de {local.Year}, {local:HH:mm}";
    }

    public static string RsvpText(RsvpStatus? status) => status switch
    {
        RsvpStatus.Yes => "Vas a asistir",
        RsvpStatus.No => "No vas a asistir",
        RsvpStatus.Maybe => "Quizá asistas",
        _ => "Sin responder todavía",
    };

    public static string FormatDuration(int minutes) => minutes switch
    {
        < 60 => $"{minutes} min",
        _ when minutes % 60 == 0 => $"{minutes / 60} h",
        _ => $"{minutes / 60} h {minutes % 60} min",
    };

    public static string ReminderSubject(SessionEmailContext session) =>
        OneLine($"Recordatorio: {session.Title} — {FormatLocal(session.StartsAt, session.TimeZoneId)}");

    public static string NoticeSubject(SessionEmailContext session, string subject) =>
        OneLine($"[{session.CampaignName}] {subject}");

    public static EmailMessage Reminder(SessionEmailContext session, EmailRecipient recipient, RsvpStatus? rsvp, string link)
    {
        var details = Details(session, rsvp);
        var intro = $"Te recordamos la próxima sesión de «{session.CampaignName}»:";
        return Build(recipient, ReminderSubject(session), intro, details, session.Notes, "Ver la sesión y responder asistencia", link);
    }

    public static EmailMessage Notice(SessionEmailContext session, EmailRecipient recipient, string subject, string message, string link)
    {
        var intro = $"Aviso de tu DM sobre la sesión «{session.Title}» de «{session.CampaignName}»:";
        return Build(recipient, NoticeSubject(session, subject), intro, [], message, "Ver la sesión", link);
    }

    private static List<(string Label, string Value)> Details(SessionEmailContext session, RsvpStatus? rsvp)
    {
        var details = new List<(string, string)>
        {
            ("Sesión", $"{session.Number} · {session.Title}"),
            ("Cuándo", $"{FormatLocal(session.StartsAt, session.TimeZoneId)} ({session.TimeZoneId})"),
        };
        if (session.DurationMinutes is { } minutes)
        {
            details.Add(("Duración", FormatDuration(minutes)));
        }

        if (!string.IsNullOrWhiteSpace(session.Location))
        {
            details.Add(("Dónde", session.Location));
        }

        details.Add(("Tu asistencia", RsvpText(rsvp)));
        return details;
    }

    private static EmailMessage Build(
        EmailRecipient recipient,
        string subject,
        string intro,
        IReadOnlyList<(string Label, string Value)> details,
        string? body,
        string buttonText,
        string link)
    {
        var text = new StringBuilder();
        text.AppendLine($"Hola, {recipient.DisplayName}:").AppendLine().AppendLine(intro).AppendLine();
        foreach (var (label, value) in details)
        {
            text.AppendLine($"{label}: {value}");
        }

        if (details.Count > 0)
        {
            text.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(body))
        {
            text.AppendLine(body.Trim()).AppendLine();
        }

        text.AppendLine($"{buttonText}: {link}").AppendLine().Append("— D&D Companion");

        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>")
            .AppendLine("<html lang=\"es\">")
            .AppendLine("<body style=\"font-family: Arial, Helvetica, sans-serif; color: #222; line-height: 1.5;\">")
            .AppendLine($"  <p>Hola, {WebUtility.HtmlEncode(recipient.DisplayName)}:</p>")
            .AppendLine($"  <p>{WebUtility.HtmlEncode(intro)}</p>");
        if (details.Count > 0)
        {
            html.AppendLine("  <table style=\"border-collapse: collapse;\">");
            foreach (var (label, value) in details)
            {
                html.AppendLine($"    <tr><td style=\"padding: 2px 12px 2px 0; color: #555;\">{WebUtility.HtmlEncode(label)}</td><td><strong>{WebUtility.HtmlEncode(value)}</strong></td></tr>");
            }

            html.AppendLine("  </table>");
        }

        if (!string.IsNullOrWhiteSpace(body))
        {
            html.AppendLine($"  <p style=\"white-space: pre-wrap;\">{WebUtility.HtmlEncode(body.Trim())}</p>");
        }

        var href = WebUtility.HtmlEncode(link);
        html.AppendLine($"  <p><a href=\"{href}\" style=\"display: inline-block; padding: 10px 18px; background: #8b1e1e; color: #fff; text-decoration: none; border-radius: 4px;\">{WebUtility.HtmlEncode(buttonText)}</a></p>")
            .AppendLine($"  <p style=\"font-size: 13px; color: #555;\">Si el botón no funciona, copia este enlace en el navegador:<br><a href=\"{href}\">{href}</a></p>")
            .AppendLine("  <p>— D&amp;D Companion</p>")
            .AppendLine("</body>")
            .Append("</html>");

        return new EmailMessage(recipient.Email, subject, html.ToString(), text.ToString());
    }

    private static string OneLine(string value) => value.Replace('\r', ' ').Replace('\n', ' ');
}
