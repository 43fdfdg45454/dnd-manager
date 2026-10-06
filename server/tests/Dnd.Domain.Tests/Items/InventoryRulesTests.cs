using Dnd.Domain.Catalog;
using Dnd.Domain.Characters;
using Dnd.Domain.Common;
using Dnd.Domain.Items;
using static Dnd.Domain.Tests.Characters.TestCatalog;
using static Dnd.Domain.Tests.Items.TestItems;

namespace Dnd.Domain.Tests.Items;

public class InventoryRulesTests
{
    private readonly Dictionary<Guid, ItemTemplate> _templates = new[] { ChainMail, Leather, Shield, Longsword, Arrow, Rope, Healing }.ToDictionary(t => t.Id);

    [Fact]
    public void Adding_an_item_belongs_to_the_campaign_of_the_character()
    {
        var character = NewCharacter();

        var item = Add(character, Longsword);

        Assert.Equal((character.Id, character.CampaignId, Longsword.Id, 1), (item.CharacterId, item.CampaignId, item.TemplateId, item.Quantity));
        Assert.Single(character.Items);
    }

    [Fact]
    public void Stackable_items_of_the_same_template_accumulate()
    {
        var character = NewCharacter();

        var first = Add(character, Arrow, 20);
        var second = Add(character, Arrow, 10);

        Assert.Same(first, second);
        Assert.Equal(30, Assert.Single(character.Items).Quantity);
    }

    [Fact]
    public void Non_stackable_or_customized_items_get_their_own_entry()
    {
        var character = NewCharacter();

        Add(character, Longsword);
        Add(character, Longsword);
        Add(character, Arrow, 5);
        Add(character, Arrow, 5, new ItemOverrides { Name = "Flecha de plata" });

        Assert.Equal(4, character.Items.Count);
        Assert.Equal([0, 1, 2, 3], character.Items.Select(i => i.SortOrder));
    }

    [Fact]
    public void Item_without_template_needs_a_name()
    {
        var character = NewCharacter();

        Assert.Throws<DomainException>(() => character.AddItem(null, ItemOverrides.None(), 1, Effective(null), Now));
    }

    [Fact]
    public void Only_weapons_armor_and_shields_can_be_equipped()
    {
        var character = NewCharacter();
        var rope = Add(character, Rope);

        var error = Assert.Throws<DomainException>(() => Update(character, rope, new ItemUpdate { Equipped = true }));

        Assert.Equal(DomainErrorKind.RuleViolation, error.Kind);
        Assert.False(rope.Equipped);
    }

    [Fact]
    public void Equipping_another_armor_or_shield_unequips_the_previous_one()
    {
        var character = NewCharacter();
        var chain = Add(character, ChainMail);
        var leather = Add(character, Leather);
        var shield = Add(character, Shield);
        var otherShield = Add(character, Shield);
        var sword = Add(character, Longsword);

        Update(character, chain, new ItemUpdate { Equipped = true });
        Update(character, shield, new ItemUpdate { Equipped = true });
        Update(character, sword, new ItemUpdate { Equipped = true });
        Update(character, leather, new ItemUpdate { Equipped = true });
        Update(character, otherShield, new ItemUpdate { Equipped = true });

        Assert.Equal((false, true), (chain.Equipped, leather.Equipped));
        Assert.Equal((false, true), (shield.Equipped, otherShield.Equipped));
        Assert.True(sword.Equipped);
    }

    [Fact]
    public void At_most_three_items_can_be_attuned()
    {
        var character = NewCharacter();
        var rings = Enumerable.Range(1, 4).Select(i => Attuned($"Ring {i}")).ToList();
        foreach (var ring in rings)
        {
            _templates[ring.Id] = ring;
        }

        var items = rings.Select(r => Add(character, r)).ToList();
        foreach (var item in items.Take(3))
        {
            Update(character, item, new ItemUpdate { Attuned = true });
        }

        var error = Assert.Throws<DomainException>(() => Update(character, items[3], new ItemUpdate { Attuned = true }));

        Assert.Equal(DomainErrorKind.Conflict, error.Kind);
        Assert.Equal(ItemLimits.AttunementLimitCode, error.Code);
        Assert.Equal(3, character.AttunedCount);
        Update(character, items[0], new ItemUpdate { Attuned = false });
        Update(character, items[3], new ItemUpdate { Attuned = true });
        Assert.Equal(3, character.AttunedCount);
    }

