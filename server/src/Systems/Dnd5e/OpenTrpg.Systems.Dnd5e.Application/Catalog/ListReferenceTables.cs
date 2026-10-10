using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions.Persistence;

namespace OpenTrpg.Systems.Dnd5e.Application.Catalog;

/// <summary>Every condition, ordered by name.</summary>
public sealed class ListConditionsHandler(ICatalogRepository catalog)
{
    public async Task<IReadOnlyList<ConditionDto>> HandleAsync(CancellationToken cancellationToken = default) =>
        (await catalog.ListConditionsAsync(cancellationToken)).Select(ConditionDto.From).ToList();
}

/// <summary>Every skill, ordered by name.</summary>
public sealed class ListSkillsHandler(ICatalogRepository catalog)
{
    public async Task<IReadOnlyList<SkillDto>> HandleAsync(CancellationToken cancellationToken = default) =>
        (await catalog.ListSkillsAsync(cancellationToken)).Select(SkillDto.From).ToList();
}

/// <summary>Every background, ordered by name, with its starting equipment resolved against the catalog.</summary>
public sealed class ListBackgroundsHandler(ICatalogRepository catalog)
{
    public async Task<IReadOnlyList<BackgroundDto>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var backgrounds = (await catalog.ListBackgroundsAsync(cancellationToken))
            .Select(b => (Definition: b, Equipment: b.StartingEquipment))
            .ToList();
        var resolve = await StartingEquipmentResolver.PrepareAsync(catalog, backgrounds.Select(b => b.Equipment), cancellationToken);
        return backgrounds.Select(b => BackgroundDto.From(b.Definition, resolve(b.Equipment))).ToList();
    }
}

/// <summary>Rules documents of the content packs in scope, ordered by title, with an optional search and category.</summary>
public sealed class ListRulesHandler(ICatalogRepository catalog)
{
    public async Task<IReadOnlyList<RuleSummaryDto>> HandleAsync(string? q, string? category, CancellationToken cancellationToken = default)
    {
        var search = CatalogQueryDefaults.NormalizeSearch(q);
        var wanted = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        return (await catalog.ListRulesAsync(cancellationToken))
            .Where(r => wanted is null || string.Equals(r.Category, wanted, StringComparison.OrdinalIgnoreCase))
            .Where(r => search is null
                || r.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                || r.Tags.Any(t => t.Contains(search, StringComparison.OrdinalIgnoreCase))
                || r.Body.Any(p => p.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .Select(RuleSummaryDto.From)
            .ToList();
    }
}

/// <summary>A rules document by index (404 when unknown).</summary>
public sealed class GetRuleHandler(ICatalogRepository catalog)
{
    public async Task<RuleDto> HandleAsync(string index, CancellationToken cancellationToken = default) =>
        await catalog.GetRuleAsync(index.Trim().ToLowerInvariant(), cancellationToken) is { } rule
            ? RuleDto.From(rule)
            : throw CatalogErrors.RuleNotFound();
}

/// <summary>The entries of a vocabulary in scope (SRD and packs), ordered by name; 404 for an unknown kind.</summary>
public sealed class ListReferenceEntriesHandler(ICatalogRepository catalog)
{
    public async Task<IReadOnlyList<ReferenceEntryDto>> HandleAsync(string kind, CancellationToken cancellationToken = default)
    {
        var known = Domain.Catalog.ReferenceEntry.Kinds.FirstOrDefault(k => string.Equals(k, kind, StringComparison.OrdinalIgnoreCase))
            ?? throw CatalogErrors.ReferenceKindNotFound();
        return (await catalog.ListReferenceEntriesAsync(known, cancellationToken)).Select(ReferenceEntryDto.From).ToList();
    }
}
