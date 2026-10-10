namespace OpenTrpg.Core.Domain.Characters;

/// <summary>Source of die rolls used by the domain (hit dice on short rests). Tests inject a fixed roller.</summary>
public interface IDiceRoller
{
    /// <summary>Rolls one die with the given number of sides: a value in [1, sides].</summary>
    int Roll(int sides);
}

/// <summary>Default roller backed by <see cref="Random.Shared"/>.</summary>
public sealed class RandomDiceRoller : IDiceRoller
{
    public static RandomDiceRoller Instance { get; } = new();

    public int Roll(int sides)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sides, 1);
        return Random.Shared.Next(1, sides + 1);
    }
}
