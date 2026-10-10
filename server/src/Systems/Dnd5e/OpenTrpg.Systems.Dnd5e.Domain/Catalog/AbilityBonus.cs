using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
namespace OpenTrpg.Systems.Dnd5e.Domain.Catalog;

/// <summary>Fixed racial ability score increase, e.g. ("con", 2).</summary>
public sealed record AbilityBonus(string Ability, int Bonus);
