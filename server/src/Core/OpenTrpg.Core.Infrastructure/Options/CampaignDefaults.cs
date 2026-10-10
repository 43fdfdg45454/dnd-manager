using OpenTrpg.Core.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace OpenTrpg.Core.Infrastructure.Options;

internal sealed class CampaignDefaults(IOptions<AppOptions> options) : ICampaignDefaults
{
    public string TimeZoneId => options.Value.DefaultTimeZone;
}
