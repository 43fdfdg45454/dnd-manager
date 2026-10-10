using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Common;
using static OpenTrpg.Core.Domain.Tests.Characters.TestCatalog;

namespace OpenTrpg.Core.Domain.Tests.Characters;

/// <summary>Phase 29, block 3: height and weight (free data) and the random tables of races and subraces.</summary>
public class HeightWeightTests
{
    [Fact]
    public void Height_is_base_plus_the_roll_and_weight_is_base_plus_height_roll_times_weight_roll()
    {
        var table = new HeightWeightTable(50, "2d8", 100, "2d4");

        Assert.Equal(59, table.HeightFor(9));
        Assert.Equal(100 + (9 * 5), table.WeightFor(heightRoll: 9, weightRoll: 5));

        // A constant modifier ("1") multiplies by itself.
        var constant = new HeightWeightTable(30, "2d4", 30, "1");
        Assert.Equal(30 + 6, constant.WeightFor(heightRoll: 6, weightRoll: 1));
    }

    [Theory]
    [InlineData("2d10", true)]
    [InlineData("1d4", true)]
    [InlineData("2D6", true)]
    [InlineData(" 3d8 ", true)]
    [InlineData("1", true)]
    [InlineData("100", true)]
    [InlineData("0", false)]
    [InlineData("101", false)]
    [InlineData("0d6", false)]
    [InlineData("11d6", false)]
    [InlineData("2d1", false)]
    [InlineData("2d", false)]
    [InlineData("d6", false)]
    [InlineData("2d6+1", false)]
    [InlineData("x2", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Modifiers_are_dice_expressions_or_whole_numbers(string? modifier, bool valid) =>
        Assert.Equal(valid, HeightWeightTable.IsValidModifier(modifier));

    [Fact]
    public void Validate_checks_the_bases_and_the_modifiers()
    {
        Assert.Null(new HeightWeightTable(50, "2d8", 100, "2d4").Validate());
        Assert.Contains("altura base", new HeightWeightTable(0, "2d8", 100, "2d4").Validate(), StringComparison.Ordinal);
        Assert.Contains("peso base", new HeightWeightTable(50, "2d8", 1001, "2d4").Validate(), StringComparison.Ordinal);
        Assert.Contains("modificador de altura", new HeightWeightTable(50, "2x8", 100, "2d4").Validate(), StringComparison.Ordinal);
        Assert.Contains("modificador de peso", new HeightWeightTable(50, "2d8", 100, "").Validate(), StringComparison.Ordinal);
    }

    [Fact]
    public void Tables_round_trip_through_json_and_parsing_is_tolerant()
    {
        var table = new HeightWeightTable(50, "2D8 ", 100, "1").Normalize();
        Assert.Equal(new HeightWeightTable(50, "2d8", 100, "1"), table);
        Assert.Equal(table, HeightWeightTable.Parse(table.ToJson()));
        Assert.Equal(
            new HeightWeightTable(44, "2d4", 115, "2d6"),
            HeightWeightTable.Parse("""{"baseHeightInches":44,"heightModifier":"2d4","baseWeightPounds":115,"weightModifier":"2d6"}"""));

        Assert.Null(HeightWeightTable.Parse(null));
        Assert.Null(HeightWeightTable.Parse(""));
        Assert.Null(HeightWeightTable.Parse("{not json"));
        Assert.Null(HeightWeightTable.Parse("""{"baseHeightInches":44,"heightModifier":"2d4"}"""));
        Assert.Null(HeightWeightTable.Parse("""{"baseHeightInches":0,"heightModifier":"2d4","baseWeightPounds":115,"weightModifier":"2d6"}"""));
    }

    [Fact]
    public void Races_and_subraces_expose_their_table()
    {
        var race = new RaceDefinition { Index = "folk", Name = "Folk", HeightWeightJson = new HeightWeightTable(50, "2d8", 100, "2d4").ToJson() };
        var subrace = new SubraceDefinition { Index = "sub", RaceIndex = "folk", Name = "Sub" };

        Assert.Equal(50, race.HeightWeight!.BaseHeightInches);
        Assert.Null(subrace.HeightWeight);
    }

    [Fact]
    public void Height_and_weight_are_set_cleared_and_validated()
    {
        var character = Dnd5eCharacter.Create(Character.Create(Guid.NewGuid(), Guid.NewGuid(), "Alto", Now));
        Assert.Equal(((int?)null, (int?)null), (character.Character.HeightInches, character.Character.WeightPounds));

        character.Character.SetHeightAndWeight(67, 165, Now);
        Assert.Equal(((int?)67, (int?)165), (character.Character.HeightInches, character.Character.WeightPounds));

        // Null keeps; 0 clears.
        character.Character.SetHeightAndWeight(null, 0, Now);
        Assert.Equal(((int?)67, (int?)null), (character.Character.HeightInches, character.Character.WeightPounds));

        Assert.Throws<DomainException>(() => character.Character.SetHeightAndWeight(201, null, Now));
        Assert.Throws<DomainException>(() => character.Character.SetHeightAndWeight(null, 2001, Now));
        Assert.Throws<DomainException>(() => character.Character.SetHeightAndWeight(-1, null, Now));

        character.ApplySheetEdit(new SheetEdit { HeightInches = 70, WeightPounds = 180 }, Now);
        Assert.Equal(((int?)70, (int?)180), (character.Character.HeightInches, character.Character.WeightPounds));
        character.ApplySheetEdit(new SheetEdit { Notes = "Sin cambios de talla." }, Now);
        Assert.Equal(((int?)70, (int?)180), (character.Character.HeightInches, character.Character.WeightPounds));
    }
}
