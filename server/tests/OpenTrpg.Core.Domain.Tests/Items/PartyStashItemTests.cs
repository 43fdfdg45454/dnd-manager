using OpenTrpg.Core.Domain.Common;
using OpenTrpg.Core.Domain.Items;
using static OpenTrpg.Core.Domain.Tests.Characters.TestCatalog;
using static OpenTrpg.Core.Domain.Tests.Items.TestItems;

namespace OpenTrpg.Core.Domain.Tests.Items;

public class PartyStashItemTests
{
    private static readonly Guid CampaignId = Guid.NewGuid();
    private static readonly Guid Dm = Guid.NewGuid();

    [Fact]
    public void Entries_need_a_template_or_a_name_and_a_valid_quantity()
    {
        Assert.Throws<DomainException>(() => PartyStashItem.Create(CampaignId, null, ItemOverrides.None(), 1, null, Dm, Now));
        Assert.Throws<DomainException>(() => PartyStashItem.Create(CampaignId, Arrow.Id, ItemOverrides.None(), 0, null, Dm, Now));

        var custom = PartyStashItem.Create(CampaignId, null, new ItemOverrides { Name = "  Gema  " }, 2, "  del dragón  ", Dm, Now);

        Assert.Equal(("Gema", 2, "del dragón", Dm), (custom.Overrides.Name, custom.Quantity, custom.Notes, custom.AddedByUserId));
    }

    [Fact]
    public void Only_plain_catalog_entries_without_charges_stack()
    {
        var arrows = PartyStashItem.Create(CampaignId, Arrow.Id, ItemOverrides.None(), 5, null, Dm, Now);
        var wand = PartyStashItem.Create(CampaignId, Arrow.Id, ItemOverrides.None(), 1, null, Dm, Now, charges: 3, chargesMax: 7);

        Assert.True(arrows.CanStackWith(Arrow.Id, ItemOverrides.None(), hasCharges: false));
        Assert.False(arrows.CanStackWith(Arrow.Id, ItemOverrides.None(), hasCharges: true));
        Assert.False(arrows.CanStackWith(Arrow.Id, new ItemOverrides { Name = "Flecha +1" }, hasCharges: false));
        Assert.False(arrows.CanStackWith(Rope.Id, ItemOverrides.None(), hasCharges: false));
        Assert.False(wand.CanStackWith(Arrow.Id, ItemOverrides.None(), hasCharges: false));
        Assert.Equal((3, 7), (wand.Charges, wand.ChargesMax));
    }

    [Fact]
    public void Quantity_changes_bump_the_version_and_cannot_go_below_zero()
    {
        var arrows = PartyStashItem.Create(CampaignId, Arrow.Id, ItemOverrides.None(), 5, null, Dm, Now);

        arrows.RemoveQuantity(2);
        arrows.AddQuantity(4);

        Assert.Equal((7, 2), (arrows.Quantity, arrows.Version));
        Assert.Throws<DomainException>(() => arrows.RemoveQuantity(8));
    }

    [Fact]
    public void Update_changes_quantity_and_notes_independently()
    {
        var arrows = PartyStashItem.Create(CampaignId, Arrow.Id, ItemOverrides.None(), 5, "Nota", Dm, Now);

        arrows.Update(3, setNotes: false, notes: null);
        Assert.Equal((3, "Nota"), (arrows.Quantity, arrows.Notes));

        arrows.Update(null, setNotes: true, notes: null);
        Assert.Equal((3, (string?)null), (arrows.Quantity, arrows.Notes));
    }
}
