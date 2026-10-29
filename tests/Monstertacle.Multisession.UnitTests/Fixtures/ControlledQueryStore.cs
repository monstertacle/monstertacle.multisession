using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.UnitTests.Fixtures;

internal abstract class ControlledQueryStore : ISessionQueryStore
{
    protected abstract ControlledStoreState State { get; }

    async ValueTask<ReservedDispatch?> ISessionQueryStore.GetReservedDispatchAsync(Guid dispatchId, CancellationToken cancellationToken)
    {
        if (State.Dispatch.Reading is not null) { await State.Dispatch.Reading(cancellationToken); }
        cancellationToken.ThrowIfCancellationRequested();
        lock (State.Gate)
        {
            if (!State.Reservations.TryGetValue(dispatchId, out ExecutionReservation? reservation)
                || State.Requests[reservation.RequestId].State != RequestState.Running
                || State.Dispatch.IsCancellationRequested(reservation.RequestId)) { return null; }
            return State.Dispatch.Snapshots.GetValueOrDefault(dispatchId);
        }
    }

    ValueTask<SessionSnapshot?> ISessionQueryStore.GetSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (State.Gate) { return ValueTask.FromResult(State.Sessions.GetValueOrDefault(sessionId)); }
    }
    ValueTask<RequestSnapshot?> ISessionQueryStore.GetRequestAsync(Guid requestId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (State.Gate) { return ValueTask.FromResult(State.Requests.GetValueOrDefault(requestId)); }
    }
    ValueTask<RecoveryCaseSnapshot?> ISessionQueryStore.GetRecoveryCaseAsync(RecoveryTarget target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (State.Gate) { return ValueTask.FromResult(State.Recovery.Read(target)); }
    }
    ValueTask<Page<RecoveryCaseSnapshot>> ISessionQueryStore.ListRecoveryCasesAsync(int pageSize, string? cursor, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (State.Gate) { return ValueTask.FromResult(new Page<RecoveryCaseSnapshot>(State.Recovery.OpenCases.Take(pageSize).ToArray(), null)); }
    }
    ValueTask<Page<SessionSnapshot>> ISessionQueryStore.ListSessionsAsync(int pageSize, string? cursor, CancellationToken cancellationToken)
        => throw new NotImplementedException();
    ValueTask<Page<HistoryEntry>> ISessionQueryStore.ReadHistoryAsync(Guid sessionId, int pageSize, string? cursor, CancellationToken cancellationToken)
        => throw new NotImplementedException();
    ValueTask<Page<RequestSnapshot>> ISessionQueryStore.ListRequestsAsync(Guid sessionId, int pageSize, string? cursor, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
