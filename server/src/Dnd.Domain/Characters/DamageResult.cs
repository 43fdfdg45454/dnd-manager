namespace Dnd.Domain.Characters;

/// <summary>Outcome of <see cref="Character.ApplyDamage"/>.</summary>
/// <param name="Damage">Damage taken (temporary hit points included).</param>
/// <param name="AbsorbedByTemporary">Part of the damage absorbed by temporary hit points.</param>
/// <param name="HitPointsCurrent">Current hit points afterwards.</param>
/// <param name="ConcentratingOn">Spell the character was concentrating on before the damage, or null.</param>
/// <param name="ConcentrationCheckDc">
/// DC of the Constitution saving throw to keep concentrating (max(10, damage / 2)) when the character was
/// concentrating and is still above 0 hit points; null otherwise.
/// </param>
/// <param name="ConcentrationEnded">True when the damage dropped the character to 0 hit points and ended its concentration.</param>
public sealed record DamageResult(
    int Damage,
    int AbsorbedByTemporary,
    int HitPointsCurrent,
    string? ConcentratingOn,
    int? ConcentrationCheckDc,
    bool ConcentrationEnded);
