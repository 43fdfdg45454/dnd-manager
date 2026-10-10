using System.Net;
using System.Net.Http.Json;
using OpenTrpg.Core.Application.Users;

namespace OpenTrpg.Core.Api.Tests;

public class UserSearchEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Search_matches_email_or_name_case_insensitively_and_excludes_inactive_users()
    {
        var marker = Guid.NewGuid().ToString("N")[..10];
        var searcher = await factory.CreateSignedInUserAsync();
        var byName = await factory.CreateSignedInUserAsync($"Bruja {marker.ToUpperInvariant()}");
        var byEmail = await factory.CreateSignedInUserAsync("Bardo", $"bard-{marker}@example.com");
        var inactive = await factory.CreateSignedInUserAsync($"Inactivo {marker}");
        await factory.SetUserActiveAsync(inactive.Id, false);

        var response = await searcher.Client.GetAsync($"/api/v1/users/search?q={marker}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var users = (await response.Content.ReadFromJsonAsync<List<UserSummaryDto>>())!;
        Assert.Equal(
            [new UserSummaryDto(byEmail.Id, "Bardo", byEmail.Email), new UserSummaryDto(byName.Id, byName.DisplayName, byName.Email)],
            users);
        Assert.DoesNotContain(users, u => u.Id == inactive.Id);
    }

    [Fact]
    public async Task Search_respects_the_limit()
    {
        var marker = Guid.NewGuid().ToString("N")[..10];
        var searcher = await factory.CreateSignedInUserAsync();
        for (var i = 0; i < 4; i++)
        {
            await factory.CreateSignedInUserAsync($"Limit {marker} {i}");
        }

        var limited = (await searcher.Client.GetFromJsonAsync<List<UserSummaryDto>>($"/api/v1/users/search?q={marker}&limit=3"))!;
        var byDefault = (await searcher.Client.GetFromJsonAsync<List<UserSummaryDto>>($"/api/v1/users/search?q={marker}"))!;

        Assert.Equal(3, limited.Count);
        Assert.Equal(4, byDefault.Count);
    }

    [Theory]
    [InlineData("/api/v1/users/search", "q")]
    [InlineData("/api/v1/users/search?q=a", "q")]
    [InlineData("/api/v1/users/search?q=%20a%20", "q")]
    [InlineData("/api/v1/users/search?q=ab&limit=0", "limit")]
    [InlineData("/api/v1/users/search?q=ab&limit=51", "limit")]
    public async Task Search_validates_query_and_limit(string url, string field)
    {
        var searcher = await factory.CreateSignedInUserAsync();

        var response = await searcher.Client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.ReadProblemAsync()).HasFieldError(field));
    }

    [Fact]
    public async Task Search_requires_authentication_but_no_admin_role()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/v1/users/search?q=example")).StatusCode);

        var user = await factory.CreateSignedInUserAsync();
        Assert.Equal(HttpStatusCode.OK, (await user.Client.GetAsync("/api/v1/users/search?q=example")).StatusCode);
    }
}
