using Dnd.Domain.Users;

namespace Dnd.Application.Abstractions.Persistence;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Tokens of the user that have not been revoked (they may be expired).</summary>
    Task<IReadOnlyList<RefreshToken>> ListNotRevokedByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    void Add(RefreshToken token);
}
