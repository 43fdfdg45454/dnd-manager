using Microsoft.Extensions.DependencyInjection;
using OpenTrpg.Core.Domain.Campaigns;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Application;
using OpenTrpg.Core.Application.Systems;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Application;

/// <summary>
/// Dungeons &amp; Dragons 5th edition with the content of the SRD 5.1 (2014), the default game system. Each part of the
/// contract delegates in the 5e services; they are resolved from the request scope when first used.
/// </summary>
public sealed class Dnd5eSystem(IServiceProvider services) : IGameSystem
{
    public const string SystemId = Campaign.DefaultSystemId;

    public const string Name = "Dungeons & Dragons 5e (SRD 5.1)";

    public const string SrdVersion = "5.1";

    public const string SrdLicense = "CC-BY-4.0";

    /// <summary>Id of the attributed ruleset in <see cref="AttributionInfo.Ruleset"/>.</summary>
    public const string SrdRuleset = "srd-5.1";

    /// <summary>Mandatory CC-BY 4.0 attribution of the SRD 5.1 content (kept in English, as published by its licensor).</summary>
    public const string SrdAttributionText =
        "This work includes material taken from the System Reference Document 5.1 (“SRD 5.1”) by Wizards of the Coast LLC "
        + "and available at https://dnd.wizards.com/resources/systems-reference-document. The SRD 5.1 is licensed under the "
        + "Creative Commons Attribution 4.0 International License available at https://creativecommons.org/licenses/by/4.0/legalcode.";

    public const string SrdFileName = "SRD_CC_v5.1.pdf";

    public const string SrdTitle = "SRD 5.1 (Systems Reference Document)";

    public const string SrdDescription = "Reglas básicas de D&D 5e, © Wizards of the Coast LLC, publicadas bajo licencia CC-BY 4.0.";

    public static GameSystemInfo SystemInfo { get; } = new(
        Name,
        SrdVersion,
        [new AttributionInfo(SrdRuleset, SrdLicense, SrdAttributionText)],
        [new SystemDocumentInfo(SrdFileName, SrdTitle, SrdDescription)])
    {
        BaseCatalogSources = [Dnd5eCatalogSources.Srd],
    };

    public string Id => SystemId;

    public GameSystemInfo Info => SystemInfo;

    public ISheetSystem Sheets => services.GetRequiredService<Dnd5eSheetSystem>();

    public ICreationSystem Creation => services.GetRequiredService<Dnd5eCreationSystem>();

    public IProgressionSystem Progression => services.GetRequiredService<Dnd5eProgressionSystem>();

    public ICombatSystem Combat => services.GetRequiredService<Dnd5eCombatSystem>();

    public IRestSystem Rests => services.GetRequiredService<Dnd5eRestSystem>();

    public ICatalogSystem Catalog => services.GetRequiredService<IDnd5eCatalogSystem>();

    public IChoiceSystem Choices => services.GetRequiredService<Dnd5eChoiceSystem>();

    public IPartySystem Party => services.GetRequiredService<Dnd5ePartySystem>();

    public IItemSystem Items => services.GetRequiredService<Dnd5eItemSystem>();

    public ICurrencySystem Currency { get; } = new Dnd5eCurrencySystem();

    public IChangeRequestSystem ChangeRequests => services.GetRequiredService<Dnd5eChangeRequestSystem>();

    public IReadOnlyList<string> RealtimeEventKinds => Dnd5eEventTypes.All;
}

/// <summary>The catalog part of D&amp;D 5e (implemented by the infrastructure: SRD seed and content packs).</summary>
public interface IDnd5eCatalogSystem : ICatalogSystem
{
}

/// <summary>Coins of D&amp;D 5e, in copper pieces.</summary>
public sealed class Dnd5eCurrencySystem : ICurrencySystem
{
    public IReadOnlyList<Denomination> Denominations { get; } =
    [
        new("pc", "Piezas de cobre", 1),
        new("pp", "Piezas de plata", 10),
        new("pe", "Piezas de electro", 50),
        new("po", "Piezas de oro", 100),
        new("ppt", "Piezas de platino", 1000),
    ];
}
