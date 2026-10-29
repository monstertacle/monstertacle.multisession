using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Checks revision-bound decisions and historic outcome preservation at the native transaction boundary.</summary>
public sealed class RecoveryDecisionTests
{
    private static RecoveryDecision Decision(RecoveryCaseSnapshot item, RecoveryDecisionKind kind)
        => new(Guid.NewGuid(), item.Target, item.Revision, kind, "explicit operator investigation", "non-secret evidence");

    /// <summary>A confirmed end changes evidence and capacity, while historic Interrupted never becomes success.</summary>
    [Fact]
    public async Task ConfirmsEndWithoutChangingHistoricFailureOrHistory()
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        ISessionStore store = fixture.Host.Store;
        await store.MarkUncertainAsync(fixture.Reservation.DispatchId, new(ErrorCode.Interrupted), CancellationToken.None);
        RecoveryCaseSnapshot item = (await store.GetRecoveryCaseAsync(new(RecoveryTargetKind.RequestExecution, fixture.Receipt.RequestId), CancellationToken.None))!;
        string history = (string)StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT SnapshotJson FROM History")!;
        await store.ResolveRecoveryAsync(Decision(item, RecoveryDecisionKind.ConfirmEnded), fixture.Session.Binding, CancellationToken.None);
        RequestSnapshot request = (await store.GetRequestAsync(fixture.Receipt.RequestId, CancellationToken.None))!;
        Assert.Equal(RequestState.Interrupted, request.State);
        Assert.Equal(ExecutionCertainty.ConfirmedEnded, request.Certainty);
        Assert.Equal(RequestState.Interrupted, request.Result?.Outcome);
        Assert.Equal(ExecutionCertainty.ConfirmedEnded, request.Result?.Certainty);
        Assert.Equal(history, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT SnapshotJson FROM History"));
        Assert.Equal(0L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
        RecoveryCaseSnapshot resolved = (await store.GetRecoveryCaseAsync(item.Target, CancellationToken.None))!;
        Assert.Equal(item.CreatedUtc, resolved.CreatedUtc);
        Assert.Equal(item.Revision + 1, resolved.Revision);
        Assert.Equal(RecoveryCaseStatus.Resolved, resolved.Status);
        Assert.Empty((await store.ListRecoveryCasesAsync(100, null, CancellationToken.None)).Items);
    }

    /// <summary>One expected revision has one winner and a decision identity can never be applied twice.</summary>
    [Fact]
    public async Task AppliesCompareAndSwapAndGlobalDecisionIdentity()
    {
        await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
        SessionSnapshot session = await fixture.CreateSessionAsync();
        await fixture.Store.MarkSessionBlockedAsync(session.SessionId, SessionBlockReason.RecoveryRequired, CancellationToken.None);
        RecoveryCaseSnapshot item = (await fixture.Store.GetRecoveryCaseAsync(new(RecoveryTargetKind.SessionOpening, session.SessionId), CancellationToken.None))!;
        RecoveryDecision decision = Decision(item, RecoveryDecisionKind.CloseSession);
        Task<bool>[] attempts = Enumerable.Range(0, 8).Select(async _ =>
        {
            try { await fixture.Store.ResolveRecoveryAsync(decision with { DecisionId = Guid.NewGuid() }, null, CancellationToken.None); return true; }
            catch (SessionOperationException failure) { Assert.Equal(ErrorCode.RevisionConflict, failure.Failure.Code); return false; }
        }).ToArray();
        Assert.Equal(1, (await Task.WhenAll(attempts)).Count(winner => winner));
        RecoveryCaseSnapshot next = (await fixture.Store.GetRecoveryCaseAsync(item.Target, CancellationToken.None))!;
        RecoveryDecision unique = Decision(next, RecoveryDecisionKind.CloseSession);
        await fixture.Store.ResolveRecoveryAsync(unique, null, CancellationToken.None);
        SessionOperationException duplicate = await Assert.ThrowsAsync<SessionOperationException>(() => fixture.Store.ResolveRecoveryAsync(unique with { ExpectedRevision = next.Revision + 1 }, null, CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.RevisionConflict, duplicate.Failure.Code);
        Assert.Equal(2L, StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT count(*) FROM RecoveryDecisions"));
    }

    /// <summary>Close cancels only still-queued input; unknown work keeps capacity until a separate confirmed-end decision.</summary>
    [Fact]
    public async Task CloseIntentRetainsUnknownThenConfirmedEndClosesSession()
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        ISessionStore store = fixture.Host.Store;
        RequestReceipt queued = await store.CommitAcceptanceAsync(new(Guid.NewGuid(), new(fixture.Session.SessionId, "queued input", TimeSpan.FromSeconds(10), null, false)), 100, CancellationToken.None);
        await store.MarkUncertainAsync(fixture.Reservation.DispatchId, new(ErrorCode.Interrupted), CancellationToken.None);
        RecoveryCaseSnapshot item = (await store.GetRecoveryCaseAsync(new(RecoveryTargetKind.RequestExecution, fixture.Receipt.RequestId), CancellationToken.None))!;
        await store.ResolveRecoveryAsync(Decision(item, RecoveryDecisionKind.CloseSession), null, CancellationToken.None);
        SessionSnapshot closing = (await store.GetSessionAsync(fixture.Session.SessionId, CancellationToken.None))!;
        Assert.Equal(SessionLifecycle.Closing, closing.Lifecycle);
        Assert.Equal(SessionBlockReason.RecoveryRequired, closing.BlockReason);
        Assert.Null(closing.ClosedUtc);
        Assert.Equal(1L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
        RequestSnapshot cancelled = (await store.GetRequestAsync(queued.RequestId, CancellationToken.None))!;
        Assert.Equal(RequestState.Cancelled, cancelled.State);
        Assert.Equal(ExecutionCertainty.NotStarted, cancelled.Certainty);
        item = (await store.GetRecoveryCaseAsync(item.Target, CancellationToken.None))!;
        await store.ResolveRecoveryAsync(Decision(item, RecoveryDecisionKind.ConfirmEnded), null, CancellationToken.None);
        SessionSnapshot closed = (await store.GetSessionAsync(fixture.Session.SessionId, CancellationToken.None))!;
        Assert.Equal(SessionLifecycle.Closed, closed.Lifecycle);
        Assert.Equal(closed.UpdatedUtc, closed.ClosedUtc);
        Assert.Equal(0L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
    }

    /// <summary>Malformed evidence is rejected before any revision or capacity change.</summary>
    [Theory]
    [InlineData("explanation")]
    [InlineData("evidence")]
    [InlineData("binding")]
    public async Task RejectsInvalidDecisionWithoutPartialMutation(string invalid)
    {
        await using PersistedDispatchFixture fixture = await PersistedDispatchFixture.CreateAsync();
        ISessionStore store = fixture.Host.Store;
        await store.MarkUncertainAsync(fixture.Reservation.DispatchId, new(ErrorCode.Interrupted), CancellationToken.None);
        RecoveryCaseSnapshot item = (await store.GetRecoveryCaseAsync(new(RecoveryTargetKind.RequestExecution, fixture.Receipt.RequestId), CancellationToken.None))!;
        RecoveryDecision decision = Decision(item, RecoveryDecisionKind.ConfirmEnded);
        if (invalid == "explanation") { decision = decision with { Explanation = " " }; }
        if (invalid == "evidence") { decision = decision with { EvidenceReference = " " }; }
        ProviderBinding? binding = invalid == "binding" ? fixture.Session.Binding! with { SessionReference = "different-root" } : fixture.Session.Binding;
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => store.ResolveRecoveryAsync(decision, binding, CancellationToken.None).AsTask());
        Assert.Equal(invalid == "binding" ? ErrorCode.ProviderUnavailable : ErrorCode.InvalidInput, failure.Failure.Code);
        Assert.Equal(item, await store.GetRecoveryCaseAsync(item.Target, CancellationToken.None));
        Assert.Equal(0L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM RecoveryDecisions"));
        Assert.Equal(1L, StoreProbe.Scalar(fixture.Host.Directory.DatabasePath, "SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL"));
    }
}
