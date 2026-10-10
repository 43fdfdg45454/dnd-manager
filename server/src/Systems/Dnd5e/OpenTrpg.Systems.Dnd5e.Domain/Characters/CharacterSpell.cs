using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;
namespace OpenTrpg.Systems.Dnd5e.Domain.Characters;

/// <summary>A spell known or prepared by a character for one of its classes. Unique per (SpellIndex, ClassIndex).</summary>
public sealed class CharacterSpell
{
    /// <summary>
    /// <see cref="ClassIndex"/> of spells granted by the race or the background (the high elf cantrip): always
    /// prepared, outside the classes' known and prepared counts.
    /// </summary>
    public const string OriginClassIndex = "race";

    private CharacterSpell()
    {
    }

    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid CharacterId { get; private set; }

    public string SpellIndex { get; private set; } = string.Empty;

    public string ClassIndex { get; private set; } = string.Empty;

    public bool IsPrepared { get; private set; }

    /// <summary>Always prepared (domain spells, ...); implies <see cref="IsPrepared"/>.</summary>
    public bool AlwaysPrepared { get; private set; }

    internal static CharacterSpell Create(Guid characterId, SpellEntry entry) => new()
    {
        CharacterId = characterId,
        SpellIndex = entry.SpellIndex,
        ClassIndex = entry.ClassIndex,
        IsPrepared = entry.IsPrepared,
        AlwaysPrepared = entry.AlwaysPrepared,
    };

    internal void Update(SpellEntry entry)
    {
        IsPrepared = entry.IsPrepared;
        AlwaysPrepared = entry.AlwaysPrepared;
    }
}
