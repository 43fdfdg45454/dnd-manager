using OpenTrpg.Core.Application.Systems;
using OpenTrpg.Core.Application.ChangeRequests;
using OpenTrpg.Core.Application.Items;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Application;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Application.Items;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Application.Characters;

/// <summary>One class of a character in the summary.</summary>
public sealed record CharacterClassSummaryDto(string ClassIndex, string ClassName, string? SubclassName, int Level);

/// <summary>The D&amp;D 5e fields of a character in the campaign roster (<see cref="ISheetSystem.BuildRosterLine"/>).</summary>
public sealed record Dnd5eRosterLineDto(
    string? RaceName,
    IReadOnlyList<CharacterClassSummaryDto> Classes,
    int Level,
    int? HitPointsCurrent,
    int? HitPointsMax);

/// <param name="CatalogMissing">True when the class or the subclass is no longer in the catalog (e.g. its content pack was deleted).</param>
public sealed record CharacterClassDto(string ClassIndex, string ClassName, string? SubclassIndex, string? SubclassName, int Level, int Order, bool CatalogMissing = false);

public sealed record CharacterProficiencyDto(Guid Id, string Type, string Key, bool Expertise, string Source);

/// <param name="SpellName">Name from the catalog (null when the spell is not in it).</param>
/// <param name="SpellLevel">Spell level from the catalog (0 = cantrip; null when unknown).</param>
/// <param name="CatalogMissing">True when the spell is no longer in the catalog (e.g. its content pack was deleted).</param>
/// <param name="Category">A <c>SpellCategory</c> name from the catalog (null when the spell is not in it).</param>
public sealed record CharacterSpellDto(Guid Id, string SpellIndex, string ClassIndex, bool IsPrepared, bool AlwaysPrepared, string? SpellName, int? SpellLevel, bool CatalogMissing = false, string? Category = null);

public sealed record CharacterOverrideDto(string Field, int Value, string? Note);

/// <summary>
/// A limited-use resource. <see cref="RollOnRest"/>: dice rolled after a rest whose results are kept in
/// <see cref="Rolls"/>; while <see cref="RollsPending"/> the player must write them (<c>POST …/resources/{id}/rolls</c>).
/// <see cref="Dice"/>: die rolled with each use ("d8"), <see cref="Source"/>: the feature or option that grants it
/// ("Ventaja táctica (nivel 3)") and <see cref="Breakdown"/>: how its maximum was obtained (pack resources).
/// </summary>
public sealed record CharacterResourceDto(Guid Id, string? Key, string Name, int Max, int Used, string Recharge, bool IsAuto)
{
    public RollOnRestDto? RollOnRest { get; init; }

    public IReadOnlyList<int> Rolls { get; init; } = [];

    public bool RollsPending { get; init; }

    public string? Dice { get; init; }

    public string? Source { get; init; }

    public ValueBreakdownDto? Breakdown { get; init; }

    /// <summary>Chosen options that spend this resource (by its key), each with its amount: the "Usar" buttons of the Combat tab.</summary>
    public IReadOnlyList<CharacterOptionCostDto> Options { get; init; } = [];

    /// <summary>Die rolled to get the uses back (<c>POST …/resources/{id}/recharge-roll</c>), or null.</summary>
    public RechargeRollDto? RechargeRoll { get; init; }

    /// <summary>
    /// What grants the resource: "feature" (class or subclass feature, <c>GET /catalog/features/{SourceIndex}</c>),
    /// "trait" (racial trait), "option", "feat" or "spell" (uses per long rest of the spell <see cref="SourceIndex"/>);
    /// null for manual resources and when unknown.
    /// </summary>
    public string? SourceKind { get; init; }

    /// <summary>Catalog index of the origin named by <see cref="SourceKind"/>, or null.</summary>
    public string? SourceIndex { get; init; }

    /// <summary>Rules text of the origin when the catalog has no detail page for it (options, feats, racial traits), or null.</summary>
    public string? SourceDescription { get; init; }

