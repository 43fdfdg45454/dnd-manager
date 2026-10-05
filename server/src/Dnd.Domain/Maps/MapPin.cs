using System.Text.RegularExpressions;
using Dnd.Domain.Common;

namespace Dnd.Domain.Maps;

/// <summary>A marker on a map. <see cref="X"/> and <see cref="Y"/> are relative to the image (0 = left/top, 1 = right/bottom).</summary>
public sealed partial class MapPin : EntityBase
{
    public const int TitleMaxLength = 100;
    public const int NoteMaxLength = 10_000;
    public const int ColorMaxLength = 9;

    private MapPin()
    {
    }

    public Guid MapId { get; private set; }

    public double X { get; private set; }

    public double Y { get; private set; }

    public string Title { get; private set; } = string.Empty;

    /// <summary>Markdown text, possibly empty.</summary>
    public string Note { get; private set; } = string.Empty;

    public string Icon { get; private set; } = "place";

    /// <summary>Hex color <c>#RRGGBB</c> or <c>#AARRGGBB</c>; null = the client's default for the icon.</summary>
    public string? Color { get; private set; }

    public Guid? LoreEntryId { get; private set; }

    public ContentVisibility Visibility { get; private set; }

    public static bool IsValidColor(string? color) => color is not null && ColorPattern().IsMatch(color);

    internal static MapPin Create(
        Guid mapId,
        double x,
        double y,
        string title,
        string? note,
        string icon,
        string? color,
        Guid? loreEntryId,
        ContentVisibility visibility,
        DateTimeOffset now)
    {
        var pin = new MapPin { MapId = mapId, CreatedAt = now };
        pin.Apply(x, y, title, note, icon, color, loreEntryId, visibility);
        return pin;
    }

    /// <summary>Replaces every field; the caller merges partial updates beforehand. Validates before changing anything.</summary>
    internal void Apply(double x, double y, string title, string? note, string icon, string? color, Guid? loreEntryId, ContentVisibility visibility)
    {
        if (double.IsNaN(x) || double.IsNaN(y) || x is < 0 or > 1 || y is < 0 or > 1)
        {
            throw DomainException.RuleViolation("Las coordenadas del pin deben estar entre 0 y 1.");
        }

        var trimmedTitle = (title ?? string.Empty).Trim();
        if (trimmedTitle.Length is 0 or > TitleMaxLength)
        {
            throw DomainException.RuleViolation($"El título del pin debe tener entre 1 y {TitleMaxLength} caracteres.");
        }

        if ((note ?? string.Empty).Length > NoteMaxLength)
        {
            throw DomainException.RuleViolation($"La nota no puede superar los {NoteMaxLength} caracteres.");
        }

        if (!MapPinIcons.IsValid(icon))
        {
            throw DomainException.RuleViolation($"El icono debe ser uno de: {string.Join(", ", MapPinIcons.All)}.");
        }

        if (color is not null && !IsValidColor(color))
        {
            throw DomainException.RuleViolation("El color debe tener el formato #RRGGBB.");
        }

        X = x;
        Y = y;
        Title = trimmedTitle;
        Note = note ?? string.Empty;
        Icon = icon;
        Color = color;
        LoreEntryId = loreEntryId;
        Visibility = visibility;
    }

    [GeneratedRegex("^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$")]
    private static partial Regex ColorPattern();
}
