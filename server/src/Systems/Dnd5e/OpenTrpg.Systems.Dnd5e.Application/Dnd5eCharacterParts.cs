using System.Text.Json;
using System.Text.Json.Nodes;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Core.Application.ContentPacks;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Application;
using OpenTrpg.Core.Application.Systems;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions.Persistence;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Application;

/// <summary>
/// The D&amp;D 5e part of the characters the core hands over (<see cref="CharacterRef.System"/>), loaded once; the
/// catalog scope of the request becomes the character's campaign.
/// </summary>
public sealed class Dnd5eCharacterParts(IDnd5eCharacterRepository characters, CatalogScopeContext scope)
{
    /// <summary>The tracked 5e character with every child collection.</summary>
    public async Task<Dnd5eCharacter> LoadAsync(CharacterRef reference, CancellationToken cancellationToken)
    {
        if (reference.System is Dnd5eCharacter loaded)
        {
            return loaded;
        }

        var character = await characters.GetWithDetailsAsync(reference.Id, cancellationToken) ?? throw CharacterErrors.CharacterNotFound();
        await scope.UseCampaignAsync(character.CampaignId, cancellationToken);
        reference.System = character;
        return character;
    }

    /// <summary>The 5e characters of several references (tracked, every child collection).</summary>
    public async Task<IReadOnlyList<Dnd5eCharacter>> LoadManyAsync(IReadOnlyList<CharacterRef> references, CancellationToken cancellationToken)
    {
        var result = new List<Dnd5eCharacter>(references.Count);
        foreach (var reference in references)
        {
            result.Add(await LoadAsync(reference, cancellationToken));
        }

        return result;
    }

    /// <summary>JSON of the 5e system with the web defaults (camelCase), as the API writes it.</summary>
    public static JsonObject ToJsonObject<T>(T value) =>
        JsonSerializer.SerializeToNode(value, JsonSerializerOptions.Web) as JsonObject ?? [];

    public static T? FromJson<T>(JsonElement element) => element.Deserialize<T>(JsonSerializerOptions.Web);
}

/// <summary>A calculated D&amp;D 5e sheet with the character and the catalog data it was calculated with.</summary>
public sealed class Dnd5eSystemSheet(Dnd5eCharacter character, CharacterSheet sheet, SheetCatalog? catalog = null) : SystemSheet
{
    public Dnd5eCharacter Character { get; } = character;

    public CharacterSheet Sheet { get; } = sheet;

    public SheetCatalog? Catalog { get; } = catalog;
}
