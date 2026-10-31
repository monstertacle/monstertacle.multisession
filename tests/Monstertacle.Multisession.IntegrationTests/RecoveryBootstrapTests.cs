using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Checks actual persisted startup state; these handoffs do not claim forced-process SC-009.</summary>
public sealed class RecoveryBootstrapTests
{
    /// <summary>Old running is conservatively interrupted; independent queued input remains dispatchable in the free slot.</summary>
    [Fact]
    public async Task RetainsOldReservationAndContinuesIndependentQueuedSession()
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        SessionSnapshot independent = await fixture.Host.CreateSessionAsync();
        RequestReceipt queued = await fixture.Host.Store.CommitAcceptanceAsync(new(Guid.NewGuid(), new(independent.SessionId, "independent", TimeSpan.FromSeconds(10), null, false)), 100, CancellationToken.None);
        await fixture.Host.ReopenAsync();
        RequestSnapshot interrupted = (await fixture.Host.Store.GetRequestAsync(fixture.Receipt.RequestId, CancellationToken.None))!;
        Assert.Equal(RequestState.Interrupted, interrupted.State);
        Assert.Equal(ExecutionCertainty.Unknown, interrupted.Certainty);
        Assert.Null(await fixture.Host.Store.GetReservedDispatchAsync(fixture.Reservation.DispatchId, CancellationToken.None));
        ExecutionReservation next = (await fixture.Host.Store.TryReserveHeadAsync(2, CancellationToken.None))!;
        Assert.Equal(queued.RequestId, next.RequestId);
        Assert.Null(await fixture.Host.Store.TryReserveHeadAsync(2, CancellationToken.None));
    }

    /// <summary>Repeated startup preserves existing unknown case identity/history rather than applying it again.</summary>
    [Fact]
    public async Task DoesNotDuplicateRecoveryProjectionOnSecondStart()
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        await fixture.Host.ReopenAsync();
        RecoveryTarget target = new(RecoveryTargetKind.RequestExecution, fixture.Receipt.RequestId);
        RecoveryCaseSnapshot first = (await fixture.Host.Store.GetRecoveryCaseAsync(target, CancellationToken.None))!;
        long count = (long)StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM History")!;
        await fixture.Host.ReopenAsync();
        Assert.Equal(first, await fixture.Host.Store.GetRecoveryCaseAsync(target, CancellationToken.None));
        Assert.Equal(count, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM History"));
    }

    /// <summary>Lost reservation evidence fails closed rather than releasing an unexplained attempt.</summary>
    [Fact]
    public async Task RefusesInconsistentRunningWithoutInventingReservation()
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        await fixture.Host.Store.DisposeAsync();
        StoreProbe.Execute(fixture.Host.Directory.DatabasePath, "DELETE FROM Reservations");
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => fixture.Host.ReopenAsync());
        Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
        Assert.Equal((long)RequestState.Running, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT State FROM Requests"));
        Assert.Equal(0L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM Reservations"));
    }
}
