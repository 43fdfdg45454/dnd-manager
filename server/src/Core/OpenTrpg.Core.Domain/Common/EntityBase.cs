namespace OpenTrpg.Core.Domain.Common;

/// <summary>Base class for all persisted aggregates and entities.</summary>
public abstract class EntityBase
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
