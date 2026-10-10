using Microsoft.EntityFrameworkCore;
using OpenTrpg.Core.Infrastructure.Persistence;

namespace OpenTrpg.Systems.Dnd5e.Infrastructure.Persistence;

/// <summary>The EF configurations of the D&amp;D 5e module: catalog tables and the 5e part of the characters.</summary>
public sealed class Dnd5eModelConfigurator : IModelConfigurator
{
    public void Configure(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Dnd5eModelConfigurator).Assembly);
}
