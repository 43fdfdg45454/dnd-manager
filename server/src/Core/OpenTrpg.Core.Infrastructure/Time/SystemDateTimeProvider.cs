using OpenTrpg.Core.Application.Abstractions;

namespace OpenTrpg.Core.Infrastructure.Time;

internal sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
