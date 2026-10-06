using Dnd.Application.Abstractions.Persistence;
using Dnd.Domain.Catalog;

namespace Dnd.Application.Catalog;

/// <summary>Class detail with its 20 levels (slots and features) and its subclasses with their features per level.</summary>
public sealed class GetClassHandler(ICatalogRepository catalog)
{
    public async Task<ClassDetailDto> HandleAsync(string index, CancellationToken cancellationToken = default)
    {
        var definition = await catalog.GetClassAsync(index, cancellationToken) ?? throw CatalogErrors.ClassNotFound();
        var levels = await catalog.ListClassLevelsAsync(definition.Index, cancellationToken);
        var subclasses = await catalog.ListSubclassesAsync(definition.Index, cancellationToken);
        var subclassLevels = await catalog.ListSubclassLevelsAsync(subclasses.Select(s => s.Index).ToList(), cancellationToken);
        var features = (await catalog.ListFeaturesByClassAsync(definition.Index, cancellationToken)).ToDictionary(f => f.Index);

        return new ClassDetailDto(
            definition.Index,
            definition.Name,
            definition.HitDie,
            definition.SavingThrows,
            definition.ProficiencyNames,
            definition.SpellcastingAbility,
            definition.IsSpellcaster,
            definition.SpellcastingLevel,
            definition.IsPactCaster,
            definition.SubclassFlavor,
            definition.StartingEquipmentText,
            CatalogJson.SkillChoices(definition.SkillChoicesJson),
            levels
                .Select(l => new ClassLevelDto(
                    l.Level,
                    l.ProfBonus,
                    l.AbilityScoreBonuses,
                    l.CantripsKnown,
                    l.SpellsKnown,
                    l.SpellSlots,
                    CatalogJson.Object(l.ClassSpecificJson),
                    Features(l.FeatureIndexes, features)))
                .ToList(),
            subclasses
                .Select(s => new SubclassDto(
                    s.Index,
                    s.Name,
                    s.Flavor,
                    s.Description,
                    subclassLevels
                        .Where(l => l.SubclassIndex == s.Index)
                        .OrderBy(l => l.Level)
                        .Select(l => new SubclassLevelDto(l.Level, Features(l.FeatureIndexes, features)))
                        .ToList()))
                .ToList());
    }

    private static List<FeatureDto> Features(IEnumerable<string> indexes, IReadOnlyDictionary<string, FeatureDefinition> features) =>
        indexes.Select(i => features.GetValueOrDefault(i)).OfType<FeatureDefinition>().Select(FeatureDto.From).ToList();
}
