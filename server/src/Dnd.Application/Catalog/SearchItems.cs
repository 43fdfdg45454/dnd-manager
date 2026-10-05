using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Common;
using Dnd.Domain.Catalog;
using FluentValidation;

namespace Dnd.Application.Catalog;

/// <param name="Category">An <see cref="ItemCategory"/> name, case-insensitive ("weapon", "AdventuringGear").</param>
/// <param name="Rarity">An <see cref="ItemRarity"/> name, case-insensitive ("rare", "very-rare").</param>
public sealed record SearchItemsQuery(string? Search, string? Category, string? Rarity, int? Page, int? PageSize);

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

        var (items, total) = await catalog.SearchSrdItemsAsync(filter, (page - 1) * pageSize, pageSize, cancellationToken);
        return new PagedResult<ItemSummaryDto>(items.Select(ItemSummaryDto.From).ToList(), total, page, pageSize);
    }
}
