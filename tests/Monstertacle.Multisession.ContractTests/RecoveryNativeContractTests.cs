using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Recovery;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Uses a controlled native adapter to prove read-only inspect and Reopen-before-CAS ordering.</summary>
public sealed class RecoveryNativeContractTests
{
    private static async Task<(InMemorySessionStore Store, RecoveryService Service, RecoveryCaseSnapshot Case)> ExecutionAsync(ControlledProvider provider)
    {
        InMemorySessionStore store = new();
        SessionSnapshot session = SchedulingData.Session();
        await store.CreateSessionAsync(session, CancellationToken.None);
        await store.CommitAcceptanceAsync(new(session.SessionId, "input", TimeSpan.FromSeconds(10), null, false), 100, CancellationToken.None);
        ExecutionReservation? reservation = await store.TryReserveHeadAsync(4, CancellationToken.None);
        Assert.NotNull(reservation);
        await store.CommitObservationAsync(reservation.DispatchId, new(ProviderObservationKind.OperationAccepted, "opaque-operation"), CancellationToken.None);
        await store.MarkUncertainAsync(reservation.DispatchId, new(ErrorCode.Interrupted), CancellationToken.None);
        RecoveryService service = new(store, [new(session.ProviderId, "test-adapter", 1, provider)], new()
        { StateDirectory = Path.GetTempPath(), Retention = new(true, null) });
        RecoveryCaseSnapshot? item = await service.GetRecoveryCaseAsync(new(RecoveryTargetKind.RequestExecution, reservation.RequestId), CancellationToken.None);
        Assert.NotNull(item);
        return (store, service, item);
    }

    private static RecoveryDecision Decision(RecoveryCaseSnapshot item, RecoveryDecisionKind kind = RecoveryDecisionKind.ConfirmEnded)
        => new(Guid.NewGuid(), item.Target, item.Revision, kind, "operator confirmed end", "investigation reference");

    /// <summary>Inspect may discover external termination without deciding a case or releasing its reservation.</summary>
    [Fact]
    public async Task InspectsKnownOperationWithoutMutation()
    {
        int inspections = 0;
        ControlledProvider provider = new()
        {
            Capabilities = new(true, true, true, false, false),
            Inspecting = (context, operation, token) =>
            {
                Assert.Equal("opaque-operation", operation);
                Interlocked.Increment(ref inspections);
                return ValueTask.FromResult(new ProviderReply(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, "external result", true, null, null));
            },
        };
        var (store, service, item) = await ExecutionAsync(provider);
        ProviderReply inspected = await service.InspectRecoveryAsync(item.Target, CancellationToken.None);
        Assert.Equal(RequestState.Succeeded, inspected.Outcome);
        Assert.Equal(1, inspections);
        Assert.Equal(item, await service.GetRecoveryCaseAsync(item.Target, CancellationToken.None));
        Assert.Single(store.Reservations);
        Assert.False(provider.Started.Task.IsCompleted);
    }

    /// <summary>A continuation is enabled only after reopening the same known native binding.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReopensSameBindingBeforePublishingResolution(bool sameIdentity)
    {
        int reopenings = 0;
        ControlledProvider provider = new()
        {
            Reopening = (context, binding, token) =>
            {
                Interlocked.Increment(ref reopenings);
                return ValueTask.FromResult(binding with { SessionReference = sameIdentity ? binding.SessionReference : "replacement", ContinuationState = "reopened-head" });
            },
        };
        var (store, service, item) = await ExecutionAsync(provider);
        if (sameIdentity)
        {
            await service.ResolveRecoveryAsync(Decision(item), CancellationToken.None);
            Assert.Empty(store.Reservations);
            Assert.Equal(SessionBlockReason.None, store.FindSession(item.SessionId)?.BlockReason);
            Assert.Equal("reopened-head", store.FindSession(item.SessionId)?.Binding?.ContinuationState);
            Assert.Equal(1, store.FindSession(item.SessionId)?.BindingRevision);
            Assert.Equal(RecoveryCaseStatus.Resolved, (await service.GetRecoveryCaseAsync(item.Target, CancellationToken.None))?.Status);
            Assert.Equal(RequestState.Interrupted, (await ((ISessionStore)store).GetRequestAsync(item.RequestId!.Value, CancellationToken.None))?.State);
        }
        else
        {
            SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => service.ResolveRecoveryAsync(Decision(item), CancellationToken.None).AsTask());
            Assert.Equal(ErrorCode.ProviderUnavailable, failure.Failure.Code);
            Assert.Equal(item, await service.GetRecoveryCaseAsync(item.Target, CancellationToken.None));
            Assert.Single(store.Reservations);
        }
        Assert.Equal(1, reopenings);
        Assert.False(provider.Started.Task.IsCompleted);
    }

    /// <summary>Native I/O does not hold the store gate; a concurrent decision invalidates its old expected revision.</summary>
    [Fact]
    public async Task RechecksRevisionAfterReopenWithoutPartialBindingPublication()
    {
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ControlledProvider provider = new()
        {
            Reopening = async (context, binding, token) =>
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(token);
                return binding with { ContinuationState = "late-head" };
            },
        };
        var (store, service, item) = await ExecutionAsync(provider);
        Task resolving = service.ResolveRecoveryAsync(Decision(item), CancellationToken.None).AsTask();
        Assert.Same(entered.Task, await Task.WhenAny(entered.Task, resolving).WaitAsync(TimeSpan.FromSeconds(5)));
        await service.ResolveRecoveryAsync(Decision(item, RecoveryDecisionKind.CloseSession), CancellationToken.None);
        release.TrySetResult();
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => resolving);
        Assert.Equal(ErrorCode.RevisionConflict, failure.Failure.Code);
        Assert.Equal(item.Binding, store.FindSession(item.SessionId)?.Binding);
        Assert.Single(store.Reservations);
        Assert.Equal(SessionLifecycle.Closing, store.FindSession(item.SessionId)?.Lifecycle);
    }

    /// <summary>An unknown target is an explicit NotFound, not invented unsupported native evidence.</summary>
    [Fact]
    public async Task UnknownInspectTargetIsNotFound()
    {
        var (_, service, _) = await ExecutionAsync(new());
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() =>
            service.InspectRecoveryAsync(new(RecoveryTargetKind.SessionOpening, Guid.NewGuid()), CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.NotFound, failure.Failure.Code);
    }
}
