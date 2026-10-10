namespace OpenTrpg.Core.Domain.Catalog;

/// <summary>Fixed racial ability score increase, e.g. ("con", 2).</summary>
public sealed record AbilityBonus(string Ability, int Bonus);
