using OpenTrpg.Core.Application.Items;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Items;
using OpenTrpg.Core.Application;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions.Persistence;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Application.Characters;

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
    IReadOnlyList<EffectPreviewDto> Preview,
    string? SpellCategory = null)
{
    /// <summary>Damage type of a trait option (draconic ancestry), or null.</summary>
    public string? DamageType { get; init; }

    /// <summary>What using the option costs, or null.</summary>
    public OptionCostDto? Cost { get; init; }

    /// <summary>Pick of the same level-up the option depends on (a skill gained at this level), or null.</summary>
    public OptionRequirementDto? Requires { get; init; }
}

/// <summary>A choice of the plan: the rule, how many picks are required, the options and the replaceable picks.</summary>
public sealed record PlannedChoice(LevelChoiceRule Rule, int Required, bool FreeText, IReadOnlyList<PlannedOption> Options, IReadOnlyList<ChoiceItem> Known)
{
    /// <summary>
    /// Choice of an option (<c>options[].choices</c>): it only applies, and is only required, when <c>Index</c> is picked
    /// in the choice <c>ChoiceKey</c> of the same level-up. Null for the choices of the class.
    /// </summary>
    public OptionRequirementDto? Requires { get; init; }

    public PlannedOption? Option(string index) => Options.FirstOrDefault(o => o.Index == index);

    /// <summary>
    /// Why fewer picks than the rule gives are required (Spanish), or null: no eligible option at all, or fewer
    /// than <see cref="LevelChoiceRule.Choose"/>. The choice stays in the plan so the player sees what was skipped.
    /// </summary>
    public string? Warning =>
        Rule.Choose <= 0 || Required >= Rule.Choose ? null
        : Required == 0 ? "Ninguna opción cumple los requisitos ahora mismo, así que esta elección no se pide en este nivel."
        : $"Solo {Required} de las {Rule.Choose} opciones que da este nivel cumplen los requisitos ahora mismo.";

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
                o.Definition?.AbilityIncrease is { } increase ? new AbilityIncreaseDto(increase.Amount, increase.From) : null,
                o.SpellCategory)
            {
                Cost = o.Cost,
                Requires = o.Requires,
                SetId = o.Definition?.SetId,
                RemoveSavingThrows = o.Definition?.Grants.RemoveSavingThrows is { } remove
                    ? new RemoveSavingThrowsDto(remove.Choose, remove.From)
                    : null,
            })
            .ToList(),
        Known.Select(ChoiceItemDto.From).ToList(),
        Warning)
    {
        AlsoFrom = Rule.AlsoFrom,
        Requires = Requires,
    };
}

/// <summary>The level-up of a character in one class, as computed by <see cref="LevelUpPlanner"/>.</summary>
public sealed class LevelUpPlan
{
    public required Dnd5eCharacter Character { get; init; }

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

    /// <summary>Invalid options and feats that this level-up must replace (their choices are also in <see cref="Choices"/>).</summary>
    public IReadOnlyList<PlannedReplacement> Replacements { get; init; } = [];

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
        Spellcasting,
        AutomaticFeatures.Select(f => new LevelUpNewFeatureDto(f.Feature.Name, f.Feature.Description, f.SubclassIndex)).ToList());
}

/// <summary>
/// Builds the level-up plan of a character for a class from the level choice catalog: the rules of the class
/// (and of its subclass) at the new class level, with their options and eligibility evaluated against the
/// character as it is now. Picks of the same request (<c>pendingPicks</c>) count for prerequisites such as a
/// pact boon or a cantrip chosen at the same level.
/// </summary>
public sealed class LevelUpPlanner(ICatalogRepository catalog, ICharacterSheetService sheets, InvalidChoicesPlanner invalidChoices)
{
    /// <summary>Feature indexes of the dataset that are options of the shared <c>fighting-styles</c> set.</summary>
    private static readonly string[] FightingStyleAliasPrefixes = ["fighter-fighting-style-", "ranger-fighting-style-", "paladin-fighting-style-"];

    private const string FightingStylePrefix = "fighting-style-";
    private const string ThievesTools = "thieves-tools";

    /// <summary>Most catalog weapons a weapon choice lists.</summary>
    private const int MaxWeaponOptions = 1000;

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

    /// <param name="pendingChoices">
    /// The answers of the same request by choice key (picks, or the feat of an ability improvement), when applying: an
    /// expertise then offers the skills picked or granted at this level. Without them (the plan shown to the player),
    /// those skills are offered with <see cref="PlannedOption.Requires"/>.
    /// </param>
    public async Task<LevelUpPlan> BuildAsync(
        Dnd5eCharacter character,
        string? classIndex,
        IReadOnlySet<string> pendingPicks,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? pendingChoices = null)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(pendingPicks);

        var allClasses = await catalog.ListClassesAsync(cancellationToken);
        var index = string.IsNullOrWhiteSpace(classIndex) ? character.OrderedClasses.FirstOrDefault()?.ClassIndex : classIndex.Trim();
        if (index is null)
        {
            throw AppException.Validation("classIndex", "Indica la clase en la que sube de nivel.");
        }

        // A class the character already has stays available when its pack is no longer enabled in the campaign.
        var definition = allClasses.FirstOrDefault(c => c.Index == index)
            ?? (character.Classes.Any(c => c.ClassIndex == index) ? await catalog.GetClassAsync(index, cancellationToken) : null)
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
        var answered = character.Choices.Where(c => c.ClassIndex == definition.Index).Select(c => c.Key).ToHashSet(StringComparer.Ordinal);
        var allRules = WithKnownSpellCounts(await catalog.ListLevelChoiceRulesAsync(definition.Index, cancellationToken), definition, subclasses);
        var rules = SelectRules(allRules, entry?.SubclassIndex, newLevel, answered);

