using System.Text.Json;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Application.Characters;

/// <summary>
/// The answers to level choices, shared by the level-up (<see cref="ApplyLevelUpHandler"/>) and by the level-1 choices of
/// the first class answered with the origin choices (<see cref="OriginChoicesHandler"/>): parsing, validation against a
/// <see cref="PlannedChoice"/> and the immediate effects of the recorded selection.
/// </summary>
public static class LevelChoiceAnswers
{
    /// <summary>An answer as sent: picks (normalized indexes or texts), replaced picks, or an ASI/feat object.</summary>
    public sealed record Answer(string Key, List<string> Selected, List<string> Replaced, Dictionary<string, int>? Asi, string? Feat, string? Ability, List<string>? RemovedSavingThrows = null);

    public static Dictionary<string, Answer> Parse(IReadOnlyList<LevelUpChoiceAnswer> choices)
    {
        var result = new Dictionary<string, Answer>(StringComparer.Ordinal);
        foreach (var choice in choices)
        {
            var key = choice?.Key?.Trim();
            if (string.IsNullOrEmpty(key))
            {
                throw AppException.Validation("choices", "Cada elección necesita su key.");
            }

            if (result.ContainsKey(key))
            {
                throw AppException.Validation("choices", $"La elección '{key}' está repetida.");
            }

            var replaced = (choice!.Replaced ?? []).Select(r => LevelUpPlanner.NormalizeIndex(r ?? string.Empty)).ToList();
            var selected = choice.Selected;
            switch (selected.ValueKind)
            {
                case JsonValueKind.Undefined or JsonValueKind.Null:
                    result[key] = new Answer(key, [], replaced, null, null, null);
                    break;
                case JsonValueKind.Array:
                    var picks = new List<string>();
                    foreach (var element in selected.EnumerateArray())
                    {
                        if (element.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(element.GetString()))
                        {
                            throw AppException.Validation("choices", $"La elección '{key}' solo admite textos en selected.");
                        }

                        picks.Add(LevelUpPlanner.NormalizeIndex(element.GetString()!));
                    }

                    result[key] = new Answer(key, picks, replaced, null, null, null);
                    break;
                case JsonValueKind.Object:
                    result[key] = ParseImprovement(key, selected, replaced);
                    break;
                default:
                    throw AppException.Validation("choices", $"La elección '{key}' tiene un selected no válido.");
            }
        }

        return result;
    }

    /// <summary>
    /// <c>{ "asi": { "str": 1, "dex": 1 } }</c> or <c>{ "feat": "grappler", "ability": "str" }</c>, plus
    /// <c>"removedSavingThrows": ["con"]</c> for a feat that asks which saving throws are lost.
    /// </summary>
    private static Answer ParseImprovement(string key, JsonElement selected, List<string> replaced)
    {
        Dictionary<string, int>? asi = null;
        string? feat = null;
        string? ability = null;
        List<string>? removed = null;
        foreach (var property in selected.EnumerateObject())
        {
            switch (property.Name.ToLowerInvariant())
            {
                case "asi" when property.Value.ValueKind == JsonValueKind.Object:
                    asi = new Dictionary<string, int>(StringComparer.Ordinal);
                    foreach (var score in property.Value.EnumerateObject())
                    {
                        if (score.Value.ValueKind != JsonValueKind.Number || !score.Value.TryGetInt32(out var amount))
                        {
                            throw AppException.Validation("choices", "La mejora de característica solo admite números.");
                        }

                        var name = score.Name.Trim().ToLowerInvariant();
                        asi[name] = asi.GetValueOrDefault(name) + amount;
                    }

                    break;
                case "feat" when property.Value.ValueKind == JsonValueKind.String:
                    feat = property.Value.GetString()?.Trim();
                    break;
                case "ability" when property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Null:
                    ability = property.Value.GetString()?.Trim().ToLowerInvariant();
                    break;
                case "removedsavingthrows" when property.Value.ValueKind == JsonValueKind.Array:
                    removed = property.Value.EnumerateArray()
                        .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString()?.Trim().ToLowerInvariant() ?? string.Empty : string.Empty)
                        .ToList();
                    break;
                default:
                    throw AppException.Validation("choices", $"La elección '{key}' tiene un campo desconocido: {property.Name}.");
            }
        }

