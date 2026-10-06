using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Common;
using Dnd.Domain.Catalog;
using Dnd.Domain.Characters;
using Dnd.Domain.Items;

namespace Dnd.Application.Characters;

/// <summary>An option of a planned choice, with its catalog definition when it comes from an option set.</summary>
public sealed record PlannedOption(
    string Index,
    string Name,
    IReadOnlyList<string> Description,
    string? PrerequisitesText,
    bool Eligible,
    string? Reason,
    int? SpellLevel,
    OptionDefinition? Definition,
    IReadOnlyList<EffectPreviewDto> Preview);

/// <summary>A choice of the plan: the rule, how many picks are required, the options and the replaceable picks.</summary>
public sealed record PlannedChoice(LevelChoiceRule Rule, int Required, bool FreeText, IReadOnlyList<PlannedOption> Options, IReadOnlyList<ChoiceItem> Known)
{
    public PlannedOption? Option(string index) => Options.FirstOrDefault(o => o.Index == index);

    public LevelUpChoiceDto ToDto() => new(
        Rule.Key,
        Rule.Name,
        Rule.Kind.ToString(),
        Rule.Choose,
        Required,
        Rule.Replaces,
        Rule.Cumulative,
        Rule.Note,
        Rule.SubclassIndex,
        Rule.SetId,
        FreeText,
        Options.Select(o => new LevelUpOptionDto(
                o.Index,
                o.Name,
                o.Description,
                o.PrerequisitesText,
                o.Eligible,
                o.Reason,
                o.SpellLevel,
                o.Preview,
                o.Definition?.AbilityIncrease is { } increase ? new AbilityIncreaseDto(increase.Amount, increase.From) : null))
            .ToList(),
        Known.Select(ChoiceItemDto.From).ToList());
}

/// <summary>The level-up of a character in one class, as computed by <see cref="LevelUpPlanner"/>.</summary>
public sealed class LevelUpPlan
{
    public required Character Character { get; init; }

    /// <summary>Sheet before the level-up.</summary>
    public required CharacterSheet Sheet { get; init; }

    public required ClassDefinition Class { get; init; }

    /// <summary>Entry of the class (null when it is a new class).</summary>
    public required CharacterClassLevel? Entry { get; init; }

    public required int NewClassLevel { get; init; }

    public required IReadOnlyList<LevelUpClassDto> Classes { get; init; }

    public required IReadOnlyList<LevelUpFeatureDto> AutomaticFeatures { get; init; }

    public required IReadOnlyList<PlannedChoice> Choices { get; init; }

    public required LevelUpSpellcastingDto? Spellcasting { get; init; }

    public bool IsNew => Entry is null;

    public LevelUpClassDto SelectedClass => Classes.First(c => c.ClassIndex == Class.Index);

    public int ConModifier => Sheet.Modifier(Abilities.Con);

    public LevelUpPlanDto ToDto() => new(
        Character.Id,
        Character.TotalLevel,
        Character.TotalLevel + 1,
        Class.Index,
        NewClassLevel,
        Class.HitDie,
        ConModifier,
        Classes,
        AutomaticFeatures,
        Choices.Select(c => c.ToDto()).ToList(),
        Spellcasting);
}

/// <summary>
/// Builds the level-up plan of a character for a class from the level choice catalog: the rules of the class
/// (and of its subclass) at the new class level, with their options and eligibility evaluated against the
/// character as it is now. Picks of the same request (<c>pendingPicks</c>) count for prerequisites such as a
/// pact boon or a cantrip chosen at the same level.
/// </summary>
public sealed class LevelUpPlanner(ICatalogRepository catalog, ICharacterSheetService sheets)
{
    /// <summary>Feature indexes of the dataset that are options of the shared <c>fighting-styles</c> set.</summary>
    private static readonly string[] FightingStyleAliasPrefixes = ["fighter-fighting-style-", "ranger-fighting-style-", "paladin-fighting-style-"];

    private const string FightingStylePrefix = "fighting-style-";
    private const string ThievesTools = "thieves-tools";

    /// <summary>"fighter-fighting-style-defense" → "fighting-style-defense"; other indexes unchanged.</summary>
    public static string NormalizeIndex(string index)
    {
        var trimmed = index.Trim();
        foreach (var prefix in FightingStyleAliasPrefixes)
        {
            if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
            {
                return FightingStylePrefix + trimmed[prefix.Length..];
            }
        }

        return trimmed;
    }

