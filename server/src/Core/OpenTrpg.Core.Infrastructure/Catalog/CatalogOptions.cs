namespace OpenTrpg.Core.Infrastructure.Catalog;

public sealed class CatalogOptions
{
    public const string SectionName = "Catalog";

    /// <summary>Imports the SRD catalog at startup (after migrations) when it is not imported yet.</summary>
    public bool SeedOnStartup { get; set; } = true;
}
