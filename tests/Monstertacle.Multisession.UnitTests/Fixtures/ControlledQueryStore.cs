using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Queries;

namespace Monstertacle.Multisession.UnitTests.Fixtures;

internal abstract class ControlledQueryStore : ControlledStoreLifetime, ISessionQueryStore, IRequestCancellationStore, ISessionClosureStore, ISessionRetentionStore, ISessionStatisticsStore
{
    public Func<CancellationToken, ValueTask<RuntimeStatistics>>? ReadingStatistics { get; init; }
    ValueTask<RuntimeStatistics> ISessionStatisticsStore.GetRuntimeStatisticsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ReadingStatistics is not null) { return ReadingStatistics(cancellationToken); }
        lock (State.Gate)
        {
            return ValueTask.FromResult(new RuntimeStatistics(State.Requests.Values.Count(item => item.State == RequestState.Queued),
                State.Reservations.Count, State.Reservations.Values.Count(item => State.Requests[item.RequestId].Certainty == ExecutionCertainty.Unknown)));
        }
    }

    ValueTask<RequestSnapshot> IRequestCancellationStore.RequestCancellationAsync(Guid requestId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (State.Gate)
        {
            return ValueTask.FromResult(ControlledRequestCancellation.Apply(State, requestId));
        }
    }
    ValueTask IRequestCancellationStore.CancelQueuedSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (State.Gate)
        {
            foreach (RequestSnapshot request in State.Requests.Values.Where(item => item.SessionId == sessionId && item.State == RequestState.Queued).ToArray())
            {
                ControlledRequestCancellation.Apply(State, request.RequestId);
            }
        }
        return ValueTask.CompletedTask;
    }


    ValueTask<SessionSnapshot> ISessionClosureStore.BeginSessionCloseAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (State.Gate) { return ValueTask.FromResult(ControlledSessionClosure.Begin(State, sessionId)); }
    }
    ValueTask<SessionSnapshot> ISessionClosureStore.RefreshSessionCloseAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (State.Gate) { return ValueTask.FromResult(ControlledSessionClosure.Refresh(State, sessionId)); }
    }

    ValueTask ISessionRetentionStore.DeleteClosedSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (State.Gate) { ControlledSessionDeletion.Apply(State, sessionId); }
        return ValueTask.CompletedTask;
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