    public async Task<LevelUpPlan> BuildAsync(
        Character character,
        string? classIndex,
        IReadOnlySet<string> pendingPicks,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(pendingPicks);

        var allClasses = await catalog.ListClassesAsync(cancellationToken);
        var index = string.IsNullOrWhiteSpace(classIndex) ? character.OrderedClasses.FirstOrDefault()?.ClassIndex : classIndex.Trim();
        if (index is null)
        {
            throw AppException.Validation("classIndex", "Indica la clase en la que sube de nivel.");
        }

        var definition = allClasses.FirstOrDefault(c => c.Index == index)
            ?? throw AppException.Validation("classIndex", $"La clase '{index}' no existe en el catálogo.");
        var sheet = await sheets.CalculateAsync(character, cancellationToken);
        var entry = character.Classes.FirstOrDefault(c => c.ClassIndex == definition.Index);
        var newLevel = (entry?.Level ?? 0) + 1;
        if (newLevel > 20)
        {
            throw AppException.Validation("classIndex", "La clase ya está en el nivel 20.");
        }

        var classes = ClassOptions(character, sheet, allClasses);
        var classLevels = await catalog.ListClassLevelsAsync(definition.Index, cancellationToken);
        var classLevel = classLevels.FirstOrDefault(l => l.Level == newLevel);
        var subclasses = await catalog.ListSubclassesAsync(definition.Index, cancellationToken);
        var rules = SelectRules(await catalog.ListLevelChoiceRulesAsync(definition.Index, cancellationToken), entry?.SubclassIndex, newLevel);

        var setIds = rules.Select(r => r.Kind == LevelChoiceKind.AsiOrFeat ? OptionSets.Feats : r.SetId).OfType<string>().ToHashSet(StringComparer.Ordinal);
        if (setIds.Count > 0)
        {
            setIds.Add(OptionSets.PactBoons);
        }

        var options = await catalog.ListOptionsBySetAsync(setIds, cancellationToken);
        var needsSpells = rules.Any(r => r.Kind is LevelChoiceKind.CantripsKnown or LevelChoiceKind.SpellsKnown or LevelChoiceKind.SpellbookSpells or LevelChoiceKind.Custom)
            || options.Any(o => o.Prerequisites.Cantrip is not null);
        var spells = needsSpells
            ? await catalog.ListAllSpellsAsync(cancellationToken)
            : await catalog.ListSpellsByIndexAsync(character.Spells.Select(s => s.SpellIndex).Distinct(StringComparer.Ordinal).ToList(), cancellationToken);
        var skills = rules.Any(r => r.Kind is LevelChoiceKind.Skill or LevelChoiceKind.Expertise)
            ? await catalog.ListSkillsAsync(cancellationToken)
            : [];

        var context = new PlanContext(character, sheet, definition, newLevel, pendingPicks, options, spells, skills, subclasses, classLevel);
        var choices = rules.Select(context.Plan).ToList();

        return new LevelUpPlan
        {
            Character = character,
            Sheet = sheet,
            Class = definition,
            Entry = entry,
            NewClassLevel = newLevel,
            Classes = classes,
            AutomaticFeatures = await AutomaticFeaturesAsync(definition.Index, entry?.SubclassIndex, newLevel, classLevel, rules, subclasses, cancellationToken),
            Choices = choices,
            Spellcasting = Spellcasting(character, definition, classLevel, newLevel, spells),
        };
    }

    /// <summary>
    /// The current classes (always allowed) and every other catalog class, allowed when the multiclassing
    /// prerequisites of the new class and of all current ones are met. The main class goes first.
    /// </summary>
    private static List<LevelUpClassDto> ClassOptions(Character character, CharacterSheet sheet, IReadOnlyList<ClassDefinition> allClasses)
    {
        int Score(string ability) => sheet.Abilities[ability].Score;
        var current = character.OrderedClasses;
        var result = new List<LevelUpClassDto>();
        foreach (var entry in current)
        {
            var definition = allClasses.FirstOrDefault(c => c.Index == entry.ClassIndex);
            result.Add(new LevelUpClassDto(
                entry.ClassIndex,
                definition?.Name ?? entry.ClassIndex,
                definition is not null && entry.Level < 20,
                definition is null ? "La clase ya no está en el catálogo." : entry.Level >= 20 ? "La clase ya está en el nivel 20." : null,
                definition?.HitDie ?? 0,
                false,
                entry.Level,
                entry.SubclassIndex));
        }

        foreach (var definition in allClasses.Where(c => current.All(e => e.ClassIndex != c.Index)))
        {
            var reason = current.Count == 0 ? null : MulticlassRules.WhyNot(definition.Index, current.Select(c => c.ClassIndex), Score);
            result.Add(new LevelUpClassDto(definition.Index, definition.Name, reason is null, reason, definition.HitDie, true, 0, null));
        }

        return result;
    }

