using System.Text.Json;

namespace Dnd.Infrastructure.Catalog;

// Read models of a content pack file (docs/content-packs.md). Every property is nullable so that missing
// fields are reported by ContentPackValidator with their path instead of failing the deserialization;
// unknown properties are rejected by the serializer (UnmappedMemberHandling.Disallow) to catch typos.

internal sealed class PackJson
{
    public int? FormatVersion { get; set; }

    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Version { get; set; }

    public List<PackClassExtensionJson?>? ClassesExtended { get; set; }

    public List<PackItemJson?>? Items { get; set; }

    public List<PackSpellJson?>? Spells { get; set; }

    public List<PackRaceJson?>? Races { get; set; }

    public List<PackBackgroundJson?>? Backgrounds { get; set; }

    /// <summary>Format 2: option sets (new ones, or options added to sets of the SRD or other packs).</summary>
    public List<PackOptionSetJson?>? OptionSets { get; set; }

    /// <summary>Trinket table rolled at character creation (d100), pointing to items of the SRD or of the pack.</summary>
    public List<PackTrinketJson?>? Trinkets { get; set; }

    /// <summary>Generic roll tables (e.g. a d100 Wild Magic Surge), optionally tied to a class or subclass.</summary>
    public List<PackRollTableJson?>? RollTables { get; set; }
}

internal sealed class PackRollTableJson
{
    public string? Key { get; set; }

    public string? Name { get; set; }

    /// <summary>"d100", "d20"...</summary>
    public string? Dice { get; set; }

    public List<PackRollTableEntryJson?>? Entries { get; set; }

    public string? ClassIndex { get; set; }

    public string? SubclassIndex { get; set; }
}

internal sealed class PackRollTableEntryJson
{
    public int? From { get; set; }

    /// <summary>Last result of the range; absent means the same as <see cref="From"/>.</summary>
    public int? To { get; set; }

    public string? Text { get; set; }
}

internal sealed class PackTrinketJson
{
    public int? Roll { get; set; }

    /// <summary>Item index (SRD or this pack).</summary>
    public string? Item { get; set; }
}

internal sealed class PackClassExtensionJson
{
    public string? ClassIndex { get; set; }

    public List<PackSubclassJson?>? Subclasses { get; set; }

    /// <summary>Format 2: level choices of the base class.</summary>
    public List<PackLevelChoiceJson?>? LevelChoices { get; set; }
}

internal sealed class PackSubclassJson
{
    public string? Index { get; set; }

    public string? Name { get; set; }

    public string? Flavor { get; set; }

    public List<string?>? Description { get; set; }

    public List<PackSubclassLevelJson?>? Levels { get; set; }

    /// <summary>Format 2: level choices of the subclass.</summary>
    public List<PackLevelChoiceJson?>? LevelChoices { get; set; }
}

internal sealed class PackSubclassLevelJson
{
    public int? Level { get; set; }

    public List<PackFeatureJson?>? Features { get; set; }

    /// <summary>Format 2: proficiencies and always-prepared spells gained at this subclass level.</summary>
    public PackGrantsJson? Grants { get; set; }
}

internal sealed class PackFeatureJson
{
    public string? Index { get; set; }

    public string? Name { get; set; }

    public List<string?>? Description { get; set; }
}

internal sealed class PackItemJson
{
    public string? Index { get; set; }

    public string? Name { get; set; }

    public string? Category { get; set; }

    public string? Subcategory { get; set; }

    public string? Rarity { get; set; }

    public bool? RequiresAttunement { get; set; }

    public int? CostCp { get; set; }

    public decimal? WeightLb { get; set; }

    public string? DamageDice { get; set; }

    public string? DamageType { get; set; }

    public string? VersatileDice { get; set; }

    public List<string?>? Properties { get; set; }

    public int? RangeNormal { get; set; }

    public int? RangeLong { get; set; }

    public int? ArmorClassBase { get; set; }

    public bool? AddDexModifier { get; set; }

    public int? MaxDexBonus { get; set; }

    public int? StrengthMinimum { get; set; }

    public bool? StealthDisadvantage { get; set; }

    public List<string?>? Description { get; set; }

    public List<string?>? Effects { get; set; }

    public List<PackModifierJson?>? Modifiers { get; set; }
}

internal sealed class PackModifierJson
{
    public string? Kind { get; set; }

    public string? Target { get; set; }

    public int? Value { get; set; }
}

internal sealed class PackSpellJson
{
    public string? Index { get; set; }

    public string? Name { get; set; }

    public int? Level { get; set; }

    public string? School { get; set; }

    public string? CastingTime { get; set; }

    public string? Range { get; set; }

    public List<string?>? Components { get; set; }

    public string? Material { get; set; }

    public string? Duration { get; set; }

    public bool? Concentration { get; set; }

    public bool? Ritual { get; set; }

    public List<string?>? Description { get; set; }

    public List<string?>? HigherLevel { get; set; }

    public List<string?>? Classes { get; set; }

