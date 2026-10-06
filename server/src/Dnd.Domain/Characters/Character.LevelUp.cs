using Dnd.Domain.Rules;

namespace Dnd.Domain.Characters;

// Level-ups granted by the DM (phase 16b): the DM grants the next level and the player completes it.
public sealed partial class Character
{
    /// <summary>Total level the character may advance to (granted by a DM), or null when nothing is pending.</summary>
    public int? PendingLevelUpTo { get; private set; }

    /// <summary>DM who granted <see cref="PendingLevelUpTo"/>.</summary>
    public Guid? LevelGrantedByUserId { get; private set; }

    public DateTimeOffset? LevelGrantedAt { get; private set; }

    /// <summary>
    /// Grants the next level (total level + 1). Does nothing and returns false when a level-up is
    /// already pending (grants never accumulate) or the character is already at the maximum level.
    /// </summary>
    public bool GrantLevelUp(Guid grantedByUserId, DateTimeOffset now)
    {
        if (PendingLevelUpTo is not null || TotalLevel >= AbilityRules.MaxLevel)
        {
            return false;
        }

        PendingLevelUpTo = TotalLevel + 1;
        LevelGrantedByUserId = grantedByUserId;
        LevelGrantedAt = now;
        Touch(now);
        return true;
    }

    /// <summary>Withdraws the pending level-up; false when there was none.</summary>
    public bool RevokeLevelUp(DateTimeOffset now)
    {
        if (PendingLevelUpTo is null)
        {
            return false;
        }

        ClearLevelUp();
        Touch(now);
        return true;
    }

    /// <summary>
    /// The hit dice of <paramref name="requested"/> the character can still spend: classes it no longer
    /// has are dropped and each count is capped at the remaining dice of its class (zero entries dropped).
    /// </summary>
    public IReadOnlyDictionary<string, int> ClampHitDiceToRemaining(IReadOnlyDictionary<string, int> requested)
    {
        ArgumentNullException.ThrowIfNull(requested);
        return requested
            .Select(e => (e.Key, Count: Math.Min(Math.Max(0, e.Value), HitDiceRemaining(e.Key))))
            .Where(e => e.Count > 0)
            .ToDictionary(e => e.Key, e => e.Count, StringComparer.Ordinal);
    }

    /// <summary>A direct class edit that reaches the pending level makes the grant moot.</summary>
    private void ClearStaleLevelUp()
    {
        if (PendingLevelUpTo is { } target && TotalLevel >= target)
        {
            ClearLevelUp();
        }
    }

    private void ClearLevelUp()
    {
        PendingLevelUpTo = null;
        LevelGrantedByUserId = null;
        LevelGrantedAt = null;
    }
}
