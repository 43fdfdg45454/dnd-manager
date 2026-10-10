using OpenTrpg.Core.Domain.Characters;
using static OpenTrpg.Systems.Dnd5e.Domain.Tests.Characters.TestCatalog;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Domain.Tests.Characters;

public class ClassResourceRulesTests
{
    private static readonly IReadOnlyDictionary<string, int> NoMods = new Dictionary<string, int>();

    [Fact]
    public void Barbarian_3_has_3_rages_per_long_rest()
    {
        var rage = Assert.Single(ClassResourceRules.For("barbarian", 3, NoMods));

        Assert.Equal(new ResourceTemplate(ClassResourceRules.Rage, "Rage", 3, ResourceRecharge.LongRest), rage);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(6, 4)]
    [InlineData(12, 5)]
    [InlineData(17, 6)]
    [InlineData(20, ClassResourceRules.Unlimited)]
    public void Rage_count_follows_the_srd_table(int level, int expected) =>
        Assert.Equal(expected, ClassResourceRules.For("barbarian", level, NoMods).Single().Max);

    [Theory]
    [InlineData(4, 3, 3, ResourceRecharge.LongRest)]
    [InlineData(5, 3, 3, ResourceRecharge.ShortRest)]
    [InlineData(5, -1, 1, ResourceRecharge.ShortRest)]
    public void Bardic_inspiration_uses_charisma_and_recharges_on_short_rest_from_level_5(int level, int cha, int max, ResourceRecharge recharge)
    {
        var mods = new Dictionary<string, int> { ["cha"] = cha };

        var inspiration = Assert.Single(ClassResourceRules.For("bard", level, mods));

        Assert.Equal((ClassResourceRules.BardicInspiration, max, recharge), (inspiration.Key, inspiration.Max, inspiration.Recharge));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(6, 2)]
    [InlineData(18, 3)]
    public void Cleric_channel_divinity_grows_with_level(int level, int expected)
    {
        var resources = ClassResourceRules.For("cleric", level, NoMods);

        Assert.Equal(expected, resources.SingleOrDefault(r => r.Key == ClassResourceRules.ChannelDivinity)?.Max ?? 0);
    }

    [Fact]
    public void Fighter_resources_by_level()
    {
        Assert.Equal([ClassResourceRules.SecondWind], ClassResourceRules.For("fighter", 1, NoMods).Select(r => r.Key));

        var at13 = ClassResourceRules.For("fighter", 13, NoMods).ToDictionary(r => r.Key, r => r.Max);
        Assert.Equal(new Dictionary<string, int> { ["second-wind"] = 1, ["action-surge"] = 1, ["indomitable"] = 2 }, at13);

        var at17 = ClassResourceRules.For("fighter", 17, NoMods).ToDictionary(r => r.Key, r => r.Max);
        Assert.Equal(new Dictionary<string, int> { ["second-wind"] = 1, ["action-surge"] = 2, ["indomitable"] = 3 }, at17);
        Assert.Equal(ResourceRecharge.LongRest, ClassResourceRules.For("fighter", 9, NoMods).Single(r => r.Key == "indomitable").Recharge);
    }

    [Fact]
    public void Other_classes_follow_the_contract_table()
    {
        Assert.Empty(ClassResourceRules.For("monk", 1, NoMods));
        Assert.Equal(new ResourceTemplate("ki", "Ki", 5, ResourceRecharge.ShortRest), ClassResourceRules.For("monk", 5, NoMods).Single());
        Assert.Equal(new ResourceTemplate("wild-shape", "Wild Shape", 2, ResourceRecharge.ShortRest), ClassResourceRules.For("druid", 2, NoMods).Single());
        Assert.Equal(ClassResourceRules.Unlimited, ClassResourceRules.For("druid", 20, NoMods).Single().Max);
        Assert.Equal(new ResourceTemplate("sorcery-points", "Sorcery Points", 4, ResourceRecharge.LongRest), ClassResourceRules.For("sorcerer", 4, NoMods).Single());
        Assert.Equal(new ResourceTemplate("arcane-recovery", "Arcane Recovery", 1, ResourceRecharge.LongRest), ClassResourceRules.For("wizard", 1, NoMods).Single());

        var paladin = ClassResourceRules.For("paladin", 3, NoMods);
        Assert.Equal([("lay-on-hands", 15, ResourceRecharge.LongRest), ("channel-divinity", 1, ResourceRecharge.ShortRest)], paladin.Select(r => (r.Key, r.Max, r.Recharge)));
        Assert.Single(ClassResourceRules.For("paladin", 2, NoMods));

        Assert.Empty(ClassResourceRules.For("warlock", 10, NoMods));
        Assert.Empty(ClassResourceRules.For("rogue", 10, NoMods));
        Assert.Empty(ClassResourceRules.For("ranger", 10, NoMods));
    }

