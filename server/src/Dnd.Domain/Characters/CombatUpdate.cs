namespace Dnd.Domain.Characters;

/// <summary>
/// Combat tracking changes, applied without approval by <see cref="Character.ApplyCombatUpdate"/>.
/// Null fields are left unchanged; <see cref="Conditions"/> replaces the whole list when given.
/// </summary>
public sealed record CombatUpdate
{
    public int? HitPointsCurrent { get; init; }

    public int? TemporaryHitPoints { get; init; }

    public int? DeathSaveSuccesses { get; init; }

    public int? DeathSaveFailures { get; init; }

    public int? ExhaustionLevel { get; init; }

    public IReadOnlyList<CharacterCondition>? Conditions { get; init; }

    public bool? Inspiration { get; init; }
}