    /// <param name="template">Current template of an automatic resource (its die, origin and breakdown), if any.</param>
    public static CharacterResourceDto From(CharacterResource r, ResourceTemplate? template = null) => new(r.Id, r.Key, r.Name, r.Max, r.Used, r.Recharge.ToString(), r.IsAuto)
    {
        RollOnRest = r.RollOnRest is { } roll ? new RollOnRestDto(roll.Dice, roll.Count, roll.Rest == RestKind.Short ? "short" : "long") : null,
        Rolls = r.Rolls,
        RollsPending = r.RollsPending,
        Dice = template?.Dice,
        Source = template?.Source,
        Breakdown = template?.Breakdown is { } breakdown && breakdown.Total == r.Max ? ValueBreakdownDto.From(breakdown) : null,
        RechargeRoll = template?.RechargeRoll is { } recharge ? new RechargeRollDto(recharge.Dice, recharge.Min) : null,
        SourceKind = template?.SourceKind,
        SourceIndex = template?.SourceIndex,
        SourceDescription = template?.SourceDescription,
    };
}

/// <summary>The uses come back with <see cref="Min"/> or more on a <see cref="Dice"/> ("d6").</summary>
public sealed record RechargeRollDto(string Dice, int Min);

/// <summary>Dice rolled after a rest: <see cref="Count"/> × <see cref="Dice"/> ("d20") after a <see cref="Rest"/> ("short" or "long") rest.</summary>
public sealed record RollOnRestDto(string Dice, int Count, string Rest);

/// <summary>Spell slots of a level; level 0 = Pact Magic slots (all of the pact slot level).</summary>
public sealed record SpellSlotDto(int Level, int Max, int Used);

public sealed record CharacterConditionDto(string Index, string? Note);

public sealed record AbilityDto(int Score, int Modifier, bool Overridden);

public sealed record SavingThrowDto(int Value, bool Proficient, bool Overridden);

public sealed record SheetSkillDto(string Index, string Name, string Ability, int Value, bool Proficient, bool Expertise, bool Overridden);

public sealed record HitDiceDto(string ClassIndex, int Die, int Total, int Remaining);

public sealed record SpellcastingDto(string ClassIndex, string Ability, int SaveDc, int AttackBonus, int? PreparedMax)
{
    /// <summary>Name of an entry of an option, feat or subclass that grants spells with its own ability (its index is <see cref="ClassIndex"/>); null for classes and "race".</summary>
    public string? Name { get; init; }

    /// <summary>True when the ability was chosen by an override (<c>spellcastingAbility.&lt;classIndex&gt;</c>, 1 = str … 6 = cha): the app marks it.</summary>
    public bool AbilityOverridden { get; init; }

    /// <summary>Spells known allowed at the class level (subclass casters from content packs); null when the class has no fixed number.</summary>
    public int? SpellsKnownMax { get; init; }

    /// <summary>Cantrips known allowed at the class level (subclass casters); null when the class has no fixed number.</summary>
    public int? CantripsKnownMax { get; init; }

    /// <summary>Highest spell level the class can cast now (by its slots, or its table without slots); 0 for cantrips only.</summary>
    public int MaxSpellLevel { get; init; }

    /// <summary>
    /// Progression of a pack class: "hitPoints" (no slots: each spell spends a use of <see cref="UsesResource"/> and the
    /// hit points of <see cref="HitPointCost"/>) or "ritualsOnly" (no slots: only rituals of its book); "full", "half",
    /// "third", "pact" or "table" otherwise; null for the SRD classes.
    /// </summary>
    public string? Progression { get; init; }

    /// <summary>"hitPoints": hit points lost by spell level (index 0 = 1st level), up to <see cref="MaxSpellLevel"/>.</summary>
    public IReadOnlyList<int> HitPointCost { get; init; } = [];

    /// <summary>"hitPoints": key of the class resource each spell spends.</summary>
    public string? UsesResource { get; init; }