    [Fact]
    public void Channel_divinity_from_two_classes_does_not_stack()
    {
        var character = NewCharacter(classes: [new ClassEntry("cleric", null, 6), new ClassEntry("paladin", null, 3)]);

        var templates = ClassResourceRules.ForClasses(character.Classes, NoMods);

        Assert.Equal(2, templates.Single(t => t.Key == ClassResourceRules.ChannelDivinity).Max);
        Assert.Equal(2, templates.Count);
    }

    [Fact]
    public void Barbarian_3_character_gets_rage_3_after_sync()
    {
        var character = NewCharacter(classes: [new ClassEntry("barbarian", null, 3)]);

        character.SyncAutoResources(ClassResourceRules.ForClasses(character.Classes, Sheet(character).AbilityModifiers));

        var rage = Assert.Single(character.Resources);
        Assert.Equal(("rage", 3, 0, true), (rage.Key, rage.Max, rage.Used, rage.IsAuto));
    }

    [Fact]
    public void SyncAutoResources_keeps_used_by_key_caps_it_and_removes_what_no_longer_applies()
    {
        var character = NewCharacter(classes: [new ClassEntry("fighter", null, 2)]);
        character.SyncAutoResources(ClassResourceRules.ForClasses(character.Classes, NoMods));
        var manual = character.AddManualResource("Varita", 7, ResourceRecharge.Dawn, Now);
        var secondWind = character.Resources.Single(r => r.Key == "second-wind");
        var actionSurge = character.Resources.Single(r => r.Key == "action-surge");
        character.SpendResource(secondWind.Id, 1, Now);
        character.SpendResource(actionSurge.Id, 1, Now);
        character.SpendResource(manual.Id, 3, Now);

        // Level 17 fighter: action surge goes to 2 and indomitable appears; spent uses are kept.
        character.ReplaceClasses([new ClassEntry("fighter", null, 17)], Now);
        character.SyncAutoResources(ClassResourceRules.ForClasses(character.Classes, NoMods));

        Assert.Same(secondWind, character.Resources.Single(r => r.Key == "second-wind"));
        Assert.Equal(1, secondWind.Used);
        Assert.Equal((2, 1), (actionSurge.Max, actionSurge.Used));
        Assert.Equal(0, character.Resources.Single(r => r.Key == "indomitable").Used);

        // Back to wizard: fighter resources vanish, the manual one stays untouched.
        character.ReplaceClasses([new ClassEntry("wizard", null, 1)], Now);
        character.SyncAutoResources(ClassResourceRules.ForClasses(character.Classes, NoMods));

        Assert.Equal([null, "arcane-recovery"], character.Resources.Select(r => r.Key).Order());
        Assert.Equal(3, manual.Used);
    }

    [Fact]
    public void SyncAutoResources_caps_used_when_the_maximum_drops()
    {
        var character = NewCharacter(classes: [new ClassEntry("barbarian", null, 6)]);
        character.SyncAutoResources(ClassResourceRules.ForClasses(character.Classes, NoMods));
        var rage = character.Resources.Single();
        character.SpendResource(rage.Id, 4, Now);

        character.ReplaceClasses([new ClassEntry("barbarian", null, 1)], Now);
        character.SyncAutoResources(ClassResourceRules.ForClasses(character.Classes, NoMods));

        Assert.Equal((2, 2), (rage.Max, rage.Used));
    }
}
