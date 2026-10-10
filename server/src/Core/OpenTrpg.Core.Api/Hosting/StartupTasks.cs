using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Infrastructure.Catalog;
using OpenTrpg.Core.Infrastructure.Files;
using OpenTrpg.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace OpenTrpg.Core.Api.Hosting;

public static class StartupTasks
{
    /// <summary>
    /// Applies migrations (if enabled), imports the SRD catalog (if enabled and not imported yet),
    /// and registers the bundled SRD PDF as a system library document (if the operator provided it).
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

        await services.GetRequiredService<SystemDocumentSeeder>().SeedAsync(cancellationToken);
    }
}