    /// <summary>
    /// Rules at the new level for the base class and the current subclass. When the class has no subclass yet and
    /// its subclass level has been reached, the subclass is asked now (late characters included) together with the
    /// rules of every subclass at this level (each flagged with its subclass). Rules that allow replacements from an
    /// earlier level and have no rule at this level appear with <c>choose</c> 0 (only replacements).
    /// </summary>
    private static List<LevelChoiceRule> SelectRules(IReadOnlyList<LevelChoiceRule> all, string? subclass, int newLevel)
    {
        var subclassRule = all.Where(r => r.Kind == LevelChoiceKind.Subclass && r.SubclassIndex is null).OrderBy(r => r.Level).FirstOrDefault();
        var choosingSubclass = subclass is null && subclassRule is not null && subclassRule.Level <= newLevel;

        var result = new List<LevelChoiceRule>();
        if (choosingSubclass)
        {
            result.Add(AtLevel(subclassRule!, newLevel, subclassRule!.Choose));
        }

        result.AddRange(all.Where(r =>
            r.Level == newLevel
            && r.Kind != LevelChoiceKind.Subclass
            && (r.SubclassIndex is null || r.SubclassIndex == subclass || (choosingSubclass && r.SubclassIndex is not null)))
            .OrderBy(r => r.SubclassIndex is null ? 0 : 1)
            .ThenBy(r => r.Kind == LevelChoiceKind.AsiOrFeat ? 0 : 1));

        var replaceable = all
            .Where(r => r.Replaces && r.Level < newLevel && (r.SubclassIndex is null || r.SubclassIndex == subclass))
            .GroupBy(r => r.Key, StringComparer.Ordinal)
            .Where(g => result.All(r => r.Key != g.Key))
            .Select(g => AtLevel(g.MaxBy(r => r.Level)!, newLevel, 0));
        result.AddRange(replaceable);
        return result;
    }

    private static LevelChoiceRule AtLevel(LevelChoiceRule rule, int level, int choose) => new()
    {
        Id = rule.Id,
        ClassIndex = rule.ClassIndex,
        SubclassIndex = rule.SubclassIndex,
        Level = level,
        Key = rule.Key,
        Name = rule.Name,
        Kind = rule.Kind,
        SetId = rule.SetId,
        Choose = choose,
        FromJson = rule.FromJson,
        FilterJson = rule.FilterJson,
        Replaces = rule.Replaces,
        Cumulative = rule.Cumulative,
        Note = rule.Note,
        Source = rule.Source,
    };

    /// <summary>
    /// Features of the class level (and of the subclass level: the current subclass, or every subclass when it is
    /// chosen at this level), excluding those that are options of a choice.
    /// </summary>
    private async Task<List<LevelUpFeatureDto>> AutomaticFeaturesAsync(
        string classIndex,
        string? subclass,
        int newLevel,
        ClassLevel? classLevel,
        IReadOnlyList<LevelChoiceRule> rules,
        IReadOnlyList<SubclassDefinition> subclasses,
        CancellationToken cancellationToken)
    {
        var subclassIndexes = subclass is not null
            ? [subclass]
            : rules.Any(r => r.Kind == LevelChoiceKind.Subclass) ? subclasses.Select(s => s.Index).ToList() : [];
        var subclassLevels = (await catalog.ListSubclassLevelsAsync(subclassIndexes, cancellationToken)).Where(l => l.Level == newLevel).ToList();
        var indexes = (classLevel?.FeatureIndexes ?? []).Concat(subclassLevels.SelectMany(l => l.FeatureIndexes)).Distinct(StringComparer.Ordinal).ToList();
        var features = (await catalog.ListFeaturesByIndexAsync(indexes, cancellationToken)).ToDictionary(f => f.Index, StringComparer.Ordinal);

        LevelUpFeatureDto? Dto(string index, string? subclassIndex) =>
            features.TryGetValue(index, out var feature)
                ? new LevelUpFeatureDto(classIndex, subclassIndex, newLevel, new LevelUpFeatureInfoDto(feature.Index, feature.Name, feature.Description))
                : null;

        return [
            .. (classLevel?.FeatureIndexes ?? []).Select(i => Dto(i, null)).OfType<LevelUpFeatureDto>(),
            .. subclassLevels.SelectMany(l => l.FeatureIndexes.Select(i => Dto(i, l.SubclassIndex))).OfType<LevelUpFeatureDto>(),
        ];
    }