    /// <summary>Save DC of the cantrips when a feature changes it (breakdown "spellSaveDc.&lt;class&gt;.cantrip"); null otherwise.</summary>
    public int? CantripSaveDc { get; init; }

    /// <summary>Spell attack bonus of the cantrips when a feature changes it ("spellAttackBonus.&lt;class&gt;.cantrip"); null otherwise.</summary>
    public int? CantripAttackBonus { get; init; }

    /// <summary>Lowest d20 roll of a spell attack that is a critical hit (20 by default).</summary>
    public int CriticalRange { get; init; } = 20;

    public string? CriticalRangeSource { get; init; }
}

/// <param name="PactSlotLevel">Spell level of the Pact Magic slots (null without them).</param>
/// <summary>An item modifier applied to the sheet; <see cref="Kind"/> is an <c>ItemModifierKind</c> name.</summary>
public sealed record ItemEffectDto(string ItemName, string Kind, string? Target, int Value);

/// <param name="ItemEffects">Item modifiers applied to the sheet, for the UI to mark the values affected by items.</param>
/// <param name="Breakdowns">
/// How every value was obtained, keyed like the override fields ("ability.dex", "save.wis", "skill.stealth",
/// "armorClass", "initiative", "speed", "hitPointsMax", "passivePerception", "proficiencyBonus") plus
/// "spellSaveDc.&lt;classIndex&gt;" and "spellAttackBonus.&lt;classIndex&gt;".
/// </param>
public sealed record CharacterSheetDto(
    IReadOnlyDictionary<string, AbilityDto> Abilities,
    int TotalLevel,
    int ProficiencyBonus,
    IReadOnlyDictionary<string, SavingThrowDto> SavingThrows,
    IReadOnlyList<SheetSkillDto> Skills,
    int PassivePerception,
    int Initiative,
    int ArmorClass,
    int Speed,
    int HitPointsMax,
    IReadOnlyList<HitDiceDto> HitDice,
    IReadOnlyList<SpellcastingDto> Spellcasting,
    int? PactSlotLevel,
    IReadOnlyList<string> OverriddenFields,
    IReadOnlyList<ItemEffectDto> ItemEffects,
    IReadOnlyDictionary<string, ValueBreakdownDto> Breakdowns)
{
    /// <summary>Damage resistances (race traits and chosen options such as a draconic ancestry).</summary>
    public IReadOnlyList<ResistanceDto> Resistances { get; init; } = [];

    /// <summary>Breath weapon of the chosen draconic ancestry (DC breakdown in <c>breakdowns["breathWeapon.dc"]</c>), or null.</summary>
    public BreathWeaponValueDto? BreathWeapon { get; init; }

    /// <summary>Damage immunities (content packs); <see cref="DefenseDto.Type"/> is a damage type.</summary>
    public IReadOnlyList<DefenseDto> Immunities { get; init; } = [];

    /// <summary>Condition immunities (content packs); <see cref="DefenseDto.Type"/> is a condition index.</summary>
    public IReadOnlyList<DefenseDto> ConditionImmunities { get; init; } = [];

    /// <summary>Saving throw proficiencies lost by an origin feat; <see cref="DefenseDto.Type"/> is the ability.</summary>
    public IReadOnlyList<DefenseDto> LostSavingThrows { get; init; } = [];

    /// <summary>Skill proficiencies the character loses (<c>grants.removeSkills</c>), with their origin.</summary>
    public IReadOnlyList<DefenseDto> LostSkills { get; init; } = [];

    /// <summary>Save DCs of the classes besides the spell one, each with its breakdown key.</summary>
    public IReadOnlyList<ClassSaveDcDto> ClassSaveDcs { get; init; } = [];

    /// <summary>Passive Investigation (breakdown "passiveInvestigation").</summary>
    public int PassiveInvestigation { get; init; }

    /// <summary>Passive Insight (breakdown "passiveInsight").</summary>
    public int PassiveInsight { get; init; }

    /// <summary>Highest score of each ability (20 unless a feature changes it).</summary>
    public IReadOnlyDictionary<string, int> AbilityMaxScores { get; init; } = new Dictionary<string, int>();

    /// <summary>"tiny", "small", "medium"...; empty without race.</summary>
    public string Size { get; init; } = string.Empty;

    /// <summary>The race, or the feature that changed the size.</summary>
    public string? SizeSource { get; init; }

    /// <summary>Swimming, climbing and flying speeds by mode ("swim" → 30); breakdowns "speed.&lt;mode&gt;".</summary>
    public IReadOnlyDictionary<string, int> Speeds { get; init; } = new Dictionary<string, int>();

    /// <summary>Failed death saves that kill the character (3 unless a feature changes it).</summary>
    public int DeathSaveFailuresToDie { get; init; } = 3;

    public string? DeathSaveFailuresSource { get; init; }

    /// <summary>A long rest recovers every hit die (instead of half).</summary>
    public bool RecoversAllHitDice { get; init; }

    public string? HitDiceRecoverySource { get; init; }
}

