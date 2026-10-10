using System.Globalization;
using System.Text.Json;
using OpenTrpg.Core.Domain.Common;

namespace OpenTrpg.Core.Domain.Sessions;

/// <summary>
/// Scheduling rules shared by campaigns and sessions: IANA time zone ids and reminder offsets.
/// </summary>
public static class CampaignSchedule
{
    public const int TimeZoneIdMaxLength = 64;
    public const int MaxOffsets = 10;
    public const int MinOffsetMinutes = 1;

    /// <summary>30 days.</summary>
    public const int MaxOffsetMinutes = 43_200;

    /// <summary>Time zone of a campaign created without one (the server default normally overrides it).</summary>
    public const string FallbackTimeZoneId = "Europe/Madrid";

    /// <summary>24 h and 2 h before the session.</summary>
    public static readonly IReadOnlyList<int> DefaultOffsetsMinutes = [1440, 120];

    /// <summary>Resolves an IANA time zone id (for example <c>Europe/Madrid</c>); false when unknown.</summary>
    public static bool TryFindTimeZone(string? id, out TimeZoneInfo timeZone)
    {
        timeZone = TimeZoneInfo.Utc;
        if (string.IsNullOrWhiteSpace(id) || id.Length > TimeZoneIdMaxLength || id != id.Trim())
        {
            return false;
        }

        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            return false;
        }
    }

    public static bool IsValidTimeZone(string? id) => TryFindTimeZone(id, out _);

    /// <summary>Returns the id when it is a valid time zone; otherwise throws a rule violation.</summary>
    public static string RequireTimeZone(string? id) =>
        IsValidTimeZone(id) ? id! : throw DomainException.RuleViolation("La zona horaria no es válida. Usa un identificador IANA, por ejemplo \"Europe/Madrid\".");

    /// <summary>The instant expressed in the time zone (an unknown id falls back to UTC).</summary>
    public static DateTimeOffset ToLocal(DateTimeOffset instant, string timeZoneId) =>
        TimeZoneInfo.ConvertTime(instant, TryFindTimeZone(timeZoneId, out var zone) ? zone : TimeZoneInfo.Utc);

    /// <summary>ISO 8601 with the UTC offset of the zone at that instant, e.g. <c>2026-10-10T20:00:00+02:00</c>.</summary>
    public static string ToLocalIso(DateTimeOffset instant, string timeZoneId) =>
        ToLocal(instant, timeZoneId).ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);

    public static bool AreValidOffsets(IReadOnlyCollection<int>? offsets) =>
        offsets is not null
        && offsets.Count <= MaxOffsets
        && offsets.All(o => o is >= MinOffsetMinutes and <= MaxOffsetMinutes)
        && offsets.Distinct().Count() == offsets.Count;

    /// <summary>Validates the offsets and returns them sorted from the earliest reminder (largest offset) to the latest.</summary>
    public static IReadOnlyList<int> RequireOffsets(IReadOnlyCollection<int>? offsets) =>
        AreValidOffsets(offsets)
            ? offsets!.OrderByDescending(o => o).ToList()
            : throw DomainException.RuleViolation(
                $"Los recordatorios deben ser como máximo {MaxOffsets} valores únicos entre {MinOffsetMinutes} y {MaxOffsetMinutes} minutos.");

    public static string SerializeOffsets(IReadOnlyList<int> offsets) => JsonSerializer.Serialize(offsets);

    public static IReadOnlyList<int> ParseOffsets(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return DefaultOffsetsMinutes;
        }

        try
        {
            return JsonSerializer.Deserialize<int[]>(json) ?? [];
        }
        catch (JsonException)
        {
            return DefaultOffsetsMinutes;
        }
    }
}
