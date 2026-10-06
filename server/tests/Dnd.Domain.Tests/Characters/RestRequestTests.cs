using Dnd.Domain.Characters;
using Dnd.Domain.Common;
using static Dnd.Domain.Tests.Characters.TestCatalog;

namespace Dnd.Domain.Tests.Characters;

public class RestRequestTests
{
    private static readonly Guid Player = Guid.NewGuid();
    private static readonly Guid Dm = Guid.NewGuid();

    [Fact]
    public void A_short_rest_request_keeps_the_dice_to_spend_and_drops_empty_entries()
    {
        var request = RestRequest.Create(Guid.NewGuid(), Guid.NewGuid(), Player, RestKind.Short,
            new Dictionary<string, int> { [" fighter "] = 2, ["wizard"] = 0 }, Now);

        Assert.True(request.IsPending);
        Assert.Equal((RestKind.Short, Now, Player), (request.Kind, request.RequestedAt, request.RequestedByUserId));
        Assert.Equal(new Dictionary<string, int> { ["fighter"] = 2 }, request.HitDice);
    }

    [Fact]
    public void A_long_rest_request_spends_no_hit_dice()
    {
        var empty = RestRequest.Create(Guid.NewGuid(), Guid.NewGuid(), Player, RestKind.Long, null, Now);

        Assert.Empty(empty.HitDice);
        Assert.Throws<DomainException>(() => RestRequest.Create(Guid.NewGuid(), Guid.NewGuid(), Player, RestKind.Long,
            new Dictionary<string, int> { ["fighter"] = 1 }, Now));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(21)]
    public void Dice_counts_out_of_range_are_rejected(int count)
    {
        Assert.Throws<DomainException>(() => RestRequest.Create(Guid.NewGuid(), Guid.NewGuid(), Player, RestKind.Short,
            new Dictionary<string, int> { ["fighter"] = count }, Now));
    }

    [Fact]
    public void Only_a_pending_request_is_resolved()
    {
        var approved = RestRequest.Create(Guid.NewGuid(), Guid.NewGuid(), Player, RestKind.Long, null, Now);
        approved.Approve(Dm, "  ", Now);

        Assert.Equal((RestRequestStatus.Approved, (Guid?)Dm, (string?)null), (approved.Status, approved.ResolvedByUserId, approved.Comment));
        var conflict = Assert.Throws<DomainException>(() => approved.Reject(Dm, null, Now));
        Assert.Equal(DomainErrorKind.Conflict, conflict.Kind);
        Assert.Throws<DomainException>(() => approved.Cancel(Player, Now));
    }

    [Fact]
    public void Rejecting_takes_an_optional_comment_and_cancelling_records_who()
    {
        var rejected = RestRequest.Create(Guid.NewGuid(), Guid.NewGuid(), Player, RestKind.Short, null, Now);
        var cancelled = RestRequest.Create(Guid.NewGuid(), Guid.NewGuid(), Player, RestKind.Short, null, Now);

        rejected.Reject(Dm, " Hay orcos cerca ", Now);
        cancelled.Cancel(Player, Now);

        Assert.Equal((RestRequestStatus.Rejected, "Hay orcos cerca"), (rejected.Status, rejected.Comment));
        Assert.Equal((RestRequestStatus.Cancelled, (Guid?)Player), (cancelled.Status, cancelled.ResolvedByUserId));
        Assert.Throws<DomainException>(() => RestRequest.Create(Guid.NewGuid(), Guid.NewGuid(), Player, RestKind.Short, null, Now)
            .Reject(Dm, new string('x', RestRequest.CommentMaxLength + 1), Now));
    }
}

public class LevelGrantTests
{
    private static readonly Guid Dm = Guid.NewGuid();

    [Fact]
    public void Granting_sets_the_next_level_once_without_accumulating()
    {
        var character = NewCharacter(classes: [new ClassEntry("fighter", null, 3)]);
        var later = Now.AddMinutes(5);

        Assert.True(character.GrantLevelUp(Dm, Now));
        Assert.False(character.GrantLevelUp(Dm, later));

        Assert.Equal((4, (Guid?)Dm, (DateTimeOffset?)Now), (character.PendingLevelUpTo, character.LevelGrantedByUserId, character.LevelGrantedAt));
    }

    [Fact]
    public void A_level_20_character_gets_no_grant()
    {
        var character = NewCharacter(classes: [new ClassEntry("fighter", null, 20)]);

        Assert.False(character.GrantLevelUp(Dm, Now));
        Assert.Null(character.PendingLevelUpTo);
    }

    [Fact]
    public void Revoking_clears_the_grant()
    {
        var character = NewCharacter(classes: [new ClassEntry("fighter", null, 1)]);
        Assert.False(character.RevokeLevelUp(Now));
        character.GrantLevelUp(Dm, Now);

        Assert.True(character.RevokeLevelUp(Now));

        Assert.Equal((null, null, null), (character.PendingLevelUpTo, character.LevelGrantedByUserId, character.LevelGrantedAt));
    }

    [Fact]
    public void A_class_edit_that_reaches_the_granted_level_clears_it()
    {
        var character = NewCharacter(classes: [new ClassEntry("fighter", null, 2)]);
        character.GrantLevelUp(Dm, Now);

        character.ReplaceClasses([new ClassEntry("fighter", null, 2)], Now);
        Assert.Equal(3, character.PendingLevelUpTo);

        character.ReplaceClasses([new ClassEntry("fighter", null, 2), new ClassEntry("wizard", null, 1)], Now);
        Assert.Null(character.PendingLevelUpTo);
    }

    [Fact]
    public void Requested_hit_dice_are_capped_at_the_remaining_ones()
    {
        var character = NewCharacter(Scores(con: 12), [new ClassEntry("fighter", null, 3), new ClassEntry("wizard", null, 1)]);
        var sheet = Sheet(character);
        character.Activate(sheet.HitPointsMax, Now);
        character.ShortRest(new Dictionary<string, int> { ["fighter"] = 2 }, sheet, new FixedDice(1), Now);

        var clamped = character.ClampHitDiceToRemaining(new Dictionary<string, int> { ["fighter"] = 3, ["wizard"] = 1, ["rogue"] = 2 });

        Assert.Equal(new Dictionary<string, int> { ["fighter"] = 1, ["wizard"] = 1 }, clamped);
    }
}
