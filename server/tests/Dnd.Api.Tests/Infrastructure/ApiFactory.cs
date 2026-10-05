using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dnd.Application.Abstractions;
using Dnd.Application.Users;
using Dnd.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Dnd.Api.Tests;

/// <summary>
/// Boots the API against an in-memory SQLite database (one open connection per factory, schema
/// created with <c>EnsureCreated</c>) and a fake email sender. The initial admin bootstrap runs
/// with <see cref="AdminEmail"/> unless a subclass disables it. The SRD catalog import is off by
/// default to keep unrelated tests fast; <see cref="CatalogApiFactory"/> turns it on.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@example.com";
    public const string AdminPassword = "admin-password-1234";
    public const string PublicUrl = "http://dnd.example.com";

    private readonly SqliteConnection _connection;
    private readonly string _filesRoot = Path.Combine(Path.GetTempPath(), $"dnd-tests-{Guid.NewGuid():N}");
    private readonly SemaphoreSlim _adminLock = new(1, 1);
    private bool _adminPasswordSet;

    public ApiFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        db.Database.EnsureCreated();
    }

    public FakeEmailSender Emails { get; } = new();

    public LogCapture Logs { get; } = new();

    protected virtual bool SeedInitialAdmin => true;

    protected virtual bool SeedCatalog => false;

    /// <summary>Whether the background <c>ReminderDispatcher</c> runs. Off by default: tests call the processor directly.</summary>
    protected virtual bool RunReminderDispatcher => false;

    /// <summary>Maximum upload size configured for the host (<c>FileStorage:MaxUploadMegabytes</c>).</summary>
    protected virtual int MaxUploadMegabytes => 200;

    /// <summary>Temporary directory used as <c>FileStorage:RootPath</c>; removed when the factory is disposed.</summary>
    public string FilesRoot => _filesRoot;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:AutoMigrate", "false");
        builder.UseSetting("App:PublicUrl", PublicUrl);
        builder.UseSetting("App:InitialAdminEmail", AdminEmail);
        builder.UseSetting("App:SeedInitialAdmin", SeedInitialAdmin ? "true" : "false");
        builder.UseSetting("Reminders:Enabled", RunReminderDispatcher ? "true" : "false");
        builder.UseSetting("Reminders:PollSeconds", "1");
        builder.UseSetting("Catalog:SeedOnStartup", SeedCatalog ? "true" : "false");
        builder.UseSetting("FileStorage:RootPath", _filesRoot);
        builder.UseSetting("FileStorage:MaxUploadMegabytes", MaxUploadMegabytes.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("Jwt:Secret", "test-only-secret-that-is-long-enough-0123456789");

        builder.ConfigureLogging(logging => logging.AddProvider(Logs));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
        });
    }

    /// <summary>Runs <paramref name="action"/> against the test database (for arranging edge cases).</summary>
    public async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>Client authenticated as the seeded admin (its password is set on first use).</summary>
    public async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = CreateClient();
        await _adminLock.WaitAsync();
        try
        {
            if (!_adminPasswordSet)
            {
                var token = Emails.LastTokenSentTo(AdminEmail);
                var response = await client.PostAsJsonAsync("/api/v1/auth/password/set", new { token, password = AdminPassword });
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                _adminPasswordSet = true;
            }
        }
        finally
        {
            _adminLock.Release();
        }

        var auth = await client.LoginAsync(AdminEmail, AdminPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    /// <summary>Creates a user through the admin API and sets its password with the emailed token.</summary>
    public async Task<TestUser> CreateUserAsync(string role = "User")
    {
        var admin = await CreateAdminClientAsync();
        var email = $"user-{Guid.NewGuid():N}@example.com";
        var response = await admin.PostAsJsonAsync("/api/v1/admin/users", new { email, displayName = "Test User", role });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = (await response.Content.ReadFromJsonAsync<UserDto>())!;

        var password = $"pw-{Guid.NewGuid():N}";
        var set = await CreateClient().PostAsJsonAsync(
            "/api/v1/auth/password/set",
            new { token = Emails.LastTokenSentTo(email), password });
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);

        return new TestUser(user, password);
    }

    /// <summary>Client authenticated as <paramref name="user"/>.</summary>
    public async Task<HttpClient> CreateClientForAsync(TestUser user)
    {
        var client = CreateClient();
        var auth = await client.LoginAsync(user.Email, user.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
            _adminLock.Dispose();
            try
            {
                Directory.Delete(_filesRoot, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
                // Nothing was stored.
            }
        }
    }
}

/// <summary>
/// Factory that imports the SRD catalog at startup. Shared by every catalog test class through the
/// <see cref="CatalogCollection"/> so the import runs once.
/// </summary>
public sealed class CatalogApiFactory : ApiFactory
{
    protected override bool SeedCatalog => true;
}

[CollectionDefinition(Name)]
public sealed class CatalogCollection : ICollectionFixture<CatalogApiFactory>
{
    public const string Name = "Catalog";
}

/// <summary>Factory whose initial admin bootstrap is turned off.</summary>
public sealed class ApiFactoryWithoutInitialAdmin : ApiFactory
{
    protected override bool SeedInitialAdmin => false;
}

public sealed record TestUser(UserDto User, string Password)
{
    public string Email => User.Email;
}
