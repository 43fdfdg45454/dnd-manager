namespace OpenTrpg.Core.Application.Systems;

/// <summary>The currency of a game system. The core stores money as an integer in the minor unit.</summary>
public interface ICurrencySystem
{
    /// <summary>Denominations from the smallest one (value 1) to the largest one.</summary>
    IReadOnlyList<Denomination> Denominations { get; }
}

/// <summary>A coin of the system.</summary>
/// <param name="Code">Short code shown next to amounts (for example <c>po</c>).</param>
/// <param name="Name">Display name (Spanish).</param>
/// <param name="Value">Value in the minor unit.</param>
public sealed record Denomination(string Code, string Name, long Value);
