using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Dnd.Api.Tests;

/// <summary>
/// Boots the API without touching the database. Tests that need PostgreSQL will use a
/// Testcontainers-backed factory introduced in the auth phase.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:AutoMigrate", "false");
    }
}
