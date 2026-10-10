using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Catalog;
using FluentValidation;
using OpenTrpg.Core.Application.Items;
using OpenTrpg.Core.Application;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions.Persistence;
using OpenTrpg.Systems.Dnd5e.Application.Items;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Application.Catalog;

/// <param name="Category">An <see cref="ItemCategory"/> name, case-insensitive ("weapon", "AdventuringGear").</param>
/// <param name="Rarity">An <see cref="ItemRarity"/> name, case-insensitive ("rare", "very-rare").</param>
/// <param name="Group">
/// One of <see cref="ItemGroups.All"/>: "simple", "martial" or "exotic" (weapons of that category; "exotic" also exotic
/// armor), "firearm" (firearms) or "bomb" (bombs).
/// </param>
public sealed record SearchItemsQuery(string? Search, string? Category, string? Rarity, int? Page, int? PageSize, string? Group = null);

/// <summary>Weapon and armor groups the item catalog filters by (<see cref="SearchItemsQuery.Group"/>).</summary>
public static class ItemGroups
{
    public const string Simple = "simple";
    public const string Martial = "martial";
    public const string Exotic = "exotic";
    public const string Firearm = "firearm";
    public const string Bomb = "bomb";

    public static IReadOnlyList<string> All { get; } = [Simple, Martial, Exotic, Firearm, Bomb];

    /// <summary>Adds the group to <paramref name="filter"/> (unchanged for null or an unknown group).</summary>
    public static ItemFilter Apply(ItemFilter filter, string? group) => group?.Trim().ToLowerInvariant() switch
    {
        Simple or Martial => filter with { Categories = [ItemCategory.Weapon], SubcategoryPrefix = group.Trim().ToLowerInvariant() },
        Exotic => filter with
        {
            Categories = [ItemCategory.Weapon, ItemCategory.Armor],
            SubcategoryPrefix = Exotic,
            Property = ProficiencyKeys.ExoticProperty,
            PropertyOrSubcategory = true,
        },
        Firearm => filter with { Property = ProficiencyKeys.FirearmProperty },
        Bomb => filter with { Property = ProficiencyKeys.BombProperty },
        _ => filter,
    };
}

public sealed class SearchItemsQueryValidator : AbstractValidator<SearchItemsQuery>
{
    public SearchItemsQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(CatalogQueryDefaults.SearchMaxLength).WithMessage("La búsqueda es demasiado larga.");
        RuleFor(x => x.Category)
            .Must(c => CatalogQueryDefaults.TryParseEnum<ItemCategory>(c, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.Category))
            .WithMessage($"La categoría debe ser una de: {string.Join(", ", Enum.GetNames<ItemCategory>())}.");
        RuleFor(x => x.Rarity)
            .Must(r => CatalogQueryDefaults.TryParseEnum<ItemRarity>(r, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.Rarity))
            .WithMessage($"La rareza debe ser una de: {string.Join(", ", Enum.GetNames<ItemRarity>())}.");
        RuleFor(x => x.Group)
            .Must(g => ItemGroups.All.Contains(g!.Trim().ToLowerInvariant(), StringComparer.Ordinal))
            .When(x => !string.IsNullOrWhiteSpace(x.Group))
            .WithMessage($"El grupo debe ser uno de: {string.Join(", ", ItemGroups.All)}.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("La página debe ser 1 o mayor.");
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, CatalogQueryDefaults.MaxPageSize)
            .WithMessage($"El tamaño de página debe estar entre 1 y {CatalogQueryDefaults.MaxPageSize}.");
    }
}

/// <summary>Paged search of SRD items (homebrew items are not listed here), ordered by name.</summary>
public sealed class SearchItemsHandler(ICatalogRepository catalog)
{
    public async Task<PagedResult<ItemSummaryDto>> HandleAsync(SearchItemsQuery query, CancellationToken cancellationToken = default)
    {
        var page = query.Page ?? 1;
        var pageSize = query.PageSize ?? CatalogQueryDefaults.DefaultPageSize;
        var filter = new ItemFilter(
            CatalogQueryDefaults.NormalizeSearch(query.Search),
            CatalogQueryDefaults.TryParseEnum<ItemCategory>(query.Category, out var category) ? category : null,
            CatalogQueryDefaults.TryParseEnum<ItemRarity>(query.Rarity, out var rarity) ? rarity : null);
        filter = ItemGroups.Apply(filter, query.Group);

        var (items, total) = await catalog.SearchSrdItemsAsync(filter, (page - 1) * pageSize, pageSize, cancellationToken);
        return new PagedResult<ItemSummaryDto>(items.Select(ItemSummaryDto.From).ToList(), total, page, pageSize);
    }
}
