namespace Dnd.Domain.Characters;

/// <summary>
/// Spent spell slots of one level. <see cref="Level"/> 1-9 are regular slots; <see cref="PactLevel"/> (0)
/// stores the spent Pact Magic slots. Maxima are not stored: they come from <see cref="CharacterSheet"/>.
/// Rows are created on the first spend.
/// </summary>
public sealed class SpellSlotState
{
    /// <summary>Level used to store the spent Pact Magic (warlock) slots.</summary>
    public const int PactLevel = 0;

    private SpellSlotState()
    {
    }

    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid CharacterId { get; private set; }

    /// <summary>0 (pact) or 1-9.</summary>
    public int Level { get; private set; }

    public int Used { get; private set; }

    internal static SpellSlotState Create(Guid characterId, int level) => new() { CharacterId = characterId, Level = level };

    internal void SetUsed(int used) => Used = used;
}
