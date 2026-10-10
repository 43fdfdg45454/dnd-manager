using OpenTrpg.Core.Application.Systems;
using OpenTrpg.Core.Infrastructure.Catalog;
using OpenTrpg.Core.Infrastructure.Files;
using OpenTrpg.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace OpenTrpg.Core.Api.Hosting;

public static class StartupTasks
{
    /// <summary>
    /// Applies migrations (if enabled), loads the base pack of every game system (if enabled and not loaded yet; the
    /// SRD in D&amp;D 5e) and registers the documents of the systems in the library (if the operator provided them).
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
            foreach (var system in services.GetRequiredService<IGameSystemRegistry>().All)
            {
                await system.Catalog.LoadBasePackAsync(cancellationToken);
            }
        }

        await services.GetRequiredService<SystemDocumentSeeder>().SeedAsync(cancellationToken);
    }
}
