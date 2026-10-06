using Dnd.Domain.Campaigns;
using Dnd.Domain.Common;

namespace Dnd.Domain.Tests.Campaigns;

public class CampaignStashTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _owner = Guid.NewGuid();

    [Fact]
    public void New_campaigns_let_players_take_from_an_empty_stash()
    {
        var campaign = Campaign.Create("Campaña", null, _owner, Now);

        Assert.True(campaign.PlayersCanTakeFromStash);
        Assert.Equal(0, campaign.StashCopperPieces);
    }

    [Fact]
    public void Settings_change_whether_players_take_from_the_stash()
    {
        var campaign = Campaign.Create("Campaña", null, _owner, Now);

        campaign.UpdateSettings(_owner, null, null, Now, playersCanTakeFromStash: false);
        Assert.False(campaign.PlayersCanTakeFromStash);

        campaign.UpdateSettings(_owner, "UTC", null, Now);
        Assert.False(campaign.PlayersCanTakeFromStash);
    }

    [Fact]
    public void Stash_gold_is_added_and_withdrawn_but_never_below_zero()
    {
        var campaign = Campaign.Create("Campaña", null, _owner, Now);

        campaign.AdjustStashGold(500, Now);
        campaign.AdjustStashGold(-200, Now);

        Assert.Equal(300, campaign.StashCopperPieces);
        Assert.Equal(DomainErrorKind.RuleViolation, Assert.Throws<DomainException>(() => campaign.AdjustStashGold(-301, Now)).Kind);
        Assert.Throws<DomainException>(() => campaign.AdjustStashGold(Campaign.MaxStashCopperPieces, Now));
        Assert.Equal(300, campaign.StashCopperPieces);
    }

    [Fact]
    public void Splitting_gives_equal_shares_and_keeps_the_remainder()
    {
        var campaign = Campaign.Create("Campaña", null, _owner, Now);
        campaign.AdjustStashGold(1000, Now);

        var share = campaign.SplitStashGold(3, Now);

        Assert.Equal((333, 1), (share, campaign.StashCopperPieces));
    }

    [Fact]
    public void Splitting_needs_recipients_and_enough_gold()
    {
        var campaign = Campaign.Create("Campaña", null, _owner, Now);
        campaign.AdjustStashGold(2, Now);

        Assert.Throws<DomainException>(() => campaign.SplitStashGold(0, Now));
        Assert.Throws<DomainException>(() => campaign.SplitStashGold(3, Now));
        Assert.Equal(2, campaign.StashCopperPieces);
    }
}
