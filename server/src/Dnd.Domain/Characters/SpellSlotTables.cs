namespace Dnd.Domain.Characters;

/// <summary>SRD 5.1 multiclass spellcaster table: slots per spell level (1-9) by spellcaster level.</summary>
public static class SpellSlotTables
{
    private static readonly int[][] Multiclass =
    [
        [2, 0, 0, 0, 0, 0, 0, 0, 0], // 1
        [3, 0, 0, 0, 0, 0, 0, 0, 0], // 2
        [4, 2, 0, 0, 0, 0, 0, 0, 0], // 3
        [4, 3, 0, 0, 0, 0, 0, 0, 0], // 4
        [4, 3, 2, 0, 0, 0, 0, 0, 0], // 5
        [4, 3, 3, 0, 0, 0, 0, 0, 0], // 6
        [4, 3, 3, 1, 0, 0, 0, 0, 0], // 7
        [4, 3, 3, 2, 0, 0, 0, 0, 0], // 8
        [4, 3, 3, 3, 1, 0, 0, 0, 0], // 9
        [4, 3, 3, 3, 2, 0, 0, 0, 0], // 10
        [4, 3, 3, 3, 2, 1, 0, 0, 0], // 11
        [4, 3, 3, 3, 2, 1, 0, 0, 0], // 12
        [4, 3, 3, 3, 2, 1, 1, 0, 0], // 13
        [4, 3, 3, 3, 2, 1, 1, 0, 0], // 14
        [4, 3, 3, 3, 2, 1, 1, 1, 0], // 15
        [4, 3, 3, 3, 2, 1, 1, 1, 0], // 16
        [4, 3, 3, 3, 2, 1, 1, 1, 1], // 17
        [4, 3, 3, 3, 3, 1, 1, 1, 1], // 18
        [4, 3, 3, 3, 3, 2, 1, 1, 1], // 19
        [4, 3, 3, 3, 3, 2, 2, 1, 1], // 20
    ];

    /// <summary>Slots for spell levels 1-9 at a multiclass spellcaster level; nine zeros for level 0.</summary>
    public static IReadOnlyList<int> MulticlassSlots(int casterLevel)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(casterLevel);
        return casterLevel == 0 ? new int[9] : [.. Multiclass[Math.Min(casterLevel, Multiclass.Length) - 1]];
    }
}
