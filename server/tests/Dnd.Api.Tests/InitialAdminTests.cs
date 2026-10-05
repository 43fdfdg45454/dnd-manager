using System.Net;
using System.Net.Http.Json;
using Dnd.Application.Users;
using Dnd.Infrastructure.Email.Templates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dnd.Api.Tests;

public class InitialAdminTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Initial_admin_is_created_at_startup_and_can_set_password_from_the_email()
    {
        var client = factory.CreateClient();

        var email = Assert.Single(factory.Emails.SentTo(ApiFactory.AdminEmail));
        Assert.Equal(AccountEmailTemplates.SetupSubject, email.Subject);
        // No request went through a proxy yet and App:PublicUrl is not set: the link is relative.
        Assert.StartsWith("/set-password?token=", FakeEmailSender.ExtractLink(email));

        var set = await client.PostAsJsonAsync(
            "/api/v1/auth/password/set",
            new { token = FakeEmailSender.ExtractToken(email), password = ApiFactory.AdminPassword });
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);

        var auth = await client.LoginAsync(ApiFactory.AdminEmail, ApiFactory.AdminPassword);
        Assert.Equal("Admin", auth.User.Role);
        Assert.Equal(InitialAdminSeeder.DefaultDisplayName, auth.User.DisplayName);
        Assert.Equal(ApiFactory.AdminEmail, auth.User.Email);
        Assert.True(auth.User.HasPassword);
    }

    [Fact]
    public void Initial_admin_link_is_logged_as_a_warning()
    {
        _ = factory.CreateClient();
        var link = FakeEmailSender.ExtractLink(factory.Emails.SentTo(ApiFactory.AdminEmail)[0]);

        Assert.Contains(factory.Logs.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains(link));
    }

    [Fact]
    public async Task Seeder_does_nothing_when_users_already_exist()
    {
        _ = factory.CreateClient();
        var sentBefore = factory.Emails.Messages.Count;

        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<InitialAdminSeeder>().SeedAsync("other-admin@example.com");
        }

        Assert.Equal(sentBefore, factory.Emails.Messages.Count);
        Assert.Empty(factory.Emails.SentTo("other-admin@example.com"));
    }
}

public class InitialAdminDisabledTests(ApiFactoryWithoutInitialAdmin factory) : IClassFixture<ApiFactoryWithoutInitialAdmin>
{
    [Fact]
    public async Task No_admin_is_created_when_the_bootstrap_is_disabled()
    {
        var client = factory.CreateClient();

        Assert.Empty(factory.Emails.Messages);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = ApiFactory.AdminEmail, password = ApiFactory.AdminPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }
}
