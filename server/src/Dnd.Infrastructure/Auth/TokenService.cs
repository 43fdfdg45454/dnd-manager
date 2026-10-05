using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Dnd.Application.Abstractions;
using Dnd.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Dnd.Infrastructure.Auth;

internal sealed class TokenService(IOptions<JwtOptions> options, IDateTimeProvider clock) : ITokenService
{
    private const int OpaqueTokenBytes = 32;

    private readonly JwtOptions _options = options.Value;
    private readonly JsonWebTokenHandler _handler = new();

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_options.RefreshTokenDays);

    public AccessToken CreateAccessToken(User user)
    {
        var now = clock.UtcNow;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret)), SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [JwtClaimTypes.Subject] = user.Id.ToString(),
                [JwtClaimTypes.Email] = user.Email,
                [JwtClaimTypes.Name] = user.DisplayName,
                [JwtClaimTypes.Role] = user.Role.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            },
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }

    public string GenerateOpaqueToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(OpaqueTokenBytes));

    public string HashToken(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
