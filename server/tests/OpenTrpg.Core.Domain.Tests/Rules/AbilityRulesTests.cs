using OpenTrpg.Core.Domain.Rules;

namespace OpenTrpg.Core.Domain.Tests.Rules;

public class AbilityRulesTests
{
    [Theory]
    [InlineData(1, -5)]
    [InlineData(8, -1)]
    [InlineData(9, -1)]
    [InlineData(10, 0)]
    [InlineData(11, 0)]
    [InlineData(15, 2)]
    [InlineData(20, 5)]
    [InlineData(30, 10)]
    public void Modifier_follows_the_srd_table(int score, int expected)
    {
        Assert.Equal(expected, AbilityRules.Modifier(score));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void Modifier_rejects_scores_outside_1_to_30(int score)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AbilityRules.Modifier(score));
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(4, 2)]
    [InlineData(5, 3)]
    [InlineData(8, 3)]
    [InlineData(9, 4)]
    [InlineData(12, 4)]
    [InlineData(13, 5)]
    [InlineData(16, 5)]
    [InlineData(17, 6)]
    [InlineData(20, 6)]
    public void ProficiencyBonus_follows_the_srd_table(int level, int expected)
    {
        Assert.Equal(expected, AbilityRules.ProficiencyBonus(level));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void ProficiencyBonus_rejects_levels_outside_1_to_20(int level)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AbilityRules.ProficiencyBonus(level));
    }
}
