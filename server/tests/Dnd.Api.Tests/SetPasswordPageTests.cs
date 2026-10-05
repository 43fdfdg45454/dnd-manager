using System.Net;

namespace Dnd.Api.Tests;

public class SetPasswordPageTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Set_password_page_is_served_as_html_outside_the_api()
    {
        var response = await factory.CreateClient().GetAsync("/set-password?token=abc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("/api/v1/auth/password/set", html);
        Assert.Contains("lang=\"es\"", html);
    }
}
