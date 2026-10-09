using Dnd.Domain.Common;

namespace Dnd.Domain.Characters;

/// <summary>
/// The animal companion of a character (a beast of the catalog chosen through a subclass feature with a
/// <see cref="Catalog.CompanionRule"/>). At most one per character. Its statblock is calculated
/// (<see cref="CompanionCalculator"/>); only the beast, the name and the current hit points are stored.
/// Hit points are tracked without approval, like the character's.
/// </summary>
public sealed class CharacterCompanion : EntityBase
{
    public const int NameMaxLength = 100;
    public const int BeastIndexMaxLength = 100;
    public const int MaxHitPoints = 999;

    private CharacterCompanion()
    {
    }

    public Guid CharacterId { get; private set; }

    public string BeastIndex { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public int HitPointsCurrent { get; private set; }

    /// <summary>Maximum hit points set by hand; null to use the calculated maximum.</summary>
    public int? HitPointsMaxOverride { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>A new companion at full hit points (<paramref name="maxHp"/>, the calculated maximum).</summary>
    public static CharacterCompanion Create(Guid characterId, string beastIndex, string name, int maxHp, DateTimeOffset now) => new()
    {
        CharacterId = characterId,
        BeastIndex = NormalizeBeast(beastIndex),
        Name = NormalizeName(name),
        HitPointsCurrent = Math.Clamp(maxHp, 0, MaxHitPoints),
        CreatedAt = now,
        UpdatedAt = now,
    };

    public void Rename(string name, DateTimeOffset now)
    {
        Name = NormalizeName(name);
        UpdatedAt = now;
    }

    /// <summary>Another beast: it arrives at full hit points (<paramref name="maxHp"/>).</summary>
    public void ChangeBeast(string beastIndex, string name, int maxHp, DateTimeOffset now)
    {
        BeastIndex = NormalizeBeast(beastIndex);
        Name = NormalizeName(name);
        HitPointsCurrent = Math.Clamp(maxHp, 0, MaxHitPoints);
        UpdatedAt = now;
    }

    /// <summary>
    /// Sets the current hit points to <paramref name="current"/> or moves them by <paramref name="delta"/> (exactly one of
    /// the two), between 0 and <paramref name="maxHp"/>.
    /// </summary>
    public void TrackHitPoints(int? delta, int? current, int maxHp, DateTimeOffset now)
    {
        if ((delta is null) == (current is null))
        {
            throw DomainException.RuleViolation("Indica un cambio (delta) o el valor actual (current) de los puntos de golpe.");
        }

        var max = Math.Max(0, maxHp);
        if (current is { } value && (value < 0 || value > max))
        {
            throw DomainException.RuleViolation($"Los puntos de golpe del compañero deben estar entre 0 y {max}.");
        }

        HitPointsCurrent = current ?? Math.Clamp(Math.Min(HitPointsCurrent, max) + delta!.Value, 0, max);
        UpdatedAt = now;
    }

    /// <summary>Back to full hit points (long rest).</summary>
    public void RestoreHitPoints(int maxHp, DateTimeOffset now)
    {
        HitPointsCurrent = Math.Clamp(maxHp, 0, MaxHitPoints);
        UpdatedAt = now;
    }

    /// <summary>The current hit points, never above the maximum (the maximum can drop, e.g. when the DM lowers a level).</summary>
    public int CurrentWithin(int maxHp) => Math.Clamp(HitPointsCurrent, 0, Math.Max(0, maxHp));

    public static string NormalizeName(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw DomainException.RuleViolation("Indica el nombre del compañero.");
        }

        if (trimmed.Length > NameMaxLength)
        {
            throw DomainException.RuleViolation($"El nombre del compañero no puede superar los {NameMaxLength} caracteres.");
        }

        return trimmed;
    }

    private static string NormalizeBeast(string? beastIndex)
    {
        var trimmed = beastIndex?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 || trimmed.Length > BeastIndexMaxLength)
        {
            throw DomainException.RuleViolation("Indica la bestia del compañero.");
        }

        return trimmed;
    }
}