/// <summary>An immunity, condition immunity or lost saving throw with what grants it.</summary>
public sealed record DefenseDto(string Type, string Source, string Label);

/// <summary>A class save DC ("CD de ejemplo" = 8 + proficiency + ability) and the key of its breakdown.</summary>
public sealed record ClassSaveDcDto(string ClassIndex, string Name, string Ability, int Dc, string BreakdownKey);

/// <summary>A resisted damage type; <see cref="Source"/> is "race", "subrace" or "background", <see cref="Label"/> what grants it.</summary>
public sealed record ResistanceDto(string DamageType, string Source, string Label);

/// <summary>Breath weapon: damage dice at the current level, damage type, saving throw ability, area and DC.</summary>
public sealed record BreathWeaponValueDto(string Name, string Source, string DamageType, string Dice, string SaveAbility, string Area, int Dc);

/// <summary>
/// The D&amp;D 5e fields of the detail of a character (<see cref="ISheetSystem.BuildDetailAsync"/>), written at the same
/// level as the core fields of <see cref="CharacterDetailDto"/>.
/// </summary>
public sealed record Dnd5eCharacterDetailDto
{
    public required string? RaceIndex { get; init; }

    public required string? RaceName { get; init; }

    public required string? SubraceIndex { get; init; }

    public required string? SubraceName { get; init; }

    public required string? BackgroundIndex { get; init; }

    public required string? BackgroundName { get; init; }

    /// <summary>True when the race or the subrace is set but no longer in the catalog (e.g. its content pack was deleted).</summary>
    public bool RaceCatalogMissing { get; init; }

    /// <summary>True when the background is set but no longer in the catalog.</summary>
    public bool BackgroundCatalogMissing { get; init; }

    /// <summary>
    /// True when anything the character references (race, subrace, background, classes, subclasses, spells) is no
    /// longer in the catalog. The sheet is still calculated (a missing class counts as d8 without features).
    /// </summary>
    public bool CatalogMissing { get; init; }

    public required string? Alignment { get; init; }

    public required bool ApplyRacialBonuses { get; init; }

    public required string HpMode { get; init; }

    public required int BaseStr { get; init; }

    public required int BaseDex { get; init; }

    public required int BaseCon { get; init; }

    public required int BaseInt { get; init; }

    public required int BaseWis { get; init; }

    public required int BaseCha { get; init; }

    public required int HitPointsCurrent { get; init; }

    public required int TemporaryHitPoints { get; init; }

    public required int DeathSaveSuccesses { get; init; }

    public required int DeathSaveFailures { get; init; }

    public required int ExhaustionLevel { get; init; }

    public required IReadOnlyList<CharacterConditionDto> Conditions { get; init; }

    public required string? ConcentratingOnSpellIndex { get; init; }

    public required bool Inspiration { get; init; }

    /// <summary>Spent hit dice by class index.</summary>
    public required IReadOnlyDictionary<string, int> HitDiceUsed { get; init; }

