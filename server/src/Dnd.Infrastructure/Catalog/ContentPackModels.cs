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
}

internal sealed class PackClassExtensionJson
{
    public string? ClassIndex { get; set; }

    public List<PackSubclassJson?>? Subclasses { get; set; }
}

internal sealed class PackSubclassJson
{
    public string? Index { get; set; }

    public string? Name { get; set; }

    public string? Flavor { get; set; }

    public List<string?>? Description { get; set; }

    public List<PackSubclassLevelJson?>? Levels { get; set; }
}

internal sealed class PackSubclassLevelJson
{
    public int? Level { get; set; }

    public List<PackFeatureJson?>? Features { get; set; }
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
}

internal sealed class PackSubraceJson
{
    public string? Index { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public List<PackAbilityBonusJson?>? AbilityBonuses { get; set; }

    public List<PackTraitJson?>? Traits { get; set; }
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
}
