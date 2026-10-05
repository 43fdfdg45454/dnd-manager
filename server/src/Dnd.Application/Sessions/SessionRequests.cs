using Dnd.Application.Common;
using Dnd.Domain.Sessions;
using FluentValidation;

namespace Dnd.Application.Sessions;

public static class SessionRules
{
    public const int NoticeSubjectMaxLength = 150;
    public const int NoticeMessageMaxLength = 5_000;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public const string TitleMessage = "El título debe tener entre 1 y 200 caracteres.";
    public const string StatusMessage = "El estado debe ser \"Scheduled\", \"Cancelled\" o \"Done\".";
    public const string RsvpMessage = "La respuesta debe ser \"Yes\", \"No\" o \"Maybe\".";
    public const string TimeZoneMessage = "La zona horaria no es válida. Usa un identificador IANA, por ejemplo \"Europe/Madrid\".";

    public static bool IsValidTitle(string? title) => title?.Trim() is { Length: > 0 and <= GameSession.TitleMaxLength };

    public static bool IsValidDuration(int? minutes) => minutes is null or (>= 1 and <= GameSession.MaxDurationMinutes);
}

public sealed record CreateSessionRequest(string Title, DateTimeOffset StartsAt, int? DurationMinutes = null, string? Location = null, string? Notes = null);

public sealed class CreateSessionRequestValidator : AbstractValidator<CreateSessionRequest>
{
    public CreateSessionRequestValidator()
    {
        RuleFor(x => x.Title).Must(SessionRules.IsValidTitle).WithMessage(SessionRules.TitleMessage);
        RuleFor(x => x.StartsAt).NotEmpty().WithMessage("Indica la fecha y hora de la sesión.");
        RuleFor(x => x.DurationMinutes).Must(SessionRules.IsValidDuration)
            .WithMessage($"La duración debe estar entre 1 y {GameSession.MaxDurationMinutes} minutos.");
        RuleFor(x => x.Location).MaximumLength(GameSession.LocationMaxLength)
            .WithMessage($"El lugar no puede superar los {GameSession.LocationMaxLength} caracteres.");
        RuleFor(x => x.Notes).MaximumLength(GameSession.NotesMaxLength)
            .WithMessage($"Las notas no pueden superar los {GameSession.NotesMaxLength} caracteres.");
    }
}

/// <summary>
/// Absent fields do not change. <c>durationMinutes</c>, <c>location</c> and <c>notes</c> accept an
/// explicit <c>null</c> to clear them. Changing <c>startsAt</c> or <c>status</c> regenerates the reminders.
/// </summary>
public sealed record UpdateSessionRequest
{
    public string? Title { get; init; }

    public DateTimeOffset? StartsAt { get; init; }

    public Optional<int?> DurationMinutes { get; init; }

    public Optional<string?> Location { get; init; }

    public Optional<string?> Notes { get; init; }

    /// <summary>Name of <see cref="SessionStatus"/>.</summary>
    public string? Status { get; init; }
}

public sealed class UpdateSessionRequestValidator : AbstractValidator<UpdateSessionRequest>
{
    public UpdateSessionRequestValidator()
    {
        RuleFor(x => x.Title).Must(SessionRules.IsValidTitle).When(x => x.Title is not null).WithMessage(SessionRules.TitleMessage);
        RuleFor(x => x.StartsAt).NotEqual(default(DateTimeOffset)).When(x => x.StartsAt is not null)
            .WithMessage("La fecha y hora de la sesión no es válida.");
        RuleFor(x => x.DurationMinutes.Value).Must(SessionRules.IsValidDuration).When(x => x.DurationMinutes.IsSet)
            .OverridePropertyName("DurationMinutes")
            .WithMessage($"La duración debe estar entre 1 y {GameSession.MaxDurationMinutes} minutos.");
        RuleFor(x => x.Location.Value).MaximumLength(GameSession.LocationMaxLength).When(x => x.Location.IsSet)
            .OverridePropertyName("Location")
            .WithMessage($"El lugar no puede superar los {GameSession.LocationMaxLength} caracteres.");
        RuleFor(x => x.Notes.Value).MaximumLength(GameSession.NotesMaxLength).When(x => x.Notes.IsSet)
            .OverridePropertyName("Notes")
            .WithMessage($"Las notas no pueden superar los {GameSession.NotesMaxLength} caracteres.");
        RuleFor(x => x.Status).Must(EnumNames.IsValid<SessionStatus>).When(x => x.Status is not null)
            .WithMessage(SessionRules.StatusMessage);
    }
}

