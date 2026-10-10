using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Common;
using OpenTrpg.Core.Domain.Maps;
using FluentValidation;

namespace OpenTrpg.Core.Application.Maps;

/// <param name="Visibility">Enum name of <see cref="ContentVisibility"/>.</param>
public sealed record CreateMapRequest(string Name, Guid FileId, string Visibility);

public sealed class CreateMapRequestValidator : AbstractValidator<CreateMapRequest>
{
    public CreateMapRequestValidator()
    {
        RuleFor(x => x.Name).Must(MapRules.IsValidName).WithMessage(MapRules.NameMessage);
        RuleFor(x => x.FileId).NotEmpty().WithMessage("Indica la imagen del mapa.");
        RuleFor(x => x.Visibility).Must(EnumNames.IsValid<ContentVisibility>).WithMessage(MapRules.VisibilityMessage);
    }
}

/// <summary>Absent fields do not change. A new <see cref="FileId"/> replaces the image (pins keep their relative position).</summary>
public sealed record UpdateMapRequest(string? Name = null, string? Visibility = null, int? SortOrder = null, Guid? FileId = null);

public sealed class UpdateMapRequestValidator : AbstractValidator<UpdateMapRequest>
{
    public UpdateMapRequestValidator()
    {
        RuleFor(x => x.Name).Must(MapRules.IsValidName).When(x => x.Name is not null).WithMessage(MapRules.NameMessage);
        RuleFor(x => x.Visibility).Must(EnumNames.IsValid<ContentVisibility>).When(x => x.Visibility is not null).WithMessage(MapRules.VisibilityMessage);
        RuleFor(x => x.SortOrder).InclusiveBetween(0, Map.MaxSortOrder).WithMessage($"El orden debe estar entre 0 y {Map.MaxSortOrder}.");
        RuleFor(x => x.FileId).NotEqual(Guid.Empty).WithMessage("El fichero no es válido.");
    }
}

/// <param name="X">Relative horizontal position, 0..1.</param>
/// <param name="Y">Relative vertical position, 0..1.</param>
/// <param name="Icon">One of <see cref="MapPinIcons.All"/>.</param>
/// <param name="Color">Hex color <c>#RRGGBB</c>.</param>
public sealed record CreatePinRequest(
    double? X,
    double? Y,
    string Title,
    string? Note,
    string Icon,
    string? Color,
    Guid? LoreEntryId,
    string Visibility);

public sealed class CreatePinRequestValidator : AbstractValidator<CreatePinRequest>
{
    public CreatePinRequestValidator()
    {
        RuleFor(x => x.X).NotNull().WithMessage("Indica la posición horizontal (x).")
            .Must(MapRules.IsRelative).WithMessage(MapRules.CoordinateMessage);
        RuleFor(x => x.Y).NotNull().WithMessage("Indica la posición vertical (y).")
            .Must(MapRules.IsRelative).WithMessage(MapRules.CoordinateMessage);
        RuleFor(x => x.Title).Must(MapRules.IsValidPinTitle).WithMessage(MapRules.PinTitleMessage);
        RuleFor(x => x.Note).MaximumLength(MapPin.NoteMaxLength).WithMessage(MapRules.NoteMessage);
        RuleFor(x => x.Icon).Must(MapPinIcons.IsValid).WithMessage(MapRules.IconMessage);
        RuleFor(x => x.Color).Must(MapPin.IsValidColor).When(x => x.Color is not null).WithMessage(MapRules.ColorMessage);
        RuleFor(x => x.Visibility).Must(EnumNames.IsValid<ContentVisibility>).WithMessage(MapRules.VisibilityMessage);
    }
}

/// <summary>
/// Absent fields do not change. An empty note clears it; <c>color: null</c> and
/// <c>loreEntryId: null</c> clear those fields.
/// </summary>
public sealed record UpdatePinRequest
{
    public double? X { get; init; }

    public double? Y { get; init; }

    public string? Title { get; init; }

    public string? Note { get; init; }

    public string? Icon { get; init; }

    public Optional<string?> Color { get; init; }

    public Optional<Guid?> LoreEntryId { get; init; }

    public string? Visibility { get; init; }
}

public sealed class UpdatePinRequestValidator : AbstractValidator<UpdatePinRequest>
{
    public UpdatePinRequestValidator()
    {
        RuleFor(x => x.X).Must(MapRules.IsRelative).When(x => x.X is not null).WithMessage(MapRules.CoordinateMessage);
        RuleFor(x => x.Y).Must(MapRules.IsRelative).When(x => x.Y is not null).WithMessage(MapRules.CoordinateMessage);
        RuleFor(x => x.Title).Must(MapRules.IsValidPinTitle).When(x => x.Title is not null).WithMessage(MapRules.PinTitleMessage);
        RuleFor(x => x.Note).MaximumLength(MapPin.NoteMaxLength).WithMessage(MapRules.NoteMessage);
        RuleFor(x => x.Icon).Must(MapPinIcons.IsValid).When(x => x.Icon is not null).WithMessage(MapRules.IconMessage);
        RuleFor(x => x.Color)
            .Must(c => !c.IsSet || c.Value is null || MapPin.IsValidColor(c.Value))
            .WithMessage(MapRules.ColorMessage);
        RuleFor(x => x.Visibility).Must(EnumNames.IsValid<ContentVisibility>).When(x => x.Visibility is not null).WithMessage(MapRules.VisibilityMessage);
    }
}

internal static class MapRules
{
    public static readonly string VisibilityMessage = $"La visibilidad debe ser {EnumNames.Describe<ContentVisibility>()}.";
    public static readonly string NameMessage = $"El nombre debe tener entre 1 y {Map.NameMaxLength} caracteres.";
    public static readonly string PinTitleMessage = $"El título debe tener entre 1 y {MapPin.TitleMaxLength} caracteres.";
    public static readonly string NoteMessage = $"La nota no puede superar los {MapPin.NoteMaxLength} caracteres.";
    public static readonly string IconMessage = $"El icono debe ser uno de: {string.Join(", ", MapPinIcons.All)}.";
    public static readonly string ColorMessage = "El color debe tener el formato #RRGGBB.";
    public static readonly string CoordinateMessage = "Las coordenadas deben estar entre 0 y 1.";

    public static bool IsValidName(string? name) => name is not null && name.Trim().Length is >= 1 and <= Map.NameMaxLength;

    public static bool IsValidPinTitle(string? title) => title is not null && title.Trim().Length is >= 1 and <= MapPin.TitleMaxLength;

    public static bool IsRelative(double? value) => value is >= 0 and <= 1;
}
