using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Common;
using FluentValidation;

namespace Dnd.Application.Catalog;

public sealed record SearchSpellsQuery(
    string? Search,
    int? Level,
    string? Class,
    string? School,
    bool? Ritual,
    bool? Concentration,
    int? Page,
    int? PageSize);

public sealed class SearchSpellsQueryValidator : AbstractValidator<SearchSpellsQuery>
{
    public SearchSpellsQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(CatalogQueryDefaults.SearchMaxLength).WithMessage("La búsqueda es demasiado larga.");
        RuleFor(x => x.Level).InclusiveBetween(0, 9).WithMessage("El nivel debe estar entre 0 (truco) y 9.");
        RuleFor(x => x.Class).MaximumLength(100).WithMessage("La clase no es válida.");
        RuleFor(x => x.School).MaximumLength(100).WithMessage("La escuela no es válida.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("La página debe ser 1 o mayor.");
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, CatalogQueryDefaults.MaxPageSize)
            .WithMessage($"El tamaño de página debe estar entre 1 y {CatalogQueryDefaults.MaxPageSize}.");
    }
}

/// <summary>Paged spell search, ordered by level and name.</summary>
public sealed class SearchSpellsHandler(ICatalogRepository catalog)
{
    public async Task<PagedResult<SpellSummaryDto>> HandleAsync(SearchSpellsQuery query, CancellationToken cancellationToken = default)
    {
        var page = query.Page ?? 1;
        var pageSize = query.PageSize ?? CatalogQueryDefaults.DefaultPageSize;
        var filter = new SpellFilter(
            CatalogQueryDefaults.NormalizeSearch(query.Search),
            query.Level,
            string.IsNullOrWhiteSpace(query.Class) ? null : query.Class.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(query.School) ? null : query.School.Trim().ToLowerInvariant(),
            query.Ritual,
            query.Concentration);

        var (items, total) = await catalog.SearchSpellsAsync(filter, (page - 1) * pageSize, pageSize, cancellationToken);
        var expansions = SpellExpansionDto.Lookup(await catalog.ListSubclassesWithExpandedSpellsAsync(cancellationToken));
        return new PagedResult<SpellSummaryDto>(items.Select(s => SpellSummaryDto.From(s, expansions[s.Index].ToList())).ToList(), total, page, pageSize);
    }
}
