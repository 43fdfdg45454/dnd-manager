using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Domain.Tests.Characters;

/// <summary>Expanded spell lists of subclasses (phase 25, block 4).</summary>
public class ExpandedSpellListTests
{
    [Fact]
    public void The_stored_list_round_trips()
    {
        var json = ExpandedSpell.ToJson([new ExpandedSpell("faerie-fire", 1), new ExpandedSpell("blur", 2)]);
        var subclass = new SubclassDefinition { Index = "ejemplo", ClassIndex = "warlock", Name = "Ejemplo", ExpandedSpellListJson = json };

        Assert.Equal([new ExpandedSpell("faerie-fire", 1), new ExpandedSpell("blur", 2)], subclass.ExpandedSpellList);
        Assert.True(subclass.ExpandedSpellIndexes.SetEquals(["faerie-fire", "blur"]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("{\"index\":\"x\"}")]
    public void Missing_or_malformed_lists_are_empty(string? json) => Assert.Empty(ExpandedSpell.Parse(json));

    [Fact]
    public void Invalid_entries_are_skipped_and_an_empty_list_is_not_stored()
    {
        Assert.Equal(
            [new ExpandedSpell("sleep", 1)],
            ExpandedSpell.Parse("""[{"index":"sleep","level":1},{"index":"","level":1},{"index":"x","level":12},{"level":1}]"""));
        Assert.Null(ExpandedSpell.ToJson([]));
    }
}
