using OpenTrpg.Core.Domain.Users;

namespace OpenTrpg.Core.Application.Abstractions.Persistence;

public interface IPasswordTokenRepository
{
    Task<PasswordToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Tokens of the user not used yet (they may be expired). All purposes when <paramref name="purpose"/> is null.</summary>
    Task<IReadOnlyList<PasswordToken>> ListUnusedByUserAsync(Guid userId, PasswordTokenPurpose? purpose, CancellationToken cancellationToken = default);

    void Add(PasswordToken token);
}
