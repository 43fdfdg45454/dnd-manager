using Dnd.Domain.Catalog;
using Dnd.Domain.Common;

namespace Dnd.Domain.Characters;

// Phase 19: decisions of the race, subrace and background made at creation (half-elf ability bonuses and skills,
// dragonborn ancestry, high elf cantrip...), stored as CharacterChoice of level 0 without class.
public sealed partial class Character
{
    /// <summary>Origin choices (race, subrace, background), in the order they were made.</summary>
    public IReadOnlyList<CharacterChoice> OriginChoices => [.. _choices.Where(c => c.IsOrigin).OrderBy(c => c.CreatedAt)];

    /// <summary>The origin choice with that key, or null.</summary>
    public CharacterChoice? OriginChoice(string key) => _choices.FirstOrDefault(c => c.IsOrigin && c.Key == key);

    /// <summary>
    /// Records (or replaces) the answer to an origin choice and applies its stored effects: skills, languages and tools
    /// become proficiencies (source race or background), a cantrip is added always prepared
    /// (<see cref="CharacterSpell.OriginClassIndex"/>). Ability bonuses, feats and trait options act through the sheet.
    /// </summary>
    public CharacterChoice RecordOriginChoice(string key, ChoiceSelection selection, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var normalized = RequireIndex(key, "elección");
        if (!OriginChoiceKeys.IsOrigin(normalized))
        {
            throw DomainException.RuleViolation($"La elección '{normalized}' no es de raza ni de trasfondo.");
        }

        RemoveOriginChoices(k => k == normalized);
        var choice = CharacterChoice.Create(Id, 0, null, normalized, selection, now);
        _choices.Add(choice);
        ApplyOriginChoiceEffects(choice);
        Touch(now);
        return choice;
    }

    /// <summary>Removes the origin choices whose key matches, undoing their stored effects. True when something was removed.</summary>
    public bool RemoveOriginChoices(Func<string, bool> keyMatches)
    {
        ArgumentNullException.ThrowIfNull(keyMatches);
        var removed = _choices.Where(c => c.IsOrigin && keyMatches(c.Key)).ToList();
        foreach (var choice in removed)
        {
            _choices.Remove(choice);
            var selection = choice.Selection;
            var source = OriginProficiencySource(choice.Key);
            foreach (var item in selection.Selected)
            {
                switch (selection.Kind)
                {
                    case OriginChoiceKeys.SkillKind:
                        RemoveOriginProficiency(ProficiencyType.Skill, item.Index, source);
                        break;
                    case OriginChoiceKeys.LanguageKind:
                        RemoveOriginProficiency(ProficiencyType.Language, item.Index, source);
                        break;
                    case OriginChoiceKeys.ToolKind:
                        RemoveOriginProficiency(ProficiencyType.Tool, item.Index, source);
                        break;
                    case OriginChoiceKeys.CantripKind:
                        if (!_choices.Any(c => c.IsOrigin && c.Selection.Kind == OriginChoiceKeys.CantripKind && c.Selection.Selected.Any(s => s.Index == item.Index)))
                        {
                            RemoveSpell(item.Index, CharacterSpell.OriginClassIndex);
                        }

                        break;
                }
            }
        }

        return removed.Count > 0;
    }

    /// <summary>
    /// Gives back the stored effects of every origin choice (after a sheet edit that replaced the proficiencies or
    /// the spells): proficiencies and the always-prepared cantrip are added when missing.
    /// </summary>
    public void ReapplyOriginChoiceEffects()
    {
        foreach (var choice in _choices.Where(c => c.IsOrigin))
        {
            ApplyOriginChoiceEffects(choice);
        }
    }

    /// <summary>
    /// Drops the origin choices that no longer apply after the race, subrace or background changed (called by
    /// <see cref="ApplySheetEdit"/> before the edited values are applied).
    /// </summary>
    private void DropStaleOriginChoices(string? race, string? subrace, string? background)
    {
        if (race != RaceIndex)
        {
            RemoveOriginChoices(k => k.StartsWith(OriginChoiceKeys.RacePrefix, StringComparison.Ordinal));
        }
        else if (subrace != SubraceIndex)
        {
            RemoveOriginChoices(OriginChoiceKeys.IsSubrace);
        }

        if (background != BackgroundIndex)
        {
            RemoveOriginChoices(OriginChoiceKeys.IsBackground);
        }
    }

    private void ApplyOriginChoiceEffects(CharacterChoice choice)
    {
        var selection = choice.Selection;
        var source = OriginProficiencySource(choice.Key);
        foreach (var item in selection.Selected)
        {
            switch (selection.Kind)
            {
                case OriginChoiceKeys.SkillKind:
                    AddProficiency(ProficiencyType.Skill, item.Index, source);
                    break;
                case OriginChoiceKeys.LanguageKind:
                    AddProficiency(ProficiencyType.Language, item.Index, source);
                    break;
                case OriginChoiceKeys.ToolKind:
                    AddProficiency(ProficiencyType.Tool, item.Index, source);
                    break;
                case OriginChoiceKeys.CantripKind:
                    AddSpell(item.Index, CharacterSpell.OriginClassIndex, isPrepared: true, alwaysPrepared: true);
                    break;
            }
        }
    }

    private void RemoveOriginProficiency(ProficiencyType type, string key, ProficiencySource source)
    {
        if (FindProficiency(type, key) is { } proficiency && proficiency.Source == source && !proficiency.Expertise)
        {
            _proficiencies.Remove(proficiency);
        }
    }

    private static ProficiencySource OriginProficiencySource(string key) =>
        OriginChoiceKeys.IsBackground(key) ? ProficiencySource.Background : ProficiencySource.Race;
}
