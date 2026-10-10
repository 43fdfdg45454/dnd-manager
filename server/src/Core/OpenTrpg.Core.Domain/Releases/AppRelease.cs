using System.Text.RegularExpressions;
using OpenTrpg.Core.Domain.Common;

namespace OpenTrpg.Core.Domain.Releases;

/// <summary>
/// A published build of the Android app: its semantic version, the monotonically increasing build
/// number the app compares against, and the APK (a stored file of kind <c>AppRelease</c>).
/// </summary>
public sealed partial class AppRelease : EntityBase
{
    public const int VersionMaxLength = 32;
    public const int NotesMaxLength = 4000;

    private AppRelease()
    {
    }

    /// <summary>Semantic version <c>major.minor.patch</c>; unique.</summary>
    public string Version { get; private set; } = string.Empty;

    /// <summary>Positive integer that grows with every build; unique. The app compares it with its own.</summary>
    public int BuildNumber { get; private set; }

    public Guid FileId { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>When true the app blocks until the user installs this release.</summary>
    public bool IsMandatory { get; private set; }

    public DateTimeOffset PublishedAt { get; private set; }

    public static bool IsValidVersion(string? version) =>
        version is not null && version.Length <= VersionMaxLength && SemanticVersionPattern().IsMatch(version);

    public static AppRelease Create(string version, int buildNumber, Guid fileId, string? notes, bool isMandatory, DateTimeOffset now)
    {
        var trimmedVersion = (version ?? string.Empty).Trim();
        if (!IsValidVersion(trimmedVersion))
        {
            throw DomainException.RuleViolation("La versión debe tener el formato mayor.menor.parche, por ejemplo 1.2.0.");
        }

        if (buildNumber < 1)
        {
            throw DomainException.RuleViolation("El número de compilación debe ser un entero positivo.");
        }

        var trimmedNotes = notes?.Trim();
        if (trimmedNotes is { Length: > NotesMaxLength })
        {
            throw DomainException.RuleViolation($"Las notas no pueden superar los {NotesMaxLength} caracteres.");
        }

        return new AppRelease
        {
            Version = trimmedVersion,
            BuildNumber = buildNumber,
            FileId = fileId,
            Notes = string.IsNullOrEmpty(trimmedNotes) ? null : trimmedNotes,
            IsMandatory = isMandatory,
            PublishedAt = now,
            CreatedAt = now,
        };
    }

    // ASCII digits only (\d would also match other Unicode digits); \z so a trailing newline is not accepted.
    [GeneratedRegex(@"^[0-9]+\.[0-9]+\.[0-9]+\z")]
    private static partial Regex SemanticVersionPattern();
}