    [Fact]
    public void Attuning_can_drop_another_attuned_item_in_the_same_operation()
    {
        var character = NewCharacter();
        var rings = Enumerable.Range(1, 4).Select(i => Attuned($"Ring {i}")).ToList();
        foreach (var ring in rings)
        {
            _templates[ring.Id] = ring;
        }

        var items = rings.Select(r => Add(character, r)).ToList();
        foreach (var item in items.Take(3))
        {
            Update(character, item, new ItemUpdate { Attuned = true });
        }

        Update(character, items[3], new ItemUpdate { Attuned = true, ReplaceAttunedItemId = items[1].Id });

        Assert.Equal(3, character.AttunedCount);
        Assert.False(items[1].Attuned);
        Assert.True(items[3].Attuned);
        Assert.Throws<DomainException>(() => Update(character, items[1], new ItemUpdate { Attuned = true, ReplaceAttunedItemId = items[1].Id }));
    }

    [Fact]
    public void Only_items_that_require_it_can_be_attuned()
    {
        var character = NewCharacter();
        var sword = Add(character, Longsword);

        Assert.Throws<DomainException>(() => Update(character, sword, new ItemUpdate { Attuned = true }));
    }

    [Fact]
    public void Using_a_consumable_subtracts_quantity_and_removes_it_at_zero()
    {
        var character = NewCharacter();
        var potion = Add(character, Healing, 2);

        Assert.Same(potion, character.UseItem(potion.Id, 1, Effective(Healing), Now));
        Assert.Equal(1, potion.Quantity);
        Assert.Null(character.UseItem(potion.Id, 1, Effective(Healing), Now));
        Assert.Empty(character.Items);
    }

    [Fact]
    public void Using_an_item_with_charges_spends_charges_and_keeps_it()
    {
        var character = NewCharacter();
        var wand = character.AddItem(null, new ItemOverrides { Name = "Varita" }, 1, Effective(null, new ItemOverrides { Name = "Varita" }), Now);
        Update(character, wand, new ItemUpdate { SetCharges = true, Charges = 3 });

        character.UseItem(wand.Id, 2, Effective(null), Now);

        Assert.Equal((1, 3), (wand.Charges, wand.ChargesMax));
        Assert.Throws<DomainException>(() => character.UseItem(wand.Id, 2, Effective(null), Now));
        Assert.Throws<DomainException>(() => Update(character, wand, new ItemUpdate { SetCharges = true, Charges = 4 }));
        Assert.Single(character.Items);
    }

    [Fact]
    public void Using_a_non_consumable_without_charges_is_rejected()
    {
        var character = NewCharacter();
        var sword = Add(character, Longsword);

        Assert.Throws<DomainException>(() => character.UseItem(sword.Id, 1, Effective(Longsword), Now));
    }

    [Fact]
    public void Removing_units_and_whole_entries()
    {
        var character = NewCharacter();
        var arrows = Add(character, Arrow, 20);

        character.RemoveItem(arrows.Id, 5, Now);
        Assert.Equal(15, arrows.Quantity);
        Assert.Throws<DomainException>(() => character.RemoveItem(arrows.Id, 16, Now));
        character.RemoveItem(arrows.Id, null, Now);
        Assert.Empty(character.Items);
        Assert.Equal(DomainErrorKind.NotFound, Assert.Throws<DomainException>(() => character.RemoveItem(arrows.Id, null, Now)).Kind);
    }

    [Fact]
    public void Money_never_goes_below_zero_and_changes_bump_the_version()
    {
        var character = NewCharacter();
        var version = character.Version;

        character.AdjustMoney(500, Now);
        character.AdjustMoney(-200, Now);

        Assert.Equal(300, character.CopperPieces);
        Assert.Throws<DomainException>(() => character.AdjustMoney(-301, Now));
        Assert.Equal(300, character.CopperPieces);
        Assert.Throws<DomainException>(() => character.AdjustMoney(Character.MaxCopperPieces, Now));
        Assert.Equal(version + 2, character.Version);
    }

    [Fact]
    public void Chain_mail_and_shield_give_armor_class_18()
    {
        var character = NewCharacter();
        character.ApplySheetEdit(new SheetEdit { BaseAbilities = Scores(dex: 14) }, Now);
        var chain = Add(character, ChainMail);
        var shield = Add(character, Shield);
        Update(character, chain, new ItemUpdate { Equipped = true });
        Update(character, shield, new ItemUpdate { Equipped = true });

        var gear = EquippedGear.FromEquipped(character.Items.Where(i => i.Equipped).Select(Resolve));

        Assert.Equal(18, Sheet(character, gear: gear).ArmorClass);
    }

    private EffectiveItem Resolve(CharacterItem item) =>
        EffectiveItem.Resolve(item.TemplateId is { } id ? _templates[id] : null, item.Overrides);

    private static CharacterItem Add(Character character, ItemTemplate template, int quantity = 1, ItemOverrides? overrides = null)
    {
        var o = overrides ?? ItemOverrides.None();
        return character.AddItem(template.Id, o, quantity, Effective(template, o), Now);
    }

    private CharacterItem Update(Character character, CharacterItem item, ItemUpdate update) =>
        character.UpdateItem(item.Id, update, Resolve, Now);
}
