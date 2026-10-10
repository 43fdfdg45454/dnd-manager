using System.Text.Json;
using OpenTrpg.Core.Application.Abstractions.Persistence;

namespace OpenTrpg.Core.Application.ChangeRequests;

/// <param name="Payload">The requested change as a JSON object (a <c>SheetPatch</c> for EditSheet, <c>{}</c> for Activate, <c>{ beastIndex, beastName, name }</c> for Companion).</param>
/// <param name="Before">
/// What the payload changes as it was when the request was created: for EditSheet a <c>SheetPatch</c>
/// with the same fields holding the previous values; for RemoveItem <c>{ quantity }</c>; for
/// AdjustMoney <c>{ copperPieces }</c>; for AddItem/CustomItem with a catalog item <c>{ template }</c>
/// (the catalog item); for Companion <c>{ beastIndex, beastName, name }</c> like the payload. Null when there is nothing
/// to compare or the request predates it.
/// </param>
public sealed record ChangeRequestDto(
    Guid Id,
    Guid CampaignId,
    Guid CharacterId,
    string CharacterName,
    Guid RequestedByUserId,
    string RequestedByDisplayName,
    string Type,
    JsonElement Payload,
    string Status,
    Guid? ResolvedByUserId,
    string? ResolvedByDisplayName,
    DateTimeOffset? ResolvedAt,
    string? Comment,
    DateTimeOffset CreatedAt,
    JsonElement? Before)
{
    private static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement.Clone();

    public static ChangeRequestDto From(ChangeRequestView view)
    {
        var request = view.Request;
        return new ChangeRequestDto(
            request.Id,
            request.CampaignId,
            request.CharacterId,
            view.CharacterName,
            request.RequestedByUserId,
            view.RequestedByDisplayName,
            request.Type.ToString(),
            ParsePayload(request.PayloadJson),
            request.Status.ToString(),
            request.ResolvedByUserId,
            view.ResolvedByDisplayName,
            request.ResolvedAt,
            request.Comment,
            request.CreatedAt,
            request.BeforeJson is null ? null : ParsePayload(request.BeforeJson));
    }

    private static JsonElement ParsePayload(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object ? document.RootElement.Clone() : EmptyObject;
        }
        catch (JsonException)
        {
            return EmptyObject;
        }
    }
}