    /// <summary>Result of the optional table of the background, e.g. "Especialidad: Bibliotecario".</summary>
    public required string BackgroundDetail { get; init; }

    public required IReadOnlyList<CharacterClassDto> Classes { get; init; }

    public required IReadOnlyList<CharacterProficiencyDto> Proficiencies { get; init; }

    public required IReadOnlyList<CharacterSpellDto> Spells { get; init; }

    public required IReadOnlyList<CharacterOverrideDto> Overrides { get; init; }

    public required IReadOnlyList<CharacterResourceDto> Resources { get; init; }

    public required IReadOnlyList<SpellSlotDto> SpellSlots { get; init; }

    public required CharacterSheetDto Sheet { get; init; }

    public required InventoryDto Inventory { get; init; }

    /// <summary>Precalculated data of the combat view.</summary>
    public required CombatSummaryDto Combat { get; init; }

    /// <summary>Rest the owner is asking the DM for, or null.</summary>
    public PendingRestDto? PendingRest { get; init; }

    /// <summary>Level granted by a DM that the player has not completed yet, or null.</summary>
    public int? PendingLevelUpTo { get; init; }

    /// <summary>The player must prepare spells (the app forces the preparation screen); see <c>/spell-preparation</c>.</summary>
    public bool SpellPreparationPending { get; init; }

    /// <summary>"Creation", "LongRest" or "LevelUp" while <see cref="SpellPreparationPending"/>; null otherwise.</summary>
    public string? SpellPreparationReason { get; init; }

    /// <summary>
    /// Options and feats whose prerequisites no longer hold: they must be replaced (<c>/invalid-choices</c>, forced step
    /// after the pending level-up; every level-up asks for them too).
    /// </summary>
    public IReadOnlyList<InvalidChoiceDto> InvalidChoices { get; init; } = [];

    /// <summary>Some resource asks for its dice to be rolled after the last rest (forced step after spell preparation).</summary>
    public bool RestRollsPending { get; init; }

    /// <summary>Level choices made when gaining levels (subclass, fighting style, ASI, feats, spells...), oldest first.</summary>
    public IReadOnlyList<CharacterChoiceDto> Choices { get; init; } = [];

    /// <summary>Feats the character has (level-ups and origin choices) with their catalog text, oldest first.</summary>
    public IReadOnlyList<CharacterFeatDto> Feats { get; init; } = [];

    /// <summary>Chosen options (and feats) whose use spends a resource ("2 Ki"), in the order they were chosen.</summary>
    public IReadOnlyList<CharacterOptionCostDto> OptionCosts { get; init; } = [];

    /// <summary>The first (oldest) companion with its recalculated statblock; null when the character has none.</summary>
    public CharacterCompanionDto? Companion { get; init; }

    /// <summary>Every companion of the character, oldest first (several when the feature's <c>maxCount</c> allows it).</summary>
    public IReadOnlyList<CharacterCompanionDto> Companions { get; init; } = [];

    /// <summary>Inventory entries with enhancements (phase 35B3): their options, changed fields and breakdowns.</summary>
    public IReadOnlyList<Items.CharacterItemEnhancementDto> ItemEnhancements { get; init; } = [];

    /// <summary>The item enhancement feature the character has reached (options per item and which ones), or null.</summary>
    public Items.ItemEnhancementFeatureDto? ItemEnhancementFeature { get; init; }

    /// <summary>Grafts of the inventory (items with <c>graft.slot</c>), by slot.</summary>
    public IReadOnlyList<Items.CharacterGraftDto> Grafts { get; init; } = [];

    /// <summary>The companion feature the character has reached (which beasts qualify); null when it has none.</summary>
    public CompanionFeatureDto? CompanionFeature { get; init; }

    /// <summary>The character has reached a companion feature and has not chosen the beast yet ("Elegir compañero").</summary>
    public bool CompanionPending { get; init; }
}

