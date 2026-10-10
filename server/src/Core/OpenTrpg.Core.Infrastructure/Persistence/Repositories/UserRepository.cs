using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace OpenTrpg.Core.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        db.Users.FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        db.Users.AnyAsync(x => x.Email == normalizedEmail, cancellationToken);

    public Task<bool> AnyAsync(CancellationToken cancellationToken = default) => db.Users.AnyAsync(cancellationToken);

    public async Task<(IReadOnlyList<User> Items, int Total)> SearchAsync(string? search, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrEmpty(search))
        {
            // Emails are stored in lower case; lower() keeps the search portable (PostgreSQL and SQLite).
            var term = search.ToLowerInvariant();
            query = query.Where(x => x.Email.Contains(term) || x.DisplayName.ToLower().Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.Email).Skip(skip).Take(take).ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<IReadOnlyList<User>> SearchActiveAsync(string term, int take, CancellationToken cancellationToken = default)
    {
        var lowered = term.ToLowerInvariant();
        return await db.Users
            .AsNoTracking()
            .Where(x => x.IsActive && (x.Email.Contains(lowered) || x.DisplayName.ToLower().Contains(lowered)))
            .OrderBy(x => x.DisplayName)
            .ThenBy(x => x.Email)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        ids.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Users.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);

    public void Add(User user) => db.Users.Add(user);
}
