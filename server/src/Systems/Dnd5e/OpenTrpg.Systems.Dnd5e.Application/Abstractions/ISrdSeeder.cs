using OpenTrpg.Core.Application;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions;
namespace OpenTrpg.Systems.Dnd5e.Application.Abstractions;

/// <summary>Imports the SRD 5.1 rules catalog. Idempotent per ruleset and dataset version.</summary>
public interface ISrdSeeder
{
    /// <returns>True when the catalog was imported; false when this dataset version was already imported.</returns>
    Task<bool> SeedAsync(CancellationToken cancellationToken = default);
}
