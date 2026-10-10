using OpenTrpg.Core.Domain.Catalog;

namespace OpenTrpg.Core.Application.Items;

/// <param name="Search">Lower-case term matched against the name; null for no search.</param>
public sealed record ItemFilter(string? Search, ItemCategory? Category, ItemRarity? Rarity)
{
    /// <summary>Any of these categories (on top of <see cref="Category"/>); null for every category.</summary>
    public IReadOnlyList<ItemCategory>? Categories { get; init; }

    /// <summary>Lower-case prefix of the subcategory ("simple" matches "Simple Melee"); null for any.</summary>
    public string? SubcategoryPrefix { get; init; }

    /// <summary>Only catalog items with one of these dataset indexes; null for any.</summary>
    public IReadOnlyList<string>? Indexes { get; init; }
}
