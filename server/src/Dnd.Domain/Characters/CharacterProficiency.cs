namespace Dnd.Domain.Characters;

/// <summary>
/// A proficiency of a character. <see cref="Key"/> is a dataset index ("stealth", "dex") or free text
/// (armor, weapons, tools, languages). Unique per (Type, Key).
/// </summary>
public sealed class CharacterProficiency
{
    private CharacterProficiency()
    {
    }

    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid CharacterId { get; private set; }

    public ProficiencyType Type { get; private set; }

    public string Key { get; private set; } = string.Empty;

    /// <summary>Double proficiency bonus. Only skills and tools admit expertise.</summary>
    public bool Expertise { get; private set; }

    public ProficiencySource Source { get; private set; }

    internal static CharacterProficiency Create(Guid characterId, ProficiencyEntry entry) => new()
    {
        CharacterId = characterId,
        Type = entry.Type,
        Key = entry.Key,
        Expertise = entry.Expertise,
        Source = entry.Source,
    };

    internal void Update(ProficiencyEntry entry)
    {
        Expertise = entry.Expertise;
        Source = entry.Source;
    }
}
