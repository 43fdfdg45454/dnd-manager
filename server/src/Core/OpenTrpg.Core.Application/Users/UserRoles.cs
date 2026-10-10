using OpenTrpg.Core.Domain.Users;

namespace OpenTrpg.Core.Application.Users;

/// <summary>Strict parsing of the role names exposed by the API ("Admin" | "User").</summary>
public static class UserRoles
{
    public static bool TryParse(string? value, out UserRole role)
    {
        switch (value)
        {
            case nameof(UserRole.Admin):
                role = UserRole.Admin;
                return true;
            case nameof(UserRole.User):
                role = UserRole.User;
                return true;
            default:
                role = default;
                return false;
        }
    }

    public static bool IsValid(string? value) => TryParse(value, out _);
}
