using System.Net;
using Dnd.Domain.Items;
using Dnd.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Dnd.Api.Tests.Items;

/// <summary>
/// Simulates another purchase of the same stock committing between the moment a buy request loads
/// the shop item and the moment it saves: when armed, the interceptor bumps the stored version of
/// the shop items right before the next save that modifies one.
/// </summary>
public sealed class StaleShopItemInterceptor : SaveChangesInterceptor
{
    private int _armed;

    public void Arm() => Interlocked.Exchange(ref _armed, 1);

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context is not null
            && context.ChangeTracker.Entries<ShopItem>().Any(e => e.State == EntityState.Modified)
            && Interlocked.Exchange(ref _armed, 0) == 1)
        {
            await context.Database.ExecuteSqlRawAsync("UPDATE \"ShopItems\" SET \"Version\" = \"Version\" + 1", cancellationToken);
        }

        return result;
    }
}

/// <summary>API factory (no SRD catalog) whose database context carries a <see cref="StaleShopItemInterceptor"/>.</summary>
public sealed class ConcurrencyApiFactory : ApiFactory
{
    public StaleShopItemInterceptor Interceptor { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services => services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(Interceptor)));
    }
}

public class ShopConcurrencyTests(ConcurrencyApiFactory factory) : IClassFixture<ConcurrencyApiFactory>
{
    [Fact]
    public async Task Purchase_racing_another_one_for_the_last_unit_returns_409()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await s.Player.CreateCharacterAsync(s.CampaignId);
        await s.Dm.GiveMoneyAsync(character.Id, 1000);
        var shop = await s.Dm.CreateShopAsync(s.CampaignId);
        var last = await s.Dm.AddShopItemAsync(shop.Id, new { overrides = new { name = "Última poción", category = "Consumable" }, priceCp = 50, stock = 1 });

        factory.Interceptor.Arm();
        var response = await s.Player.BuyAsync(shop.Id, character.Id, last.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var inventory = await s.Player.GetInventoryAsync(character.Id);
        Assert.Equal(1000, inventory.CopperPieces);
        Assert.Empty(inventory.Items);
        Assert.Equal(HttpStatusCode.OK, (await s.Player.BuyAsync(shop.Id, character.Id, last.Id)).StatusCode);
    }
}
