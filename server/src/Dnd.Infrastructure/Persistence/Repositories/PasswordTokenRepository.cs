using Dnd.Application.Abstractions.Persistence;
using Dnd.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Dnd.Infrastructure.Persistence.Repositories;

internal sealed class PasswordTokenRepository(AppDbContext db) : IPasswordTokenRepository
{
    public Task<PasswordToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        db.PasswordTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<PasswordToken>> ListUnusedByUserAsync(Guid userId, PasswordTokenPurpose? purpose, CancellationToken cancellationToken = default)
    {
        var query = db.PasswordTokens.Where(x => x.UserId == userId && x.UsedAt == null);
        if (purpose is { } p)
        {
            query = query.Where(x => x.Purpose == p);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public void Add(PasswordToken token) => db.PasswordTokens.Add(token);
}
