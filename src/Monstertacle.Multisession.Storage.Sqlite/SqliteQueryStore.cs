using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Storage.Sqlite;

// Serial read delegation; dispatch/history/recovery ports remain separate behavioral cycles.
internal abstract class SqliteQueryStore(SqliteStoreState state) : ISessionQueryStore, IStoreBootstrap, IRequestCancellationStore, ISessionClosureStore, IAsyncDisposable
{
    protected SqliteStoreState State { get; } = state;

    ValueTask IStoreBootstrap.StartAsync(IStateDirectoryLease lease, CancellationToken cancellationToken)
        => State.Queue.WriteAsync(connection => State.Start(connection, lease), cancellationToken);
    ValueTask IStoreBootstrap.StopAcceptanceAsync(CancellationToken cancellationToken)
        => State.Queue.WriteAsync(_ => State.StopAcceptance(), cancellationToken);
    ValueTask<ReservedDispatch?> ISessionQueryStore.GetReservedDispatchAsync(Guid dispatchId, CancellationToken cancellationToken)
        => new(State.Queue.ExecuteAsync(connection => State.IsStarted ? StoredDispatchAccess.Read(connection, dispatchId)?.Snapshot : null, cancellationToken));
    ValueTask<SessionSnapshot?> ISessionQueryStore.GetSessionAsync(Guid sessionId, CancellationToken cancellationToken)
        => new(State.Queue.ExecuteAsync(connection => QueryTransactions.Session(connection, sessionId), cancellationToken));
    ValueTask<Page<SessionSnapshot>> ISessionQueryStore.ListSessionsAsync(int pageSize, string? cursor, CancellationToken cancellationToken)
        => new(State.Queue.ExecuteAsync(connection => SqlitePageQueries.Sessions(connection, State.StoreId, pageSize, cursor), cancellationToken));
    ValueTask<Page<HistoryEntry>> ISessionQueryStore.ReadHistoryAsync(Guid sessionId, int pageSize, string? cursor, CancellationToken cancellationToken)
        => new(State.Queue.ExecuteAsync(connection => SqlitePageQueries.History(connection, State.StoreId, sessionId, pageSize, cursor), cancellationToken));
    ValueTask<RequestSnapshot?> ISessionQueryStore.GetRequestAsync(Guid requestId, CancellationToken cancellationToken)
        => new(State.Queue.ExecuteAsync(connection => QueryTransactions.Request(connection, requestId), cancellationToken));
    ValueTask<Page<RequestSnapshot>> ISessionQueryStore.ListRequestsAsync(Guid sessionId, int pageSize, string? cursor, CancellationToken cancellationToken)
        => new(State.Queue.ExecuteAsync(connection => SqlitePageQueries.Requests(connection, State.StoreId, sessionId, pageSize, cursor), cancellationToken));
    ValueTask<RecoveryCaseSnapshot?> ISessionQueryStore.GetRecoveryCaseAsync(RecoveryTarget target, CancellationToken cancellationToken)
        => new(State.Queue.ExecuteAsync(connection => RecoveryCases.Read(connection, target), cancellationToken));
    ValueTask<Page<RecoveryCaseSnapshot>> ISessionQueryStore.ListRecoveryCasesAsync(int pageSize, string? cursor, CancellationToken cancellationToken)
        => new(State.Queue.ExecuteAsync(connection => SqlitePageQueries.Recovery(connection, State.StoreId, pageSize, cursor), cancellationToken));

    ValueTask<RequestSnapshot> IRequestCancellationStore.RequestCancellationAsync(Guid requestId, CancellationToken cancellationToken)
        => new(State.Queue.ExecuteAsync(connection => CancellationTransactions.Request(connection, State, requestId, cancellationToken), cancellationToken));
    ValueTask IRequestCancellationStore.CancelQueuedSessionAsync(Guid sessionId, CancellationToken cancellationToken)
        => State.Queue.WriteAsync(connection => CancellationTransactions.CancelQueued(connection, State, sessionId, cancellationToken), cancellationToken);

    ValueTask<SessionSnapshot> ISessionClosureStore.BeginSessionCloseAsync(Guid sessionId, CancellationToken cancellationToken)
        => new(State.Queue.ExecuteAsync(connection => SessionCloseTransactions.Begin(connection, State, sessionId, cancellationToken), cancellationToken));
    ValueTask<SessionSnapshot> ISessionClosureStore.RefreshSessionCloseAsync(Guid sessionId, CancellationToken cancellationToken)
        => new(State.Queue.ExecuteAsync(connection => SessionCloseTransactions.Refresh(connection, State, sessionId, cancellationToken), cancellationToken));

    public ValueTask DisposeAsync() => State.Queue.DisposeAsync();
}
