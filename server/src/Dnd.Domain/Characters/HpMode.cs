namespace Dnd.Domain.Characters;

/// <summary>How the maximum hit points are obtained.</summary>
public enum HpMode
{
    /// <summary>Computed: max die at 1st level of the main class, then (die / 2 + 1) per level, plus Con.</summary>
    Average,

    /// <summary>Taken from the <c>hitPointsMax</c> override, which is then mandatory.</summary>
    Manual,
}
