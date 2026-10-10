using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Application;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions.Persistence;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Application.Characters;

/// <summary>Applies the grants of the options and feats a character has (level-ups and forced replacements).</summary>
public static class ChoiceGrants
{
    /// <summary>
    /// Grants of the current picks (options and feats) and of the subclass levels reached: proficiencies (source
    /// Class) and always-prepared spells, from the level in the class they require. Always-prepared spells granted
    /// by replaced options are removed first (and given back if another pick still grants them).
    /// </summary>
    public static async Task ApplyAsync(ICatalogRepository catalog, Dnd5eCharacter character, IReadOnlyList<string> replacedOptions, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await SyncGrantedOptionsAsync(catalog, character, now, cancellationToken);
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

    /// <summary>
    /// Options granted by the options, feats and subclass levels the character has (<c>grants.options</c>): the missing
    /// ones are recorded as granted picks of the class of their source, and those whose source is gone are removed.
    /// Granted options may grant further options (a few rounds at most).
    /// </summary>
    private static async Task SyncGrantedOptionsAsync(ICatalogRepository catalog, Dnd5eCharacter character, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var firstClass = character.OrderedClasses.FirstOrDefault()?.ClassIndex;
        if (firstClass is null)
        {
            return;
        }

        var subclassIndexes = character.Classes.Select(c => c.SubclassIndex).OfType<string>().ToList();
        var subclassGrants = new List<(string ClassIndex, int Level, string Option)>();
        foreach (var subclassLevel in await catalog.ListSubclassLevelsAsync(subclassIndexes, cancellationToken))
        {
            var owner = character.Classes.FirstOrDefault(c => c.SubclassIndex == subclassLevel.SubclassIndex);
            if (owner is not null && subclassLevel.Level <= owner.Level)
            {
                subclassGrants.AddRange(subclassLevel.Grants.Options.Select(o => (owner.ClassIndex, subclassLevel.Level, o)));
            }
        }

        for (var round = 0; round < 4; round++)
        {
            var picks = character.ActivePicks().Where(p => p.SetId is not null).ToList();
            var definitions = (await catalog.ListOptionsByIndexAsync(picks.Select(p => p.Item.Index).Distinct(StringComparer.Ordinal).ToList(), cancellationToken))
                .ToDictionary(o => o.Index, StringComparer.Ordinal);
            var desired = new Dictionary<string, (string ClassIndex, int Level)>(StringComparer.Ordinal);
            foreach (var (classIndex, level, option) in subclassGrants)
            {
                desired.TryAdd(option, (classIndex, level));
            }

            foreach (var pick in picks)
            {
                var classIndex = pick.ClassIndex ?? firstClass;
                var level = pick.ClassIndex is null ? 1 : pick.Level;
                foreach (var option in definitions.GetValueOrDefault(pick.Item.Index)?.Grants.Options ?? [])
                {
                    desired.TryAdd(option, (classIndex, Math.Max(1, level)));
                }
            }

            var removed = character.RemoveGrantedOptions(index => !desired.ContainsKey(index), now);
            var missing = desired.Keys.Where(i => !picks.Any(p => p.Item.Index == i)).ToList();
            var granted = missing.Count == 0 ? [] : await catalog.ListOptionsByIndexAsync(missing, cancellationToken);
            var added = 0;
            foreach (var option in granted)
            {
                var (classIndex, level) = desired[option.Index];
                if (character.AddGrantedOption(classIndex, level, new ChoiceItem(option.Index, option.Name), option.SetId, now))
                {
                    added++;
                }
            }

            if (added == 0 && removed.Count == 0)
            {
                return;
            }
        }
    }
}
