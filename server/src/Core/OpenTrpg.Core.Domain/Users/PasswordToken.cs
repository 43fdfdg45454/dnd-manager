using OpenTrpg.Core.Domain.Common;

namespace OpenTrpg.Core.Domain.Users;

/// <summary>Single-use token to set or reset a password. Only the SHA-256 hash is persisted.</summary>
public sealed class PasswordToken : EntityBase
{
    public static readonly TimeSpan SetupLifetime = TimeSpan.FromHours(48);
    public static readonly TimeSpan ResetLifetime = TimeSpan.FromHours(1);

    private PasswordToken()
    {
    }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public PasswordTokenPurpose Purpose { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }

    public static TimeSpan LifetimeFor(PasswordTokenPurpose purpose) => purpose switch
    {
        PasswordTokenPurpose.Setup => SetupLifetime,
        PasswordTokenPurpose.Reset => ResetLifetime,
        _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, null),
    };

    public static PasswordToken Create(Guid userId, string tokenHash, PasswordTokenPurpose purpose, DateTimeOffset now) => new()
    {
        UserId = userId,
        TokenHash = tokenHash,
        Purpose = purpose,
        CreatedAt = now,
        ExpiresAt = now.Add(LifetimeFor(purpose)),
    };

    public bool IsUsable(DateTimeOffset now) => UsedAt is null && now < ExpiresAt;

    /// <summary>Consumes the token (also used to invalidate it when a newer one is issued).</summary>
    public void MarkUsed(DateTimeOffset now) => UsedAt ??= now;
}