    public List<string?>? Subclasses { get; set; }

    public string? AttackType { get; set; }

    public PackSpellDamageJson? Damage { get; set; }

    public string? DcAbility { get; set; }

    /// <summary>Optional SpellCategory name; derived from damage and saving throw when absent.</summary>
    public string? Category { get; set; }
}

internal sealed class PackSpellDamageJson
{
    public string? Type { get; set; }

    public Dictionary<string, string?>? AtSlotLevel { get; set; }

    public Dictionary<string, string?>? AtCharacterLevel { get; set; }
}

internal sealed class PackRaceJson
{
    public string? Index { get; set; }

    public string? Name { get; set; }

    public int? Speed { get; set; }

    public string? Size { get; set; }

    public string? SizeDescription { get; set; }

    public List<PackAbilityBonusJson?>? AbilityBonuses { get; set; }

    public List<string?>? Languages { get; set; }

    public string? Age { get; set; }

    public string? Alignment { get; set; }

    public List<PackTraitJson?>? Traits { get; set; }

    public List<PackSubraceJson?>? Subraces { get; set; }

    /// <summary>Decisions asked at creation (phase 19).</summary>
    public PackOriginChoicesJson? Choices { get; set; }

    /// <summary>Damage types always resisted ("fire").</summary>
    public List<string?>? Resistances { get; set; }
}

internal sealed class PackSubraceJson
{
    public string? Index { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public List<PackAbilityBonusJson?>? AbilityBonuses { get; set; }

    public List<PackTraitJson?>? Traits { get; set; }

    public PackOriginChoicesJson? Choices { get; set; }

    public List<string?>? Resistances { get; set; }
}

/// <summary>Decisions of a race, subrace or background (normalized to <c>RaceChoices</c>).</summary>
internal sealed class PackOriginChoicesJson
{
    public PackAbilityBonusChoiceJson? AbilityBonuses { get; set; }

    public PackPickChoiceJson? Skills { get; set; }

    public PackPickChoiceJson? Languages { get; set; }

    public PackPickChoiceJson? Tools { get; set; }

    public PackCantripChoiceJson? Cantrip { get; set; }

    public PackFeatChoiceJson? Feats { get; set; }

    public List<PackTraitOptionChoiceJson?>? TraitOptions { get; set; }
}

internal sealed class PackAbilityBonusChoiceJson
{
    public int? Choose { get; set; }

    public int? Amount { get; set; }

    /// <summary>Ability indexes; absent = any of the six.</summary>
    public List<string?>? From { get; set; }
}

internal sealed class PackPickChoiceJson
{
    public int? Choose { get; set; }

    /// <summary>Allowed values; absent = any.</summary>
    public List<string?>? From { get; set; }
}

internal sealed class PackCantripChoiceJson
{
    public int? Choose { get; set; }

    /// <summary>Class whose spell list is used ("wizard"), or "any".</summary>
    public string? SpellList { get; set; }

    public List<string?>? From { get; set; }
}

internal sealed class PackFeatChoiceJson
{
    public int? Choose { get; set; }
}

internal sealed class PackTraitOptionChoiceJson
{
    public string? Key { get; set; }

    public string? Name { get; set; }

    public int? Choose { get; set; }

    public List<PackTraitOptionJson?>? Options { get; set; }
}

internal sealed class PackTraitOptionJson
{
    public string? Index { get; set; }

    public string? Name { get; set; }

    public List<string?>? Description { get; set; }

    /// <summary>Damage type resisted with this option ("fire").</summary>
    public string? DamageType { get; set; }
}

internal sealed class PackAbilityBonusJson
{
    public string? Ability { get; set; }

    public int? Bonus { get; set; }
}

internal sealed class PackTraitJson
{
    public string? Index { get; set; }

    public string? Name { get; set; }

    public List<string?>? Description { get; set; }
}

internal sealed class PackBackgroundJson
{
    public string? Index { get; set; }

    public string? Name { get; set; }

    public string? FeatureName { get; set; }

    public List<string?>? FeatureDescription { get; set; }

    public List<string?>? SkillProficiencies { get; set; }

    public string? StartingEquipmentText { get; set; }

    /// <summary>Structured starting equipment (same schema as the catalog, without <c>gold</c>).</summary>
    public PackStartingEquipmentJson? StartingEquipment { get; set; }

    /// <summary>Decisions asked at creation (languages, tools, skills...).</summary>
    public PackOriginChoicesJson? Choices { get; set; }

    /// <summary>Personality tables: traits, ideals, bonds and flaws.</summary>
    public PackPersonalityJson? Personality { get; set; }

    /// <summary>Optional tables of the background (specialty, scheme, origin...).</summary>
    public List<PackBackgroundTableJson?>? OptionalTables { get; set; }
}

internal sealed class PackPersonalityJson
{
    public List<string?>? Traits { get; set; }

    public List<PackIdealJson?>? Ideals { get; set; }

