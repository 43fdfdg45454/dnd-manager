using Dnd.Domain.Catalog;

namespace Dnd.Application.Catalog;

/// <param name="Search">Lower-case term matched against the name; null for no search.</param>
/// <param name="ClassIndex">Class index ("wizard"); null for every class.</param>
/// <param name="School">Lower-case school name or index ("evocation"); null for every school.</param>
public sealed record SpellFilter(string? Search, int? Level, string? ClassIndex, string? School, bool? Ritual, bool? Concentration);

/// <param name="Search">Lower-case term matched against the name; null for no search.</param>
public sealed record ItemFilter(string? Search, ItemCategory? Category, ItemRarity? Rarity);

internal static class CatalogQueryDefaults
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;
    public const int SearchMaxLength = 100;

    public static string? NormalizeSearch(string? search) =>
        string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLowerInvariant();

    /// <summary>Parses enum names leniently: case-insensitive, ignoring spaces, hyphens and underscores ("very-rare").</summary>
    public static bool TryParseEnum<TEnum>(string? value, out TEnum result)
        where TEnum : struct, Enum
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var compact = new string(value.Where(ch => ch is not (' ' or '-' or '_')).ToArray());
        return !compact.All(char.IsDigit) && Enum.TryParse(compact, ignoreCase: true, out result) && Enum.IsDefined(result);
    }
}
