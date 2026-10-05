namespace Dnd.Api.Auth;

public static class AuthPolicies
{
    /// <summary>Requires an authenticated user with the global role <c>Admin</c>.</summary>
    public const string Admin = "Admin";
}
