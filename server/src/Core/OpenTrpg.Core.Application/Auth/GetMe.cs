using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Application.Users;

namespace OpenTrpg.Core.Application.Auth;

public sealed class GetMeHandler(IUserRepository users)
{
    public async Task<UserDto> HandleAsync(Guid currentUserId, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(currentUserId, cancellationToken)
            ?? throw AppException.Unauthorized("La sesión no es válida.");
        return UserDto.From(user);
    }
}
