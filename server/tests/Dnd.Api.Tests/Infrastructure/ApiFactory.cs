using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dnd.Application.Abstractions;
using Dnd.Application.Users;
using Dnd.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
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
/// created with <c>EnsureCreated</c>) and a fake email sender. The initial admin is created through
/// <c>POST /api/v1/setup/admin</c> (see <see cref="CreateAdminClientAsync"/>) unless a subclass disables it. The SRD catalog import is off by
/// default to keep unrelated tests fast; <see cref="CatalogApiFactory"/> turns it on.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@example.com";
    public const string AdminPassword = "admin-password-1234";

    /// <summary>Default <c>App:PublicUrl</c> of the test host: the origin of every link in the emails.</summary>
    public const string DefaultPublicUrl = "https://dnd.example.com";

    private readonly SqliteConnection _connection;
    private readonly string _filesRoot = Path.Combine(Path.GetTempPath(), $"dnd-tests-{Guid.NewGuid():N}");
    private readonly SemaphoreSlim _adminLock = new(1, 1);
    private bool _adminCreated;

    public ApiFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        db.Database.EnsureCreated();
    }

    public FakeEmailSender Emails { get; } = new();

    public LogCapture Logs { get; } = new();

    /// <summary>Whether <see cref="CreateAdminClientAsync"/> creates the initial admin (<see cref="AdminEmail"/>) on first use.</summary>
    protected virtual bool CreateInitialAdmin => true;

    protected virtual bool SeedCatalog => false;

    /// <summary>Whether the background <c>ReminderDispatcher</c> runs. Off by default: tests call the processor directly.</summary>
    protected virtual bool RunReminderDispatcher => false;

    /// <summary>Maximum upload size configured for the host (<c>FileStorage:MaxUploadMegabytes</c>).</summary>
    protected virtual int MaxUploadMegabytes => 200;

    /// <summary><c>App:PublicUrl</c> of the host (mandatory in the API).</summary>
    protected virtual string PublicUrl => DefaultPublicUrl;

    /// <summary>Address the test server reports as the client's, i.e. the proxy the request came from.</summary>
    protected virtual string RemoteIpAddress => "127.0.0.1";

    /// <summary>Temporary directory used as <c>FileStorage:RootPath</c>; removed when the factory is disposed.</summary>
    public string FilesRoot => _filesRoot;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:AutoMigrate", "false");
        builder.UseSetting("App:PublicUrl", PublicUrl);
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

            // The test server has no client address: give it one so the forwarded headers logic can judge it.
            services.AddSingleton<IStartupFilter>(new RemoteIpStartupFilter(IPAddress.Parse(RemoteIpAddress)));
        });
    }

    /// <summary>Runs <paramref name="action"/> against the test database (for arranging edge cases).</summary>
    public async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>
    /// Creates the initial admin through <c>POST /api/v1/setup/admin</c> the first time it is needed (setup only
    /// works while there are no users, so every helper that adds users must call this first). Does nothing when
    /// <see cref="CreateInitialAdmin"/> is false.
    /// </summary>
    public async Task EnsureInitialAdminAsync()
    {
        await _adminLock.WaitAsync();
        try
        {
            if (CreateInitialAdmin && !_adminCreated)
            {
                var response = await CreateClient().PostAsJsonAsync(
                    "/api/v1/setup/admin",
                    new { email = AdminEmail, displayName = "Administrador", password = AdminPassword });
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
                _adminCreated = true;
            }
        }
        finally
        {
            _adminLock.Release();
        }
    }

    /// <summary>Client authenticated as the initial admin (created through the setup endpoint on first use).</summary>
    public async Task<HttpClient> CreateAdminClientAsync()
    {
        await EnsureInitialAdminAsync();
        var client = CreateClient();
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

/// <summary>Factory that never creates the initial admin: the instance stays in its first-boot state.</summary>
public sealed class ApiFactoryWithoutInitialAdmin : ApiFactory
{
    protected override bool CreateInitialAdmin => false;
}

internal sealed class RemoteIpStartupFilter(IPAddress address) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, nextMiddleware) =>
        {
            context.Connection.RemoteIpAddress = address;
            return nextMiddleware(context);
        });
        next(app);
    };
}

public sealed record TestUser(UserDto User, string Password)
{
    public string Email => User.Email;
}
