namespace OpenTrpg.Core.Application.Sessions;

public sealed record RsvpDto(Guid UserId, string DisplayName, string Status, string? Comment);

/// <param name="Pending">Members of the campaign who have not answered yet.</param>
public sealed record RsvpCountsDto(int Yes, int No, int Maybe, int Pending);

public sealed record ReminderDto(int OffsetMinutes, DateTimeOffset SendAt, DateTimeOffset? SentAt, DateTimeOffset? FailedAt);

/// <param name="StartsAtLocal">ISO 8601 with the offset of the campaign's time zone at that instant.</param>
/// <param name="MyRsvp">Name of the answer of the current user ("Yes" | "No" | "Maybe"), or null when pending.</param>
/// <param name="Reminders">Only for DMs; null for players.</param>
public sealed record SessionDto(
    Guid Id,
    int Number,
    Guid CampaignId,
    string CampaignName,
    string Title,
    DateTimeOffset StartsAt,
    string StartsAtLocal,
    string TimeZoneId,
    int? DurationMinutes,
    string? Location,
    string? Notes,
    string? SummaryMarkdown,
    DateTimeOffset? SummaryUpdatedAt,
    string Status,
    string? MyRsvp,
    IReadOnlyList<RsvpDto> Rsvps,
    RsvpCountsDto Counts,
    IReadOnlyList<ReminderDto>? Reminders);

public sealed record SessionSummaryDto(
    Guid Id,
    int Number,
    string Title,
    DateTimeOffset StartsAt,
    string StartsAtLocal,
    string Status,
    string SummaryMarkdown,
    DateTimeOffset? SummaryUpdatedAt);

/// <summary>What the public page of an email link shows: the session as seen by the user the link was issued to.</summary>
public sealed record PublicSessionDto(
    Guid Id,
    int Number,
    string CampaignName,
    string Title,
    DateTimeOffset StartsAt,
    string StartsAtLocal,
    string TimeZoneId,
    int? DurationMinutes,
    string? Location,
    string? Notes,
    string Status,
    string UserDisplayName,
    string? MyRsvp,
    string? MyComment);
