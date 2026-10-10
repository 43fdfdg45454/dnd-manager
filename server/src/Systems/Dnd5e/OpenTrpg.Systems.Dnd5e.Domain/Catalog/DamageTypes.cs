namespace OpenTrpg.Systems.Dnd5e.Domain.Catalog;

/// <summary>The damage types and conditions of the SRD (indexes) and the sizes of creatures.</summary>
public static class DamageTypes
{
    public static IReadOnlyList<string> All { get; } =
        ["acid", "bludgeoning", "cold", "fire", "force", "lightning", "necrotic", "piercing", "poison", "psychic", "radiant", "slashing", "thunder"];

    /// <summary>"fire" → "Fuego"; unknown types unchanged.</summary>
    public static string Spanish(string type) => type switch
    {
        "acid" => "Ácido",
        "bludgeoning" => "Contundente",
        "cold" => "Frío",
        "fire" => "Fuego",
        "force" => "Fuerza",
        "lightning" => "Relámpago",
        "necrotic" => "Necrótico",
        "piercing" => "Perforante",
        "poison" => "Veneno",
        "psychic" => "Psíquico",
        "radiant" => "Radiante",
        "slashing" => "Cortante",
        "thunder" => "Trueno",
        _ => type,
    };

    /// <summary>Condition indexes of the SRD.</summary>
    public static IReadOnlyList<string> Conditions { get; } =
    [
        "blinded", "charmed", "deafened", "exhaustion", "frightened", "grappled", "incapacitated", "invisible", "paralyzed",
        "petrified", "poisoned", "prone", "restrained", "stunned", "unconscious",
    ];

    /// <summary>Sizes of creatures, smallest first.</summary>
    public static IReadOnlyList<string> Sizes { get; } = ["tiny", "small", "medium", "large", "huge", "gargantuan"];
}
