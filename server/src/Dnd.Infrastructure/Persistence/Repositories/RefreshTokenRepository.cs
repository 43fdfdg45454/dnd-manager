using Dnd.Application.Abstractions.Persistence;
using Dnd.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Dnd.Infrastructure.Persistence.Repositories;

internal sealed class RefreshTokenRepository(AppDbContext db) : IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> ListNotRevokedByUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await db.RefreshTokens.Where(x => x.UserId == userId && x.RevokedAt == null).ToListAsync(cancellationToken);

    public void Add(RefreshToken token) => db.RefreshTokens.Add(token);
}