        // Choices that the options of this class the character has add at this level (options[].levelChoices).
        var classPicks = character.ActivePicks().Where(p => p.ClassIndex == definition.Index && p.SetId is not null).Select(p => p.Item.Index).Distinct(StringComparer.Ordinal).ToList();
        if (classPicks.Count > 0)
        {
            foreach (var picked in await catalog.ListOptionsByIndexAsync(classPicks, cancellationToken))
            {
                rules.AddRange(picked.LevelChoices.Where(r => r.Level == newLevel).Select(r => r.ToRule(picked.Index, definition.Index, null, newLevel)));
            }
        }
        if (entry is null && character.Classes.Count > 0)
        {
            // Multiclassing: the creation wizard only covered the first class, so the level-1 choices of a new
            // caster class (its cantrips, spells known or spellbook) are asked here.
            rules.InsertRange(0, NewClassSpellRules(definition, classLevel));
            if (MulticlassSkillRule(definition) is { } multiclassSkill)
            {
                rules.Insert(0, multiclassSkill);
            }
        }

        // Skills (picked, or granted by the subclass or an option) are resolved before the expertise of the same level.
        rules = LevelChoiceOrder.Sort(rules);

        var setIds = rules.Select(r => r.Kind == LevelChoiceKind.AsiOrFeat ? OptionSets.Feats : r.SetId).OfType<string>()
            .Concat(rules.SelectMany(r => r.AlsoFrom))
            .ToHashSet(StringComparer.Ordinal);
        if (setIds.Count > 0)
        {
            setIds.Add(OptionSets.PactBoons);
        }

        var options = (await catalog.ListOptionsBySetAsync(setIds, cancellationToken)).ToList();

        // "from" may name options of other sets (a set that reuses options of another one).
        var loaded = options.Select(o => o.Index).ToHashSet(StringComparer.Ordinal);
        var named = rules.Where(r => r.SetId is not null && r.Kind is LevelChoiceKind.OptionSet or LevelChoiceKind.Custom)
            .SelectMany(r => r.From ?? [])
            .Select(NormalizeIndex)
            .Where(i => !loaded.Contains(i))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (named.Count > 0)
        {
            options.AddRange(await catalog.ListOptionsByIndexAsync(named, cancellationToken));
        }

        // Choices made when an option is taken (options[].choices) may draw from other sets.
        var optionChoices = options.SelectMany(o => o.Choices).ToList();
        var subSets = optionChoices.Select(c => c.SetId).OfType<string>()
            .Concat(optionChoices.SelectMany(c => LevelChoiceJson.ParseStringList(c.AlsoFromJson) ?? []))
            .Where(id => !setIds.Contains(id))
            .ToHashSet(StringComparer.Ordinal);
        if (subSets.Count > 0)
        {
            var known = options.Select(o => o.Index).ToHashSet(StringComparer.Ordinal);
            options.AddRange((await catalog.ListOptionsBySetAsync(subSets, cancellationToken)).Where(o => !known.Contains(o.Index)));
        }

        var choiceKinds = rules.Select(r => r.Kind).Concat(optionChoices.Select(c => c.Kind)).ToHashSet();

        var prerequisites = options.Select(o => o.Prerequisites)
            .Concat(rules.Any(r => r.Kind == LevelChoiceKind.Subclass) ? subclasses.Select(s => s.Prerequisites) : [])
            .Where(p => !p.IsEmpty)
            .ToList();
        var needsSpells = choiceKinds.Any(k => k is LevelChoiceKind.CantripsKnown or LevelChoiceKind.SpellsKnown or LevelChoiceKind.SpellbookSpells or LevelChoiceKind.Custom)
            || prerequisites.Any(p => p.ReferencedSpells.Any());
        var spells = needsSpells
            ? await catalog.ListAllSpellsAsync(cancellationToken)
            : await catalog.ListSpellsByIndexAsync(character.Spells.Select(s => s.SpellIndex).Distinct(StringComparer.Ordinal).ToList(), cancellationToken);
        var skills = choiceKinds.Any(k => k is LevelChoiceKind.Skill or LevelChoiceKind.Expertise)
            ? await catalog.ListSkillsAsync(cancellationToken)
            : [];
        var weapons = choiceKinds.Contains(LevelChoiceKind.Weapon)
            ? (await catalog.SearchSrdItemsAsync(new ItemFilter(null, ItemCategory.Weapon, null), 0, MaxWeaponOptions, cancellationToken)).Items
            : [];
        var races = prerequisites.Any(p => p.ReferencedRaces.Any())
            ? (await catalog.ListRacesAsync(cancellationToken)).ToDictionary(r => r.Index, r => r.Name, StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);
        var names = await PrerequisiteNamesAsync(prerequisites, options, allClasses, cancellationToken);
        var size = prerequisites.Any(p => p.Sizes.Count > 0 || p.AnyOf.Any(a => a.Sizes.Count > 0)) && character.RaceIndex is { } raceIndex
            ? (await catalog.GetRaceAsync(raceIndex, cancellationToken))?.Size
            : null;

        // Subclass levels whose skill grants an expertise of this level may double (the current subclass, or every
        // subclass when it is chosen now).
        var expertiseSubclasses = !rules.Any(r => r.Kind == LevelChoiceKind.Expertise) ? []
            : entry?.SubclassIndex is { } currentSubclass ? [currentSubclass]
            : rules.Any(r => r.Kind == LevelChoiceKind.Subclass) ? subclasses.Select(s => s.Index).ToList()
            : new List<string>();
        var subclassLevels = expertiseSubclasses.Count > 0
            ? await catalog.ListSubclassLevelsAsync(expertiseSubclasses, cancellationToken)
            : [];

        var context = new PlanContext(character, sheet, definition, newLevel, pendingPicks, options, spells, skills, subclasses, classLevel, races)
        {
            PendingChoices = pendingChoices,
            SubclassLevels = subclassLevels,
            CurrentSubclass = entry?.SubclassIndex,
            Names = names,
            Size = size,
            Weapons = weapons,
            IsPactClass = c => allClasses.FirstOrDefault(d => d.Index == c)?.IsPactCaster ?? c == "warlock",
        };
        var choices = rules.Select(context.Plan).ToList();

