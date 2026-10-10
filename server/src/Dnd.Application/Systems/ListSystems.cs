namespace Dnd.Application.Systems;

/// <summary>A registered game system as listed by <c>GET /api/v1/systems</c>.</summary>
public sealed record GameSystemDto(string Id, string Name, string Version, bool IsDefault);

/// <summary>Lists the game systems registered in this instance, in registration order.</summary>
public sealed class ListSystemsHandler(IGameSystemRegistry registry)
{
    public IReadOnlyList<GameSystemDto> Handle() =>
        registry.All
            .Select(s => new GameSystemDto(s.Id, s.Info.Name, s.Info.Version, ReferenceEquals(s, registry.Default)))
            .ToList();
}
