namespace Dnd.Application.Abstractions;

/// <summary>Server-wide defaults applied to new campaigns (configured under <c>App</c>).</summary>
public interface ICampaignDefaults
{
    /// <summary>IANA time zone assigned to new campaigns (<c>App:DefaultTimeZone</c>).</summary>
    string TimeZoneId { get; }
}