        return new Answer(key, [], replaced, asi, string.IsNullOrEmpty(feat) ? null : feat, string.IsNullOrEmpty(ability) ? null : ability, removed);
    }

    /// <summary>
    /// An option (or feat) offered by two choices of the same level-up (an improvement that also offers a set and a
    /// choice of that set) is taken in one of them only, unless it is repeatable.
    /// </summary>
    public static void EnsureOptionsPickedOnce(List<(PlannedChoice Choice, ChoiceSelection Selection)> resolved, Dictionary<string, Answer> answers)
    {
        var picked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (choice, _) in resolved)
        {
            var answer = answers[choice.Rule.Key];
            var picks = answer.Feat is { } feat ? [.. answer.Selected, feat] : answer.Selected;
            foreach (var option in picks.Select(p => choice.Options.FirstOrDefault(o => o.Index == p)).OfType<PlannedOption>())
            {
                if (option.Definition is { Repeatable: false } && !picked.Add(option.Index))
                {
                    throw AppException.Validation("choices", $"«{option.Name}» solo puede elegirse una vez en esta subida de nivel.");
                }
            }
        }
    }

    public static ChoiceSelection? ResolvePicks(PlannedChoice choice, Answer answer)
    {
        var rule = choice.Rule;
        if (answer.Asi is not null || answer.Feat is not null)
        {
            throw AppException.Validation("choices", $"La elección «{rule.Name}» espera una lista en selected.");
        }

        if (answer.Replaced.Count > 0 && !rule.Replaces)
        {
            throw AppException.Validation("choices", $"La elección «{rule.Name}» no permite sustituir.");
        }

        if (answer.Replaced.Count > 1)
        {
            throw AppException.Validation("choices", $"En «{rule.Name}» solo se puede sustituir una elección por nivel.");
        }

        var replaced = new List<ChoiceItem>();
        foreach (var index in answer.Replaced)
        {
            replaced.Add(choice.Known.FirstOrDefault(k => k.Index == index)
                ?? throw AppException.Validation("choices", $"«{index}» no se puede sustituir en «{rule.Name}»: no lo tienes."));
        }

        if (answer.Selected.Distinct(StringComparer.Ordinal).Count() != answer.Selected.Count)
        {
            throw AppException.Validation("choices", $"La elección «{rule.Name}» repite una opción.");
        }

        var expected = choice.Required + replaced.Count;
        if (answer.Selected.Count != expected)
        {
            throw AppException.Validation("choices", $"La elección «{rule.Name}» necesita exactamente {expected} {(expected == 1 ? "opción" : "opciones")}.");
        }

        var selected = new List<ChoiceItem>();
        foreach (var index in answer.Selected)
        {
            if (choice.FreeText)
            {
                if (index.Length > Dnd5eCharacter.IndexMaxLength)
                {
                    throw AppException.Validation("choices", $"«{rule.Name}»: cada valor admite como máximo {Dnd5eCharacter.IndexMaxLength} caracteres.");
                }

                selected.Add(new ChoiceItem(index, index));
                continue;
            }

            var option = choice.Option(index)
                ?? throw AppException.Validation("choices", $"«{index}» no es una opción de «{rule.Name}».");
            if (!option.Eligible)
            {
                throw AppException.Validation("choices", $"«{option.Name}» no cumple los requisitos: {option.Reason}");
            }

            if (replaced.Any(r => r.Index == index))
            {
                throw AppException.Validation("choices", $"«{option.Name}» no puede sustituirse por sí misma.");
            }

            selected.Add(new ChoiceItem(option.Index, option.Name));
        }

        return selected.Count == 0 && replaced.Count == 0
            ? null
            : new ChoiceSelection { Kind = rule.Kind.ToString(), Name = rule.Name, SetId = rule.SetId, Selected = selected, Replaced = replaced };
    }

    /// <summary>Ability Score Improvement (+2 to one ability or +1 to two, never above 20) or an eligible feat.</summary>
    public static ChoiceSelection ResolveImprovement(CharacterSheet sheet, PlannedChoice choice, Answer answer)
    {
        var rule = choice.Rule;
        int Natural(string ability) => sheet.Breakdowns.TryGetValue($"ability.{ability}", out var breakdown)
            ? breakdown.Parts.Where(p => p.Source is not (BreakdownSources.Item or BreakdownSources.Override)).Sum(p => p.Value)
            : sheet.Abilities[ability].Score;

        void EnsureLimit(string ability, int amount)
        {
            var max = sheet.AbilityMaxScores.GetValueOrDefault(ability, SheetCalculator.ImprovementMaxScore);
            if (Natural(ability) + amount > max)
            {
                throw AppException.Validation("choices", $"La mejora no puede subir {BreakdownLabels.Ability(ability)} por encima de {max}.");
            }
        }

        if (answer.Selected.Count > 0 || answer.Replaced.Count > 0 || (answer.Asi is null) == (answer.Feat is null))
        {
            throw AppException.Validation("choices", $"«{rule.Name}»: envía {{ \"asi\": {{...}} }} o {{ \"feat\": \"...\" }}.");
        }

        if (answer.Asi is { } asi)
        {
            var increases = asi.Where(a => a.Value != 0).ToDictionary(a => a.Key, a => a.Value, StringComparer.Ordinal);
            if (increases.Keys.Any(a => !Abilities.IsValid(a)) || increases.Values.Any(v => v is < 1 or > 2) || increases.Values.Sum() != 2)
            {
                throw AppException.Validation("choices", "La mejora de característica es +2 a una característica o +1 a dos.");
            }

            foreach (var (ability, amount) in increases)
            {
                EnsureLimit(ability, amount);
            }

            return new ChoiceSelection { Kind = rule.Kind.ToString(), Name = rule.Name, Asi = increases };
        }

        var feat = choice.Option(answer.Feat!)
            ?? throw AppException.Validation("choices", $"«{answer.Feat}» no es una dote disponible.");
        if (!feat.Eligible)
        {
            throw AppException.Validation("choices", $"«{feat.Name}» no cumple los requisitos: {feat.Reason}");
        }

        string? raised = null;
        if (feat.Definition?.AbilityIncrease is { } increase)
        {
            raised = answer.Ability ?? (increase.From.Count == 1 ? increase.From[0] : null);
            if (raised is null || !Abilities.IsValid(raised) || (increase.From.Count > 0 && !increase.From.Contains(raised, StringComparer.Ordinal)))
            {
                throw AppException.Validation("choices", $"«{feat.Name}»: elige la característica que sube ({(increase.From.Count == 0 ? "cualquiera" : string.Join(", ", increase.From))}).");
            }

            EnsureLimit(raised, increase.Amount);
        }
        else if (answer.Ability is not null)
        {
            throw AppException.Validation("choices", $"«{feat.Name}» no sube ninguna característica.");
        }

        return new ChoiceSelection
        {
            Kind = rule.Kind.ToString(),
            Name = rule.Name,
            SetId = feat.Definition?.SetId ?? OptionSets.Feats,
            Feat = new ChoiceItem(feat.Index, feat.Name),
            Ability = raised,
            RemovedSavingThrows = OriginChoicesHandler.RemovedSavingThrowsOf(feat, answer.RemovedSavingThrows),
        };
    }

    // ---- Effects -------------------------------------------------------------------------------------

    /// <summary>Immediate effects of a choice; option effects (modifiers, increases, resources) live in the sheet.</summary>
    public static void ApplyEffects(Dnd5eCharacter character, string classIndex, PlannedChoice choice, ChoiceSelection selection, List<string> replacedOptions, DateTimeOffset now)
    {
        switch (choice.Rule.Kind)
        {
            case LevelChoiceKind.Subclass when selection.Selected.Count == 1:
                character.SetSubclass(classIndex, selection.Selected[0].Index, now);
                break;
            case LevelChoiceKind.Expertise:
                foreach (var item in selection.Selected)
                {
                    character.GrantExpertise(item.Index == "thieves-tools" ? ProficiencyType.Tool : ProficiencyType.Skill, item.Index);
                }

                break;
            case LevelChoiceKind.Skill:
                foreach (var item in selection.Selected)
                {
                    character.AddProficiency(ProficiencyType.Skill, item.Index, ProficiencySource.Class);
                }

                break;
            case LevelChoiceKind.SavingThrow:
                foreach (var item in selection.Selected)
                {
                    character.AddProficiency(ProficiencyType.SavingThrow, item.Index, ProficiencySource.Class);
                }

                break;
            case LevelChoiceKind.Weapon:
                foreach (var item in selection.Selected)
                {
                    character.AddProficiency(ProficiencyType.Weapon, item.Index, ProficiencySource.Class);
                }

                break;
            case LevelChoiceKind.Resistance:
                // Read from the stored selection by the sheet (ChoiceEffects.ChosenResistances).
                break;
            case LevelChoiceKind.Language or LevelChoiceKind.Tool:
                var type = choice.Rule.Kind == LevelChoiceKind.Language ? ProficiencyType.Language : ProficiencyType.Tool;
                foreach (var item in selection.Selected)
                {
                    character.AddProficiency(type, item.Index, ProficiencySource.Class);
                }

                break;
            case LevelChoiceKind.CantripsKnown or LevelChoiceKind.SpellsKnown or LevelChoiceKind.SpellbookSpells:
                foreach (var item in selection.Replaced)
                {
                    character.RemoveSpell(item.Index, classIndex);
                }

                // Wizard spellbook spells are known, not prepared.
                var prepared = choice.Rule.Kind != LevelChoiceKind.SpellbookSpells;
                foreach (var item in selection.Selected)
                {
                    character.AddSpell(item.Index, classIndex, prepared, alwaysPrepared: false);
                }

                break;
            case LevelChoiceKind.OptionSet or LevelChoiceKind.Custom:
                replacedOptions.AddRange(selection.Replaced.Select(r => r.Index));
                break;
        }
    }

    /// <summary>Indexes picked in the answers (options and feats): they count for the prerequisites of the same request.</summary>
    public static HashSet<string> PendingPicks(IReadOnlyDictionary<string, Answer> answers) =>
        answers.Values.SelectMany(a => a.Feat is null ? a.Selected : [.. a.Selected, a.Feat]).ToHashSet(StringComparer.Ordinal);

    /// <summary>The picks of each answer by choice key (the feat included).</summary>
    public static Dictionary<string, IReadOnlyList<string>> PendingChoices(IReadOnlyDictionary<string, Answer> answers) =>
        answers.Values.ToDictionary(
            a => a.Key,
            a => (IReadOnlyList<string>)(a.Feat is null ? a.Selected : [.. a.Selected, a.Feat]),
            StringComparer.Ordinal);

    /// <summary>Validates an answer against its planned choice: an ability improvement or feat, or picks.</summary>
    public static ChoiceSelection? Resolve(CharacterSheet sheet, PlannedChoice choice, Answer answer) =>
        choice.Rule.Kind == LevelChoiceKind.AsiOrFeat ? ResolveImprovement(sheet, choice, answer) : ResolvePicks(choice, answer);

    /// <summary>
    /// Effects of the selections recorded at once: expertise goes last, since it doubles skills picked or granted
    /// (subclass, options) in the same request.
    /// </summary>
    public static async Task ApplyAllEffectsAsync(
        ICatalogRepository catalog,
        Dnd5eCharacter character,
        string classIndex,
        IReadOnlyList<(PlannedChoice Choice, ChoiceSelection Selection)> resolved,
        List<string> replacedOptions,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        foreach (var (choice, selection) in resolved.Where(r => r.Choice.Rule.Kind != LevelChoiceKind.Expertise))
        {
            ApplyEffects(character, classIndex, choice, selection, replacedOptions, now);
        }

        await ChoiceGrants.ApplyAsync(catalog, character, replacedOptions, now, cancellationToken);
        foreach (var (choice, selection) in resolved.Where(r => r.Choice.Rule.Kind == LevelChoiceKind.Expertise))
        {
            ApplyEffects(character, classIndex, choice, selection, replacedOptions, now);
        }
    }
}
