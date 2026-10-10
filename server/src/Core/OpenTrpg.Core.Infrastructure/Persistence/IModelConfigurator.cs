using Microsoft.EntityFrameworkCore;

namespace OpenTrpg.Core.Infrastructure.Persistence;

/// <summary>
/// Adds the entity configurations of a game system module to the shared model: <see cref="AppDbContext"/> applies
/// every registered configurator, so the core and the modules keep a single migration history.
/// </summary>
public interface IModelConfigurator
{
    void Configure(ModelBuilder modelBuilder);
}