    private static LevelUpSpellcastingDto? Spellcasting(Character character, ClassDefinition definition, ClassLevel? classLevel, int newLevel, IReadOnlyList<SpellDefinition> spells)
    {
        if (definition.SpellcastingAbility is null || classLevel is null)
        {
            return null;
        }

        var levels = spells.ToDictionary(s => s.Index, s => s.Level, StringComparer.Ordinal);
        var own = character.Spells.Where(s => s.ClassIndex == definition.Index && !s.AlwaysPrepared).ToList();
        return new LevelUpSpellcastingDto(
            definition.Index,
            definition.SpellcastingAbility,
            definition.IsPactCaster,
            classLevel.CantripsKnown,
            classLevel.SpellsKnown,
            MaxSpellLevel(classLevel),
            own.Count(s => levels.GetValueOrDefault(s.SpellIndex, -1) == 0),
            own.Count(s => levels.GetValueOrDefault(s.SpellIndex, -1) > 0),
            classLevel.SpellSlots);
    }

    /// <summary>Highest spell level with slots in the class table at that level (pact slots included); 0 without slots.</summary>
    public static int MaxSpellLevel(ClassLevel? classLevel)
    {
        if (classLevel is null)
        {
            return 0;
        }

        for (var level = classLevel.SpellSlots.Count; level >= 1; level--)
        {
            if (classLevel.SpellSlots[level - 1] > 0)
            {
                return level;
            }
        }

        return 0;
    }

