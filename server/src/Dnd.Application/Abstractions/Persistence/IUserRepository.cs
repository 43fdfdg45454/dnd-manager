using Dnd.Domain.Users;

namespace Dnd.Application.Abstractions.Persistence;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <param name="normalizedEmail">Email already normalized with <see cref="User.NormalizeEmail"/>.</param>
    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    Task<bool> AnyAsync(CancellationToken cancellationToken = default);

    /// <summary>Case-insensitive search on email and display name, ordered by email.</summary>
    Task<(IReadOnlyList<User> Items, int Total)> SearchAsync(string? search, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Active users whose email or display name contains <paramref name="term"/> (case-insensitive),
    /// ordered by display name and email.
    /// </summary>
    Task<IReadOnlyList<User>> SearchActiveAsync(string term, int take, CancellationToken cancellationToken = default);

    void Add(User user);
}
