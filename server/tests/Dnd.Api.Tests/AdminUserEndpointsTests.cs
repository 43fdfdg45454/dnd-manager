using System.Net;
using System.Net.Http.Json;
using Dnd.Application.Common;
using Dnd.Application.Users;
using Dnd.Infrastructure.Email.Templates;

namespace Dnd.Api.Tests;

public class AdminUserEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Create_user_sends_setup_email_whose_token_sets_the_password()
    {
        var admin = await factory.CreateAdminClientAsync();
        var email = $"New.Player-{Guid.NewGuid():N}@Example.com";

        var response = await admin.PostAsJsonAsync("/api/v1/admin/users", new { email, displayName = "  New Player ", role = "User" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<UserDto>())!;
        Assert.Equal(email.ToLowerInvariant(), created.Email);
        Assert.Equal("New Player", created.DisplayName);
        Assert.Equal("User", created.Role);
        Assert.True(created.IsActive);
        Assert.False(created.HasPassword);
        Assert.Null(created.LastLoginAt);

        var message = factory.Emails.LastSentTo(created.Email);
        Assert.Equal(AccountEmailTemplates.SetupSubject, message.Subject);
        Assert.Contains("48 horas", message.TextBody);
        Assert.Contains("set-password?token=", message.HtmlBody);

        var client = factory.CreateClient();
        var set = await client.PostAsJsonAsync("/api/v1/auth/password/set", new { token = FakeEmailSender.ExtractToken(message), password = "a-good-password" });
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);

        var auth = await client.LoginAsync(created.Email, "a-good-password");
        Assert.True(auth.User.HasPassword);
    }

    [Fact]
    public async Task Create_user_with_existing_email_returns_409()
    {
        var admin = await factory.CreateAdminClientAsync();
        var user = await factory.CreateUserAsync();

        var response = await admin.PostAsJsonAsync("/api/v1/admin/users", new { email = user.Email.ToUpperInvariant(), displayName = "Dup", role = "User" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(409, (await response.ReadProblemAsync()).GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Create_user_validates_fields()
    {
        var admin = await factory.CreateAdminClientAsync();

        var response = await admin.PostAsJsonAsync("/api/v1/admin/users", new { email = "not-an-email", displayName = "", role = "Superuser" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.True(problem.HasFieldError("email"));
        Assert.True(problem.HasFieldError("displayName"));
        Assert.True(problem.HasFieldError("role"));
    }

    [Fact]
    public async Task Regular_user_gets_403_and_anonymous_gets_401_on_admin_endpoints()
    {
        var user = await factory.CreateUserAsync();
        var client = await factory.CreateClientForAsync(user);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/users")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/v1/admin/users", new { email = "x@example.com", displayName = "X", role = "User" })).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PatchAsJsonAsync($"/api/v1/admin/users/{user.User.Id}", new { role = "Admin" })).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PostAsync($"/api/v1/admin/users/{user.User.Id}/setup-email", null)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/v1/admin/users")).StatusCode);
    }

    [Fact]
    public async Task Admin_cannot_deactivate_or_demote_themselves()
    {
        var admin = await factory.CreateAdminClientAsync();
        var me = (await admin.GetFromJsonAsync<UserDto>("/api/v1/auth/me"))!;

        var deactivate = await admin.PatchAsJsonAsync($"/api/v1/admin/users/{me.Id}", new { isActive = false });
        Assert.Equal(HttpStatusCode.BadRequest, deactivate.StatusCode);
        Assert.True((await deactivate.ReadProblemAsync()).HasFieldError("isActive"));

        var demote = await admin.PatchAsJsonAsync($"/api/v1/admin/users/{me.Id}", new { role = "User" });
        Assert.Equal(HttpStatusCode.BadRequest, demote.StatusCode);
        Assert.True((await demote.ReadProblemAsync()).HasFieldError("role"));

        var rename = await admin.PatchAsJsonAsync($"/api/v1/admin/users/{me.Id}", new { displayName = "Jefe", role = "Admin", isActive = true });
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
        Assert.Equal("Jefe", (await rename.Content.ReadFromJsonAsync<UserDto>())!.DisplayName);
    }

    [Fact]
    public async Task Inactive_user_cannot_login_nor_refresh()
    {
        var admin = await factory.CreateAdminClientAsync();
        var user = await factory.CreateUserAsync();
        var client = factory.CreateClient();
        var session = await client.LoginAsync(user.Email, user.Password);

        var patch = await admin.PatchAsJsonAsync($"/api/v1/admin/users/{user.User.Id}", new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        Assert.False((await patch.Content.ReadFromJsonAsync<UserDto>())!.IsActive);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = user.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);

        var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = session.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);

        // Reactivated users can log in again.
        (await admin.PatchAsJsonAsync($"/api/v1/admin/users/{user.User.Id}", new { isActive = true })).EnsureSuccessStatusCode();
        await client.LoginAsync(user.Email, user.Password);
    }

    [Fact]
    public async Task Update_user_changes_role_and_returns_404_for_unknown_user()
    {
        var admin = await factory.CreateAdminClientAsync();
        var user = await factory.CreateUserAsync();

        var promote = await admin.PatchAsJsonAsync($"/api/v1/admin/users/{user.User.Id}", new { role = "Admin" });
        Assert.Equal(HttpStatusCode.OK, promote.StatusCode);
        Assert.Equal("Admin", (await promote.Content.ReadFromJsonAsync<UserDto>())!.Role);

        var promotedClient = await factory.CreateClientForAsync(user);
        Assert.Equal(HttpStatusCode.OK, (await promotedClient.GetAsync("/api/v1/admin/users")).StatusCode);

        var invalidRole = await admin.PatchAsJsonAsync($"/api/v1/admin/users/{user.User.Id}", new { role = "God" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidRole.StatusCode);

        var missing = await admin.PatchAsJsonAsync($"/api/v1/admin/users/{Guid.NewGuid()}", new { displayName = "Nobody" });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task List_users_supports_search_and_paging()
    {
        var admin = await factory.CreateAdminClientAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        foreach (var name in new[] { "Alpha", "Beta", "Gamma" })
        {
            var response = await admin.PostAsJsonAsync(
                "/api/v1/admin/users",
                new { email = $"{name.ToLowerInvariant()}-{marker}@example.com", displayName = $"{name} {marker}", role = "User" });
            response.EnsureSuccessStatusCode();
        }

        var page1 = (await admin.GetFromJsonAsync<PagedResult<UserDto>>($"/api/v1/admin/users?search={marker.ToUpperInvariant()}&page=1&pageSize=2"))!;
        Assert.Equal(3, page1.Total);
        Assert.Equal(1, page1.Page);
        Assert.Equal(2, page1.PageSize);
        Assert.Equal(["alpha", "beta"], page1.Items.Select(u => u.Email.Split('-')[0]));

        var page2 = (await admin.GetFromJsonAsync<PagedResult<UserDto>>($"/api/v1/admin/users?search={marker}&page=2&pageSize=2"))!;
        Assert.Equal("gamma", Assert.Single(page2.Items).Email.Split('-')[0]);

        var all = (await admin.GetFromJsonAsync<PagedResult<UserDto>>("/api/v1/admin/users"))!;
        Assert.Equal(50, all.PageSize);
        Assert.Contains(all.Items, u => u.Email == ApiFactory.AdminEmail);

        var invalid = await admin.GetAsync("/api/v1/admin/users?page=0&pageSize=1000");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var problem = await invalid.ReadProblemAsync();
        Assert.True(problem.HasFieldError("page"));
        Assert.True(problem.HasFieldError("pageSize"));
    }

    [Fact]
    public async Task Resend_setup_email_issues_a_new_token_and_invalidates_the_previous_one()
    {
        var admin = await factory.CreateAdminClientAsync();
        var email = $"resend-{Guid.NewGuid():N}@example.com";
        var created = (await (await admin.PostAsJsonAsync("/api/v1/admin/users", new { email, displayName = "Resend", role = "User" }))
            .Content.ReadFromJsonAsync<UserDto>())!;
        var firstToken = factory.Emails.LastTokenSentTo(email);

        var resend = await admin.PostAsync($"/api/v1/admin/users/{created.Id}/setup-email", null);
        Assert.Equal(HttpStatusCode.Accepted, resend.StatusCode);
        Assert.Equal(2, factory.Emails.SentTo(email).Count);
        var secondToken = factory.Emails.LastTokenSentTo(email);
        Assert.NotEqual(firstToken, secondToken);

        var client = factory.CreateClient();
        var withOld = await client.PostAsJsonAsync("/api/v1/auth/password/set", new { token = firstToken, password = "resend-password-1" });
        Assert.Equal(HttpStatusCode.BadRequest, withOld.StatusCode);
        var withNew = await client.PostAsJsonAsync("/api/v1/auth/password/set", new { token = secondToken, password = "resend-password-1" });
        Assert.Equal(HttpStatusCode.NoContent, withNew.StatusCode);

        var missing = await admin.PostAsync($"/api/v1/admin/users/{Guid.NewGuid()}/setup-email", null);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
}
