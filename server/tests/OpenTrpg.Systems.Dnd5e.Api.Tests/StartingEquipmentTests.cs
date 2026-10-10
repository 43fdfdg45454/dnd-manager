using System.Net;
using System.Net.Http.Json;
using OpenTrpg.Core.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;
using OpenTrpg.Systems.Dnd5e.Application.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Core.Api.Tests;
using OpenTrpg.Core.Api.Tests.Items;

namespace OpenTrpg.Systems.Dnd5e.Api.Tests;

/// <summary>Structured starting equipment of classes and backgrounds and the equipment category picker (phase 17).</summary>
[Collection(CatalogCollection.Name)]
public class StartingEquipmentTests(CatalogApiFactory factory, ITestOutputHelper output)
{
    private const string Base = "/api/v1/systems/dnd5e/catalog";

    private async Task<T> GetAsync<T>(string url)
    {
        var client = (await factory.CreateSignedInUserAsync()).Client;
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    [Fact]
    public async Task Fighter_has_no_fixed_items_and_four_choices_starting_with_chain_mail()
    {
        var equipment = (await GetAsync<ClassDetailDto>($"{Base}/classes/fighter")).StartingEquipment!;

        Assert.Empty(equipment.Fixed);
        Assert.Equal(4, equipment.Choices.Count);
        Assert.Equal(new StartingGoldDto("5d4", 10), equipment.Gold);
        Assert.Null(equipment.FixedGoldCp);

        var armor = equipment.Choices[0];
        Assert.Equal(1, armor.Choose);
        Assert.Equal("(a) chain mail or (b) leather armor, longbow, and 20 arrows", armor.Description);
        var chainMail = Assert.Single(armor.Options[0].Items);
        Assert.Equal(("chain-mail", "Chain Mail", 1), (chainMail.Item, chainMail.Name, chainMail.Quantity));
        Assert.NotNull(chainMail.TemplateId);
        Assert.Equal("Chain Mail", armor.Options[0].Label);
        Assert.Equal(["leather-armor", "longbow", "arrow"], armor.Options[1].Items.Select(i => i.Item));
        Assert.Equal(20, armor.Options[1].Items[2].Quantity);
        Assert.Equal("Leather Armor, Longbow, 20 Arrows", armor.Options[1].Label);

        // (a) a martial weapon and a shield or (b) two martial weapons.
        var weapons = equipment.Choices[1];
        var weaponAndShield = weapons.Options[0];
        Assert.Equal("shield", Assert.Single(weaponAndShield.Items).Item);
        Assert.Equal(new StartingCategoryPickDto("martial-weapons", "Martial Weapons", 1), Assert.Single(weaponAndShield.Categories));
        var twoWeapons = weapons.Options[1];
        Assert.Empty(twoWeapons.Items);
        Assert.Equal(2, Assert.Single(twoWeapons.Categories).Choose);

        var packs = equipment.Choices[3];
        var dungeoneers = Assert.Single(packs.Options[0].Items);
        Assert.Equal("dungeoneers-pack", dungeoneers.Item);
        Assert.Contains(dungeoneers.Contents!, c => c.Item == "crowbar" && c.TemplateId is not null);
    }

    [Fact]
    public async Task Rogue_has_three_fixed_items()
    {
        var equipment = (await GetAsync<ClassDetailDto>($"{Base}/classes/rogue")).StartingEquipment!;

        Assert.Equal(
            [("leather-armor", 1), ("dagger", 2), ("thieves-tools", 1)],
            equipment.Fixed.Select(i => (i.Item, i.Quantity)));
        Assert.All(equipment.Fixed, i => Assert.NotNull(i.TemplateId));
        Assert.Equal(3, equipment.Choices.Count);
        Assert.Equal(new StartingGoldDto("4d4", 10), equipment.Gold);
    }

    [Fact]
    public async Task Bard_and_cleric_offer_category_picks_and_proficiency_notes()
    {
        var bard = (await GetAsync<ClassDetailDto>($"{Base}/classes/bard")).StartingEquipment!;
        var instrument = bard.Choices[2];
        Assert.Equal("lute", Assert.Single(instrument.Options[0].Items).Item);
        Assert.Equal("musical-instruments", Assert.Single(instrument.Options[1].Categories).Category);
        Assert.Equal("Any other musical instrument", instrument.Options[1].Label);

        var cleric = (await GetAsync<ClassDetailDto>($"{Base}/classes/cleric")).StartingEquipment!;
        Assert.Equal(5, cleric.Choices.Count);
        Assert.Equal("Warhammer (if proficient)", cleric.Choices[0].Options[1].Label);
        var holySymbol = Assert.Single(cleric.Choices[4].Options);
        Assert.Equal(("holy-symbols", 1), (Assert.Single(holySymbol.Categories).Category, cleric.Choices[4].Choose));
    }

    [Theory]
    [InlineData("fighter", "5d4", 10)]
    [InlineData("monk", "5d4", 1)]
    [InlineData("barbarian", "2d4", 10)]
    [InlineData("sorcerer", "3d4", 10)]
    public async Task Classes_carry_their_alternative_starting_wealth(string index, string dice, int multiplier)
    {
        var equipment = (await GetAsync<ClassDetailDto>($"{Base}/classes/{index}")).StartingEquipment!;

        Assert.Equal(new StartingGoldDto(dice, multiplier), equipment.Gold);
    }

    [Fact]
    public async Task Acolyte_has_fixed_items_a_holy_symbol_choice_and_15_gold()
    {
        var acolyte = Assert.Single(await GetAsync<List<BackgroundDto>>($"{Base}/backgrounds"), b => b.Index == "acolyte");
        var equipment = acolyte.StartingEquipment!;

        Assert.Equal(["clothes-common", "pouch"], equipment.Fixed.Select(i => i.Item));
        Assert.All(equipment.Fixed, i => Assert.NotNull(i.TemplateId));
        var choice = Assert.Single(equipment.Choices);
        Assert.Equal(new StartingCategoryPickDto("holy-symbols", "Holy Symbols", 1), Assert.Single(Assert.Single(choice.Options).Categories));
        Assert.Equal(1500, equipment.FixedGoldCp);
        Assert.Null(equipment.Gold);
    }

    [Fact]
    public async Task Martial_weapons_category_lists_the_martial_weapons()
    {
        var category = await GetAsync<EquipmentCategoryDto>($"{Base}/equipment-categories/martial-weapons");

        Assert.Equal(("martial-weapons", "Martial Weapons"), (category.Index, category.Name));
        Assert.Equal(23, category.Items.Count);
        Assert.Contains(category.Items, i => i.Index == "longsword" && i.Name == "Longsword");
        Assert.DoesNotContain(category.Items, i => i.Index == "dagger");
        Assert.All(category.Items, i => Assert.NotEqual(Guid.Empty, i.TemplateId));
        Assert.Equal(category.Items.Select(i => i.Name).Order(StringComparer.CurrentCultureIgnoreCase), category.Items.Select(i => i.Name));
    }

    [Fact]
    public async Task Unknown_equipment_category_is_404_and_requires_authentication()
    {
        var client = (await factory.CreateSignedInUserAsync()).Client;
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Base}/equipment-categories/no-existe")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync($"{Base}/equipment-categories/martial-weapons")).StatusCode);
    }