/// <summary>
/// A subclass feature that grants an animal companion (content packs, <c>features[].companion</c>): the beasts of the
/// catalog with challenge rating up to <see cref="MaxChallengeRating"/> and one of <see cref="Sizes"/> (empty: any size).
/// </summary>
/// <param name="HitPoints">"beast" or "max(beast, N*classLevel)".</param>
public sealed record CompanionFeatureDto(
    string FeatureIndex,
    string FeatureName,
    string ClassIndex,
    int ClassLevel,
    double MaxChallengeRating,
    string MaxChallengeRatingText,
    IReadOnlyList<string> Sizes,
    string HitPoints,
    bool ProficiencyBonusFromCharacter,
    bool AttackBonusFromCharacter)
{
    /// <summary>Closed list of creatures (any type) of the feature; empty when the companion is a beast of the filter.</summary>
    public IReadOnlyList<string> CreatureIndexes { get; init; } = [];

    /// <summary>The creatures of <see cref="CreatureIndexes"/> found in the catalog, for the chooser.</summary>
    public IReadOnlyList<CompanionCreatureDto> Creatures { get; init; } = [];

    /// <summary>How many companions the character may have at its level (1 by default).</summary>
    public int MaxCount { get; init; } = 1;

    /// <summary>Highest total challenge rating of all the companions at the class level, or null when the feature has no budget.</summary>
    public double? ChallengeBudget { get; init; }

    /// <summary><see cref="ChallengeBudget"/> as text ("1/2"), or null.</summary>
    public string? ChallengeBudgetText { get; init; }

    /// <summary>Bonuses the character gives to its companions, or null.</summary>
    public CompanionBonusesDto? Bonuses { get; init; }

    /// <summary>What the companions gain up to the class level (texts of <c>featuresByLevel</c>).</summary>
    public IReadOnlyList<CompanionFeatureTextDto> Features { get; init; } = [];
}

/// <summary>A creature of a companion feature's closed list.</summary>
public sealed record CompanionCreatureDto(string Index, string Name, string Type, string Size, double ChallengeRating, string ChallengeRatingText);

/// <summary>Bonuses to the companions: each a number ("2") or "proficiencyBonus"; null when none.</summary>
public sealed record CompanionBonusesDto(string? ArmorClass, string? SavingThrows, string? Attack, string? Damage);

/// <summary>A text of what the companions gain at a level of the feature's class.</summary>
public sealed record CompanionFeatureTextDto(int Level, string Text);

/// <summary>A damage component of a companion attack, with the character's bonus already folded into the dice ("2d4+4").</summary>
public sealed record CompanionDamageDto(string Dice, string? Type);

/// <summary>
/// An action of the companion. <see cref="AttackBonus"/> (null without an attack roll) and the flat bonus of the first damage
/// component come with their breakdowns (<see cref="AttackBreakdown"/>, <see cref="DamageBreakdown"/>).
/// </summary>
public sealed record CompanionAttackDto(
    string Name,
    string Description,
    int? AttackBonus,
    ValueBreakdownDto? AttackBreakdown,
    IReadOnlyList<CompanionDamageDto> Damage,
    ValueBreakdownDto? DamageBreakdown,
    bool IsMultiattack);

/// <summary>
/// The animal companion of a character: the stored beast, name and current hit points, and the statblock recalculated
/// with the character's proficiency bonus. <see cref="Breakdowns"/> explains "armorClass", "hitPointsMax", "save.&lt;ability&gt;"
/// and "skill.&lt;skill&gt;" like the sheet's. <see cref="BeastMissing"/> is true when the beast is no longer in the catalog.
/// </summary>
public sealed record CharacterCompanionDto(
    Guid Id,
    string BeastIndex,
    string BeastName,
    string Name,
    string Size,
    string ChallengeRatingText,
    int HitPointsCurrent,
    int HitPointsMax,
    int? HitPointsMaxOverride,
    int ArmorClass,
    IReadOnlyDictionary<string, int> Speeds,
    IReadOnlyDictionary<string, int> Abilities,
    IReadOnlyDictionary<string, int> SavingThrows,
    IReadOnlyDictionary<string, int> Skills,
    IReadOnlyDictionary<string, string> Senses,
    int PassivePerception,
    IReadOnlyList<Catalog.BeastTraitDto> Traits,
    IReadOnlyList<CompanionAttackDto> Attacks,
    IReadOnlyDictionary<string, ValueBreakdownDto> Breakdowns,
    bool BeastMissing)
{
    /// <summary>Creature type ("beast", "undead"...).</summary>
    public string Type { get; init; } = Catalog.BeastDto.BeastType;

    /// <summary>What the companion has gained up to the class level of the feature (texts).</summary>
    public IReadOnlyList<CompanionFeatureTextDto> Features { get; init; } = [];
}

