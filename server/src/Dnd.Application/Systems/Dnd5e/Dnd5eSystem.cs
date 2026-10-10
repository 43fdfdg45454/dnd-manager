using Dnd.Domain.Campaigns;
using Dnd.Domain.Catalog;

namespace Dnd.Application.Systems.Dnd5e;

/// <summary>Dungeons &amp; Dragons 5th edition with the content of the SRD 5.1 (2014), the default game system.</summary>
public sealed class Dnd5eSystem : IGameSystem
{
    public const string SystemId = Campaign.DefaultSystemId;

    public const string Name = "Dungeons & Dragons 5e (SRD 5.1)";

    public const string SrdVersion = "5.1";

    public const string SrdLicense = "CC-BY-4.0";

    /// <summary>Mandatory CC-BY 4.0 attribution of the SRD 5.1 content (kept in English, as published by its licensor).</summary>
    public const string SrdAttributionText =
        "This work includes material taken from the System Reference Document 5.1 (“SRD 5.1”) by Wizards of the Coast LLC "
        + "and available at https://dnd.wizards.com/resources/systems-reference-document. The SRD 5.1 is licensed under the "
        + "Creative Commons Attribution 4.0 International License available at https://creativecommons.org/licenses/by/4.0/legalcode.";

    public const string SrdFileName = "SRD_CC_v5.1.pdf";

    public const string SrdTitle = "SRD 5.1 (Systems Reference Document)";

    public const string SrdDescription = "Reglas básicas de D&D 5e, © Wizards of the Coast LLC, publicadas bajo licencia CC-BY 4.0.";

    public string Id => SystemId;

    public GameSystemInfo Info { get; } = new(
        Name,
        SrdVersion,
        [new AttributionInfo(CatalogImport.SrdRuleset, SrdLicense, SrdAttributionText)],
        [new SystemDocumentInfo(SrdFileName, SrdTitle, SrdDescription)]);
}
