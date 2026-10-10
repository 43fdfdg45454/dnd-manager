namespace OpenTrpg.Systems.Dnd5e.Domain.Catalog;

/// <summary>
/// A creature statblock of the catalog (table <c>Dnd5eCreatures</c>): the 87 beasts of the SRD and the creatures of the
/// content packs. The searchable columns are copied out of <see cref="DataJson"/>, the full statblock (the
/// application's <c>BeastDto</c> as JSON).
/// </summary>
public sealed class CreatureDefinition
{
    public const int TypeMaxLength = 40;

    public required string Index { get; init; }

    public required string Name { get; init; }

    /// <summary>"beast", "monstrosity", "humanoid"... (lowercase).</summary>
    public required string Type { get; init; }

    public string? Subtype { get; init; }

    /// <summary>"Tiny".."Gargantuan".</summary>
    public required string Size { get; init; }

    public double ChallengeRating { get; init; }

    /// <summary>The whole statblock as JSON.</summary>
    public required string DataJson { get; init; }

    /// <summary>"srd" or the id of the content pack that defines the creature.</summary>
    public string Source { get; init; } = Dnd5eCatalogSources.Srd;
}

/// <summary>A rules document of a content pack (table <c>Dnd5eRules</c>): variant rules, firearms, auxiliary levels... No mechanical effect.</summary>
public sealed class RuleDefinition
{
    public const int CategoryMaxLength = 40;
    public const int MaxTags = 20;

    public required string Index { get; init; }

    public required string Title { get; init; }

    /// <summary>"variant", "multiclassing", "equipment", "general"...</summary>
    public required string Category { get; init; }

    /// <summary>Paragraphs in light Markdown.</summary>
    public IReadOnlyList<string> Body { get; init; } = [];

    public IReadOnlyList<string> Tags { get; init; } = [];

    public required string Source { get; init; }
}

/// <summary>
/// An entry of an extensible vocabulary (table <c>Dnd5eReferenceEntries</c>): languages, weapon properties, equipment
/// categories, damage types, magic schools and tools. The SRD ones are seeded; packs add theirs.
/// </summary>
public sealed class ReferenceEntry
{
    public const int KindMaxLength = 40;

    public const string Languages = "languages";
    public const string WeaponProperties = "weaponProperties";
    public const string EquipmentCategories = "equipmentCategories";
    public const string DamageTypes = "damageTypes";
    public const string MagicSchools = "magicSchools";
    public const string Tools = "tools";

    public static readonly IReadOnlyList<string> Kinds = [Languages, WeaponProperties, EquipmentCategories, DamageTypes, MagicSchools, Tools];

    /// <summary>One of <see cref="Kinds"/>.</summary>
    public required string Kind { get; init; }

    public required string Index { get; init; }

    public required string Name { get; init; }

    /// <summary>Description paragraphs as a JSON array of strings.</summary>
    public string DescriptionJson { get; init; } = "[]";

    public required string Source { get; init; }
}
