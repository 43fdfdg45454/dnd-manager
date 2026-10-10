namespace OpenTrpg.Core.Application.Systems;

/// <summary>
/// A registered game system (D&amp;D 5e is the first one): its identity and information plus the parts of the rules
/// the core calls (sheets, creation, rests, items, change requests, catalog…). Registered per request scope (see
/// <see cref="GameSystemServiceCollectionExtensions.AddGameSystem{TSystem}"/>); a campaign's system is resolved
/// with <see cref="CampaignSystems"/>.
/// </summary>
public interface IGameSystem
{
    /// <summary>Stable id stored in <c>Campaign.SystemId</c> (for example <c>dnd5e</c>).</summary>
    string Id { get; }

    /// <summary>Name, version, attributions and system documents.</summary>
    GameSystemInfo Info { get; }

    ISheetSystem Sheets { get; }

    ICreationSystem Creation { get; }

    IProgressionSystem Progression { get; }

    ICombatSystem Combat { get; }

    IRestSystem Rests { get; }

    ICatalogSystem Catalog { get; }

    IChoiceSystem Choices { get; }

    IPartySystem Party { get; }

    IItemSystem Items { get; }

    ICurrencySystem Currency { get; }

    IChangeRequestSystem ChangeRequests { get; }

    /// <summary>Realtime event kinds the system publishes, in addition to the core ones (<c>CampaignEventTypes</c>).</summary>
    IReadOnlyList<string> RealtimeEventKinds { get; }
}

/// <summary>Descriptive information of a game system.</summary>
/// <param name="Name">Display name (for example "Dungeons &amp; Dragons 5e (SRD 5.1)").</param>
/// <param name="Version">Version of the rules the system implements.</param>
/// <param name="Attributions">Mandatory license attributions of the system's content.</param>
/// <param name="SystemDocuments">Freely licensed documents the instance may register in its library for this system.</param>
public sealed record GameSystemInfo(
    string Name,
    string Version,
    IReadOnlyList<AttributionInfo> Attributions,
    IReadOnlyList<SystemDocumentInfo> SystemDocuments)
{
    /// <summary>Catalog sources of the system's base content (for example <c>srd</c>), always listed.</summary>
    public IReadOnlyList<string> BaseCatalogSources { get; init; } = [];
}

/// <summary>License attribution of a body of content (kept in the language its licensor publishes it).</summary>
/// <param name="Ruleset">Id of the attributed ruleset (for example <c>srd-5.1</c>).</param>
/// <param name="License">License id (for example <c>CC-BY-4.0</c>).</param>
/// <param name="Text">Attribution text that must be shown.</param>
public sealed record AttributionInfo(string Ruleset, string License, string Text);

/// <summary>A freely licensed document of the system that the operator can place in the instance's library.</summary>
/// <param name="FileName">File name expected under the system documents folder.</param>
/// <param name="Title">Title of the library entry.</param>
/// <param name="Description">Description of the library entry (Spanish).</param>
public sealed record SystemDocumentInfo(string FileName, string Title, string Description);
