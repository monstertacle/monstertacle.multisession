using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Recovery;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.ContractTests.Fixtures;

internal sealed record OperationRecoveryScenario(InMemorySessionStore Store, RecoveryService Service, RecoveryCaseSnapshot Case)
{
    internal RecoveryDecision Decision(RecoveryDecisionKind kind = RecoveryDecisionKind.ConfirmEnded)
        => new(Guid.NewGuid(), Case.Target, Case.Revision, kind, "known operation investigated", "fixture evidence");
    internal static async Task<OperationRecoveryScenario> CreateAsync(ISessionProvider provider, bool recordOperation = true, bool lazy = false)
    {
        InMemorySessionStore store = new();
        SessionSnapshot session = SchedulingData.Session();
        if (lazy) { session = session with { Binding = session.Binding! with { SessionReference = null, ContinuationState = null } }; }
        await store.CreateSessionAsync(session, CancellationToken.None);
        await store.CommitAcceptanceAsync(new(session.SessionId, "new input", TimeSpan.FromSeconds(10), null, false), 100, CancellationToken.None);
        ExecutionReservation? reservation = await store.TryReserveHeadAsync(4, CancellationToken.None);
        Assert.NotNull(reservation);
        if (lazy) { await store.CommitObservationAsync(reservation.DispatchId, new(ProviderObservationKind.SessionEstablished, "observed-first"), CancellationToken.None); }
        if (recordOperation) { await store.CommitObservationAsync(reservation.DispatchId, new(ProviderObservationKind.OperationAccepted, "new-operation"), CancellationToken.None); }
        await store.MarkUncertainAsync(reservation.DispatchId, new(ErrorCode.Interrupted), CancellationToken.None);
        RecoveryService service = new(store, [new(session.ProviderId, "test-adapter", 1, provider)], new() { StateDirectory = Path.GetTempPath(), Retention = new(true, null) });
        RecoveryCaseSnapshot? item = await service.GetRecoveryCaseAsync(new(RecoveryTargetKind.RequestExecution, reservation.RequestId), CancellationToken.None);
        Assert.NotNull(item);
        return new(store, service, item);
    }
}
