using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Common;
using FluentValidation;

namespace Dnd.Application.Users;

public sealed record ListUsersQuery(string? Search, int? Page, int? PageSize)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;
}

public sealed class ListUsersQueryValidator : AbstractValidator<ListUsersQuery>
{
    public ListUsersQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("La página debe ser 1 o mayor.");
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, ListUsersQuery.MaxPageSize)
            .WithMessage($"El tamaño de página debe estar entre 1 y {ListUsersQuery.MaxPageSize}.");
        RuleFor(x => x.Search).MaximumLength(200).WithMessage("La búsqueda es demasiado larga.");
    }
}

public sealed class ListUsersHandler(IUserRepository users)
{
    public async Task<PagedResult<UserDto>> HandleAsync(ListUsersQuery query, CancellationToken cancellationToken = default)
    {
        var page = query.Page ?? 1;
        var pageSize = query.PageSize ?? ListUsersQuery.DefaultPageSize;
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();

        var (items, total) = await users.SearchAsync(search, (page - 1) * pageSize, pageSize, cancellationToken);
        return new PagedResult<UserDto>(items.Select(UserDto.From).ToList(), total, page, pageSize);
    }
}
