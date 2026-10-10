namespace OpenTrpg.Core.Domain.Characters;

/// <summary>A condition currently affecting a character, e.g. ("poisoned", "hasta el final del combate").</summary>
public sealed record CharacterCondition(string Index, string? Note = null);