    [Fact]
    public async Task Every_srd_starting_item_and_category_resolves()
    {
        var unresolved = new List<string>();
        var categories = new HashSet<string>(StringComparer.Ordinal);
        void Check(StartingEquipmentDto equipment, string owner)
        {
            var items = equipment.Fixed.Concat(equipment.Choices.SelectMany(c => c.Options).SelectMany(o => o.Items));
            foreach (var item in items.SelectMany(i => (i.Contents ?? []).Prepend(i)).Where(i => i.TemplateId is null))
            {
                unresolved.Add($"{owner}: {item.Item}");
            }

            foreach (var pick in equipment.Choices.SelectMany(c => c.Options).SelectMany(o => o.Categories))
            {
                categories.Add(pick.Category);
            }
        }

        foreach (var summary in await GetAsync<List<ClassSummaryDto>>($"{Base}/classes"))
        {
            Check((await GetAsync<ClassDetailDto>($"{Base}/classes/{summary.Index}")).StartingEquipment!, summary.Index);
        }

        foreach (var background in await GetAsync<List<BackgroundDto>>($"{Base}/backgrounds"))
        {
            Check(background.StartingEquipment!, background.Index);
        }

        output.WriteLine($"Unresolved starting items ({unresolved.Count}): {string.Join(", ", unresolved)}");
        Assert.Empty(unresolved);

        foreach (var category in categories)
        {
            Assert.NotEmpty((await GetAsync<EquipmentCategoryDto>($"{Base}/equipment-categories/{category}")).Items);
        }

        await factory.WithDbAsync(async db =>
        {
            Assert.Equal(39, await db.Set<EquipmentCategory>().CountAsync(x => x.Source == Dnd5eCatalogSources.Srd));
            Assert.Contains("\"equipmentCategories\":39", (await db.ContentPacks.SingleAsync()).CountsJson);
        });
    }

    [Fact]
    public void Stored_json_round_trips_and_malformed_json_is_ignored()
    {
        var equipment = new StartingEquipment(
            [new StartingItem("explorers-pack", 1, "Explorer's Pack", [new StartingItem("backpack", 1)])],
            [new StartingEquipmentChoice("(a) x or (b) y", 1, [new StartingEquipmentOption("Any martial weapon", [], [new StartingCategoryPick("martial-weapons", 1)])])],
            new StartingGold("5d4", 10),
            null);

        var parsed = StartingEquipment.Parse(equipment.ToJson())!;

        Assert.Equal("backpack", parsed.Fixed[0].Contents![0].Item);
        Assert.Equal("martial-weapons", parsed.Choices[0].Options[0].Categories[0].Category);
        Assert.Equal(new StartingGold("5d4", 10), parsed.Gold);
        Assert.DoesNotContain("fixedGoldCp", equipment.ToJson(), StringComparison.Ordinal);
        Assert.Null(StartingEquipment.Parse("{not json"));
        Assert.Null(StartingEquipment.Parse(null));
        Assert.Empty(StartingEquipment.Parse("{}")!.Choices);
    }
}
