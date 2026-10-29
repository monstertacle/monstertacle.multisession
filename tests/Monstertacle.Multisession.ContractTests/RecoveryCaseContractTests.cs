using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Recovery;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Tests recovery targets and CAS in controlled state; not a SQLite durability proof.</summary>
public sealed class RecoveryCaseContractTests
{
    private static RecoveryService Service(InMemorySessionStore store) => new(store, [], new()
    {
        StateDirectory = Path.GetTempPath(),
        Retention = new(true, null),
    });

    private static async Task<(InMemorySessionStore Store, SessionSnapshot Session, RecoveryTarget Target)> OpeningAsync()
    {
        InMemorySessionStore store = new();
        SessionSnapshot session = SchedulingData.Session() with { Lifecycle = SessionLifecycle.Opening, Binding = null };
        await store.CreateSessionAsync(session, CancellationToken.None);
        await store.MarkSessionBlockedAsync(session.SessionId, SessionBlockReason.RecoveryRequired, CancellationToken.None);
        return (store, session, new(RecoveryTargetKind.SessionOpening, session.SessionId));
    }

    private static RecoveryDecision Decision(RecoveryCaseSnapshot item, RecoveryDecisionKind kind)
        => new(Guid.NewGuid(), item.Target, item.Revision, kind, "explicit operator investigation", "non-secret evidence reference");

    /// <summary>An uncertain opening is visible under its local session identity, without an invented request.</summary>
    [Fact]
    public async Task ReadsOpeningWithoutDummyRequestAndListsCurrentRevision()
    {
        var (store, session, target) = await OpeningAsync();
        RecoveryService recovery = Service(store);
        RecoveryCaseSnapshot? item = await recovery.GetRecoveryCaseAsync(target, CancellationToken.None);
        Assert.NotNull(item);
        Assert.Equal(target, item.Target);
        Assert.Equal(session.SessionId, item.SessionId);
        Assert.Null(item.RequestId);
        Assert.Null(item.DispatchId);
        Assert.Equal(1, item.Revision);
        Assert.Equal(RecoveryCaseStatus.Open, item.Status);
        Assert.Empty(store.Submissions);
        Assert.Equal(item, Assert.Single((await recovery.ListRecoveryCasesAsync(100, null, CancellationToken.None)).Items));
        Assert.Equal(item, await recovery.GetRecoveryCaseAsync(target, CancellationToken.None));
        Assert.Null(await recovery.GetRecoveryCaseAsync(new(RecoveryTargetKind.SessionOpening, Guid.NewGuid()), CancellationToken.None));
    }

    /// <summary>A close decision preserves unconfirmed work until a separate end decision.</summary>
    [Fact]
    public async Task CloseWithoutEndEvidenceKeepsCaseOpenThenExplicitEndCanClose()
    {
        var (store, session, target) = await OpeningAsync();
        RecoveryService recovery = Service(store);
        RecoveryCaseSnapshot? original = await recovery.GetRecoveryCaseAsync(target, CancellationToken.None);
        Assert.NotNull(original);
        await recovery.ResolveRecoveryAsync(Decision(original, RecoveryDecisionKind.CloseSession), CancellationToken.None);
        RecoveryCaseSnapshot? closing = await recovery.GetRecoveryCaseAsync(target, CancellationToken.None);
        Assert.NotNull(closing);
        Assert.Equal(original.CreatedUtc, closing.CreatedUtc);
        Assert.Equal(2, closing.Revision);
        Assert.Equal(RecoveryCaseStatus.Open, closing.Status);
        Assert.Equal(ExecutionCertainty.Unknown, closing.Certainty);
        Assert.Equal(SessionLifecycle.Closing, store.FindSession(session.SessionId)?.Lifecycle);
        await recovery.ResolveRecoveryAsync(Decision(closing, RecoveryDecisionKind.ConfirmEnded), CancellationToken.None);
        RecoveryCaseSnapshot? resolved = await recovery.GetRecoveryCaseAsync(target, CancellationToken.None);
        Assert.NotNull(resolved);
        Assert.Equal(3, resolved.Revision);
        Assert.Equal(RecoveryCaseStatus.Resolved, resolved.Status);
        Assert.Equal(SessionLifecycle.Closed, store.FindSession(session.SessionId)?.Lifecycle);
        Assert.Empty((await recovery.ListRecoveryCasesAsync(100, null, CancellationToken.None)).Items);
    }

