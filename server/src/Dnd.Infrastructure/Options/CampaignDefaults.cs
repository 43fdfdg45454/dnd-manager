using Dnd.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Dnd.Infrastructure.Options;

internal sealed class CampaignDefaults(IOptions<AppOptions> options) : ICampaignDefaults
{
    public string TimeZoneId => options.Value.DefaultTimeZone;
}
