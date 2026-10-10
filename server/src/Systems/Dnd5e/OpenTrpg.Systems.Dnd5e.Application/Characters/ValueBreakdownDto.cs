using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Application;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Application.Characters;

/// <summary>One term of a calculated value; <see cref="Source"/> is a source id of the game system ("ability", "item", "override"...).</summary>
public sealed record BreakdownPartDto(string Source, string Label, int Value);

/// <summary>A calculated value explained point by point: the part values add up to <see cref="Total"/>.</summary>
public sealed record ValueBreakdownDto(int Total, IReadOnlyList<BreakdownPartDto> Parts)
{
    public static ValueBreakdownDto From(ValueBreakdown breakdown) =>
        new(breakdown.Total, breakdown.Parts.Select(p => new BreakdownPartDto(p.Source, p.Label, p.Value)).ToList());
}
