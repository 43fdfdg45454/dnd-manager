namespace OpenTrpg.Core.Domain.Characters;

/// <summary>When a limited-use resource gets its uses back.</summary>
public enum ResourceRecharge
{
    /// <summary>Restored by short and long rests.</summary>
    ShortRest,

    /// <summary>Restored by long rests.</summary>
    LongRest,

    /// <summary>Restored at dawn: rests do not restore it; it is restored by hand.</summary>
    Dawn,

    /// <summary>Never restored automatically.</summary>
    Manual,
}
