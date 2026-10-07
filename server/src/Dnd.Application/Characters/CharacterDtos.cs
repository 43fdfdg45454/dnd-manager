using Dnd.Application.ChangeRequests;
using Dnd.Application.Items;
using Dnd.Domain.Characters;

namespace Dnd.Application.Characters;

/// <summary>One class of a character in the summary.</summary>
public sealed record CharacterClassSummaryDto(string ClassIndex, string ClassName, string? SubclassName, int Level);

/// <summary>What every member sees of a character. Hit points only for the owner and DMs (null otherwise).</summary>
public sealed record CharacterSummaryDto(
    Guid Id,
    Guid CampaignId,
    Guid? OwnerUserId,
    string? OwnerDisplayName,
    string Name,
    string Status,
    string? RaceName,
    IReadOnlyList<CharacterClassSummaryDto> Classes,
    int Level,
    int? HitPointsCurrent,
    int? HitPointsMax,
    string? PortraitUrl);

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
/// </summary>
public sealed record CharacterResourceDto(Guid Id, string? Key, string Name, int Max, int Used, string Recharge, bool IsAuto)
{
    public RollOnRestDto? RollOnRest { get; init; }

    public IReadOnlyList<int> Rolls { get; init; } = [];

    public bool RollsPending { get; init; }

    public static CharacterResourceDto From(CharacterResource r) => new(r.Id, r.Key, r.Name, r.Max, r.Used, r.Recharge.ToString(), r.IsAuto)
    {
        RollOnRest = r.RollOnRest is { } roll ? new RollOnRestDto(roll.Dice, roll.Count, roll.Rest == RestKind.Short ? "short" : "long") : null,
        Rolls = r.Rolls,
        RollsPending = r.RollsPending,
    };
}

/// <summary>Dice rolled after a rest: <see cref="Count"/> × <see cref="Dice"/> ("d20") after a <see cref="Rest"/> ("short" or "long") rest.</summary>
public sealed record RollOnRestDto(string Dice, int Count, string Rest);

/// <summary>Spell slots of a level; level 0 = Pact Magic slots (all of the pact slot level).</summary>
public sealed record SpellSlotDto(int Level, int Max, int Used);

public sealed record CharacterConditionDto(string Index, string? Note);

public sealed record AbilityDto(int Score, int Modifier, bool Overridden);

public sealed record SavingThrowDto(int Value, bool Proficient, bool Overridden);

public sealed record SheetSkillDto(string Index, string Name, string Ability, int Value, bool Proficient, bool Expertise, bool Overridden);

public sealed record HitDiceDto(string ClassIndex, int Die, int Total, int Remaining);

public sealed record SpellcastingDto(string ClassIndex, string Ability, int SaveDc, int AttackBonus, int? PreparedMax);

/// <param name="PactSlotLevel">Spell level of the Pact Magic slots (null without them).</param>
/// <summary>An item modifier applied to the sheet; <see cref="Kind"/> is an <c>ItemModifierKind</c> name.</summary>
public sealed record ItemEffectDto(string ItemName, string Kind, string? Target, int Value);

/// <summary>One term of a calculated value; <see cref="Source"/> is a <c>BreakdownSources</c> value ("ability", "item", "override"...).</summary>
public sealed record BreakdownPartDto(string Source, string Label, int Value);

/// <summary>A calculated value explained point by point: the part values add up to <see cref="Total"/>.</summary>
public sealed record ValueBreakdownDto(int Total, IReadOnlyList<BreakdownPartDto> Parts)
{
    public static ValueBreakdownDto From(ValueBreakdown breakdown) =>
        new(breakdown.Total, breakdown.Parts.Select(p => new BreakdownPartDto(p.Source, p.Label, p.Value)).ToList());
}

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
}

/// <summary>A resisted damage type; <see cref="Source"/> is "race", "subrace" or "background", <see cref="Label"/> what grants it.</summary>
public sealed record ResistanceDto(string DamageType, string Source, string Label);

/// <summary>Breath weapon: damage dice at the current level, damage type, saving throw ability, area and DC.</summary>
public sealed record BreathWeaponValueDto(string Name, string Source, string DamageType, string Dice, string SaveAbility, string Area, int Dc);

/// <summary>Full character: stored fields, child collections, calculated sheet, inventory and pending change requests.</summary>
public sealed record CharacterDetailDto
{
    public required Guid Id { get; init; }

    public required Guid CampaignId { get; init; }

    public required Guid? OwnerUserId { get; init; }

    public required string? OwnerDisplayName { get; init; }

    public required string Name { get; init; }

    public required string Status { get; init; }

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

    public required int CopperPieces { get; init; }

    /// <summary>Spent hit dice by class index.</summary>
    public required IReadOnlyDictionary<string, int> HitDiceUsed { get; init; }

    public required string Notes { get; init; }

    public required string Backstory { get; init; }

    public required string PersonalityTraits { get; init; }

    public required string Ideals { get; init; }

    public required string Bonds { get; init; }

    public required string Flaws { get; init; }

    /// <summary>Result of the optional table of the background, e.g. "Especialidad: Bibliotecario".</summary>
    public required string BackgroundDetail { get; init; }

    public required Guid? PortraitFileId { get; init; }

    /// <summary>Relative download URL (<c>/api/v1/files/{id}</c>) of the portrait, or null when there is none.</summary>
    public required string? PortraitUrl { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public required IReadOnlyList<CharacterClassDto> Classes { get; init; }

    public required IReadOnlyList<CharacterProficiencyDto> Proficiencies { get; init; }

    public required IReadOnlyList<CharacterSpellDto> Spells { get; init; }

    public required IReadOnlyList<CharacterOverrideDto> Overrides { get; init; }

    public required IReadOnlyList<CharacterResourceDto> Resources { get; init; }

    public required IReadOnlyList<SpellSlotDto> SpellSlots { get; init; }

    public required CharacterSheetDto Sheet { get; init; }

    public required IReadOnlyList<ChangeRequestDto> PendingChangeRequests { get; init; }

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

/// <summary>One attack: equipped weapon (with <see cref="ItemId"/>) or the unarmed strike. Damage like "1d8+3".</summary>
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
    ValueBreakdownDto DamageBreakdown);

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
