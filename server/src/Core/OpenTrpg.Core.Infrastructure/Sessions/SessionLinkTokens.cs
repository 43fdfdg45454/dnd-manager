using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Infrastructure.Auth;
using Microsoft.Extensions.Options;

namespace OpenTrpg.Core.Infrastructure.Sessions;

/// <summary>
/// Token format: <c>{userId as 32 hex digits}.{base64url(HMAC-SHA256(Jwt:Secret, "{sessionId}:{userId}"))}</c>.
/// It does not expire: it only allows seeing one session and answering attendance as that user, and
/// stops working when the user leaves the campaign or the session is deleted.
/// </summary>
internal sealed class SessionLinkTokens(IOptions<JwtOptions> options) : ISessionLinkTokens
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes(options.Value.Secret);

    public string Create(Guid sessionId, Guid userId) =>
        $"{userId:N}.{Base64Url.EncodeToString(Sign(sessionId, userId))}";

    public bool TryValidate(Guid sessionId, string? token, out Guid userId)
    {
        userId = Guid.Empty;
        if (string.IsNullOrEmpty(token) || token.Length > 128)
        {
            return false;
        }

        var parts = token.Split('.');
        if (parts.Length != 2 || !Guid.TryParseExact(parts[0], "N", out var candidate) || !Base64Url.IsValid(parts[1]))
        {
            return false;
        }

        var signature = Base64Url.DecodeFromChars(parts[1]);
        if (!CryptographicOperations.FixedTimeEquals(signature, Sign(sessionId, candidate)))
        {
            return false;
        }

        userId = candidate;
        return true;
    }

    private byte[] Sign(Guid sessionId, Guid userId) =>
        HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes($"{sessionId:D}:{userId:D}"));
}
