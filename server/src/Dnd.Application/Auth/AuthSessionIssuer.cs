using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Users;
using Dnd.Domain.Users;

namespace Dnd.Application.Auth;

/// <summary>Issues an access JWT plus a new refresh token. The caller saves the unit of work.</summary>
public sealed class AuthSessionIssuer(ITokenService tokens, IRefreshTokenRepository refreshTokens, IDateTimeProvider clock)
{
    public (AuthResponse Response, string RefreshTokenHash) Issue(User user)
    {
        var accessToken = tokens.CreateAccessToken(user);
        var refreshToken = tokens.GenerateOpaqueToken();
        var refreshTokenHash = tokens.HashToken(refreshToken);

        refreshTokens.Add(RefreshToken.Create(user.Id, refreshTokenHash, clock.UtcNow, tokens.RefreshTokenLifetime));

        var response = new AuthResponse(accessToken.Token, accessToken.ExpiresAt, refreshToken, UserDto.From(user));
        return (response, refreshTokenHash);
    }

    /// <summary>Revokes every refresh token of the user that is still not revoked.</summary>
    public async Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        foreach (var token in await refreshTokens.ListNotRevokedByUserAsync(userId, cancellationToken))
        {
            token.Revoke(now);
        }
    }
}
