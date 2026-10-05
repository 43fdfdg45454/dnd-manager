using Dnd.Domain.Common;

namespace Dnd.Domain.Library;

/// <summary>
/// A PDF of the instance's library (global, not tied to a campaign). System documents (the bundled
/// SRD) cannot be deleted.
/// </summary>
public sealed class LibraryDocument : EntityBase
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    private LibraryDocument()
    {
    }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public LibraryCategory Category { get; private set; }

    public Guid FileId { get; private set; }

    public int? PageCount { get; private set; }

    public bool IsSystem { get; private set; }

    /// <summary>Uploader; null for system documents.</summary>
    public Guid? UploadedByUserId { get; private set; }

    public static LibraryDocument Create(
        string title,
        string? description,
        LibraryCategory category,
        Guid fileId,
        bool isSystem,
        Guid? uploadedByUserId,
        DateTimeOffset now) => new()
    {
        Title = NormalizeTitle(title),
        Description = NormalizeDescription(description),
        Category = category,
        FileId = fileId,
        IsSystem = isSystem,
        UploadedByUserId = uploadedByUserId,
        CreatedAt = now,
    };

    /// <summary>Null arguments keep the current value; an empty description clears it.</summary>
    public void Update(string? title, string? description, LibraryCategory? category)
    {
        var newTitle = title is null ? Title : NormalizeTitle(title);
        var newDescription = description is null ? Description : NormalizeDescription(description);

        Title = newTitle;
        Description = newDescription;
        Category = category ?? Category;
    }

    /// <summary>System documents are never deleted (400).</summary>
    public void EnsureCanDelete()
    {
        if (IsSystem)
        {
            throw DomainException.RuleViolation("Los documentos del sistema no se pueden borrar.");
        }
    }

    private static string NormalizeTitle(string title)
    {
        var trimmed = (title ?? string.Empty).Trim();
        return trimmed.Length is 0 or > TitleMaxLength
            ? throw DomainException.RuleViolation($"El título debe tener entre 1 y {TitleMaxLength} caracteres.")
            : trimmed;
    }

    private static string? NormalizeDescription(string? description)
    {
        var trimmed = description?.Trim();
        if (trimmed is { Length: > DescriptionMaxLength })
        {
            throw DomainException.RuleViolation($"La descripción no puede superar los {DescriptionMaxLength} caracteres.");
        }

        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