    public List<string?>? Bonds { get; set; }

    public List<string?>? Flaws { get; set; }
}

internal sealed class PackIdealJson
{
    public string? Text { get; set; }

    public string? Alignment { get; set; }
}

internal sealed class PackBackgroundTableJson
{
    public string? Key { get; set; }

    public string? Name { get; set; }

    public List<string?>? Entries { get; set; }
}

internal sealed class PackStartingEquipmentJson
{
    public List<PackStartingItemJson?>? Fixed { get; set; }

    public List<PackStartingChoiceJson?>? Choices { get; set; }

    /// <summary>Only classes have an alternative starting wealth; given here it is reported as an error.</summary>
    public JsonElement? Gold { get; set; }

    public int? FixedGoldCp { get; set; }
}

internal sealed class PackStartingItemJson
{
    public string? Item { get; set; }

    public int? Quantity { get; set; }
}

internal sealed class PackStartingChoiceJson
{
    public string? Description { get; set; }

    public int? Choose { get; set; }

    public List<PackStartingOptionJson?>? Options { get; set; }
}

internal sealed class PackStartingOptionJson
{
    public string? Label { get; set; }

    public List<PackStartingItemJson?>? Items { get; set; }

    /// <summary>Shorthand for one entry of <see cref="Categories"/>.</summary>
    public string? Category { get; set; }

    public int? CategoryChoose { get; set; }

    public List<PackCategoryPickJson?>? Categories { get; set; }
}

internal sealed class PackCategoryPickJson
{
    public string? Category { get; set; }

    public int? Choose { get; set; }
}

// ---- Format 2: level choices -----------------------------------------------------------------------

internal sealed class PackOptionSetJson
{
    public string? SetId { get; set; }

    public string? Name { get; set; }

    public List<PackOptionJson?>? Options { get; set; }
}

internal sealed class PackOptionJson
{
    public string? Index { get; set; }

    public string? Name { get; set; }

    public List<string?>? Description { get; set; }

    public string? PrerequisitesText { get; set; }

    public PackPrerequisitesJson? Prerequisites { get; set; }

    public List<PackChoiceModifierJson?>? Modifiers { get; set; }

    public PackAbilityIncreaseJson? AbilityIncrease { get; set; }

    public PackGrantsJson? Grants { get; set; }

    public PackResourceJson? Resource { get; set; }
}

internal sealed class PackPrerequisitesJson
{
    public int? MinLevel { get; set; }

    public string? PactBoon { get; set; }

    public string? Cantrip { get; set; }

    public Dictionary<string, int?>? Abilities { get; set; }
}

internal sealed class PackChoiceModifierJson
{
    public string? Kind { get; set; }

    public string? Target { get; set; }

    public int? Value { get; set; }

    public string? Condition { get; set; }
}

internal sealed class PackAbilityIncreaseJson
{
    public int? Amount { get; set; }

    public List<string?>? From { get; set; }
}

internal sealed class PackGrantsJson
{
    public List<string?>? Skills { get; set; }

    public List<string?>? Cantrips { get; set; }

    public List<PackGrantedSpellJson?>? Spells { get; set; }

    public List<string?>? Armor { get; set; }

    public List<string?>? Weapons { get; set; }

    public List<string?>? Tools { get; set; }

    public List<string?>? Languages { get; set; }

    public List<string?>? SavingThrows { get; set; }
}

internal sealed class PackGrantedSpellJson
{
    public string? Index { get; set; }

    public int? MinLevel { get; set; }
}

internal sealed class PackResourceJson
{
    public string? Key { get; set; }

    public string? Name { get; set; }

    /// <summary>An integer or a formula text (proficiencyBonus, classLevel, halfClassLevel, mod:cha).</summary>
    public JsonElement? Max { get; set; }

    public string? Recharge { get; set; }

    /// <summary>Dice rolled after a rest and kept in the resource (Portent-like features).</summary>
    public PackRollOnRestJson? RollOnRest { get; set; }
}

internal sealed class PackRollOnRestJson
{
    /// <summary>"d20".</summary>
    public string? Dice { get; set; }

    public int? Count { get; set; }

    /// <summary>"short" or "long".</summary>
    public string? Rest { get; set; }
}

internal sealed class PackLevelChoiceJson
{
    public int? Level { get; set; }

    public string? Key { get; set; }

    public string? Name { get; set; }

    public string? Kind { get; set; }

    public string? SetId { get; set; }

    public int? Choose { get; set; }

    public List<string?>? From { get; set; }

    public bool? Replaces { get; set; }

    public bool? Cumulative { get; set; }

    public string? Note { get; set; }

    public PackChoiceFilterJson? Filter { get; set; }
}

internal sealed class PackChoiceFilterJson
{
    public string? SpellList { get; set; }

    public List<int?>? SpellLevels { get; set; }

    public bool? MaxSpellLevelBySlots { get; set; }

    public string? Source { get; set; }

    public bool? CantripsOnly { get; set; }
}
