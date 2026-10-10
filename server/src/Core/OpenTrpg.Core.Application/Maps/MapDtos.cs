using OpenTrpg.Core.Application.Files;
using OpenTrpg.Core.Domain.Maps;

namespace OpenTrpg.Core.Application.Maps;

/// <param name="X">Relative horizontal position (0 = left edge, 1 = right edge).</param>
/// <param name="Y">Relative vertical position (0 = top edge, 1 = bottom edge).</param>
/// <param name="Note">Markdown text; null when empty.</param>
public sealed record MapPinDto(
    Guid Id,
    Guid MapId,
    double X,
    double Y,
    string Title,
    string? Note,
    string Icon,
    string? Color,
    Guid? LoreEntryId,
    string Visibility,
    DateTimeOffset CreatedAt)
{
    public static MapPinDto From(MapPin pin) => new(
        pin.Id,
        pin.MapId,
        pin.X,
        pin.Y,
        pin.Title,
        pin.Note.Length == 0 ? null : pin.Note,
        pin.Icon,
        pin.Color,
        pin.LoreEntryId,
        pin.Visibility.ToString(),
        pin.CreatedAt);
}

/// <param name="Url">Relative download URL of the image: <c>/api/v1/files/{fileId}</c>.</param>
public sealed record MapSummaryDto(
    Guid Id,
    Guid CampaignId,
    string Name,
    Guid FileId,
    string Url,
    int WidthPx,
    int HeightPx,
    string Visibility,
    int SortOrder,
    DateTimeOffset CreatedAt)
{
    public static MapSummaryDto From(Map map) => new(
        map.Id,
        map.CampaignId,
        map.Name,
        map.FileId,
        FileUrls.For(map.FileId),
        map.WidthPx,
        map.HeightPx,
        map.Visibility.ToString(),
        map.SortOrder,
        map.CreatedAt);
}

/// <summary>A map with its pins (only the visible ones for players).</summary>
public sealed record MapDto(
    Guid Id,
    Guid CampaignId,
    string Name,
    Guid FileId,
    string Url,
    int WidthPx,
    int HeightPx,
    string Visibility,
    int SortOrder,
    DateTimeOffset CreatedAt,
    IReadOnlyList<MapPinDto> Pins);
