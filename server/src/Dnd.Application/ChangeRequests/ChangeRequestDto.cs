using System.Text.Json;
using Dnd.Application.Abstractions.Persistence;

namespace Dnd.Application.ChangeRequests;

/// <param name="Payload">The requested change as a JSON object (a <c>SheetPatch</c> for EditSheet, <c>{}</c> for Activate).</param>
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
    DateTimeOffset CreatedAt)
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
            request.CreatedAt);
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
