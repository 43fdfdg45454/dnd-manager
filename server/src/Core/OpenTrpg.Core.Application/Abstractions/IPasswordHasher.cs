using OpenTrpg.Core.Domain.Users;

namespace OpenTrpg.Core.Application.Abstractions;

public enum PasswordVerificationOutcome
{
    Failed,
    Success,
    SuccessRehashNeeded,
}

public interface IPasswordHasher
{
    string Hash(User user, string password);

    PasswordVerificationOutcome Verify(User user, string passwordHash, string password);
}
