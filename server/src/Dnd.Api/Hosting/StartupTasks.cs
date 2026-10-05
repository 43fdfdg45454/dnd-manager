using Dnd.Application.Abstractions;
using Dnd.Application.Users;
using Dnd.Infrastructure.Catalog;
using Dnd.Infrastructure.Options;
using Dnd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dnd.Api.Hosting;

public static class StartupTasks
{
    /// <summary>
    /// Applies migrations (if enabled), imports the SRD catalog (if enabled and not imported yet) and
    /// then bootstraps the initial admin (if enabled).
    /// </summary>
    public static async Task InitializeAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;

        if (app.Configuration.GetValue<bool>("Database:AutoMigrate"))
        {
            await services.GetRequiredService<AppDbContext>().Database.MigrateAsync(cancellationToken);
        }

        if (services.GetRequiredService<IOptions<CatalogOptions>>().Value.SeedOnStartup)
        {
            await services.GetRequiredService<ISrdSeeder>().SeedAsync(cancellationToken);
        }

        var appOptions = services.GetRequiredService<IOptions<AppOptions>>().Value;
        if (appOptions.SeedInitialAdmin)
        {
            await services.GetRequiredService<InitialAdminSeeder>().SeedAsync(appOptions.InitialAdminEmail, cancellationToken);
        }
    }
}
