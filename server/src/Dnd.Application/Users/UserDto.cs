using Dnd.Domain.Users;

namespace Dnd.Application.Users;

public sealed record UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    bool IsActive,
    bool HasPassword,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt)
{
    public static UserDto From(User user) => new(
        user.Id,
        user.Email,
        user.DisplayName,
        user.Role.ToString(),
        user.IsActive,
        user.HasPassword,
        user.CreatedAt,
        user.LastLoginAt);
}