        // Choices of the options offered (options[].choices): they apply when the option is picked.
        foreach (var parent in choices.Where(c => c.Rule.Kind is LevelChoiceKind.OptionSet or LevelChoiceKind.AsiOrFeat or LevelChoiceKind.Custom).ToList())
        {
            foreach (var option in parent.Options.Where(o => o.Definition is { ChoicesJson: not null }))
            {
                foreach (var sub in option.Definition!.Choices)
                {
                    var rule = sub.ToRule(option.Index, definition.Index, parent.Rule.SubclassIndex, newLevel);
                    choices.Add(context.Plan(rule) with { Requires = new OptionRequirementDto(parent.Rule.Key, option.Index) });
                }
            }
        }

        var replacements = await invalidChoices.PlanAsync(character, sheet, cancellationToken);
        choices.AddRange(replacements.Select(r => r.Choice));

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
            Spellcasting = SubclassSpellcasting(character, definition, SubclassCasting(definition, subclasses, entry?.SubclassIndex), classLevel, newLevel, spells)
                ?? Spellcasting(character, definition, classLevel, newLevel, spells),
            Replacements = replacements,
        };
    }

    /// <summary>Names of the classes and of the subclasses and options outside the plan that the prerequisites name.</summary>
    private async Task<Dictionary<string, string>> PrerequisiteNamesAsync(
        IReadOnlyList<OptionPrerequisites> prerequisites,
        IReadOnlyList<OptionDefinition> options,
        IReadOnlyList<ClassDefinition> allClasses,
        CancellationToken cancellationToken)
    {
        var names = allClasses.ToDictionary(c => c.Index, c => c.Name, StringComparer.Ordinal);
        if (prerequisites.Count == 0)
        {
            return names;
        }

        var known = options.Select(o => o.Index).ToHashSet(StringComparer.Ordinal);
        var otherOptions = prerequisites.SelectMany(p => p.ReferencedOptions).Where(o => !known.Contains(o)).Distinct(StringComparer.Ordinal).ToList();
        if (otherOptions.Count > 0)
        {
            foreach (var option in await catalog.ListOptionsByIndexAsync(otherOptions, cancellationToken))
            {
                names[option.Index] = option.Name;
            }
        }

        var subclassIndexes = prerequisites.SelectMany(p => p.ReferencedSubclasses).Distinct(StringComparer.Ordinal).ToList();
        if (subclassIndexes.Count > 0)
        {
            foreach (var subclass in await catalog.ListSubclassesByIndexAsync(subclassIndexes, cancellationToken))
            {
                names[subclass.Index] = subclass.Name;
            }
        }

        return names;
    }

    /// <summary>
    /// The spellcasting a subclass gives to a class that does not cast on its own (content packs), or null: the class
    /// casts by itself, or <paramref name="subclass"/> is not a subclass of it with <c>spellcasting</c>.
    /// </summary>
    private static SubclassSpellcasting? SubclassCasting(ClassDefinition definition, IReadOnlyList<SubclassDefinition> subclasses, string? subclass) =>
        definition.MayGainSubclassSpellcasting && subclass is not null
            ? subclasses.FirstOrDefault(s => s.Index == subclass)?.Spellcasting
            : null;

    /// <summary>
    /// <see cref="LevelChoiceKind.SpellsKnown"/>/<see cref="LevelChoiceKind.CantripsKnown"/> rules of a subclass with
    /// <c>spellcasting</c> that give no <c>choose</c> learn the increase of its <c>spellsKnown</c>/<c>cantripsKnown</c>
    /// table from the previous class level.
    /// </summary>
    private static List<LevelChoiceRule> WithKnownSpellCounts(IReadOnlyList<LevelChoiceRule> rules, ClassDefinition definition, IReadOnlyList<SubclassDefinition> subclasses) =>
        rules.Select(rule =>
        {
            if (rule.Choose > 0
                || rule.Kind is not (LevelChoiceKind.SpellsKnown or LevelChoiceKind.CantripsKnown)
                || SubclassCasting(definition, subclasses, rule.SubclassIndex) is not { } casting)
            {
                return rule;
            }

            var table = rule.Kind == LevelChoiceKind.SpellsKnown ? casting.SpellsKnown : casting.CantripsKnown;
            var increase = (Domain.Catalog.SubclassSpellcasting.At(table, rule.Level) ?? 0) - (Domain.Catalog.SubclassSpellcasting.At(table, rule.Level - 1) ?? 0);
            return increase > 0 ? AtLevel(rule, rule.Level, increase) : rule;
        }).ToList();

    /// <summary>Key of the skill gained when multiclassing into a bard, ranger or rogue.</summary>
    public const string MulticlassSkillKey = "multiclass-skill";

    /// <summary>
    /// Multiclassing into a bard (any skill), ranger or rogue (a skill of the class list) gives one skill proficiency
    /// (PHB): a <see cref="LevelChoiceKind.Skill"/> choice of the plan.
    /// </summary>
    private static LevelChoiceRule? MulticlassSkillRule(ClassDefinition definition)
    {
        if (MulticlassRules.SkillsFor(definition.Index, definition.Multiclassing) is not { } skills)
        {
            return null;
        }

        string? from = null;
        if (skills.FromClassList)
        {
            using var document = System.Text.Json.JsonDocument.Parse(definition.SkillChoicesJson);
            if (document.RootElement.TryGetProperty("from", out var list) && list.ValueKind == System.Text.Json.JsonValueKind.Array && list.GetArrayLength() > 0)
            {
                from = list.GetRawText();
            }
        }

        return new LevelChoiceRule
        {
            Id = LevelChoiceRule.IdFor(definition.Index, null, 1, MulticlassSkillKey),
            ClassIndex = definition.Index,
            Level = 1,
            Key = MulticlassSkillKey,
            Name = "Habilidad de multiclase",
            Kind = LevelChoiceKind.Skill,
            Choose = skills.Choose,
            FromJson = from,
            Note = skills.FromClassList
                ? $"Al entrar en {BreakdownLabels.Class(definition.Index)} como multiclase ganas una habilidad de su lista."
                : $"Al entrar en {BreakdownLabels.Class(definition.Index)} como multiclase ganas una habilidad cualquiera.",
        };
    }

    public const string CantripsKey = "cantrips";
    public const string SpellsKnownKey = "spells-known";
    public const string SpellbookKey = "spellbook";

    /// <summary>Spells a wizard starts its spellbook with (SRD: six 1st-level spells).</summary>
    public const int StartingSpellbookSpells = 6;

    /// <summary>
    /// Level-1 spell choices of a class taken by multiclassing, from its class table: the cantrips known, the
    /// spells known (bard, sorcerer, warlock...) or, for the wizard, the six spells of the spellbook. Classes that
    /// prepare from their list (cleric, druid) only pick cantrips; half casters without level-1 spells pick nothing.
    /// </summary>
    private static List<LevelChoiceRule> NewClassSpellRules(ClassDefinition definition, ClassLevel? level1)
    {
        var result = new List<LevelChoiceRule>();
        if (definition.SpellcastingAbility is null || level1 is null)
        {
            return result;
        }

        LevelChoiceRule Rule(string key, string name, LevelChoiceKind kind, int choose, string note, string filter) => new()
        {
            Id = LevelChoiceRule.IdFor(definition.Index, null, 1, key),
            ClassIndex = definition.Index,
            Level = 1,
            Key = key,
            Name = name,
            Kind = kind,
            Choose = choose,
            Note = note,
            FilterJson = filter,
        };

        if (level1.CantripsKnown is > 0 and var cantrips)
        {
            result.Add(Rule(
                CantripsKey,
                "Cantrips",
                LevelChoiceKind.CantripsKnown,
                cantrips,
                $"Al entrar en {BreakdownLabels.Class(definition.Index)} como multiclase conoces sus trucos de nivel 1.",
                $$"""{"spellList":"{{definition.Index}}","cantripsOnly":true}"""));
        }

        if (definition.Index == "wizard")
        {
            result.Add(Rule(
                SpellbookKey,
                "Spellbook",
                LevelChoiceKind.SpellbookSpells,
                StartingSpellbookSpells,
                "Tu libro de conjuros empieza con seis conjuros de nivel 1 de la lista del mago.",
                """{"spellList":"wizard","maxSpellLevelBySlots":true}"""));
        }
        else if (level1.SpellsKnown is > 0 and var spells && MaxSpellLevel(level1) > 0)
        {
            result.Add(Rule(
                SpellsKnownKey,
                "Spells Known",
                LevelChoiceKind.SpellsKnown,
                spells,
                $"Al entrar en {BreakdownLabels.Class(definition.Index)} como multiclase conoces sus conjuros de nivel 1.",
                $$"""{"spellList":"{{definition.Index}}","maxSpellLevelBySlots":true}"""));
        }

        return result;
    }

    /// <summary>
    /// The current classes (always allowed) and every other catalog class, allowed when the multiclassing
    /// prerequisites of the new class and of all current ones are met. The main class goes first.
    /// </summary>
    private static List<LevelUpClassDto> ClassOptions(Dnd5eCharacter character, CharacterSheet sheet, IReadOnlyList<ClassDefinition> allClasses)
    {
        int Score(string ability) => sheet.Abilities[ability].Score;
        var current = character.OrderedClasses;
        var result = new List<LevelUpClassDto>();
        foreach (var entry in current)
        {
            var definition = allClasses.FirstOrDefault(c => c.Index == entry.ClassIndex);
            var maxLevel = definition?.IsAuxiliary == true ? 1 : 20;
            result.Add(new LevelUpClassDto(
                entry.ClassIndex,
                definition?.Name ?? entry.ClassIndex,
                definition is not null && entry.Level < maxLevel,
                definition is null ? "La clase ya no está en el catálogo."
                    : entry.Level < maxLevel ? null
                    : maxLevel == 1 ? "Un nivel auxiliar se toma una sola vez." : "La clase ya está en el nivel 20.",
                definition?.HitDie ?? 0,
                false,
                entry.Level,
                entry.SubclassIndex)
            {
                Auxiliary = definition?.IsAuxiliary == true,
                PrerequisitesText = definition?.PrerequisitesText,
            });
        }

        var names = allClasses.ToDictionary(c => c.Index, c => c.Name, StringComparer.Ordinal);
        var picks = character.ActivePicks().Select(p => p.Item.Index).ToHashSet(StringComparer.Ordinal);
        foreach (var definition in allClasses.Where(c => current.All(e => e.ClassIndex != c.Index)))
        {
            // Auxiliary levels (H14): after level 1, without the multiclass ability prerequisites, with their own.
            var reason = definition.IsAuxiliary
                ? current.Count == 0
                    ? "Un nivel auxiliar no se toma al crear el personaje."
                    : PrerequisiteEvaluator.WhyNot(definition.Prerequisites, new PrerequisiteFacts
                    {
                        Character = character,
                        Sheet = sheet,
                        CharacterLevel = character.TotalLevel + 1,
                        ClassLevel = character.TotalLevel + 1,
                        Picks = picks,
                        IsPactClass = index => allClasses.FirstOrDefault(c => c.Index == index)?.IsPactCaster ?? index == "warlock",
                        Name = index => names.GetValueOrDefault(index, index),
                        Detailed = true,
                    })
                : current.Count == 0 ? null : MulticlassRules.WhyNot(definition.Index, current.Select(c => c.ClassIndex), Score, index => allClasses.FirstOrDefault(c => c.Index == index)?.Multiclassing);
            result.Add(new LevelUpClassDto(definition.Index, definition.Name, reason is null, reason, definition.HitDie, true, 0, null)
            {
                Auxiliary = definition.IsAuxiliary,
                PrerequisitesText = definition.PrerequisitesText,
            });
        }

        return result;
    }

    /// <summary>
    /// Rules at the new level for the base class and the current subclass. When the class has no subclass yet and
    /// its subclass level has been reached, the subclass is asked now (late characters included) together with the
    /// rules of every subclass at this level (each flagged with its subclass). Subclass choices of earlier levels
    /// that were never answered are caught up now: when the subclass is taken late, those of every subclass
    /// from the subclass level on; with a subclass already set (a draconic sorcerer created at level 1), those
    /// of that subclass without a recorded choice of their key (<paramref name="answered"/>). Rules that allow
    /// replacements from an earlier level and have no rule at this level appear with <c>choose</c> 0 (only
    /// replacements).
    /// </summary>
    private static List<LevelChoiceRule> SelectRules(IReadOnlyList<LevelChoiceRule> all, string? subclass, int newLevel, IReadOnlySet<string> answered)
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

        var missed = all.Where(r =>
            r.SubclassIndex is not null
            && r.Kind != LevelChoiceKind.Subclass
            && r.Level < newLevel
            && (choosingSubclass ? r.Level >= subclassRule!.Level : r.SubclassIndex == subclass && !answered.Contains(r.Key)));
        var caughtUp = missed
            .GroupBy(r => (r.SubclassIndex, r.Key))
            .Where(g => result.All(r => r.SubclassIndex != g.Key.SubclassIndex || r.Key != g.Key.Key))
            .Select(g => g.OrderBy(r => r.Level).ToList())
            .Select(group => AtLevel(group[0], newLevel, group.Sum(r => r.Choose)));
        result.AddRange(caughtUp);

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
        After = rule.After,
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

    private static LevelUpSpellcastingDto? Spellcasting(Dnd5eCharacter character, ClassDefinition definition, ClassLevel? classLevel, int newLevel, IReadOnlyList<SpellDefinition> spells)
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
            ClassMaxSpellLevel(definition, classLevel),
            own.Count(s => levels.GetValueOrDefault(s.SpellIndex, -1) == 0),
            own.Count(s => levels.GetValueOrDefault(s.SpellIndex, -1) > 0),
            classLevel.SpellSlots,
            definition.PreparesSpells && ClassMaxSpellLevel(definition, classLevel) > 0);
    }

    /// <summary>Spellcasting of the plan for a class that casts through its subclass (content packs); null when it does not.</summary>
    private static LevelUpSpellcastingDto? SubclassSpellcasting(
        Dnd5eCharacter character,
        ClassDefinition definition,
        SubclassSpellcasting? casting,
        ClassLevel? classLevel,
        int newLevel,
        IReadOnlyList<SpellDefinition> spells)
    {
        if (casting is null || newLevel < casting.FromLevel)
        {
            return null;
        }

        // A class that only knows cantrips keeps its ability and its cantrips table when the subclass has none.
        var levels = spells.ToDictionary(s => s.Index, s => s.Level, StringComparer.Ordinal);
        var own = character.Spells.Where(s => s.ClassIndex == definition.Index && !s.AlwaysPrepared).ToList();
        var slots = casting.SlotsAt(newLevel);
        return new LevelUpSpellcastingDto(
            definition.Index,
            definition.SpellcastingAbility ?? casting.Ability,
            casting.IsPact,
            casting.CantripsKnown.Count > 0 ? casting.CantripsKnownAt(newLevel) : classLevel?.CantripsKnown,
            casting.SpellsKnownAt(newLevel),
            SheetCalculator.MaxSpellLevel(slots),
            own.Count(s => levels.GetValueOrDefault(s.SpellIndex, -1) == 0),
            own.Count(s => levels.GetValueOrDefault(s.SpellIndex, -1) > 0),
            slots,
            false);
    }

    /// <summary>
    /// Highest spell level of a class at a level: its <c>maxSpellLevel</c> table for the progressions without slots
    /// ("hitPoints", "ritualsOnly"), the slots of the level otherwise.
    /// </summary>
    public static int ClassMaxSpellLevel(ClassDefinition definition, ClassLevel? classLevel) =>
        classLevel is not null && definition.Spellcasting?.MaxSpellLevelAt(classLevel.Level) is { } max ? max : MaxSpellLevel(classLevel);

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
        Dnd5eCharacter character,
        CharacterSheet sheet,
        ClassDefinition definition,
        int newLevel,
        IReadOnlySet<string> pendingPicks,
        IReadOnlyList<OptionDefinition> options,
        IReadOnlyList<SpellDefinition> spells,
        IReadOnlyList<SkillDefinition> skills,
        IReadOnlyList<SubclassDefinition> subclasses,
        ClassLevel? classLevel,
        IReadOnlyDictionary<string, string> races)
    {
        private readonly IReadOnlyList<ActivePick> _active = character.ActivePicks();
        private readonly Dictionary<string, OptionDefinition> _options = options.ToDictionary(o => o.Index, StringComparer.Ordinal);
        private readonly Dictionary<string, SpellDefinition> _spells = spells.ToDictionary(s => s.Index, StringComparer.Ordinal);
        private readonly List<PlannedChoice> _planned = [];
        private PrerequisiteFacts? _facts;

        /// <summary>Names of the classes, subclasses and options of other sets the prerequisites name.</summary>
        public IReadOnlyDictionary<string, string> Names { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Size of the character's race, or null.</summary>
        public string? Size { get; init; }

        public Func<string, bool> IsPactClass { get; init; } = _ => false;

        /// <summary>Catalog weapons, for the weapon choices.</summary>
        public IReadOnlyList<ItemTemplate> Weapons { get; init; } = [];

        /// <summary>Answers of the request by choice key (applying), or null (the plan shown to the player).</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>>? PendingChoices { get; init; }

        /// <summary>Subclass levels whose skill grants count for the expertise of this level.</summary>
        public IReadOnlyList<SubclassLevel> SubclassLevels { get; init; } = [];

        public string? CurrentSubclass { get; init; }

        public PlannedChoice Plan(LevelChoiceRule rule)
        {
            var planned = PlanRule(rule);
            _planned.Add(planned);
            return planned;
        }

        private PlannedChoice PlanRule(LevelChoiceRule rule)
        {
            var filter = rule.Filter;
            var freeText = false;
            List<PlannedOption> list;
            switch (rule.Kind)
            {
                case LevelChoiceKind.Subclass:
                    list = subclasses.Select(SubclassOption).ToList();
                    break;
                case LevelChoiceKind.AsiOrFeat:
                    list = SetOptions(OptionSets.Feats, null, rule.AlsoFrom, rule.SubclassIndex);
                    break;
                case LevelChoiceKind.OptionSet:
                    list = rule.SetId is null ? [] : SetOptions(rule.SetId, rule.From, rule.AlsoFrom, rule.SubclassIndex);
                    break;
                case LevelChoiceKind.Custom when rule.SetId is not null:
                    list = SetOptions(rule.SetId, rule.From, [], rule.SubclassIndex);
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
                    list = ExpertiseOptions(rule);
                    break;
                case LevelChoiceKind.SavingThrow:
                    list = Abilities.All
                        .Where(a => rule.From is null || rule.From.Contains(a, StringComparer.Ordinal))
                        .Select(a =>
                        {
                            var has = character.FindProficiency(ProficiencyType.SavingThrow, a) is not null
                                || (sheet.SavingThrows.TryGetValue(a, out var save) && save.Proficient && !save.Overridden);
                            return new PlannedOption(a, BreakdownLabels.Ability(a), [], null, !has, has ? "Ya tienes esta competencia." : null, null, null, []);
                        })
                        .ToList();
                    break;
                case LevelChoiceKind.Weapon:
                    list = WeaponOptions(rule, filter);
                    break;
                case LevelChoiceKind.Resistance:
                    list = (rule.From is { Count: > 0 } types ? types.Select(t => t.ToLowerInvariant()) : DamageTypes.All)
                        .Select(t =>
                        {
                            var has = sheet.Resistances.Any(r => r.DamageType == t);
                            return new PlannedOption(t, DamageTypes.Spanish(t), [], null, !has, has ? "Ya tienes esta resistencia." : null, null, null, []);
                        })
                        .ToList();
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

        /// <summary>
        /// Options of a set (or the <paramref name="from"/> options, of any set) plus those of the <paramref name="alsoFrom"/>
        /// sets, without the ones the character already has (repeatable feats are offered again). The choice of a
        /// <paramref name="subclass"/> only applies with it, so its options are checked as if it were chosen (it may be
        /// picked at this same level).
        /// </summary>
        private List<PlannedOption> SetOptions(string setId, IReadOnlyList<string>? from, IReadOnlyList<string> alsoFrom, string? subclass = null)
        {
            var facts = subclass is null || Facts.Pending.Contains(subclass) || character.Classes.Any(c => c.SubclassIndex == subclass)
                ? Facts
                : Facts with { Pending = Facts.Pending.Append(subclass).ToHashSet(StringComparer.Ordinal) };
            var allowed = from?.Select(NormalizeIndex).ToHashSet(StringComparer.Ordinal);
            var taken = _active.Select(p => p.Item.Index).ToHashSet(StringComparer.Ordinal);
            return options
                .Where(o => (allowed is null ? o.SetId == setId : allowed.Contains(o.Index)) || alsoFrom.Contains(o.SetId, StringComparer.Ordinal))
                .Where(o => (!taken.Contains(o.Index) || o.Repeatable) && !o.CreationOnly)
                .Select(o =>
                {
                    var reason = PrerequisiteEvaluator.WhyNot(o.Prerequisites, facts);
                    return new PlannedOption(o.Index, o.Name, o.Description, o.PrerequisitesText ?? PrerequisitesText(o.Prerequisites), reason is null, reason, null, o, Preview(o))
                    {
                        Cost = OptionCosts.Of(o, character, options),
                    };
                })
                .ToList();
        }

        /// <summary>
        /// Catalog weapons of a weapon choice (<c>from</c>, or the categories and firearms of the filter); not eligible
        /// when the character is already proficient with them (by index or by group).
        /// </summary>
        private List<PlannedOption> WeaponOptions(LevelChoiceRule rule, ChoiceFilter filter)
        {
            var keys = character.Proficiencies.Where(p => p.Type == ProficiencyType.Weapon).Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
            return Weapons
                .Where(w => w.Index is not null)
                .Where(w => rule.From is null || rule.From.Contains(w.Index!, StringComparer.Ordinal))
                .Where(w => filter.WeaponCategories.Count == 0
                    || filter.WeaponCategories.Any(c => (w.Subcategory ?? string.Empty).StartsWith(c, StringComparison.OrdinalIgnoreCase)))
                .Where(w => filter.Firearms is null || w.Properties.Contains(ProficiencyKeys.FirearmProperty, StringComparer.OrdinalIgnoreCase) == filter.Firearms)
                .Select(w =>
                {
                    var has = keys.Contains(w.Index!) || ProficiencyKeys.WeaponGroups(w.Subcategory, w.Properties).Any(keys.Contains);
                    return new PlannedOption(w.Index!, w.Name, [w.Subcategory ?? string.Empty], null, !has, has ? "Ya tienes competencia con esta arma." : null, null, null, []);
                })
                .ToList();
        }

        /// <summary>Spanish prerequisites text derived from the structured ones ("Fuerza 13, competencia con armadura pesada"), or null when there are none.</summary>
        private string? PrerequisitesText(OptionPrerequisites prerequisites) => PrerequisiteEvaluator.Text(prerequisites, Name, definition.Index);

        /// <summary>Display name of an index the prerequisites name: option, spell, race, subclass or class.</summary>
        private string Name(string index) =>
            _options.GetValueOrDefault(index)?.Name
            ?? _spells.GetValueOrDefault(index)?.Name
            ?? races.GetValueOrDefault(index)
            ?? subclasses.FirstOrDefault(s => s.Index == index)?.Name
            ?? Names.GetValueOrDefault(index)
            ?? BreakdownLabels.Class(index);

        /// <summary>What the prerequisites are checked against: the character, the new class level and the picks of this request.</summary>
        private PrerequisiteFacts Facts => _facts ??= new PrerequisiteFacts
        {
            Character = character,
            Sheet = sheet,
            ClassIndex = definition.Index,
            ClassLevel = newLevel,
            CharacterLevel = character.TotalLevel + 1,
            Picks = _active.Select(p => p.Item.Index).Concat(pendingPicks).ToHashSet(StringComparer.Ordinal),
            Pending = pendingPicks,
            Size = Size,
            IsPactClass = IsPactClass,
            Name = Name,
            CurrentPactBoon = _active.FirstOrDefault(p => p.SetId == OptionSets.PactBoons)?.Item.Name
                ?? pendingPicks.Select(p => _options.GetValueOrDefault(p)).FirstOrDefault(o => o?.SetId == OptionSets.PactBoons)?.Name,
            Detailed = true,
        };

        /// <summary>A subclass of the choice, not eligible while its prerequisites fail.</summary>
        private PlannedOption SubclassOption(SubclassDefinition subclass)
        {
            var reason = PrerequisiteEvaluator.WhyNot(subclass.Prerequisites, Facts);
            return new PlannedOption(
                subclass.Index,
                subclass.Name,
                subclass.Description,
                subclass.PrerequisitesText ?? PrerequisitesText(subclass.Prerequisites),
                reason is null,
                reason,
                null,
                null,
                []);
        }

        private bool HasPick(string index) => _active.Any(p => p.Item.Index == index) || pendingPicks.Contains(index);

        /// <summary>
        /// Skills the character is proficient with (without expertise), plus those gained at this level before the
        /// expertise: picked in a skill choice, or granted by the subclass or an option chosen now. Without the answers
        /// of the request, the ones that depend on a pick carry <see cref="PlannedOption.Requires"/>. <c>from</c>
        /// limits the skills offered.
        /// </summary>
        private List<PlannedOption> ExpertiseOptions(LevelChoiceRule rule)
        {
            var names = skills.ToDictionary(s => s.Index, s => s.Name, StringComparer.Ordinal);
            var result = character.Proficiencies
                .Where(p => p.Type == ProficiencyType.Skill && !p.Expertise)
                .Select(p => SkillKey(p.Key))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(k => k, StringComparer.Ordinal)
                .Select(k => Simple(k, names.GetValueOrDefault(k, k), []))
                .ToList();
            foreach (var (skill, requires) in SameLevelSkills())
            {
                if (character.FindProficiency(ProficiencyType.Skill, skill) is not null)
                {
                    continue;
                }

                var existing = result.FindIndex(o => o.Index == skill);
                if (existing < 0)
                {
                    result.Add(Simple(skill, names.GetValueOrDefault(skill, skill), []) with { Requires = requires });
                }
                else if (requires is null && result[existing].Requires is not null)
                {
                    result[existing] = result[existing] with { Requires = null };
                }
            }

            if (definition.Index == "rogue" && character.FindProficiency(ProficiencyType.Tool, ThievesTools) is { Expertise: false })
            {
                result.Add(Simple(ThievesTools, "Thieves' Tools", []));
            }

            return rule.From is { } from ? result.Where(o => from.Contains(o.Index, StringComparer.Ordinal)).ToList() : result;
        }

        private static string SkillKey(string key) => key.StartsWith("skill-", StringComparison.Ordinal) ? key["skill-".Length..] : key;

        /// <summary>
        /// Skills gained at this level by the choices planned so far and by the subclass, each with the pick it depends
        /// on (null when it is certain: granted by the current subclass, or picked in the request being applied).
        /// </summary>
        private IEnumerable<(string Skill, OptionRequirementDto? Requires)> SameLevelSkills()
        {
            // Without the answers every candidate is offered with its condition; with them only what was picked.
            IEnumerable<(string Skill, OptionRequirementDto? Requires)> Picked(string key, string index, IEnumerable<string> gained)
            {
                if (PendingChoices is null)
                {
                    return gained.Select(s => (SkillKey(s), (OptionRequirementDto?)new OptionRequirementDto(key, index)));
                }

                return PendingChoices.TryGetValue(key, out var picks) && picks.Contains(index, StringComparer.Ordinal)
                    ? gained.Select(s => (SkillKey(s), (OptionRequirementDto?)null))
                    : [];
            }

            IEnumerable<string> SubclassSkills(string subclass) => SubclassLevels
                .Where(l => l.SubclassIndex == subclass && l.Level <= newLevel)
                .SelectMany(l => l.Grants.Skills);

            if (CurrentSubclass is { } current)
            {
                foreach (var skill in SubclassSkills(current))
                {
                    yield return (SkillKey(skill), null);
                }
            }

            foreach (var choice in _planned)
            {
                var key = choice.Rule.Key;
                IEnumerable<(string, OptionRequirementDto?)> gained = choice.Rule.Kind switch
                {
                    LevelChoiceKind.Subclass => choice.Options.SelectMany(o => Picked(key, o.Index, SubclassSkills(o.Index))),
                    LevelChoiceKind.Skill => choice.Options.Where(o => o.Eligible).SelectMany(o => Picked(key, o.Index, [o.Index])),
                    LevelChoiceKind.OptionSet or LevelChoiceKind.Custom or LevelChoiceKind.AsiOrFeat => choice.Options
                        .Where(o => o.Eligible && o.Definition?.Grants.Skills is { Count: > 0 })
                        .SelectMany(o => Picked(key, o.Index, o.Definition!.Grants.Skills)),
                    _ => [],
                };
                foreach (var item in gained)
                {
                    yield return item;
                }
            }
        }

        /// <summary>
        /// Spells of the class list (or the filter's) of the allowed levels that the character does not know for the class.
        /// The class list includes the expanded spell list of the character's subclass (content packs).
        /// </summary>
        /// <remarks>
        /// A class that casts through its subclass (content packs) takes the subclass's spell list and the highest slot
        /// level of its progression. Spells outside <see cref="ChoiceFilter.Schools"/> are listed as not eligible, with the
        /// reason, except at the levels of <see cref="ChoiceFilter.SchoolsExceptAt"/>.
        /// </remarks>
        private List<PlannedOption> SpellCandidates(LevelChoiceRule rule, ChoiceFilter filter)
        {
            var subclassIndex = rule.SubclassIndex ?? character.Classes.FirstOrDefault(c => c.ClassIndex == definition.Index)?.SubclassIndex;
            var casting = SubclassCasting(definition, subclasses, subclassIndex);
            var classList = casting?.SpellList ?? definition.Index;
            var list = filter.SpellList ?? classList;
            // The subclass's expanded list counts as part of the class's own list (not of another list a filter names).
            var expanding = list == classList ? subclasses.FirstOrDefault(s => s.Index == subclassIndex) : null;
            var maxSpellLevel = casting is not null ? SheetCalculator.MaxSpellLevel(casting.SlotsAt(newLevel)) : ClassMaxSpellLevel(definition, classLevel);
            IReadOnlyList<int> levels = rule.Kind == LevelChoiceKind.CantripsKnown || filter.CantripsOnly
                ? [0]
                : filter.SpellLevels.Count > 0
                    ? filter.SpellLevels
                    : Enumerable.Range(1, Math.Max(0, maxSpellLevel)).ToList();
            var known = character.Spells.Where(s => s.ClassIndex == definition.Index).Select(s => s.SpellIndex).ToHashSet(StringComparer.Ordinal);
            var schoolsReason = filter.Schools.Count > 0 ? filter.SchoolsReason() : null;
            return spells
                .Where(s => levels.Contains(s.Level) && !known.Contains(s.Index))
                .Where(s => list == ChoiceFilter.AnyList || s.ClassIndexes.Contains(list, StringComparer.Ordinal) || expanding?.Expands(s) == true)
                .Where(s => !filter.RitualOnly || (definition.Spellcasting?.CastsAsRitual(s.Index, s.Ritual) ?? s.Ritual))
                .Where(filter.AllowsTags)
                .Select(s => filter.AllowsSchool(s.School, newLevel) ? SpellOption(s) : SpellOption(s) with { Eligible = false, Reason = schoolsReason })
                .OrderBy(o => o.Eligible ? 0 : 1)
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
            new(spell.Index, spell.Name, spell.Description.Take(1).ToList(), null, true, null, spell.Level, null, [], spell.Category.ToString());

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
            ItemModifierKind.PassiveBonus when target == ModifierTargets.Perception => ("passivePerception", "Percepción pasiva", sheet.PassivePerception),
            ItemModifierKind.PassiveBonus when target == ModifierTargets.Investigation => (SheetCalculator.PassiveInvestigationKey, "Investigación pasiva", sheet.PassiveInvestigation),
            ItemModifierKind.PassiveBonus => (SheetCalculator.PassiveInsightKey, "Perspicacia pasiva", sheet.PassiveInsight),
            ItemModifierKind.SpellSaveDcBonus => (null, "CD de conjuros", null),
            ItemModifierKind.SpellAttackBonus => (null, "Ataque de conjuro", null),
            ItemModifierKind.ArmorClassFormula => ("armorClass", "CA alternativa (base)", null),
            ItemModifierKind.CriticalRange => (null, "Crítico desde", null),
            ItemModifierKind.AbilityMax when target is not null => ($"ability.{target}", $"Máximo de {BreakdownLabels.Ability(target)}", null),
            ItemModifierKind.Speed when target is not null => (SheetCalculator.SpeedKey(target), $"Velocidad de {ModifierTargets.SpeedLabel(target)}", null),
            ItemModifierKind.DeathSaveFailures => (null, "Fallos de salvación contra muerte para morir", null),
            ItemModifierKind.HitDiceRecovery => (null, "Recupera todos los dados de golpe en descanso largo", null),
            ItemModifierKind.ResourceMaxBonus => (null, $"Usos máximos de {target}", null),
            _ => (null, modifier.Kind.ToString(), null),
        };

        // A formula is shown with its value now (the class level counted as the character level).
        var value = modifier.Formula is { } formula
            ? ModifierFormula.Parts(formula, string.Empty, new ModifierFormula.Context(sheet.ProficiencyBonus, sheet.TotalLevel, sheet.TotalLevel, sheet.Modifier, sheet.Speed))?.Sum(p => p.Value) ?? 0
            : modifier.Value;
        var additive = modifier.Kind is not (ItemModifierKind.ArmorClassFormula or ItemModifierKind.CriticalRange or ItemModifierKind.AbilityMax
            or ItemModifierKind.Speed or ItemModifierKind.DeathSaveFailures or ItemModifierKind.HitDiceRecovery);
        var holds = modifier.Condition is null
            || (modifier.Condition == ModifierConditions.WearingArmor && sheet.WearsArmor)
            || (modifier.Condition == ModifierConditions.NoArmor && !sheet.WearsArmor);
        return new EffectPreviewDto(
            BreakdownSources.Feature,
            affected.Label,
            value,
            affected.Field,
            additive ? affected.Before : null,
            additive && affected.Before is { } before ? before + (holds ? value : 0) : null,
            modifier.Condition is null ? null : ModifierConditions.Describe(modifier.Condition));
    }
}
