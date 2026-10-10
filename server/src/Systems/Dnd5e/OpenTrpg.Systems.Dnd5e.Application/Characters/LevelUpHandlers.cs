using System.Text.Json;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Application;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions.Persistence;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Application.Characters;

/// <summary>
/// The level-up plan of a character for a class (<c>?classIndex=</c>, the main class by default). The owner
/// needs a level granted by a DM (409 otherwise); DMs can always look. Other players get 403.
/// </summary>
public sealed class GetLevelUpPlanHandler(Dnd5eCharacterLoader characters, LevelUpPlanner planner)
{
    public async Task<LevelUpPlanDto> HandleAsync(Guid currentUserId, Guid characterId, string? classIndex, CancellationToken cancellationToken = default)
    {
        var loaded = await characters.LoadAsync(characterId, currentUserId, cancellationToken);
        LevelUpRules.EnsureCanLevelUp(loaded, currentUserId);
        var plan = await planner.BuildAsync(loaded.Character, classIndex, new HashSet<string>(StringComparer.Ordinal), cancellationToken);
        return plan.ToDto();
    }
}

/// <summary>
/// Applies a level-up: validates the class (multiclassing prerequisites), the hit points rolled (1..die) and the
/// answers to every choice of the plan, then in one transaction adds the class level, records the choices and the
/// roll, applies their effects (subclass, expertise, proficiencies, spells, grants), recalculates the sheet (the
/// hit points grow by the roll + Con, at least 1) and clears the pending level. Owner with a granted level, or DMs.
/// </summary>
public sealed class ApplyLevelUpHandler(
    Dnd5eCharacterLoader characters,
    LevelUpPlanner planner,
    InvalidChoicesPlanner invalidChoices,
    ICatalogRepository catalog,
    ICharacterSheetService sheets,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task<CharacterDetailDto> HandleAsync(Guid currentUserId, Guid characterId, LevelUpRequest request, CancellationToken cancellationToken = default)
    {
        var loaded = await characters.LoadAsync(characterId, currentUserId, cancellationToken);
        LevelUpRules.EnsureCanLevelUp(loaded, currentUserId);
        var character = loaded.Character;
        var now = clock.UtcNow;
        await ApplyAsync(character, request, now, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.CharacterUpdatedAsync(character.CampaignId, character.Id, now, cancellationToken);
        await notifier.NotifyAsync(new CampaignEvent(Dnd5eEventTypes.LevelUpCompleted, character.CampaignId, character.Id, null, now), cancellationToken);
        return await sheets.BuildDetailAsync(character, cancellationToken);
    }

    /// <summary>Applies the level-up (checked against the plan) and recalculates the sheet; the caller saves.</summary>
    public async Task ApplyAsync(Dnd5eCharacter character, LevelUpRequest request, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var answers = LevelChoiceAnswers.Parse(request.Choices ?? []);
        var plan = await planner.BuildAsync(character, request.ClassIndex, LevelChoiceAnswers.PendingPicks(answers), cancellationToken, LevelChoiceAnswers.PendingChoices(answers));

        if (!plan.SelectedClass.Allowed)
        {
            throw AppException.Validation("classIndex", plan.SelectedClass.Reason ?? "No puedes subir de nivel en esta clase.");
        }

        if (request.HitPointsRolled < 1 || request.HitPointsRolled > plan.Class.HitDie)
        {
            throw AppException.Validation("hitPointsRolled", $"La tirada de puntos de golpe debe estar entre 1 y {plan.Class.HitDie} (d{plan.Class.HitDie}).");
        }

        var resolved = Resolve(plan, answers);

        // ---- Apply (one SaveChanges: one transaction) -------------------------------------------------
        var oldMax = plan.Sheet.HitPointsMax;
        var classIndex = plan.Class.Index;

        // Invalid options and feats are swapped first, at the class levels they had.
        if (plan.Replacements.Count > 0)
        {
            await invalidChoices.ApplyAsync(character, plan.Replacements, request.Choices ?? [], now, cancellationToken);
        }

        var entry = character.AdvanceClass(classIndex, now);
        if (plan.IsNew)
        {
            foreach (var (type, key) in MulticlassRules.ProficienciesFor(classIndex, plan.Class.Multiclassing))
            {
                character.AddProficiency(type, key, ProficiencySource.Class);
            }
        }

        // Proficiencies that grow with the levels of a class taken as a new class (never the first class).
        if (entry.Order > 0 && plan.Class.Multiclassing is { ProficienciesByLevel.Count: > 0 } growing)
        {
            foreach (var (_, gained) in growing.ProficienciesByLevel.Where(p => p.Key <= entry.Level))
            {
                foreach (var (list, type) in new[] { (gained.Armor, ProficiencyType.Armor), (gained.Weapons, ProficiencyType.Weapon), (gained.Tools, ProficiencyType.Tool) })
                {
                    foreach (var key in list)
                    {
                        character.AddProficiency(type, key, ProficiencySource.Class);
                    }
                }
            }
        }

        character.RecordChoice(
            entry.Level,
            classIndex,
            CharacterChoice.HitPointsKey,
            new ChoiceSelection { Kind = ChoiceSelection.HitPointsKind, Name = "Puntos de golpe", Roll = request.HitPointsRolled },
            now);
        if (character.HpMode == HpMode.Manual)
        {
            character.RaiseHitPointsMaxOverride(Math.Max(1, request.HitPointsRolled + plan.ConModifier));
        }

        // Expertise goes last: it doubles skills picked or granted (subclass, options) in this same level-up.
        foreach (var (choice, selection) in resolved)
        {
            character.RecordChoice(entry.Level, classIndex, choice.Rule.Key, selection, now);
        }

        await LevelChoiceAnswers.ApplyAllEffectsAsync(catalog, character, classIndex, resolved, [], now, cancellationToken);

        var sheet = await sheets.RecalculateAsync(character, cancellationToken);
        character.GainHitPoints(sheet.HitPointsMax - oldMax, sheet.HitPointsMax, now);

        // A level in a class that prepares spells (the first one with slots included, e.g. paladin 2) lets the
        // character change its preparation: the app forces the preparation screen.
        if (sheet.PreparingClasses.Any(s => s.ClassIndex == classIndex))
        {
            character.RequireSpellPreparation(SpellPreparationReason.LevelUp, now);
        }
    }

    // ---- Validation ----------------------------------------------------------------------------------

    /// <summary>Validates every answer against the plan and turns it into the selection to record.</summary>
    private static List<(PlannedChoice Choice, ChoiceSelection Selection)> Resolve(LevelUpPlan plan, Dictionary<string, LevelChoiceAnswers.Answer> answers)
    {
        var subclassChoice = plan.Choices.FirstOrDefault(c => c.Rule.Kind == LevelChoiceKind.Subclass);
        var subclass = plan.Entry?.SubclassIndex;
        if (subclassChoice is not null && answers.TryGetValue(subclassChoice.Rule.Key, out var subclassAnswer) && subclassAnswer.Selected.Count == 1)
        {
            subclass = subclassAnswer.Selected[0];
        }

        var result = new List<(PlannedChoice, ChoiceSelection)>();
        var used = plan.Replacements.Select(r => r.Choice.Rule.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var choice in plan.Choices.Where(c => !InvalidChoicesPlanner.IsReplacement(c.Rule.Key)))
        {
            var rule = choice.Rule;
            if (rule.SubclassIndex is { } ruleSubclass && ruleSubclass != subclass)
            {
                continue;
            }

            // A choice of an option only applies when that option is picked in this same level-up (an answer left over
            // from another pick is ignored).
            if (choice.Requires is { } requires
                && !(answers.TryGetValue(requires.ChoiceKey, out var parent) && (parent.Selected.Contains(requires.Index, StringComparer.Ordinal) || parent.Feat == requires.Index)))
            {
                used.Add(rule.Key);
                continue;
            }

            if (!answers.TryGetValue(rule.Key, out var answer))
            {
                if (choice.Required > 0)
                {
                    throw AppException.Validation("choices", $"Falta la elección «{rule.Name}».");
                }

                continue;
            }

            used.Add(rule.Key);
            var selection = LevelChoiceAnswers.Resolve(plan.Sheet, choice, answer);
            if (selection is not null)
            {
                result.Add((choice, selection));
            }
        }

        if (answers.Keys.FirstOrDefault(k => !used.Contains(k)) is { } unknown)
        {
            throw AppException.Validation("choices", $"La elección '{unknown}' no corresponde a esta subida de nivel.");
        }

        LevelChoiceAnswers.EnsureOptionsPickedOnce(result, answers);
        return result;
    }

}

/// <summary>Who may level a character up.</summary>
public static class LevelUpRules
{
    /// <summary>The owner (with a level granted by a DM) and DMs; 403 for other players, 409 without a grant or at level 20.</summary>
    public static void EnsureCanLevelUp(LoadedDnd5eCharacter loaded, Guid actorUserId)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        if (!loaded.Character.CanViewSheet(actorUserId, loaded.IsDm))
        {
            throw AppException.Forbidden("Solo el dueño del personaje o un DM pueden subirlo de nivel.");
        }

        loaded.Character.LevelUpTarget(loaded.IsDm);
    }
}
