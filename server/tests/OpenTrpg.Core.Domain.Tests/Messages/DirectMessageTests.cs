using OpenTrpg.Core.Domain.Common;
using OpenTrpg.Core.Domain.Messages;

namespace OpenTrpg.Core.Domain.Tests.Messages;

public class DirectMessageTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_bodies_are_rejected(string body)
    {
        Assert.Equal(
            DomainErrorKind.RuleViolation,
            Assert.Throws<DomainException>(() => DirectMessage.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), body, Now)).Kind);
    }

    [Fact]
    public void Bodies_are_trimmed_and_limited()
    {
        var message = DirectMessage.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "  **Cuidado** con el posadero.  ", Now);

        Assert.Equal("**Cuidado** con el posadero.", message.Body);
        Assert.Throws<DomainException>(() =>
            DirectMessage.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('a', DirectMessage.BodyMaxLength + 1), Now));
    }

    [Fact]
    public void Reading_again_keeps_the_first_read_time()
    {
        var message = DirectMessage.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Hola", Now);
        Assert.False(message.IsRead);

        message.MarkRead(Now.AddMinutes(1));
        message.MarkRead(Now.AddMinutes(5));

        Assert.Equal(Now.AddMinutes(1), message.ReadAt);
    }
}
