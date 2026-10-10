using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Domain.Users;

namespace OpenTrpg.Core.Application.Auth;

/// <summary>
/// Creates a new single-use password token and invalidates the previous unused ones with the same
/// purpose. Returns the plain token (only its hash is stored). The caller saves the unit of work.
/// </summary>
public sealed class PasswordTokenIssuer(ITokenService tokens, IPasswordTokenRepository passwordTokens, IDateTimeProvider clock)
{
    public async Task<string> IssueAsync(User user, PasswordTokenPurpose purpose, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        foreach (var previous in await passwordTokens.ListUnusedByUserAsync(user.Id, purpose, cancellationToken))
        {
            previous.MarkUsed(now);
        }

        var token = tokens.GenerateOpaqueToken();
        passwordTokens.Add(PasswordToken.Create(user.Id, tokens.HashToken(token), purpose, now));
        return token;
    }
}