/// <summary>
/// A feat of the character, for the "Rasgos" tab of the sheet: the catalog description, the literal prerequisite
/// and the <see cref="Ability"/> it raised. <see cref="Description"/> is empty when the feat is gone from the catalog.
/// </summary>
/// <param name="Level">Level of the class at which it was taken; 0 for origin choices (variant human, packs).</param>
public sealed record CharacterFeatDto(
    string Index,
    string Name,
    IReadOnlyList<string> Description,
    string? PrerequisitesText,
    string? Ability,
    int Level,
    string? ClassIndex)
{
    public static CharacterFeatDto From(CharacterChoice choice, OptionDefinition? definition)
    {
        var feat = choice.Selection.Feat!;
        return new CharacterFeatDto(
            feat.Index,
            definition?.Name ?? feat.Name,
            definition?.Description ?? [],
            definition?.PrerequisitesText,
            choice.Selection.Ability,
            choice.Level,
            choice.ClassIndex);
    }
}

/// <summary>Something picked in a level choice, with its name.</summary>
public sealed record ChoiceItemDto(string Index, string Name)
{
    public static ChoiceItemDto From(ChoiceItem item) => new(item.Index, item.Name);
}

/// <summary>
/// A level choice of the character. <see cref="Selected"/>/<see cref="Replaced"/> for picks; <see cref="Asi"/>
/// ("str" → 1) for an Ability Score Improvement; <see cref="Feat"/> (and the <see cref="Ability"/> it raised) for a feat.
/// </summary>
/// <param name="Level">Level of the class at which it was chosen; 0 for origin choices (race, background).</param>
/// <param name="ClassIndex">Class of the choice; null for origin choices (keys <c>race.*</c>, <c>background.*</c>).</param>
/// <param name="Name">Name of the choice ("Fighting Style").</param>
/// <param name="Kind">A <c>LevelChoiceKind</c> name.</param>
public sealed record CharacterChoiceDto(
    Guid Id,
    int Level,
    string? ClassIndex,
    string Key,
    string Name,
    string Kind,
    IReadOnlyList<ChoiceItemDto> Selected,
    IReadOnlyList<ChoiceItemDto> Replaced,
    IReadOnlyDictionary<string, int>? Asi,
    ChoiceItemDto? Feat,
    string? Ability,
    DateTimeOffset CreatedAt)
{
    public static CharacterChoiceDto From(CharacterChoice choice)
    {
        var selection = choice.Selection;
        return new CharacterChoiceDto(
            choice.Id,
            choice.Level,
            choice.ClassIndex,
            choice.Key,
            selection.Name,
            selection.Kind,
            selection.Selected.Select(ChoiceItemDto.From).ToList(),
            selection.Replaced.Select(ChoiceItemDto.From).ToList(),
            selection.Asi,
            selection.Feat is null ? null : ChoiceItemDto.From(selection.Feat),
            selection.Ability,
            choice.CreatedAt);
    }
}

// ---- Combat view (phase 6) -------------------------------------------------------------------

