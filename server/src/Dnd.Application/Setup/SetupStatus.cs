using Dnd.Application.Abstractions.Persistence;

namespace Dnd.Application.Setup;

/// <summary>Whether the instance still needs its first administrator (there are no users yet).</summary>
public sealed record SetupStatusDto(bool NeedsSetup);

public sealed class GetSetupStatusHandler(IUserRepository users)
{
    public async Task<SetupStatusDto> HandleAsync(CancellationToken cancellationToken = default) =>
        new(NeedsSetup: !await users.AnyAsync(cancellationToken));
}
