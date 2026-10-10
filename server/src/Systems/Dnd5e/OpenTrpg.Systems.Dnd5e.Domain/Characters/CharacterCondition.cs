using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;
namespace OpenTrpg.Systems.Dnd5e.Domain.Characters;

/// <summary>A condition currently affecting a character, e.g. ("poisoned", "hasta el final del combate").</summary>
public sealed record CharacterCondition(string Index, string? Note = null);
