using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Queries;
using Monstertacle.Multisession.Requests;

namespace Monstertacle.Multisession.UnitTests.Fixtures;

internal abstract class ControlledQueryStore : ISessionQueryStore, IStoreBootstrap, IRequestCancellationStore, IAsyncDisposable
{
    private bool _ownsState;
    private bool _disposed;
    protected abstract ControlledStoreState State { get; }
    public Func<ValueTask>? Disposing { get; init; }

    ValueTask IStoreBootstrap.StartAsync(IStateDirectoryLease lease, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (State.Gate)
        {
            if (_disposed || !lease.IsHeld) { throw new SessionOperationException(new PublicError(ErrorCode.InvalidConfiguration)); }
            if (State.Started) { throw new SessionOperationException(new PublicError(ErrorCode.StoreAlreadyOwned)); }
            ControlledStoreBootstrap.Recover(State);
            State.Started = true;
            State.Accepting = true;
            _ownsState = true;
        }
        return ValueTask.CompletedTask;
    }
    ValueTask IStoreBootstrap.StopAcceptanceAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (State.Gate) { State.Accepting = false; }
        return ValueTask.CompletedTask;
    }
    ValueTask<RequestSnapshot> IRequestCancellationStore.RequestCancellationAsync(Guid requestId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (State.Gate)
        {
            RequestSnapshot before = State.Requests.GetValueOrDefault(requestId)
                ?? throw new SessionOperationException(new(ErrorCode.NotFound, RequestId: requestId));
            RequestSnapshot updated = RequestCancellationPolicy.Apply(before, State.Clock.GetUtcNow());
            if (updated == before) { return ValueTask.FromResult(before); }
            State.Requests[requestId] = updated;
            if (before.State == RequestState.Queued)
            {
                ExecutionReservation local = new(Guid.NewGuid(), requestId, before.SessionId, null, updated.EndedUtc!.Value);
                State.Dispatch.RecordCompletion(new(local, State.Sessions[before.SessionId], State.Submissions[requestId], before.Sequence,
                    State.Sessions[before.SessionId].BindingRevision), updated.Result!);
            }
            else { State.Dispatch.RequestCancellation(requestId); }
            return ValueTask.FromResult(updated);
        }
    }


    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        if (Disposing is not null) { await Disposing(); }
        lock (State.Gate)
        {
            if (_ownsState) { State.Started = false; State.Accepting = false; _ownsState = false; }
            _disposed = true;
        }
    }

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
        lock (State.Gate)
        {
            return ValueTask.FromResult(ControlledPageProjection.Read(State.Recovery.OpenCases,
                item => new CursorKey(item.Target.Id, Utc: item.CreatedUtc, TargetKind: item.Target.Kind),
                new(State.StoreId, PageListKind.Recovery), pageSize, cursor));
        }
    }
    ValueTask<Page<SessionSnapshot>> ISessionQueryStore.ListSessionsAsync(int pageSize, string? cursor, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (State.Gate)
        {
            return ValueTask.FromResult(ControlledPageProjection.Read(State.Sessions.Values,
                item => new CursorKey(item.SessionId, Utc: item.CreatedUtc), new(State.StoreId, PageListKind.Sessions), pageSize, cursor));
        }
    }
    ValueTask<Page<HistoryEntry>> ISessionQueryStore.ReadHistoryAsync(Guid sessionId, int pageSize, string? cursor, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (State.Gate)
        {
            return ValueTask.FromResult(ControlledPageProjection.Read(State.Dispatch.History.Where(item => item.SessionId == sessionId),
                item => new CursorKey(item.HistoryId, item.HistoryOrdinal), new(State.StoreId, PageListKind.History, sessionId), pageSize, cursor));
        }
    }
    ValueTask<Page<RequestSnapshot>> ISessionQueryStore.ListRequestsAsync(Guid sessionId, int pageSize, string? cursor, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (State.Gate)
        {
            return ValueTask.FromResult(ControlledPageProjection.Read(State.Requests.Values.Where(item => item.SessionId == sessionId),
                item => new CursorKey(item.RequestId, item.Sequence), new(State.StoreId, PageListKind.Requests, sessionId), pageSize, cursor));
        }
    }
}
