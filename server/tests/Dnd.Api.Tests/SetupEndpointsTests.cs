using System.Net;
using System.Net.Http.Json;
using Dnd.Application.Setup;
using Dnd.Application.Users;

namespace Dnd.Api.Tests;

public class SetupEndpointsTests
{
    private const string Email = "first-admin@example.com";
    private const string Password = "first-admin-password";

    private static object Body(string email = Email, string displayName = "Primera Admin", string password = Password) =>
        new { email, displayName, password };

    [Fact]
    public async Task First_admin_is_created_once_and_can_log_in()
    {
        using var factory = new ApiFactoryWithoutInitialAdmin();
        var client = factory.CreateClient();

        var before = await client.GetFromJsonAsync<SetupStatusDto>("/api/v1/setup/status");
        Assert.True(before!.NeedsSetup);

        var created = await client.PostAsJsonAsync("/api/v1/setup/admin", Body());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var user = await created.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal(Email, user!.Email);
        Assert.Equal("Admin", user.Role);
        Assert.True(user.HasPassword);

        var auth = await client.LoginAsync(Email, Password);
        Assert.Equal("Admin", auth.User.Role);
        Assert.Equal("Primera Admin", auth.User.DisplayName);

        var second = await client.PostAsJsonAsync("/api/v1/setup/admin", Body("other-admin@example.com"));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "other-admin@example.com", password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);

        var after = await client.GetFromJsonAsync<SetupStatusDto>("/api/v1/setup/status");
        Assert.False(after!.NeedsSetup);
    }

    [Fact]
    public async Task Setup_is_closed_when_the_instance_already_has_users()
    {
        using var factory = new ApiFactory();
        _ = await factory.CreateAdminClientAsync();
        var client = factory.CreateClient();

        Assert.False((await client.GetFromJsonAsync<SetupStatusDto>("/api/v1/setup/status"))!.NeedsSetup);
        var response = await client.PostAsJsonAsync("/api/v1/setup/admin", Body());
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Simultaneous_requests_create_a_single_admin()
    {
        using var factory = new ApiFactoryWithoutInitialAdmin();
        var client = factory.CreateClient();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(i =>
            client.PostAsJsonAsync("/api/v1/setup/admin", Body($"admin-{i}@example.com"))));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(4, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task A_short_password_is_rejected_in_spanish()
    {
        using var factory = new ApiFactoryWithoutInitialAdmin();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/setup/admin", Body(password: "corta"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("La contraseña debe tener al menos", await response.Content.ReadAsStringAsync());
        Assert.True((await client.GetFromJsonAsync<SetupStatusDto>("/api/v1/setup/status"))!.NeedsSetup);
    }

    [Theory]
    [InlineData("not-an-email", "Nombre")]
    [InlineData(Email, "")]
    public async Task Invalid_email_or_name_is_rejected(string email, string displayName)
    {
        using var factory = new ApiFactoryWithoutInitialAdmin();

        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/setup/admin", Body(email, displayName));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Admin_page_is_served_as_uncached_html()
    {
        using var factory = new ApiFactoryWithoutInitialAdmin();

        var response = await factory.CreateClient().GetAsync("/admin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        Assert.Contains("Crear administrador", await response.Content.ReadAsStringAsync());
    }
}