/// <summary>An empty (or blank) text removes the summary.</summary>
public sealed record SetSessionSummaryRequest(string? SummaryMarkdown);

public sealed class SetSessionSummaryRequestValidator : AbstractValidator<SetSessionSummaryRequest>
{
    public SetSessionSummaryRequestValidator()
    {
        RuleFor(x => x.SummaryMarkdown).MaximumLength(GameSession.SummaryMaxLength)
            .WithMessage($"El resumen no puede superar los {GameSession.SummaryMaxLength} caracteres.");
    }
}

/// <param name="Status">Name of <see cref="RsvpStatus"/>.</param>
public sealed record RsvpRequest(string Status, string? Comment = null);

public sealed class RsvpRequestValidator : AbstractValidator<RsvpRequest>
{
    public RsvpRequestValidator()
    {
        RuleFor(x => x.Status).Must(EnumNames.IsValid<RsvpStatus>).WithMessage(SessionRules.RsvpMessage);
        RuleFor(x => x.Comment).MaximumLength(SessionRsvp.CommentMaxLength)
            .WithMessage($"El comentario no puede superar los {SessionRsvp.CommentMaxLength} caracteres.");
    }
}

public sealed record NotifySessionRequest(string Subject, string Message);

public sealed class NotifySessionRequestValidator : AbstractValidator<NotifySessionRequest>
{
    public NotifySessionRequestValidator()
    {
        RuleFor(x => x.Subject).NotEmpty().WithMessage("Indica el asunto del aviso.")
            .MaximumLength(SessionRules.NoticeSubjectMaxLength)
            .WithMessage($"El asunto no puede superar los {SessionRules.NoticeSubjectMaxLength} caracteres.")
            .Must(s => s is null || !s.Contains('\r') && !s.Contains('\n')).WithMessage("El asunto debe ser de una sola línea.");
        RuleFor(x => x.Message).NotEmpty().WithMessage("Escribe el mensaje del aviso.")
            .MaximumLength(SessionRules.NoticeMessageMaxLength)
            .WithMessage($"El mensaje no puede superar los {SessionRules.NoticeMessageMaxLength} caracteres.");
    }
}

/// <summary>Query string of <c>GET /campaigns/{id}/sessions</c>. <c>To</c> is exclusive.</summary>
/// <param name="IncludePast">Also return sessions that already took place. Ignored when <paramref name="From"/> is given.</param>
public sealed record ListSessionsQuery(DateTimeOffset? From, DateTimeOffset? To, bool? IncludePast);

public sealed class ListSessionsQueryValidator : AbstractValidator<ListSessionsQuery>
{
    public ListSessionsQueryValidator()
    {
        RuleFor(x => x.To).GreaterThan(x => x.From!.Value).When(x => x.From is not null && x.To is not null)
            .WithMessage("\"to\" debe ser posterior a \"from\".");
    }
}

/// <summary>Query string of <c>GET /me/sessions</c>. <c>To</c> is exclusive.</summary>
public sealed record MySessionsQuery(DateTimeOffset? From, DateTimeOffset? To);

public sealed class MySessionsQueryValidator : AbstractValidator<MySessionsQuery>
{
    public MySessionsQueryValidator()
    {
        RuleFor(x => x.To).GreaterThan(x => x.From!.Value).When(x => x.From is not null && x.To is not null)
            .WithMessage("\"to\" debe ser posterior a \"from\".");
    }
}

public sealed record JournalQuery(int? Page, int? PageSize);

public sealed class JournalQueryValidator : AbstractValidator<JournalQuery>
{
    public JournalQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("La página debe ser 1 o mayor.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, SessionRules.MaxPageSize)
            .WithMessage($"El tamaño de página debe estar entre 1 y {SessionRules.MaxPageSize}.");
    }
}

/// <summary>Body of the public RSVP endpoint (the link token authorizes it).</summary>
public sealed record PublicRsvpRequest(string Status);

public sealed class PublicRsvpRequestValidator : AbstractValidator<PublicRsvpRequest>
{
    public PublicRsvpRequestValidator()
    {
        RuleFor(x => x.Status).Must(EnumNames.IsValid<RsvpStatus>).WithMessage(SessionRules.RsvpMessage);
    }
}
