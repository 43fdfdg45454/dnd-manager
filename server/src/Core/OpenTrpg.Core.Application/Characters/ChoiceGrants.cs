using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Domain.Characters;

namespace OpenTrpg.Core.Application.Characters;

/// <summary>Applies the grants of the options and feats a character has (level-ups and forced replacements).</summary>
public static class ChoiceGrants
{
    /// <summary>
    /// Grants of the current picks (options and feats) and of the subclass levels reached: proficiencies (source
    /// Class) and always-prepared spells, from the level in the class they require. Always-prepared spells granted
    /// by replaced options are removed first (and given back if another pick still grants them).
    /// </summary>
    public static async Task ApplyAsync(ICatalogRepository catalog, Dnd5eCharacter character, IReadOnlyList<string> replacedOptions, CancellationToken cancellationToken)
    {
        var levels = character.Classes.ToDictionary(c => c.ClassIndex, c => c.Level, StringComparer.Ordinal);
        var picks = character.ActivePicks().Where(p => p.SetId is not null).ToList();
        var optionIndexes = picks.Select(p => p.Item.Index).Concat(replacedOptions).Distinct(StringComparer.Ordinal).ToList();
        var options = (await catalog.ListOptionsByIndexAsync(optionIndexes, cancellationToken)).ToDictionary(o => o.Index, StringComparer.Ordinal);

        foreach (var replaced in replacedOptions)
        {
            if (options.TryGetValue(replaced, out var option))
            {
                var classIndexes = character.Choices.Where(c => c.Selection.Replaced.Any(r => r.Index == replaced)).Select(c => c.ClassIndex ?? CharacterSpell.OriginClassIndex);
                foreach (var classIndex in classIndexes.Distinct(StringComparer.Ordinal))
                {
                    foreach (var spell in option.Grants.Spells.Select(s => s.Index).Concat(option.Grants.Cantrips))
                    {
                        if (character.Spells.Any(s => s.SpellIndex == spell && s.ClassIndex == classIndex && s.AlwaysPrepared))
                        {
                            character.RemoveSpell(spell, classIndex);
                        }
                    }
                }
            }
        }

        var grants = picks
            .Select(p => (ClassIndex: p.ClassIndex ?? CharacterSpell.OriginClassIndex, Grants: options.GetValueOrDefault(p.Item.Index)?.Grants))
            .Where(g => g.Grants is { IsEmpty: false })
            .Select(g => (g.ClassIndex, Grants: g.Grants!))
            .ToList();

        var subclassIndexes = character.Classes.Select(c => c.SubclassIndex).OfType<string>().ToList();
        foreach (var subclassLevel in await catalog.ListSubclassLevelsAsync(subclassIndexes, cancellationToken))
        {
            var owner = character.Classes.FirstOrDefault(c => c.SubclassIndex == subclassLevel.SubclassIndex);
            if (owner is not null && subclassLevel.Level <= owner.Level && subclassLevel.Grants is { IsEmpty: false } subclassGrants)
            {
                grants.Add((owner.ClassIndex, subclassGrants));
            }
        }

        foreach (var (classIndex, granted) in grants)
        {
            var level = classIndex == CharacterSpell.OriginClassIndex ? character.TotalLevel : levels.GetValueOrDefault(classIndex);
            foreach (var skill in granted.Skills)
            {
                character.AddProficiency(ProficiencyType.Skill, skill, ProficiencySource.Class);
            }

            foreach (var (list, type) in new[]
                     {
                         (granted.Armor, ProficiencyType.Armor), (granted.Weapons, ProficiencyType.Weapon), (granted.Tools, ProficiencyType.Tool),
                         (granted.Languages, ProficiencyType.Language), (granted.SavingThrows, ProficiencyType.SavingThrow),
                     })
            {
                foreach (var key in list)
                {
                    character.AddProficiency(type, key, ProficiencySource.Class);
                }
            }

            foreach (var cantrip in granted.Cantrips)
            {
                character.AddSpell(cantrip, classIndex, isPrepared: true, alwaysPrepared: true);
            }

            foreach (var spell in granted.Spells.Where(s => (s.MinLevel ?? 0) <= level))
            {
                character.AddSpell(spell.Index, classIndex, isPrepared: true, alwaysPrepared: true);
            }
        }
    }
}
