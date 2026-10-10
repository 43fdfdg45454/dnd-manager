namespace OpenTrpg.Core.Domain.Catalog;

/// <summary>
/// What a content pack adds to a race of the SRD or of another pack (<c>races[].extends</c>): traits and fixed grants.
/// Its subraces are ordinary <see cref="SubraceDefinition"/> rows of the pack. One per pack and race.
/// </summary>
public sealed class RaceExtensionDefinition
{
    /// <summary>"&lt;pack id&gt;:&lt;race index&gt;".</summary>
    public required string Id { get; init; }

    public required string RaceIndex { get; init; }

    public IReadOnlyList<string> TraitIndexes { get; init; } = [];

    /// <summary><see cref="OptionGrants"/> JSON added to the race's own, or null.</summary>
    public string? GrantsJson { get; init; }

    public OptionGrants Grants => LevelChoiceJson.ParseGrants(GrantsJson);

    /// <summary><see cref="HeightWeightTable"/> JSON (random height and weight), or null when there is none.</summary>
    public string? HeightWeightJson { get; init; }

    public HeightWeightTable? HeightWeight => HeightWeightTable.Parse(HeightWeightJson);

    /// <summary>Id of the content pack that added it.</summary>
    public required string Source { get; init; }

    public static string IdFor(string source, string raceIndex) => $"{source}:{raceIndex}";
}
