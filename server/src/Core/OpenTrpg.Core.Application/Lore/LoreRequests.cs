using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Common;
using OpenTrpg.Core.Domain.Lore;
using FluentValidation;

namespace OpenTrpg.Core.Application.Lore;

/// <summary>Query string of <c>GET /campaigns/{id}/lore</c>.</summary>
/// <param name="Category">Enum name of <see cref="LoreCategory"/>.</param>
/// <param name="Search">Text looked up (case-insensitive) in titles and content.</param>
/// <param name="ParentId">Only the direct children of this entry.</param>
public sealed record ListLoreQuery(string? Category, string? Search, Guid? ParentId);

public sealed class ListLoreQueryValidator : AbstractValidator<ListLoreQuery>
{
    public ListLoreQueryValidator()
    {
        RuleFor(x => x.Category).Must(EnumNames.IsValid<LoreCategory>)
            .When(x => !string.IsNullOrEmpty(x.Category))
            .WithMessage(LoreRules.CategoryMessage);
        RuleFor(x => x.Search).MaximumLength(100).WithMessage("La búsqueda no puede superar los 100 caracteres.");
    }
}

/// <param name="Category">Enum name of <see cref="LoreCategory"/>.</param>
/// <param name="Visibility">Enum name of <see cref="ContentVisibility"/>.</param>
public sealed record CreateLoreRequest(
    string Title,
    string Category,
    string? ContentMarkdown,
    string Visibility,
    Guid? ParentId = null,
    Guid? CoverFileId = null);

public sealed class CreateLoreRequestValidator : AbstractValidator<CreateLoreRequest>
{
    public CreateLoreRequestValidator()
    {
        RuleFor(x => x.Title).Must(LoreRules.IsValidTitle).WithMessage(LoreRules.TitleMessage);
        RuleFor(x => x.Category).Must(EnumNames.IsValid<LoreCategory>).WithMessage(LoreRules.CategoryMessage);
        RuleFor(x => x.ContentMarkdown).MaximumLength(LoreEntry.ContentMaxLength).WithMessage(LoreRules.ContentMessage);
        RuleFor(x => x.Visibility).Must(EnumNames.IsValid<ContentVisibility>).WithMessage(LoreRules.VisibilityMessage);
    }
}

/// <summary>
/// Absent fields do not change. <c>parentId: null</c> moves the entry to the root and
/// <c>coverFileId: null</c> removes the cover.
/// </summary>
public sealed record UpdateLoreRequest
{
    public string? Title { get; init; }

    public string? Category { get; init; }

    public string? ContentMarkdown { get; init; }

    public string? Visibility { get; init; }

    public int? SortOrder { get; init; }

    public Optional<Guid?> ParentId { get; init; }

    public Optional<Guid?> CoverFileId { get; init; }
}

public sealed class UpdateLoreRequestValidator : AbstractValidator<UpdateLoreRequest>
{
    public UpdateLoreRequestValidator()
    {
        RuleFor(x => x.Title).Must(LoreRules.IsValidTitle).When(x => x.Title is not null).WithMessage(LoreRules.TitleMessage);
        RuleFor(x => x.Category).Must(EnumNames.IsValid<LoreCategory>).When(x => x.Category is not null).WithMessage(LoreRules.CategoryMessage);
        RuleFor(x => x.ContentMarkdown).MaximumLength(LoreEntry.ContentMaxLength).WithMessage(LoreRules.ContentMessage);
        RuleFor(x => x.Visibility).Must(EnumNames.IsValid<ContentVisibility>).When(x => x.Visibility is not null).WithMessage(LoreRules.VisibilityMessage);
        RuleFor(x => x.SortOrder).InclusiveBetween(0, LoreEntry.MaxSortOrder).WithMessage($"El orden debe estar entre 0 y {LoreEntry.MaxSortOrder}.");
    }
}

public sealed record AddLoreAttachmentRequest(Guid FileId, string? Caption = null);

public sealed class AddLoreAttachmentRequestValidator : AbstractValidator<AddLoreAttachmentRequest>
{
    public AddLoreAttachmentRequestValidator()
    {
        RuleFor(x => x.FileId).NotEmpty().WithMessage("Indica el fichero.");
        RuleFor(x => x.Caption).MaximumLength(LoreAttachment.CaptionMaxLength)
            .WithMessage($"El pie de foto no puede superar los {LoreAttachment.CaptionMaxLength} caracteres.");
    }
}

internal static class LoreRules
{
    public static readonly string CategoryMessage = $"La categoría debe ser {EnumNames.Describe<LoreCategory>()}.";
    public static readonly string VisibilityMessage = $"La visibilidad debe ser {EnumNames.Describe<ContentVisibility>()}.";
    public static readonly string TitleMessage = $"El título debe tener entre 1 y {LoreEntry.TitleMaxLength} caracteres.";
    public static readonly string ContentMessage = $"El contenido no puede superar los {LoreEntry.ContentMaxLength} caracteres.";

    public static bool IsValidTitle(string? title) => title is not null && title.Trim().Length is >= 1 and <= LoreEntry.TitleMaxLength;
}
