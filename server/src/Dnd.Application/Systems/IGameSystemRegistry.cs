using Dnd.Domain.Campaigns;

namespace Dnd.Application.Systems;

/// <summary>The game systems registered in this instance (see <see cref="GameSystemServiceCollectionExtensions.AddGameSystem{TSystem}"/>).</summary>
public interface IGameSystemRegistry
{
    /// <summary>Every registered system, in registration order.</summary>
    IReadOnlyList<IGameSystem> All { get; }

    /// <summary>System used when a campaign does not choose one (<see cref="Campaign.DefaultSystemId"/>, or the first registered).</summary>
    IGameSystem Default { get; }

    /// <summary>The system with that id (case-insensitive, trimmed), or null when it is not registered.</summary>
    IGameSystem? Find(string id);
}

/// <summary>Registry built from every <see cref="IGameSystem"/> in the container.</summary>
public sealed class GameSystemRegistry : IGameSystemRegistry
{
    public GameSystemRegistry(IEnumerable<IGameSystem> systems)
    {
        var all = new List<IGameSystem>();
        foreach (var system in systems)
        {
            if (all.Any(s => string.Equals(s.Id, system.Id, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"The game system '{system.Id}' is registered twice.");
            }

            all.Add(system);
        }

        if (all.Count == 0)
        {
            throw new InvalidOperationException("No game system is registered.");
        }

        All = all;
        Default = all.FirstOrDefault(s => s.Id == Campaign.DefaultSystemId) ?? all[0];
    }

    public IReadOnlyList<IGameSystem> All { get; }

    public IGameSystem Default { get; }

    public IGameSystem? Find(string id) =>
        All.FirstOrDefault(s => string.Equals(s.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));
}
