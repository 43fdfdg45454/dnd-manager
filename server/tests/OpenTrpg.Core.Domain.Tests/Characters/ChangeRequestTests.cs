using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Common;
using static OpenTrpg.Core.Domain.Tests.Characters.TestCatalog;

namespace OpenTrpg.Core.Domain.Tests.Characters;

public class ChangeRequestTests
{
    private readonly Guid _player = Guid.NewGuid();
    private readonly Guid _dm = Guid.NewGuid();

    [Fact]
    public void Create_starts_pending()
    {
        var request = NewRequest(payload: null);

        Assert.Equal((ChangeRequestStatus.Pending, "{}", _player, Now), (request.Status, request.PayloadJson, request.RequestedByUserId, request.CreatedAt));
        Assert.True(request.IsPending);
        Assert.Null(request.ResolvedAt);
    }

    [Fact]
    public void Approve_records_the_resolution()
    {
        var request = NewRequest();
        var later = Now.AddMinutes(5);

        request.Approve(_dm, "  Bien  ", later);

        Assert.Equal((ChangeRequestStatus.Approved, (Guid?)_dm, (DateTimeOffset?)later, "Bien"), (request.Status, request.ResolvedByUserId, request.ResolvedAt, request.Comment));
    }

    [Fact]
    public void Reject_requires_a_comment()
    {
        var request = NewRequest();

        Assert.Equal(DomainErrorKind.RuleViolation, Assert.Throws<DomainException>(() => request.Reject(_dm, "  ", Now)).Kind);

        request.Reject(_dm, "Demasiado oro", Now);
        Assert.Equal((ChangeRequestStatus.Rejected, "Demasiado oro"), (request.Status, request.Comment));
    }

    [Fact]
    public void Only_the_requester_can_cancel()
    {
        var request = NewRequest();

        Assert.Equal(DomainErrorKind.Forbidden, Assert.Throws<DomainException>(() => request.Cancel(_dm, Now)).Kind);

        request.Cancel(_player, Now);
        Assert.Equal((ChangeRequestStatus.Cancelled, (Guid?)_player), (request.Status, request.ResolvedByUserId));
    }

    [Theory]
    [InlineData(ChangeRequestStatus.Approved)]
    [InlineData(ChangeRequestStatus.Rejected)]
    [InlineData(ChangeRequestStatus.Cancelled)]
    public void Resolved_requests_cannot_transition_again(ChangeRequestStatus status)
    {
        var request = NewRequest();
        switch (status)
        {
            case ChangeRequestStatus.Approved:
                request.Approve(_dm, null, Now);
                break;
            case ChangeRequestStatus.Rejected:
                request.Reject(_dm, "No", Now);
                break;
            default:
                request.Cancel(_player, Now);
                break;
        }

        Assert.Equal(DomainErrorKind.Conflict, Assert.Throws<DomainException>(() => request.Approve(_dm, null, Now)).Kind);
        Assert.Equal(DomainErrorKind.Conflict, Assert.Throws<DomainException>(() => request.Reject(_dm, "No", Now)).Kind);
        Assert.Equal(DomainErrorKind.Conflict, Assert.Throws<DomainException>(() => request.Cancel(_player, Now)).Kind);
        Assert.Equal(status, request.Status);
    }

    [Fact]
    public void Comments_have_a_maximum_length()
    {
        var request = NewRequest();

        Assert.Throws<DomainException>(() => request.Approve(_dm, new string('c', ChangeRequest.CommentMaxLength + 1), Now));
        Assert.True(request.IsPending);
    }

    private ChangeRequest NewRequest(string? payload = """{"name":"Nuevo"}""") =>
        ChangeRequest.Create(Guid.NewGuid(), Guid.NewGuid(), _player, ChangeRequestTypes.Other, payload, Now);
}
