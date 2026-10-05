using Dnd.Domain.Catalog;

namespace Dnd.Application.Catalog;

/// <summary>Mandatory CC-BY 4.0 attribution of the SRD 5.1 content (kept in English, as published by its licensor).</summary>
public sealed class GetAttributionHandler
{
    public const string License = "CC-BY-4.0";

    public const string Text =
        "This work includes material taken from the System Reference Document 5.1 (“SRD 5.1”) by Wizards of the Coast LLC "
        + "and available at https://dnd.wizards.com/resources/systems-reference-document. The SRD 5.1 is licensed under the "
        + "Creative Commons Attribution 4.0 International License available at https://creativecommons.org/licenses/by/4.0/legalcode.";

    public AttributionDto Handle() => new(CatalogImport.SrdRuleset, License, Text);
}
