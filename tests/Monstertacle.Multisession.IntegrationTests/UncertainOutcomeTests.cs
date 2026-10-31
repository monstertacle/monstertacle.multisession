using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Unknown external termination must not erase the observed local failure category.</summary>
public sealed class UncertainOutcomeTests
{
    /// <summary>Reopening and confirmed-end clarification retain the original local outcome and frozen history.</summary>
    [Theory]
    [InlineData(ErrorCode.Cancelled, RequestState.Cancelled)]
    [InlineData(ErrorCode.TimedOut, RequestState.TimedOut)]
    [InlineData(ErrorCode.ExecutionFailed, RequestState.Failed)]
    [InlineData(ErrorCode.Interrupted, RequestState.Interrupted)]
    public async Task PreservesLocalOutcomeAcrossRestartAndClarification(ErrorCode code, RequestState outcome)
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        ISessionStore store = fixture.Host.Store;
        await store.MarkUncertainAsync(fixture.Reservation.DispatchId, new(code), CancellationToken.None);
        RequestSnapshot before = (await store.GetRequestAsync(fixture.Receipt.RequestId, CancellationToken.None))!;
        Assert.Equal(outcome, before.State);
        Assert.Equal(outcome, before.Result?.Outcome);
        Assert.Equal(code, before.Result?.Error?.Code);
        Assert.Equal(ExecutionCertainty.Unknown, before.Certainty);
        Assert.Equal(1L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
        string history = (string)StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT SnapshotJson FROM History")!;
        await fixture.Host.ReopenAsync();
        store = fixture.Host.Store;
        Assert.Equal(before, await store.GetRequestAsync(before.RequestId, CancellationToken.None));
        RecoveryCaseSnapshot item = (await store.GetRecoveryCaseAsync(new(RecoveryTargetKind.RequestExecution, before.RequestId), CancellationToken.None))!;
        await store.ResolveRecoveryAsync(new(Guid.NewGuid(), item.Target, item.Revision, RecoveryDecisionKind.ConfirmEnded,
            "confirmed externally ended", "non-secret evidence"), fixture.Session.Binding, CancellationToken.None);
        RequestSnapshot clarified = (await store.GetRequestAsync(before.RequestId, CancellationToken.None))!;
        Assert.Equal(outcome, clarified.State);
        Assert.Equal(outcome, clarified.Result?.Outcome);
        Assert.Equal(ExecutionCertainty.ConfirmedEnded, clarified.Certainty);
        Assert.Equal(history, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT SnapshotJson FROM History"));
        Assert.Equal(1L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM Results"));
        Assert.Equal(0L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
    }
}