    /// <summary>Evaluates the rules of one plan.</summary>
    private sealed class PlanContext(
        Character character,
        CharacterSheet sheet,
        ClassDefinition definition,
        int newLevel,
        IReadOnlySet<string> pendingPicks,
        IReadOnlyList<OptionDefinition> options,
        IReadOnlyList<SpellDefinition> spells,
        IReadOnlyList<SkillDefinition> skills,
        IReadOnlyList<SubclassDefinition> subclasses,
        ClassLevel? classLevel)
    {
        private readonly IReadOnlyList<ActivePick> _active = character.ActivePicks();
        private readonly Dictionary<string, OptionDefinition> _options = options.ToDictionary(o => o.Index, StringComparer.Ordinal);
        private readonly Dictionary<string, SpellDefinition> _spells = spells.ToDictionary(s => s.Index, StringComparer.Ordinal);

        public PlannedChoice Plan(LevelChoiceRule rule)
        {
            var filter = rule.Filter;
            var freeText = false;
            List<PlannedOption> list;
            switch (rule.Kind)
            {
                case LevelChoiceKind.Subclass:
                    list = subclasses.Select(s => Simple(s.Index, s.Name, s.Description)).ToList();
                    break;
                case LevelChoiceKind.AsiOrFeat:
                    list = SetOptions(OptionSets.Feats, null);
                    break;
                case LevelChoiceKind.OptionSet:
                    list = rule.SetId is null ? [] : SetOptions(rule.SetId, rule.From);
                    break;
                case LevelChoiceKind.Custom when rule.SetId is not null:
                    list = SetOptions(rule.SetId, rule.From);
                    break;
                case LevelChoiceKind.Custom when filter.Source is ChoiceFilter.SpellbookSource or ChoiceFilter.KnownSource:
                    list = character.Spells
                        .Where(s => s.ClassIndex == definition.Index)
                        .Select(s => _spells.GetValueOrDefault(s.SpellIndex))
                        .OfType<SpellDefinition>()
                        .Where(s => filter.SpellLevels.Count == 0 || filter.SpellLevels.Contains(s.Level))
                        .OrderBy(s => s.Level)
                        .ThenBy(s => s.Name, StringComparer.Ordinal)
                        .Select(SpellOption)
                        .ToList();
                    break;
                case LevelChoiceKind.Expertise:
                    list = ExpertiseOptions();
                    break;
                case LevelChoiceKind.Skill:
                    list = skills
                        .Where(s => character.FindProficiency(ProficiencyType.Skill, s.Index) is null)
                        .Where(s => rule.From is null || rule.From.Contains(s.Index, StringComparer.Ordinal))
                        .Select(s => Simple(s.Index, s.Name, s.Description))
                        .ToList();
                    break;
                case LevelChoiceKind.CantripsKnown or LevelChoiceKind.SpellsKnown or LevelChoiceKind.SpellbookSpells:
                    list = SpellCandidates(rule, filter);
                    break;
                default:
                    // Languages, tools and free custom choices: the listed values, or any text.
                    var from = rule.From ?? [];
                    list = from.Select(f => Simple(f, f, [])).ToList();
                    freeText = from.Count == 0;
                    break;
            }

            var eligible = list.Count(o => o.Eligible);
            var required = rule.Choose <= 0 ? 0
                : freeText || rule.Kind == LevelChoiceKind.AsiOrFeat ? rule.Choose
                : Math.Min(rule.Choose, eligible);
            return new PlannedChoice(rule, required, freeText, list, rule.Replaces ? Known(rule) : []);
        }

        /// <summary>Options of a set (or the <paramref name="from"/> subset), without the ones the character already has.</summary>
        private List<PlannedOption> SetOptions(string setId, IReadOnlyList<string>? from)
        {
            var allowed = from?.Select(NormalizeIndex).ToHashSet(StringComparer.Ordinal);
            var taken = _active.Select(p => p.Item.Index).ToHashSet(StringComparer.Ordinal);
            return options
                .Where(o => o.SetId == setId && (allowed is null || allowed.Contains(o.Index)) && !taken.Contains(o.Index))
                .Select(o =>
                {
                    var reason = WhyNot(o);
                    return new PlannedOption(o.Index, o.Name, o.Description, o.PrerequisitesText, reason is null, reason, null, o, Preview(o));
                })
                .ToList();
        }

        private string? WhyNot(OptionDefinition option)
        {
            var prerequisites = option.Prerequisites;
            var reasons = new List<string>();
            if (prerequisites.MinLevel is { } minLevel && newLevel < minLevel)
            {
                reasons.Add($"Requiere nivel {minLevel} de {definition.Name}.");
            }

            if (prerequisites.PactBoon is { } pactBoon && !HasPick(pactBoon))
            {
                reasons.Add($"Requiere {_options.GetValueOrDefault(pactBoon)?.Name ?? pactBoon}.");
            }

            if (prerequisites.Cantrip is { } cantrip
                && !character.Spells.Any(s => s.SpellIndex == cantrip)
                && !pendingPicks.Contains(cantrip))
            {
                reasons.Add($"Requiere el truco {_spells.GetValueOrDefault(cantrip)?.Name ?? cantrip}.");
            }

            foreach (var (ability, minimum) in prerequisites.Abilities)
            {
                if (Abilities.IsValid(ability) && sheet.Abilities[ability].Score < minimum)
                {
                    reasons.Add($"Requiere {BreakdownLabels.Ability(ability)} {minimum}.");
                }
            }

            return reasons.Count == 0 ? null : string.Join(" ", reasons);
        }

        private bool HasPick(string index) => _active.Any(p => p.Item.Index == index) || pendingPicks.Contains(index);

        private List<PlannedOption> ExpertiseOptions()
        {
            var names = skills.ToDictionary(s => s.Index, s => s.Name, StringComparer.Ordinal);
            var result = character.Proficiencies
                .Where(p => p.Type == ProficiencyType.Skill && !p.Expertise)
                .Select(p => p.Key.StartsWith("skill-", StringComparison.Ordinal) ? p.Key["skill-".Length..] : p.Key)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(k => k, StringComparer.Ordinal)
                .Select(k => Simple(k, names.GetValueOrDefault(k, k), []))
                .ToList();
            if (definition.Index == "rogue" && character.FindProficiency(ProficiencyType.Tool, ThievesTools) is { Expertise: false })
            {
                result.Add(Simple(ThievesTools, "Thieves' Tools", []));
            }

            return result;
        }

        /// <summary>Spells of the class list (or the filter's) of the allowed levels that the character does not know for the class.</summary>
        private List<PlannedOption> SpellCandidates(LevelChoiceRule rule, ChoiceFilter filter)
        {
            var list = filter.SpellList ?? definition.Index;
            IReadOnlyList<int> levels = rule.Kind == LevelChoiceKind.CantripsKnown || filter.CantripsOnly
                ? [0]
                : filter.SpellLevels.Count > 0
                    ? filter.SpellLevels
                    : Enumerable.Range(1, Math.Max(0, MaxSpellLevel(classLevel))).ToList();
            var known = character.Spells.Where(s => s.ClassIndex == definition.Index).Select(s => s.SpellIndex).ToHashSet(StringComparer.Ordinal);
            return spells
                .Where(s => levels.Contains(s.Level) && !known.Contains(s.Index))
                .Where(s => list == ChoiceFilter.AnyList || s.ClassIndexes.Contains(list, StringComparer.Ordinal))
                .Select(SpellOption)
                .ToList();
        }

        /// <summary>Picks of this key that may be replaced: options chosen before, or the class's spells for spell choices.</summary>
        private List<ChoiceItem> Known(LevelChoiceRule rule)
        {
            if (rule.Kind is LevelChoiceKind.SpellsKnown or LevelChoiceKind.CantripsKnown)
            {
                var cantrips = rule.Kind == LevelChoiceKind.CantripsKnown;
                return character.Spells
                    .Where(s => s.ClassIndex == definition.Index && !s.AlwaysPrepared)
                    .Select(s => _spells.GetValueOrDefault(s.SpellIndex))
                    .OfType<SpellDefinition>()
                    .Where(s => cantrips ? s.Level == 0 : s.Level > 0)
                    .OrderBy(s => s.Level)
                    .ThenBy(s => s.Name, StringComparer.Ordinal)
                    .Select(s => new ChoiceItem(s.Index, s.Name))
                    .ToList();
            }

            return _active.Where(p => p.ClassIndex == definition.Index && p.Key == rule.Key).Select(p => p.Item).ToList();
        }

        private static PlannedOption Simple(string index, string name, IReadOnlyList<string> description) =>
            new(index, name, description, null, true, null, null, null, []);

        private static PlannedOption SpellOption(SpellDefinition spell) =>
            new(spell.Index, spell.Name, spell.Description.Take(1).ToList(), null, true, null, spell.Level, null, []);

        private List<EffectPreviewDto> Preview(OptionDefinition option) =>
            option.Modifiers.Select(m => PreviewOf(m, sheet)).ToList();
    }

