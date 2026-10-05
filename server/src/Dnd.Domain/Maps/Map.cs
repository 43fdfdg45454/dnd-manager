using Dnd.Domain.Common;

namespace Dnd.Domain.Maps;

/// <summary>
/// A campaign map: an image (stored file) with pins. The pixel size of the image is copied here so
/// the client can lay it out before downloading it.
/// </summary>
public sealed class Map : EntityBase
{
    public const int NameMaxLength = 100;
    public const int MaxSortOrder = 100_000;

    private readonly List<MapPin> _pins = [];

    private Map()
    {
    }

    public Guid CampaignId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public Guid FileId { get; private set; }

    public int WidthPx { get; private set; }

    public int HeightPx { get; private set; }

    public ContentVisibility Visibility { get; private set; }

    public int SortOrder { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<MapPin> Pins => _pins;

    public static Map Create(Guid campaignId, string name, Guid fileId, int widthPx, int heightPx, ContentVisibility visibility, int sortOrder, DateTimeOffset now)
    {
        EnsureSize(widthPx, heightPx);
        return new Map
        {
            CampaignId = campaignId,
            Name = NormalizeName(name),
            FileId = fileId,
            WidthPx = widthPx,
            HeightPx = heightPx,
            Visibility = visibility,
            SortOrder = sortOrder,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>Null arguments keep the current value.</summary>
    public void Update(string? name, ContentVisibility? visibility, int? sortOrder, DateTimeOffset now)
    {
        var newName = name is null ? Name : NormalizeName(name);
        var newSort = sortOrder is null ? SortOrder : sortOrder is >= 0 and <= MaxSortOrder
            ? sortOrder.Value
            : throw DomainException.RuleViolation($"El orden debe estar entre 0 y {MaxSortOrder}.");

        Name = newName;
        SortOrder = newSort;
        Visibility = visibility ?? Visibility;
        UpdatedAt = now;
    }

    /// <summary>Swaps the image. Pins keep their relative position.</summary>
    public void ReplaceImage(Guid fileId, int widthPx, int heightPx, DateTimeOffset now)
    {
        EnsureSize(widthPx, heightPx);
        FileId = fileId;
        WidthPx = widthPx;
        HeightPx = heightPx;
        UpdatedAt = now;
    }

    public MapPin AddPin(
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
        var pin = MapPin.Create(Id, x, y, title, note, icon, color, loreEntryId, visibility, now);
        _pins.Add(pin);
        UpdatedAt = now;
        return pin;
    }

    public MapPin FindPin(Guid pinId) =>
        _pins.FirstOrDefault(p => p.Id == pinId) ?? throw DomainException.NotFound("Pin no encontrado.");

    /// <summary>Applies the given changes to a pin; <see langword="null" /> arguments keep the current value, the setters change the optional ones.</summary>
    public MapPin UpdatePin(
        Guid pinId,
        double? x,
        double? y,
        string? title,
        string? note,
        string? icon,
        (bool IsSet, string? Value) color,
        (bool IsSet, Guid? Value) loreEntryId,
        ContentVisibility? visibility,
        DateTimeOffset now)
    {
        var pin = FindPin(pinId);
        pin.Apply(
            x ?? pin.X,
            y ?? pin.Y,
            title ?? pin.Title,
            note ?? pin.Note,
            icon ?? pin.Icon,
            color.IsSet ? color.Value : pin.Color,
            loreEntryId.IsSet ? loreEntryId.Value : pin.LoreEntryId,
            visibility ?? pin.Visibility);
        UpdatedAt = now;
        return pin;
    }

    public void RemovePin(Guid pinId, DateTimeOffset now)
    {
        _pins.Remove(FindPin(pinId));
        UpdatedAt = now;
    }

    private static string NormalizeName(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        return trimmed.Length is 0 or > NameMaxLength
            ? throw DomainException.RuleViolation($"El nombre del mapa debe tener entre 1 y {NameMaxLength} caracteres.")
            : trimmed;
    }

    private static void EnsureSize(int widthPx, int heightPx)
    {
        if (widthPx < 1 || heightPx < 1)
        {
            throw DomainException.RuleViolation("El tamaño de la imagen del mapa no es válido.");
        }
    }
}
