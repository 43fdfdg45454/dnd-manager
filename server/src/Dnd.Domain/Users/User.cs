using Dnd.Domain.Common;

namespace Dnd.Domain.Users;

public sealed class User : EntityBase
{
    public const int EmailMaxLength = 254;
    public const int DisplayNameMaxLength = 100;

    private User()
    {
    }

    /// <summary>Unique, always stored trimmed and in lower case.</summary>
    public string Email { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>Null until the user sets a password through the setup link.</summary>
    public string? PasswordHash { get; private set; }

    public UserRole Role { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    /// <summary>Whether the user receives session reminders and DM notices by email. On by default.</summary>
    public bool NotificationsEnabled { get; private set; } = true;

    public bool HasPassword => PasswordHash is not null;

    public static User Create(string email, string displayName, UserRole role, DateTimeOffset now) => new()
    {
        Email = NormalizeEmail(email),
        DisplayName = displayName.Trim(),
        Role = role,
        IsActive = true,
        CreatedAt = now,
    };

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public void SetPasswordHash(string passwordHash) => PasswordHash = passwordHash;

    public void RecordLogin(DateTimeOffset now) => LastLoginAt = now;

    public void Rename(string displayName) => DisplayName = displayName.Trim();

    public void ChangeRole(UserRole role) => Role = role;

    public void SetActive(bool isActive) => IsActive = isActive;

    public void SetNotificationsEnabled(bool enabled) => NotificationsEnabled = enabled;
}
