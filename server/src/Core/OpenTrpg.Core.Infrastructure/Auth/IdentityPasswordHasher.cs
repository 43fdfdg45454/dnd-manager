using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Domain.Users;
using Microsoft.AspNetCore.Identity;

namespace OpenTrpg.Core.Infrastructure.Auth;

/// <summary>Wraps <see cref="PasswordHasher{TUser}"/> from Microsoft.Extensions.Identity.Core (PBKDF2).</summary>
internal sealed class IdentityPasswordHasher : Application.Abstractions.IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(User user, string password) => _hasher.HashPassword(user, password);

    public PasswordVerificationOutcome Verify(User user, string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(user, passwordHash, password) switch
        {
            PasswordVerificationResult.Success => PasswordVerificationOutcome.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordVerificationOutcome.SuccessRehashNeeded,
            _ => PasswordVerificationOutcome.Failed,
        };
}
