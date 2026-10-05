using Dnd.Domain.Users;

namespace Dnd.Application.Abstractions;

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
