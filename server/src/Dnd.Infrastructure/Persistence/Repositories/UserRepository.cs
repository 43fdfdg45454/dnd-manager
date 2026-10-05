using Dnd.Application.Abstractions.Persistence;
using Dnd.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Dnd.Infrastructure.Persistence.Repositories;

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

    public void Add(User user) => db.Users.Add(user);
}