    /// <summary>Concurrent decisions with one revision have exactly one winner.</summary>
    [Fact]
    public async Task UsesCompareAndSwapAndRejectsDuplicateDecisionIdentity()
    {
        var (store, _, target) = await OpeningAsync();
        RecoveryService recovery = Service(store);
        RecoveryCaseSnapshot? item = await recovery.GetRecoveryCaseAsync(target, CancellationToken.None);
        Assert.NotNull(item);
        RecoveryDecision decision = Decision(item, RecoveryDecisionKind.CloseSession);
        TaskCompletionSource start = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool>[] attempts = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            await start.Task;
            try { await recovery.ResolveRecoveryAsync(decision with { DecisionId = Guid.NewGuid() }, CancellationToken.None); return true; }
            catch (SessionOperationException failure) when (failure.Failure.Code == ErrorCode.RevisionConflict) { return false; }
        })).ToArray();
        start.TrySetResult();
        Assert.Equal(1, (await Task.WhenAll(attempts)).Count(won => won));
        RecoveryCaseSnapshot? next = await recovery.GetRecoveryCaseAsync(target, CancellationToken.None);
        Assert.NotNull(next);
        RecoveryDecision unique = Decision(next, RecoveryDecisionKind.CloseSession);
        await recovery.ResolveRecoveryAsync(unique, CancellationToken.None);
        SessionOperationException conflict = await Assert.ThrowsAsync<SessionOperationException>(() =>
            recovery.ResolveRecoveryAsync(unique with { ExpectedRevision = next.Revision + 1 }, CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.RevisionConflict, conflict.Failure.Code);
    }

    /// <summary>Reading unknown native state is unsupported without mutation or reopening.</summary>
    [Fact]
    public async Task InspectWithoutNativeReferenceDoesNotMutateCase()
    {
        var (store, _, target) = await OpeningAsync();
        RecoveryService recovery = Service(store);
        RecoveryCaseSnapshot? before = await recovery.GetRecoveryCaseAsync(target, CancellationToken.None);
        Assert.NotNull(before);
        ProviderReply result = await recovery.InspectRecoveryAsync(target, CancellationToken.None);
        Assert.Equal(ErrorCode.UnsupportedProviderAction, result.Error?.Code);
        Assert.Equal(ExecutionCertainty.Unknown, result.Certainty);
        Assert.Equal(before, await recovery.GetRecoveryCaseAsync(target, CancellationToken.None));
    }

    /// <summary>Execution recovery keeps historical Interrupted rather than inventing success.</summary>
    [Fact]
    public async Task ExecutionTargetRetainsFailedResultAndReservationUntilConfirmed()
    {
        InMemorySessionStore store = new();
        SessionSnapshot session = SchedulingData.Session();
        await store.CreateSessionAsync(session, CancellationToken.None);
        RequestReceipt accepted = await store.CommitAcceptanceAsync(new(session.SessionId, "private input", TimeSpan.FromSeconds(10), "workspace", true), 100, CancellationToken.None);
        ExecutionReservation? reservation = await store.TryReserveHeadAsync(4, CancellationToken.None);
        Assert.NotNull(reservation);
        await store.MarkUncertainAsync(reservation.DispatchId, new(ErrorCode.Interrupted), CancellationToken.None);
        RecoveryService recovery = Service(store);
        RecoveryTarget target = new(RecoveryTargetKind.RequestExecution, accepted.RequestId);
        RecoveryCaseSnapshot? item = await recovery.GetRecoveryCaseAsync(target, CancellationToken.None);
        Assert.NotNull(item);
        Assert.Equal(accepted.RequestId, item.RequestId);
        Assert.Equal(reservation.DispatchId, item.DispatchId);
        await recovery.ResolveRecoveryAsync(Decision(item, RecoveryDecisionKind.CloseSession), CancellationToken.None);
        Assert.Single(store.Reservations);
        RecoveryCaseSnapshot? closing = await recovery.GetRecoveryCaseAsync(target, CancellationToken.None);
        Assert.NotNull(closing);
        await recovery.ResolveRecoveryAsync(Decision(closing, RecoveryDecisionKind.ConfirmEnded), CancellationToken.None);
        RequestSnapshot? request = await ((ISessionStore)store).GetRequestAsync(accepted.RequestId, CancellationToken.None);
        Assert.Equal(RequestState.Interrupted, request?.State);
        Assert.Equal(ExecutionCertainty.ConfirmedEnded, request?.Certainty);
        Assert.Empty(store.Reservations);
    }
}
