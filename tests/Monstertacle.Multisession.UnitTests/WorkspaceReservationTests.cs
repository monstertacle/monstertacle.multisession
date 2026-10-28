using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Scheduling;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Checks occupancy independently of local request outcomes or provider names.</summary>
public sealed class WorkspaceReservationTests
{
    /// <summary>An unreleased reservation blocks every later request in its session, including readers.</summary>
    [Fact]
    public void ExcludesReservedSession()
    {
        SessionSnapshot session = SchedulingData.Session();
        QueuedRequest queued = SchedulingData.Queued(session.SessionId);
        ExecutionReservation active = SchedulingData.Reserved(session.SessionId);

        Assert.Null(ReservationPolicy.SelectHead([queued], [active], [session], 2));
    }

    /// <summary>A blocked writer head cannot be bypassed; an independent third workspace still uses a free slot.</summary>
    [Fact]
    public void SkipsBlockedSessionHeadWithoutBreakingItsFifo()
    {
        SessionSnapshot blocked = SchedulingData.Session();
        SessionSnapshot independent = SchedulingData.Session();
        QueuedRequest head = SchedulingData.Queued(blocked.SessionId, 1, 0, "shared");
        QueuedRequest later = SchedulingData.Queued(blocked.SessionId, 2, 1, "different");
        QueuedRequest allowed = SchedulingData.Queued(independent.SessionId, 1, 2, "third");
        ExecutionReservation active = SchedulingData.Reserved(Guid.NewGuid(), "shared");

        Assert.Null(ReservationPolicy.SelectHead([later, head], [active], [blocked], 2));
        Assert.Equal(allowed, ReservationPolicy.SelectHead([later, allowed, head], [active], [blocked, independent], 2));
    }

    /// <summary>All unreleased reservations occupy global capacity, even when the limit was lowered.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void KeepsAllUnreleasedSlotsOccupied(int occupied)
    {
        SessionSnapshot session = SchedulingData.Session();
        ExecutionReservation[] active = Enumerable.Range(0, occupied)
            .Select(_ => SchedulingData.Reserved(Guid.NewGuid())).ToArray();

        Assert.Null(ReservationPolicy.SelectHead([SchedulingData.Queued(session.SessionId)], active, [session], 1));
    }

    /// <summary>Non-open or explicitly blocked sessions cannot be dispatched.</summary>
    [Theory]
    [InlineData(SessionLifecycle.Opening, SessionBlockReason.None)]
    [InlineData(SessionLifecycle.Closing, SessionBlockReason.None)]
    [InlineData(SessionLifecycle.Closed, SessionBlockReason.None)]
    [InlineData(SessionLifecycle.Open, SessionBlockReason.RecoveryRequired)]
    [InlineData(SessionLifecycle.Open, SessionBlockReason.ProviderUnavailable)]
    [InlineData(SessionLifecycle.Open, SessionBlockReason.StorageFailure)]
    public void ExcludesUnavailableSession(SessionLifecycle lifecycle, SessionBlockReason reason)
    {
        SessionSnapshot session = SchedulingData.Session() with { Lifecycle = lifecycle, BlockReason = reason };

        Assert.Null(ReservationPolicy.SelectHead([SchedulingData.Queued(session.SessionId)], [], [session], 2));
    }

    /// <summary>A dangling queued identity does not create an implicit session or provider.</summary>
    [Fact]
    public void ExcludesUnknownSession()
    {
        Assert.Null(ReservationPolicy.SelectHead([SchedulingData.Queued(Guid.NewGuid())], [], [], 2));
    }

    /// <summary>Invalid limits fail as configuration errors rather than silently admitting work.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsInvalidSlotLimit(int limit)
    {
        SessionOperationException failure = Assert.Throws<SessionOperationException>(
            () => ReservationPolicy.SelectHead([], [], [], limit));

        Assert.Equal(ErrorCode.InvalidConfiguration, failure.Failure.Code);
    }

    /// <summary>Host keys are compared ordinally; non-writing requests do not acquire a write reservation.</summary>
    [Theory]
    [InlineData("Shared")]
    [InlineData(null)]
    public void AllowsDistinctOrdinalKeyOrNonWriter(string? key)
    {
        SessionSnapshot session = SchedulingData.Session();
        QueuedRequest queued = SchedulingData.Queued(session.SessionId, key: key);
        ExecutionReservation active = SchedulingData.Reserved(Guid.NewGuid(), "shared");

        Assert.Equal(queued, ReservationPolicy.SelectHead([queued], [active], [session], 2));
    }

    /// <summary>No eligible work means no manufactured dispatch.</summary>
    [Fact]
    public void LeavesEmptyQueueUnreserved()
    {
        Assert.Null(ReservationPolicy.SelectHead([], [], [], 2));
    }
}