/// <summary>
/// One attack: equipped weapon (with <see cref="ItemId"/>; a second profile of the same item is a second attack), a
/// natural weapon (<see cref="Natural"/>, <see cref="Notes"/> says what grants it) or the unarmed strike. Damage like
/// "1d8+3".
/// </summary>
public sealed record AttackDto(
    Guid? ItemId,
    string Name,
    int AttackBonus,
    string Damage,
    string? DamageType,
    string? VersatileDamage,
    string? Range,
    IReadOnlyList<string> Properties,
    string? Notes,
    ValueBreakdownDto AttackBreakdown,
    ValueBreakdownDto DamageBreakdown)
{
    /// <summary>Lowest d20 roll that is a critical hit (20 unless a feature lowers it).</summary>
    public int CriticalRange { get; init; } = 20;

    /// <summary>What lowered <see cref="CriticalRange"/>, or null.</summary>
    public string? CriticalRangeSource { get; init; }

    /// <summary>Problems wielding it ("Requiere Fuerza 16; tienes 14.").</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    public bool Natural { get; init; }
}

/// <summary>A consumable of the inventory (potions, scrolls, ammunition...) for the quick-use list.</summary>
public sealed record QuickConsumableDto(Guid ItemId, string Name, int Quantity, int? Charges);

/// <summary>Class panel of the combat view; <see cref="Data"/> depends on <see cref="ClassIndex"/> (barbarian, wizard, paladin).</summary>
public sealed record ClassPanelDto(string ClassIndex, int Level, object Data);

/// <summary>A once-per-long-rest feature (long rest resource with one use).</summary>
public sealed record OnceSinceLongRestDto(string? Key, string Name, bool Used);

/// <summary>Everything the combat view needs, precalculated.</summary>
/// <param name="SpellSlots">Regular slots, levels 1-9 with a maximum (or spent ones).</param>
/// <param name="PactSlots">Pact Magic slots (level = pact slot level), or null.</param>
public sealed record CombatSummaryDto(
    IReadOnlyList<AttackDto> Attacks,
    IReadOnlyList<SpellSlotDto> SpellSlots,
    SpellSlotDto? PactSlots,
    IReadOnlyList<CharacterResourceDto> Resources,
    IReadOnlyList<QuickConsumableDto> QuickConsumables,
    IReadOnlyList<ClassPanelDto> ClassPanels,
    IReadOnlyList<OnceSinceLongRestDto> OnceSinceLongRest);

public sealed record UsesDto(int Max, int Used);

public sealed record BarbarianPanelData(int RageDamageBonus, UsesDto RageUses, bool RecklessAttack, int BrutalCriticalDice, int UnarmoredDefenseAc);

public sealed record ArcaneRecoveryPanelDto(bool Used, int SlotLevelsRecoverable);

/// <param name="Spellbook">Wizard spells (cantrips excluded), by index.</param>
/// <param name="Prepared">Prepared wizard spells (cantrips excluded), by index.</param>
public sealed record WizardPanelData(IReadOnlyList<string> Spellbook, IReadOnlyList<string> Prepared, int PreparedMax, ArcaneRecoveryPanelDto ArcaneRecovery);

/// <summary>Druid panel (Circle of the Land): Natural Recovery, same shape as Arcane Recovery.</summary>
public sealed record DruidPanelData(ArcaneRecoveryPanelDto NaturalRecovery);

public sealed record LayOnHandsPanelDto(int Pool, int Used);

/// <param name="ExtraDice">Divine Smite dice for a slot of that level, e.g. "2d8".</param>
public sealed record SmiteSlotDto(int Level, int Available, string ExtraDice);

public sealed record DivineSmitePanelDto(IReadOnlyList<SmiteSlotDto> SlotsByLevel);

/// <param name="AuraRange">Aura range in feet (0 before paladin level 6).</param>
public sealed record PaladinPanelData(LayOnHandsPanelDto LayOnHands, DivineSmitePanelDto DivineSmite, UsesDto ChannelDivinity, int AuraRange);

/// <summary>Result of Divine Smite: the updated character and the extra damage dice ("2d8").</summary>
public sealed record DivineSmiteResultDto(CharacterDetailDto Character, string DamageDice);
