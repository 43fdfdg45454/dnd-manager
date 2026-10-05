using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Common;
using Dnd.Application.Users;

namespace Dnd.Application.Auth;

public sealed class GetMeHandler(IUserRepository users)
{
    public async Task<UserDto> HandleAsync(Guid currentUserId, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(currentUserId, cancellationToken)
            ?? throw AppException.Unauthorized("La sesión no es válida.");
        return UserDto.From(user);
    }
}
