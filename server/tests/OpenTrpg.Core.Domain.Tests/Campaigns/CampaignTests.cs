using OpenTrpg.Core.Domain.Campaigns;
using OpenTrpg.Core.Domain.Common;

namespace OpenTrpg.Core.Domain.Tests.Campaigns;

public class CampaignTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _owner = Guid.NewGuid();
    private readonly Guid _dm = Guid.NewGuid();
    private readonly Guid _player = Guid.NewGuid();
    private readonly Guid _outsider = Guid.NewGuid();

    [Fact]
    public void Create_registers_the_creator_as_owner_member()
    {
        var campaign = Campaign.Create("  Nombre  ", null, _owner, Now);

        Assert.Equal("Nombre", campaign.Name);
        Assert.Equal(string.Empty, campaign.Description);
        Assert.Equal(_owner, campaign.OwnerId);
        Assert.Equal(Campaign.DefaultSystemId, campaign.SystemId);
        Assert.Equal(Now, campaign.UpdatedAt);
        var member = Assert.Single(campaign.Members);
        Assert.Equal((campaign.Id, _owner, CampaignRole.Owner, Now), (member.CampaignId, member.UserId, member.Role, member.JoinedAt));
    }

    [Theory]
    [InlineData(null, "dnd5e")]
    [InlineData("", "dnd5e")]
    [InlineData("   ", "dnd5e")]
    [InlineData("dnd5e", "dnd5e")]
    [InlineData("  DnD5e ", "dnd5e")]
    [InlineData("my-system-2", "my-system-2")]
    public void Create_normalizes_the_game_system(string? systemId, string expected)
    {
        var campaign = Campaign.Create("Nombre", null, _owner, Now, systemId: systemId);

        Assert.Equal(expected, campaign.SystemId);
    }

    [Theory]
    [InlineData("dnd 5e")]
    [InlineData("dnd_5e")]
    [InlineData("dñd5e")]
    [InlineData("a234567890123456789012345678901234")]
    public void Create_rejects_malformed_game_systems(string systemId)
    {
        var error = Assert.Throws<DomainException>(() => Campaign.Create("Nombre", null, _owner, Now, systemId: systemId));
        Assert.Equal(DomainErrorKind.RuleViolation, error.Kind);
    }

    [Fact]
    public void Create_accepts_a_game_system_of_the_maximum_length()
    {
        var systemId = new string('a', Campaign.SystemIdMaxLength);

        Assert.Equal(systemId, Campaign.Create("Nombre", null, _owner, Now, systemId: systemId).SystemId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_empty_names(string name)
    {
        var error = Assert.Throws<DomainException>(() => Campaign.Create(name, null, _owner, Now));
        Assert.Equal(DomainErrorKind.RuleViolation, error.Kind);
    }

    [Fact]
    public void Create_rejects_too_long_name_or_description()
    {
        Assert.Throws<DomainException>(() => Campaign.Create(new string('n', Campaign.NameMaxLength + 1), null, _owner, Now));
        Assert.Throws<DomainException>(() => Campaign.Create("ok", new string('d', Campaign.DescriptionMaxLength + 1), _owner, Now));
    }

    [Theory]
    [InlineData(CampaignRole.Owner, CampaignRole.Player, true)]
    [InlineData(CampaignRole.Owner, CampaignRole.Owner, true)]
    [InlineData(CampaignRole.DM, CampaignRole.Player, true)]
    [InlineData(CampaignRole.DM, CampaignRole.DM, true)]
    [InlineData(CampaignRole.DM, CampaignRole.Owner, false)]
    [InlineData(CampaignRole.Player, CampaignRole.Player, true)]
    [InlineData(CampaignRole.Player, CampaignRole.DM, false)]
    public void IsAtLeast_follows_the_rank(CampaignRole role, CampaignRole minimum, bool expected) =>
        Assert.Equal(expected, role.IsAtLeast(minimum));

    [Fact]
    public void AddMember_enforces_who_can_add_which_role()
    {
        var campaign = NewCampaign();
        var newcomer = Guid.NewGuid();

        AssertKind(DomainErrorKind.Forbidden, () => campaign.AddMember(_dm, newcomer, CampaignRole.DM, Now));
        AssertKind(DomainErrorKind.Forbidden, () => campaign.AddMember(_player, newcomer, CampaignRole.Player, Now));
        AssertKind(DomainErrorKind.Forbidden, () => campaign.AddMember(_outsider, newcomer, CampaignRole.Player, Now));
        AssertKind(DomainErrorKind.RuleViolation, () => campaign.AddMember(_owner, newcomer, CampaignRole.Owner, Now));
        AssertKind(DomainErrorKind.Conflict, () => campaign.AddMember(_owner, _player, CampaignRole.Player, Now));

        var member = campaign.AddMember(_dm, newcomer, CampaignRole.Player, Now);
        Assert.Equal((newcomer, CampaignRole.Player), (member.UserId, member.Role));
        Assert.Equal(4, campaign.Members.Count);
    }

    [Fact]
    public void ChangeRole_is_owner_only_and_never_touches_the_owner()
    {
        var campaign = NewCampaign();

        AssertKind(DomainErrorKind.Forbidden, () => campaign.ChangeRole(_dm, _player, CampaignRole.DM));
        AssertKind(DomainErrorKind.RuleViolation, () => campaign.ChangeRole(_owner, _owner, CampaignRole.DM));
        AssertKind(DomainErrorKind.RuleViolation, () => campaign.ChangeRole(_owner, _dm, CampaignRole.Owner));
        AssertKind(DomainErrorKind.NotFound, () => campaign.ChangeRole(_owner, _outsider, CampaignRole.DM));

        campaign.ChangeRole(_owner, _player, CampaignRole.DM);
        campaign.ChangeRole(_owner, _dm, CampaignRole.Player);
        Assert.Equal(CampaignRole.DM, campaign.RoleOf(_player));
        Assert.Equal(CampaignRole.Player, campaign.RoleOf(_dm));
    }

    [Fact]
    public void RemoveMember_rules()
    {
        var campaign = NewCampaign();
        var otherDm = Guid.NewGuid();
        campaign.AddMember(_owner, otherDm, CampaignRole.DM, Now);

        AssertKind(DomainErrorKind.RuleViolation, () => campaign.RemoveMember(_owner, _owner));
        AssertKind(DomainErrorKind.RuleViolation, () => campaign.RemoveMember(_dm, _owner));
        AssertKind(DomainErrorKind.Forbidden, () => campaign.RemoveMember(_dm, otherDm));
        AssertKind(DomainErrorKind.Forbidden, () => campaign.RemoveMember(_player, _dm));
        AssertKind(DomainErrorKind.NotFound, () => campaign.RemoveMember(_owner, _outsider));

        campaign.RemoveMember(_dm, _player);
        campaign.RemoveMember(_owner, otherDm);
        Assert.Null(campaign.RoleOf(_player));
        Assert.Null(campaign.RoleOf(otherDm));
        Assert.Equal(2, campaign.Members.Count);
    }

    [Fact]
    public void Leave_is_allowed_to_everyone_but_the_owner()
    {
        var campaign = NewCampaign();

        AssertKind(DomainErrorKind.RuleViolation, () => campaign.Leave(_owner));
        AssertKind(DomainErrorKind.NotFound, () => campaign.Leave(_outsider));

        campaign.Leave(_dm);
        campaign.Leave(_player);
        Assert.Equal(_owner, Assert.Single(campaign.Members).UserId);
    }

    [Fact]
    public void TransferOwnership_swaps_roles_and_returns_the_record()
    {
        var campaign = NewCampaign();
        var later = Now.AddHours(1);

        var transfer = campaign.TransferOwnership(_owner, _player, CampaignRole.DM, later);

        Assert.Equal(_player, campaign.OwnerId);
        Assert.Equal(later, campaign.UpdatedAt);
        Assert.Equal(CampaignRole.Owner, campaign.RoleOf(_player));
        Assert.Equal(CampaignRole.DM, campaign.RoleOf(_owner));
        Assert.Single(campaign.Members, m => m.Role == CampaignRole.Owner);
        Assert.Equal(
            (campaign.Id, _owner, _player, CampaignRole.DM, later),
            (transfer.CampaignId, transfer.FromUserId, transfer.ToUserId, transfer.PreviousOwnerNewRole, transfer.TransferredAt));
    }

    [Fact]
    public void TransferOwnership_rules()
    {
        var campaign = NewCampaign();

        AssertKind(DomainErrorKind.Forbidden, () => campaign.TransferOwnership(_dm, _player, CampaignRole.DM, Now));
        AssertKind(DomainErrorKind.RuleViolation, () => campaign.TransferOwnership(_owner, _owner, CampaignRole.DM, Now));
        AssertKind(DomainErrorKind.RuleViolation, () => campaign.TransferOwnership(_owner, _outsider, CampaignRole.DM, Now));
        AssertKind(DomainErrorKind.RuleViolation, () => campaign.TransferOwnership(_owner, _dm, CampaignRole.Owner, Now));
        Assert.Equal(_owner, campaign.OwnerId);
    }

    [Fact]
    public void UpdateDetails_requires_at_least_dm_and_keeps_null_fields()
    {
        var campaign = Campaign.Create("Nombre", "Descripción", _owner, Now);
        campaign.AddMember(_owner, _dm, CampaignRole.DM, Now);
        campaign.AddMember(_owner, _player, CampaignRole.Player, Now);

        AssertKind(DomainErrorKind.Forbidden, () => campaign.UpdateDetails(_player, "x", null, Now));

        campaign.UpdateDetails(_dm, "Otro", null, Now.AddMinutes(1));
        Assert.Equal(("Otro", "Descripción", Now.AddMinutes(1)), (campaign.Name, campaign.Description, campaign.UpdatedAt));
    }

    private Campaign NewCampaign()
    {
        var campaign = Campaign.Create("Campaña", "", _owner, Now);
        campaign.AddMember(_owner, _dm, CampaignRole.DM, Now);
        campaign.AddMember(_owner, _player, CampaignRole.Player, Now);
        return campaign;
    }

    private static void AssertKind(DomainErrorKind expected, Action action) =>
        Assert.Equal(expected, Assert.Throws<DomainException>(action).Kind);

    private static void AssertKind(DomainErrorKind expected, Func<object> action) =>
        Assert.Equal(expected, Assert.Throws<DomainException>(action).Kind);
}
