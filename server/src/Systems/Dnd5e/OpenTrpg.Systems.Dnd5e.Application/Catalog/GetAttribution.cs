using OpenTrpg.Core.Application.Systems;
using OpenTrpg.Core.Application;
using OpenTrpg.Systems.Dnd5e.Application;

namespace OpenTrpg.Systems.Dnd5e.Application.Catalog;

/// <summary>
/// Mandatory attribution of the catalog content: the first attribution of the default game system
/// (for D&amp;D 5e, the CC-BY 4.0 notice of the SRD 5.1).
/// </summary>
public sealed class GetAttributionHandler(IGameSystemRegistry systems)
{
    public AttributionDto Handle()
    {
        var attribution = systems.Default.Info.Attributions[0];
        return new(attribution.Ruleset, attribution.License, attribution.Text);
    }
}
