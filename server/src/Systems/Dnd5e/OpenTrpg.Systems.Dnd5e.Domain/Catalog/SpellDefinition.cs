using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
namespace OpenTrpg.Systems.Dnd5e.Domain.Catalog;

public sealed class SpellDefinition
{
    public required string Index { get; init; }

    public required string Name { get; init; }

    /// <summary>0 = cantrip, 1..9 = spell level.</summary>
    public int Level { get; init; }

    /// <summary>Magic school name, e.g. "Evocation".</summary>
    public string School { get; init; } = string.Empty;

    public string CastingTime { get; init; } = string.Empty;

    public string Range { get; init; } = string.Empty;

    /// <summary>"V", "S" and/or "M".</summary>
    public IReadOnlyList<string> Components { get; init; } = [];

    public string? Material { get; init; }

    public string Duration { get; init; } = string.Empty;

    public bool Concentration { get; init; }

    public bool Ritual { get; init; }

    public IReadOnlyList<string> Description { get; init; } = [];

    public IReadOnlyList<string> HigherLevel { get; init; } = [];

    public IReadOnlyList<string> ClassIndexes { get; init; } = [];

    public IReadOnlyList<string> SubclassIndexes { get; init; } = [];

    /// <summary>"melee" or "ranged" for spell attacks.</summary>
    public string? AttackType { get; init; }

    /// <summary>
    /// JSON array of damage parts: <c>[{"type":"Fire","atSlotLevel":{"3":"8d6"},"atCharacterLevel":null}]</c>.
    /// </summary>
    public string? DamageJson { get; init; }

    /// <summary>
    /// JSON object with the healing per slot level: <c>{"1":"1d8 + MOD","2":"2d8 + MOD"}</c>, where
    /// <c>MOD</c> stands for the spellcasting ability modifier.
    /// </summary>
    public string? HealJson { get; init; }

    /// <summary>Ability index of the saving throw ("dex", "wis", ...).</summary>
    public string? DcAbility { get; init; }

    /// <summary>What the spell is mainly for (icon in the app).</summary>
    public SpellCategory Category { get; init; } = SpellCategory.Utility;

    /// <summary>"srd" or the id of the content pack that added it (see <see cref="CatalogSources"/>).</summary>
    public string Source { get; init; } = Dnd5eCatalogSources.Srd;
}
