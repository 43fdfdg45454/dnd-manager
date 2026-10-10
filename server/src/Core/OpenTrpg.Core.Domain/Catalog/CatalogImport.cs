using OpenTrpg.Core.Domain.Common;

namespace OpenTrpg.Core.Domain.Catalog;

/// <summary>
/// Record of a rules catalog import; makes the seed idempotent per ruleset and dataset version.
/// Content packs use the ruleset <c>pack:&lt;id&gt;</c> (<see cref="CatalogSources.PackRuleset"/>), with
/// their version and display name.
/// </summary>
public sealed class CatalogImport : EntityBase
{
    public const string SrdRuleset = "srd-5.1";

    public const int RulesetMaxLength = 64;

    public required string Ruleset { get; init; }

    public required string DatasetVersion { get; init; }

    /// <summary>Display name of a content pack; null for the SRD.</summary>
    public string? Name { get; init; }

    public DateTimeOffset ImportedAt { get; init; }

    /// <summary>JSON object with the number of rows imported per table.</summary>
    public string CountsJson { get; init; } = "{}";
}
