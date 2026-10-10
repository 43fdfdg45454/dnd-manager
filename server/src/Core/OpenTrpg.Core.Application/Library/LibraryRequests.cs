using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Library;
using FluentValidation;

namespace OpenTrpg.Core.Application.Library;

/// <param name="Search">Text looked up (case-insensitive) in title and description.</param>
/// <param name="Category">Enum name of <see cref="LibraryCategory"/>.</param>
public sealed record ListLibraryQuery(string? Search, string? Category);

public sealed class ListLibraryQueryValidator : AbstractValidator<ListLibraryQuery>
{
    public ListLibraryQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(100).WithMessage("La búsqueda no puede superar los 100 caracteres.");
        RuleFor(x => x.Category).Must(EnumNames.IsValid<LibraryCategory>)
            .When(x => !string.IsNullOrEmpty(x.Category))
            .WithMessage(LibraryRules.CategoryMessage);
    }
}

/// <param name="Category">Enum name of <see cref="LibraryCategory"/>.</param>
/// <param name="FileId">A PDF uploaded as <c>LibraryDocument</c>.</param>
public sealed record CreateLibraryDocumentRequest(string Title, string? Description, string Category, Guid FileId);

public sealed class CreateLibraryDocumentRequestValidator : AbstractValidator<CreateLibraryDocumentRequest>
{
    public CreateLibraryDocumentRequestValidator()
    {
        RuleFor(x => x.Title).Must(LibraryRules.IsValidTitle).WithMessage(LibraryRules.TitleMessage);
        RuleFor(x => x.Description).MaximumLength(LibraryDocument.DescriptionMaxLength).WithMessage(LibraryRules.DescriptionMessage);
        RuleFor(x => x.Category).Must(EnumNames.IsValid<LibraryCategory>).WithMessage(LibraryRules.CategoryMessage);
        RuleFor(x => x.FileId).NotEmpty().WithMessage("Indica el fichero PDF.");
    }
}

/// <summary>Absent fields do not change; an empty description clears it.</summary>
public sealed record UpdateLibraryDocumentRequest(string? Title = null, string? Description = null, string? Category = null);

public sealed class UpdateLibraryDocumentRequestValidator : AbstractValidator<UpdateLibraryDocumentRequest>
{
    public UpdateLibraryDocumentRequestValidator()
    {
        RuleFor(x => x.Title).Must(LibraryRules.IsValidTitle).When(x => x.Title is not null).WithMessage(LibraryRules.TitleMessage);
        RuleFor(x => x.Description).MaximumLength(LibraryDocument.DescriptionMaxLength).WithMessage(LibraryRules.DescriptionMessage);
        RuleFor(x => x.Category).Must(EnumNames.IsValid<LibraryCategory>).When(x => x.Category is not null).WithMessage(LibraryRules.CategoryMessage);
    }
}

public sealed record RecommendDocumentRequest(string? Note = null);

public sealed class RecommendDocumentRequestValidator : AbstractValidator<RecommendDocumentRequest>
{
    public RecommendDocumentRequestValidator()
    {
        RuleFor(x => x.Note).MaximumLength(CampaignDocument.NoteMaxLength)
            .WithMessage($"La nota no puede superar los {CampaignDocument.NoteMaxLength} caracteres.");
    }
}

internal static class LibraryRules
{
    public static readonly string CategoryMessage = $"La categoría debe ser {EnumNames.Describe<LibraryCategory>()}.";
    public static readonly string TitleMessage = $"El título debe tener entre 1 y {LibraryDocument.TitleMaxLength} caracteres.";
    public static readonly string DescriptionMessage = $"La descripción no puede superar los {LibraryDocument.DescriptionMaxLength} caracteres.";

    public static bool IsValidTitle(string? title) => title is not null && title.Trim().Length is >= 1 and <= LibraryDocument.TitleMaxLength;
}
