using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Monstertacle.Multisession.Requests;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Session closure is atomic cancellation and retains unknown execution evidence.</summary>
public sealed class SessionClosureTests
{
    /// <summary>Idle or queued close is final, idempotent and durable, without deleting history or changing identity.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClosesIdleOrQueuedWithoutSending(bool queued)
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot original = await fixture.CreateSessionAsync();
        RequestReceipt? receipt = queued ? await new RequestAdmission(fixture.Store, fixture.Options).SubmitAsync(
            new(original.SessionId, "original input", TimeSpan.FromSeconds(10), null, false), CancellationToken.None) : null;
        await fixture.Catalog.CloseSessionAsync(original.SessionId, CancellationToken.None);
        SessionSnapshot closed = (await fixture.Store.GetSessionAsync(original.SessionId, CancellationToken.None))!;
        Assert.Equal(SessionLifecycle.Closed, closed.Lifecycle);
        Assert.NotNull(closed.ClosedUtc);
        Assert.Equal(closed.ClosedUtc, closed.UpdatedUtc);
        Assert.Equal(original.Binding, closed.Binding);
        string durable = (string)StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT SnapshotJson FROM Sessions")!;
        await fixture.Catalog.CloseSessionAsync(original.SessionId, CancellationToken.None);
        await fixture.ReopenAsync();
        Assert.Equal(durable, StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT SnapshotJson FROM Sessions"));
        Assert.Equal(closed.ClosedUtc, (await fixture.Store.GetSessionAsync(original.SessionId, CancellationToken.None))?.ClosedUtc);
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => new RequestAdmission(fixture.Store, fixture.Options).SubmitAsync(
            new(original.SessionId, "too late", TimeSpan.FromSeconds(10), null, false), CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.SessionClosed, failure.Failure.Code);
        Assert.Null(await fixture.Store.TryReserveHeadAsync(4, CancellationToken.None));
        if (receipt is not null)
        {
            RequestSnapshot cancelled = (await fixture.Store.GetRequestAsync(receipt.RequestId, CancellationToken.None))!;
            Assert.Equal(RequestState.Cancelled, cancelled.State);
            Assert.Equal(ExecutionCertainty.NotStarted, cancelled.Certainty);
            Assert.Single((await fixture.Store.ReadHistoryAsync(original.SessionId, 100, null, CancellationToken.None)).Items);
        }
    }

    /// <summary>Unknown execution must keep Closing and its reservation until explicit confirmed-end resolution.</summary>
    [Fact]
    public async Task KeepsUnknownClosingUntilRecoveryConfirmsEnd()
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        await fixture.Host.Store.MarkUncertainAsync(fixture.Reservation.DispatchId, new(ErrorCode.Interrupted), CancellationToken.None);
        await fixture.Host.Catalog.CloseSessionAsync(fixture.Session.SessionId, CancellationToken.None);
        SessionSnapshot closing = (await fixture.Host.Store.GetSessionAsync(fixture.Session.SessionId, CancellationToken.None))!;
        Assert.Equal(SessionLifecycle.Closing, closing.Lifecycle);
        Assert.Equal(SessionBlockReason.RecoveryRequired, closing.BlockReason);
        Assert.Null(closing.ClosedUtc);
        Assert.Equal(1L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
        RecoveryCaseSnapshot item = (await fixture.Host.Store.GetRecoveryCaseAsync(new(RecoveryTargetKind.RequestExecution, fixture.Receipt.RequestId), CancellationToken.None))!;
        await fixture.Host.Store.ResolveRecoveryAsync(new(Guid.NewGuid(), item.Target, item.Revision, RecoveryDecisionKind.ConfirmEnded,
            "operator confirmed end", "non-secret evidence"), null, CancellationToken.None);
        Assert.Equal(SessionLifecycle.Closed, (await fixture.Host.Store.GetSessionAsync(fixture.Session.SessionId, CancellationToken.None))?.Lifecycle);
        Assert.Equal(RequestState.Interrupted, (await fixture.Host.Store.GetRequestAsync(fixture.Receipt.RequestId, CancellationToken.None))?.State);
    }

    /// <summary>Only this local session can be closed and caller cancellation before the transaction changes nothing.</summary>
    [Fact]
    public async Task RejectsMissingOrPreCancelledClose()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await fixture.CreateSessionAsync();
        string durable = (string)StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT SnapshotJson FROM Sessions")!;
        SessionOperationException missing = await Assert.ThrowsAsync<SessionOperationException>(() => fixture.Catalog.CloseSessionAsync(Guid.NewGuid(), CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.NotFound, missing.Failure.Code);
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Catalog.CloseSessionAsync(session.SessionId, cancelled.Token).AsTask());
        Assert.Equal(durable, StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT SnapshotJson FROM Sessions"));
    }

    /// <summary>Closing cannot be bypassed by a delayed native-open binding commit.</summary>
    [Fact]
    public async Task PreservesOpeningUncertaintyAndRefusesLateReopen()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        Guid id = Guid.NewGuid();
        ProviderSessionContext context = new(id, "model", ExecutionKind.ModelResponse, new(null, null), new Dictionary<string, string>());
        await fixture.Store.CreateSessionAsync(new(id, "connection-I", "profile", context, null) { SettingsRevision = "revision" }, CancellationToken.None);
        await fixture.Catalog.CloseSessionAsync(id, CancellationToken.None);
        Assert.Equal(SessionLifecycle.Closing, (await fixture.Store.GetSessionAsync(id, CancellationToken.None))?.Lifecycle);
        Assert.NotNull(await fixture.Store.GetRecoveryCaseAsync(new(RecoveryTargetKind.SessionOpening, id), CancellationToken.None));
        SessionOperationException late = await Assert.ThrowsAsync<SessionOperationException>(() => fixture.Store.CommitSessionBindingAsync(id,
            new("fixture-adapter", 7, "late-native", null), CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.SessionClosed, late.Failure.Code);
        Assert.Equal(SessionLifecycle.Closing, (await fixture.Store.GetSessionAsync(id, CancellationToken.None))?.Lifecycle);
    }

    /// <summary>A failure after queued result insertion rolls back closure and cancellation together.</summary>
    [Fact]
    public async Task RollsBackCloseAndQueuedCancellationOnWriteFailure()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await fixture.CreateSessionAsync();
        RequestReceipt receipt = await new RequestAdmission(fixture.Store, fixture.Options).SubmitAsync(
            new(session.SessionId, "queued", TimeSpan.FromSeconds(10), null, false), CancellationToken.None);
        StoreProbe.Execute(fixture.Directory.DatabasePath, "CREATE TRIGGER FailClose AFTER UPDATE OF Lifecycle ON Sessions BEGIN SELECT RAISE(FAIL,'private-close-marker'); END;");
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => fixture.Catalog.CloseSessionAsync(session.SessionId, CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
        Assert.DoesNotContain("private-close-marker", failure.ToString(), StringComparison.Ordinal);
        Assert.Equal(SessionLifecycle.Open, (await fixture.Store.GetSessionAsync(session.SessionId, CancellationToken.None))?.Lifecycle);
        Assert.Equal(RequestState.Queued, (await fixture.Store.GetRequestAsync(receipt.RequestId, CancellationToken.None))?.State);
        Assert.Equal(0L, StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT count(*) FROM Results"));
        Assert.Equal(0L, StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT count(*) FROM History"));
    }
}
