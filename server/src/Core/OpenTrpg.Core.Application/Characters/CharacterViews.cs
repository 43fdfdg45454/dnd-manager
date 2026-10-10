using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.ChangeRequests;
using OpenTrpg.Core.Application.Files;
using OpenTrpg.Core.Application.Systems;
using OpenTrpg.Core.Domain.Characters;

namespace OpenTrpg.Core.Application.Characters;

/// <summary>
/// Full character: the core fields (identity, owner, texts, money, pending change requests) and, at the same level,
/// the fields of the campaign's game system (<see cref="ISheetSystem.BuildDetailAsync"/>: sheet, inventory…).
/// </summary>
public sealed record CharacterDetailDto
{
    public required Guid Id { get; init; }

    public required Guid CampaignId { get; init; }

    public required Guid? OwnerUserId { get; init; }

    public required string? OwnerDisplayName { get; init; }

    public required string Name { get; init; }

    public required string Status { get; init; }

    /// <summary>Money in the minor unit of the system's currency (copper pieces in D&amp;D 5e).</summary>
    public required int CopperPieces { get; init; }

    public required string Notes { get; init; }

    public required string Backstory { get; init; }

    public required string PersonalityTraits { get; init; }

    public required string Ideals { get; init; }

    public required string Bonds { get; init; }

    public required string Flaws { get; init; }

    /// <summary>Height in inches, or null when not given. No mechanical effect.</summary>
    public required int? HeightInches { get; init; }

    /// <summary>Weight in pounds, or null when not given. No mechanical effect.</summary>
    public required int? WeightPounds { get; init; }

    public required Guid? PortraitFileId { get; init; }

    /// <summary>Relative download URL (<c>/api/v1/files/{id}</c>) of the portrait, or null when there is none.</summary>
    public required string? PortraitUrl { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public required IReadOnlyList<ChangeRequestDto> PendingChangeRequests { get; init; }

    /// <summary>The fields of the game system, serialized at the same level as the core ones.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? SystemFields { get; init; }
}

/// <summary>
/// What every member sees of a character in the campaign roster: the core fields and, at the same level, the fields of
/// the game system (<see cref="ISheetSystem.BuildRosterLine"/>; hit points only for the owner and DMs).
/// </summary>
public sealed record CharacterSummaryDto
{
    public required Guid Id { get; init; }

    public required Guid CampaignId { get; init; }

    public required Guid? OwnerUserId { get; init; }

    public required string? OwnerDisplayName { get; init; }

    public required string Name { get; init; }

    public required string Status { get; init; }

    public required string? PortraitUrl { get; init; }

    /// <summary>The fields of the game system, serialized at the same level as the core ones.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? SystemFields { get; init; }
}

/// <summary>Builds the detail and the roster of characters: the core part here, the system part from the campaign's system.</summary>
public sealed class CharacterViews(CampaignSystems systems, IUserRepository users, IChangeRequestRepository changeRequests)
{
    public Task<CharacterDetailDto> BuildDetailAsync(Character character, CancellationToken cancellationToken = default) =>
        BuildDetailAsync(new CharacterRef(character), cancellationToken);

    public async Task<CharacterDetailDto> BuildDetailAsync(CharacterRef reference, CancellationToken cancellationToken = default)
    {
        var character = reference.Character;
        var system = await systems.ForCharacterAsync(character, cancellationToken);
        var systemFields = await system.Sheets.BuildDetailAsync(reference, cancellationToken);
        var ownerName = character.OwnerUserId is { } ownerId
            ? (await users.GetDisplayNamesAsync([ownerId], cancellationToken)).GetValueOrDefault(ownerId)
            : null;
        var pending = await changeRequests.ListViewsAsync(
            new ChangeRequestQuery(CharacterId: character.Id, Status: ChangeRequestStatus.Pending),
            cancellationToken);

        return new CharacterDetailDto
        {
            Id = character.Id,
            CampaignId = character.CampaignId,
            OwnerUserId = character.OwnerUserId,
            OwnerDisplayName = ownerName,
            Name = character.Name,
            Status = character.Status.ToString(),
            CopperPieces = character.Money,
            Notes = character.Notes,
            Backstory = character.Backstory,
            PersonalityTraits = character.PersonalityTraits,
            Ideals = character.Ideals,
            Bonds = character.Bonds,
            Flaws = character.Flaws,
            HeightInches = character.HeightInches,
            WeightPounds = character.WeightPounds,
            PortraitFileId = character.PortraitFileId,
            PortraitUrl = FileUrls.For(character.PortraitFileId),
            CreatedAt = character.CreatedAt,
            UpdatedAt = character.UpdatedAt,
            PendingChangeRequests = pending.Select(ChangeRequestDto.From).ToList(),
            SystemFields = JsonFields.From(WithoutCoreFields(systemFields, DetailCoreFields)),
        };
    }

    /// <summary>Summaries sorted by name. The system shows hit points only for characters the viewer owns, or all to a DM.</summary>
    public async Task<IReadOnlyList<CharacterSummaryDto>> BuildSummariesAsync(
        Guid campaignId,
        IReadOnlyList<Character> characters,
        Guid viewerUserId,
        bool viewerIsDm,
        CancellationToken cancellationToken = default)
    {
        var system = await systems.ForCampaignAsync(campaignId, cancellationToken);
        var references = characters.Select(c => new CharacterRef(c)).ToList();
        var sheets = await system.Sheets.CalculateManyAsync(references, cancellationToken);
        var ownerIds = characters.Select(c => c.OwnerUserId).OfType<Guid>().Distinct().ToList();
        var owners = await users.GetDisplayNamesAsync(ownerIds, cancellationToken);

        return references
            .OrderBy(r => r.Character.Name, StringComparer.InvariantCultureIgnoreCase)
            .ThenBy(r => r.Id)
            .Select(r =>
            {
                var c = r.Character;
                var line = system.Sheets.BuildRosterLine(r, sheets[c.Id], c.CanViewSheet(viewerUserId, viewerIsDm));
                return new CharacterSummaryDto
                {
                    Id = c.Id,
                    CampaignId = c.CampaignId,
                    OwnerUserId = c.OwnerUserId,
                    OwnerDisplayName = c.OwnerUserId is { } owner ? owners.GetValueOrDefault(owner) : null,
                    Name = c.Name,
                    Status = c.Status.ToString(),
                    PortraitUrl = FileUrls.For(c.PortraitFileId),
                    SystemFields = JsonFields.From(WithoutCoreFields(line.Fields, SummaryCoreFields)),
                };
            })
            .ToList();
    }

    private static readonly HashSet<string> DetailCoreFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "campaignId", "ownerUserId", "ownerDisplayName", "name", "status", "copperPieces", "notes", "backstory",
        "personalityTraits", "ideals", "bonds", "flaws", "heightInches", "weightPounds", "portraitFileId", "portraitUrl",
        "createdAt", "updatedAt", "pendingChangeRequests",
    };

    private static readonly HashSet<string> SummaryCoreFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "campaignId", "ownerUserId", "ownerDisplayName", "name", "status", "portraitUrl",
    };

    /// <summary>The system's fields without any that would collide with a core field (the core ones win).</summary>
    private static JsonObject WithoutCoreFields(JsonObject fields, HashSet<string> coreFields)
    {
        foreach (var key in fields.Select(p => p.Key).Where(coreFields.Contains).ToList())
        {
            fields.Remove(key);
        }

        return fields;
    }
}
