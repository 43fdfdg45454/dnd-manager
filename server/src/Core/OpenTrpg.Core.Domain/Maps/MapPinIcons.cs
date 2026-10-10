namespace OpenTrpg.Core.Domain.Maps;

/// <summary>Icon names a map pin can use (the client draws the glyph).</summary>
public static class MapPinIcons
{
    public static readonly IReadOnlyList<string> All = ["place", "city", "dungeon", "quest", "npc", "danger", "custom"];

    public static bool IsValid(string? icon) => icon is not null && All.Contains(icon, StringComparer.Ordinal);
}
