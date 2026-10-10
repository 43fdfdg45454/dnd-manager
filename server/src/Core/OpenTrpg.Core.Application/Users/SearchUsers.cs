using OpenTrpg.Core.Application.Abstractions.Persistence;
using FluentValidation;

namespace OpenTrpg.Core.Application.Users;

public sealed record UserSummaryDto(Guid Id, string DisplayName, string Email);

public sealed record SearchUsersQuery(string? Q, int? Limit)
{
    public const int MinQueryLength = 2;
    public const int MaxQueryLength = 100;
    public const int DefaultLimit = 10;
    public const int MaxLimit = 50;
}

public sealed class SearchUsersQueryValidator : AbstractValidator<SearchUsersQuery>
{
    public SearchUsersQueryValidator()
    {
        RuleFor(x => x.Q)
            .Must(q => q is not null && q.Trim().Length >= SearchUsersQuery.MinQueryLength)
            .WithMessage($"Escribe al menos {SearchUsersQuery.MinQueryLength} caracteres para buscar.")
            .MaximumLength(SearchUsersQuery.MaxQueryLength).WithMessage("La búsqueda es demasiado larga.");
        RuleFor(x => x.Limit)
            .InclusiveBetween(1, SearchUsersQuery.MaxLimit)
            .WithMessage($"El límite debe estar entre 1 y {SearchUsersQuery.MaxLimit}.");
    }
}

/// <summary>Active users whose email or display name contains the query. Any authenticated user can search.</summary>
public sealed class SearchUsersHandler(IUserRepository users)
{
    public async Task<IReadOnlyList<UserSummaryDto>> HandleAsync(SearchUsersQuery query, CancellationToken cancellationToken = default)
    {
        var found = await users.SearchActiveAsync(query.Q!.Trim(), query.Limit ?? SearchUsersQuery.DefaultLimit, cancellationToken);
        return found.Select(u => new UserSummaryDto(u.Id, u.DisplayName, u.Email)).ToList();
    }
}
