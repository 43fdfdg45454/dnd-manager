using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Common;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Domain.Characters;

// Phase 19: decisions of the race, subrace and background made at creation (half-elf ability bonuses and skills,
// dragonborn ancestry, high elf cantrip...), stored as CharacterChoice of level 0 without class.
public sealed partial class Dnd5eCharacter
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
                    case OriginChoiceKeys.ArmorKind:
                        RemoveOriginProficiency(ProficiencyType.Armor, item.Index, source);
                        break;
                    case OriginChoiceKeys.WeaponKind:
                        RemoveOriginProficiency(ProficiencyType.Weapon, item.Index, source);
                        break;
                    case OriginChoiceKeys.SavingThrowKind:
                        RemoveOriginProficiency(ProficiencyType.SavingThrow, item.Index, source);
                        break;
                    case OriginChoiceKeys.CantripKind or OriginChoiceKeys.SpellKind:
                        if (!_choices.Any(c => c.IsOrigin
                                && c.Selection.Kind is OriginChoiceKeys.CantripKind or OriginChoiceKeys.SpellKind
                                && c.Selection.Selected.Any(s => s.Index == item.Index)))
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
                case OriginChoiceKeys.ArmorKind:
                    AddProficiency(ProficiencyType.Armor, item.Index, source);
                    break;
                case OriginChoiceKeys.WeaponKind:
                    AddProficiency(ProficiencyType.Weapon, item.Index, source);
                    break;
                case OriginChoiceKeys.SavingThrowKind:
                    AddProficiency(ProficiencyType.SavingThrow, item.Index, source);
                    break;
                case OriginChoiceKeys.CantripKind or OriginChoiceKeys.SpellKind:
                    AddSpell(item.Index, CharacterSpell.OriginClassIndex, isPrepared: true, alwaysPrepared: true);
                    break;
            }
        }
    }

    /// <summary>
    /// Brings the fixed grants of the race and of the subrace (phase 25) in line with the catalog: each kind is an origin
    /// choice <c>race.grant.&lt;kind&gt;</c> / <c>race.subrace.grant.&lt;kind&gt;</c> whose effects are proficiencies (source
    /// Race) and always-prepared spells without a class (<see cref="CharacterSpell.OriginClassIndex"/>). Spells with a
    /// <c>minLevel</c> above the total level are left out until the character reaches it. Grants no longer in the catalog
    /// are removed (changing the race already drops them, see <see cref="DropStaleOriginChoices"/>). True when something changed.
    /// </summary>
    public bool SyncRaceGrants(OptionGrants? race, OptionGrants? subrace, DateTimeOffset now)
    {
        var changed = SyncGrants(OriginChoiceKeys.RaceGrantPrefix, RaceIndex is null ? null : race, "raza", now);
        changed |= SyncGrants(OriginChoiceKeys.SubraceGrantPrefix, SubraceIndex is null ? null : subrace, "subraza", now);
        return changed;
    }

    private bool SyncGrants(string prefix, OptionGrants? grants, string origin, DateTimeOffset now)
    {
        var level = Math.Max(1, TotalLevel);
        var desired = new (string Suffix, string Kind, string Name, IEnumerable<string> Items)[]
        {
            ("skills", OriginChoiceKeys.SkillKind, "Habilidades", grants?.Skills ?? []),
            ("armor", OriginChoiceKeys.ArmorKind, "Armaduras", grants?.Armor ?? []),
            ("weapons", OriginChoiceKeys.WeaponKind, "Armas", grants?.Weapons ?? []),
            ("tools", OriginChoiceKeys.ToolKind, "Herramientas", grants?.Tools ?? []),
            ("languages", OriginChoiceKeys.LanguageKind, "Idiomas", grants?.Languages ?? []),
            ("saving-throws", OriginChoiceKeys.SavingThrowKind, "Salvaciones", grants?.SavingThrows ?? []),
            ("cantrips", OriginChoiceKeys.CantripKind, "Trucos", grants?.Cantrips ?? []),
            ("spells", OriginChoiceKeys.SpellKind, "Conjuros", (grants?.Spells ?? []).Where(s => (s.MinLevel ?? 0) <= level).Select(s => s.Index)),
        };

        var changed = false;
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (suffix, kind, name, items) in desired)
        {
            var key = prefix + suffix;
            keys.Add(key);
            var indexes = items.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.Trim()).Distinct(StringComparer.Ordinal).ToList();
            var existing = OriginChoice(key);
            if (indexes.Count == 0)
            {
                changed |= existing is not null && RemoveOriginChoices(k => k == key);
                continue;
            }

            if (existing is not null && existing.Selection.Kind == kind && existing.Selection.Selected.Select(s => s.Index).SequenceEqual(indexes, StringComparer.Ordinal))
            {
                // Same grant: make sure its effects are there (a full replacement of the proficiencies may have dropped them).
                ApplyOriginChoiceEffects(existing);
                continue;
            }

            RecordOriginChoice(
                key,
                new ChoiceSelection { Kind = kind, Name = $"{name} ({origin})", Selected = indexes.Select(i => new ChoiceItem(i, i)).ToList() },
                now);
            changed = true;
        }

        changed |= RemoveOriginChoices(k => k.StartsWith(prefix, StringComparison.Ordinal) && !keys.Contains(k));
        return changed;
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
