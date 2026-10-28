using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Scheduling;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Checks committed session FIFO rather than client invocation or enumeration order.</summary>
public sealed class SchedulingTests
{
    /// <summary>A clock change cannot make the second request overtake its own committed head.</summary>
    [Fact]
    public void UsesSequenceBeforeAcceptanceTimeWithinSession()
    {
        SessionSnapshot session = SchedulingData.Session();
        QueuedRequest head = SchedulingData.Queued(session.SessionId, 1, 2);
        QueuedRequest later = SchedulingData.Queued(session.SessionId, 2, -1);

        QueuedRequest? selected = ReservationPolicy.SelectHead([later, head], [], [session], 2);

        Assert.Equal(head, selected);
    }

    /// <summary>The oldest eligible session head wins, not an older later request in another session.</summary>
    [Fact]
    public void ChoosesOldestAcrossSessionHeads()
    {
        SessionSnapshot first = SchedulingData.Session();
        SessionSnapshot second = SchedulingData.Session();
        QueuedRequest head = SchedulingData.Queued(first.SessionId, 1, 2);
        QueuedRequest later = SchedulingData.Queued(first.SessionId, 2, -1);
        QueuedRequest otherHead = SchedulingData.Queued(second.SessionId, 1, 1);

        QueuedRequest? selected = ReservationPolicy.SelectHead([later, head, otherHead], [], [first, second], 2);

        Assert.Equal(otherHead, selected);
    }

    /// <summary>Equal committed timestamps are resolved by ordinal canonical request identity.</summary>
    [Fact]
    public void ResolvesTimestampTieByOrdinalRequestId()
    {
        SessionSnapshot first = SchedulingData.Session();
        SessionSnapshot second = SchedulingData.Session();
        QueuedRequest lower = SchedulingData.Queued(first.SessionId) with { RequestId = Guid.Parse("00000001-0000-0000-0000-000000000001") };
        QueuedRequest higher = SchedulingData.Queued(second.SessionId) with { RequestId = Guid.Parse("000000ff-0000-0000-0000-000000000001") };

        Assert.Equal(lower, ReservationPolicy.SelectHead([higher, lower], [], [first, second], 2));
        Assert.Equal(lower, ReservationPolicy.SelectHead([lower, higher], [], [first, second], 2));
    }
}
