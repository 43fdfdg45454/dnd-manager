using Dnd.Domain.Users;

namespace Dnd.Application.Abstractions;

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

public interface ITokenService
{
    TimeSpan RefreshTokenLifetime { get; }

    /// <summary>Signed JWT with the claims <c>sub</c>, <c>email</c>, <c>name</c> and <c>role</c>.</summary>
    AccessToken CreateAccessToken(User user);

    /// <summary>32 random bytes encoded as base64url. Used for refresh and password tokens.</summary>
    string GenerateOpaqueToken();

    /// <summary>SHA-256 of the token as lower-case hex. Only this value is persisted.</summary>
    string HashToken(string token);
}
