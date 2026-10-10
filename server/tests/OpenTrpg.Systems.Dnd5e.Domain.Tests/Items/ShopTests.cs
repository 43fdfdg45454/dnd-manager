using OpenTrpg.Core.Domain.Common;
using OpenTrpg.Core.Domain.Items;
using static OpenTrpg.Systems.Dnd5e.Domain.Tests.Characters.TestCatalog;
using static OpenTrpg.Systems.Dnd5e.Domain.Tests.Items.TestItems;
using OpenTrpg.Systems.Dnd5e.Domain.Tests.Items;

namespace OpenTrpg.Systems.Dnd5e.Domain.Tests.Items;

public class ShopTests
{
    [Fact]
    public void New_shops_are_closed_with_the_default_buyback()
    {
        var shop = Shop.Create(Guid.NewGuid(), "  Herrería  ", null, null, Now);

        Assert.Equal(("Herrería", false, Shop.DefaultBuybackPercent), (shop.Name, shop.IsOpen, shop.BuybackPercent));
        Assert.Throws<DomainException>(() => Shop.Create(Guid.NewGuid(), "x", null, 101, Now));
    }

    [Fact]
    public void Selling_to_a_character_needs_an_open_shop_and_stock()
    {
        var shop = Shop.Create(Guid.NewGuid(), "Tienda", null, null, Now);
        var item = shop.AddItem(ChainMail.Id, ItemOverrides.None(), 7500, 1, Now);

        Assert.Equal(DomainErrorKind.Conflict, Assert.Throws<DomainException>(() => shop.SellToCharacter(item.Id, 1, Now)).Kind);
        shop.Update(null, null, true, null, Now);
        var version = item.Version;

        Assert.Equal(7500, shop.SellToCharacter(item.Id, 1, Now));
        Assert.Equal(0, item.Stock);
        Assert.Equal(version + 1, item.Version);
        Assert.Equal(DomainErrorKind.RuleViolation, Assert.Throws<DomainException>(() => shop.SellToCharacter(item.Id, 1, Now)).Kind);
    }

    [Fact]
    public void Unlimited_stock_never_runs_out()
    {
        var shop = Shop.Create(Guid.NewGuid(), "Tienda", null, null, Now);
        shop.Update(null, null, true, null, Now);
        var item = shop.AddItem(Arrow.Id, ItemOverrides.None(), 5, null, Now);

        Assert.Equal(500, shop.SellToCharacter(item.Id, 100, Now));
        Assert.Null(item.Stock);
    }

    [Fact]
    public void Buying_from_a_character_pays_the_buyback_percent_and_restocks()
    {
        var shop = Shop.Create(Guid.NewGuid(), "Tienda", null, 40, Now);
        shop.Update(null, null, true, null, Now);
        var tracked = shop.AddItem(Longsword.Id, ItemOverrides.None(), 1500, 2, Now);

        var matching = shop.FindMatching(Longsword.Id);
        var paid = shop.BuyFromCharacter(matching, matching!.Price, 2, Now);

        Assert.Same(tracked, matching);
        Assert.Equal(1200, paid);
        Assert.Equal(4, tracked.Stock);
        Assert.Null(shop.FindMatching(null));
    }

    [Fact]
    public void Custom_shop_items_need_a_name()
    {
        var shop = Shop.Create(Guid.NewGuid(), "Tienda", null, null, Now);

        Assert.Throws<DomainException>(() => shop.AddItem(null, ItemOverrides.None(), 10, null, Now));
        Assert.Equal("Poción casera", shop.AddItem(null, new ItemOverrides { Name = "Poción casera" }, 10, null, Now).Overrides.Name);
    }
}
