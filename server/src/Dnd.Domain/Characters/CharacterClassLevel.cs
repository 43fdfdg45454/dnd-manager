namespace Dnd.Domain.Characters;

/// <summary>Levels of a character in one class. Created and changed only through <see cref="Character.ReplaceClasses"/>.</summary>
public sealed class CharacterClassLevel
{
    private CharacterClassLevel()
    {
    }

    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid CharacterId { get; private set; }

    public string ClassIndex { get; private set; } = string.Empty;

    public string? SubclassIndex { get; private set; }

    /// <summary>1-20.</summary>
    public int Level { get; private set; }

    /// <summary>0 = main class (the one taken at character level 1).</summary>
    public int Order { get; private set; }

    internal static CharacterClassLevel Create(Guid characterId, ClassEntry entry, int order) => new()
    {
        CharacterId = characterId,
        ClassIndex = entry.ClassIndex,
        SubclassIndex = entry.SubclassIndex,
        Level = entry.Level,
        Order = order,
    };

    internal void Update(ClassEntry entry, int order)
    {
        SubclassIndex = entry.SubclassIndex;
        Level = entry.Level;
        Order = order;
    }
}