    /// <summary>"CA 16 → 17": the affected value now and with the modifier (when its condition holds now).</summary>
    private static EffectPreviewDto PreviewOf(ChoiceModifier modifier, CharacterSheet sheet)
    {
        var target = modifier.Target;
        (string? Field, string Label, int? Before) affected = modifier.Kind switch
        {
            ItemModifierKind.ArmorClassBonus => ("armorClass", "CA", sheet.ArmorClass),
            ItemModifierKind.InitiativeBonus => ("initiative", "Iniciativa", sheet.Initiative),
            ItemModifierKind.SpeedBonus => ("speed", "Velocidad", sheet.Speed),
            ItemModifierKind.HitPointsMaxBonus => ("hitPointsMax", "PG máximos", sheet.HitPointsMax),
            ItemModifierKind.AbilityBonus when target is not null && sheet.Abilities.TryGetValue(target, out var ability) =>
                ($"ability.{target}", BreakdownLabels.Ability(target), ability.Score),
            ItemModifierKind.SaveBonus when target is not null && sheet.SavingThrows.TryGetValue(target, out var save) =>
                ($"save.{target}", $"Salvación de {BreakdownLabels.Ability(target)}", save.Value),
            ItemModifierKind.SaveBonus => (null, "Salvaciones", null),
            ItemModifierKind.SkillBonus when target is not null && sheet.Skills.FirstOrDefault(s => s.Index == target) is { } skill =>
                ($"skill.{target}", skill.Name, skill.Value),
            ItemModifierKind.SkillBonus => (null, "Habilidades", null),
            ItemModifierKind.AttackBonus => ("attacks", "Ataque", null),
            ItemModifierKind.DamageBonus => ("attacks", "Daño", null),
            _ => (null, modifier.Kind.ToString(), null),
        };
        var holds = modifier.Condition is null || (modifier.Condition == ModifierConditions.WearingArmor && sheet.WearsArmor);
        return new EffectPreviewDto(
            BreakdownSources.Feature,
            affected.Label,
            modifier.Value,
            affected.Field,
            affected.Before,
            affected.Before is { } before ? before + (holds ? modifier.Value : 0) : null,
            modifier.Condition is null ? null : ModifierConditions.Describe(modifier.Condition));
    }
}
