using Dnd.Domain.Common;

namespace Dnd.Domain.Catalog;

/// <summary>Record of a rules catalog import; makes the seed idempotent per ruleset and dataset version.</summary>
public sealed class CatalogImport : EntityBase
{
    public const string SrdRuleset = "srd-5.1";

    public required string Ruleset { get; init; }

    public required string DatasetVersion { get; init; }

    public DateTimeOffset ImportedAt { get; init; }

    /// <summary>JSON object with the number of rows imported per table.</summary>
    public string CountsJson { get; init; } = "{}";
}
