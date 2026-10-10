namespace OpenTrpg.Core.Infrastructure.Auth;

/// <summary>Claim names used in the access token (inbound claim mapping is disabled).</summary>
public static class JwtClaimTypes
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string Role = "role";
}
