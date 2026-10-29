using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Storage.Sqlite;

// Format foundation only: domain reads/bootstrap require their subsequent behavioral cycles.
internal abstract class SqliteQueryStore(SqliteConnection connection) : ISessionQueryStore, IStoreBootstrap, IAsyncDisposable
{
    private readonly object _disposeGate = new();
    private Task? _disposal;

    ValueTask IStoreBootstrap.StartAsync(IStateDirectoryLease lease, CancellationToken cancellationToken) => throw new NotImplementedException();
    ValueTask IStoreBootstrap.StopAcceptanceAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
    ValueTask<ReservedDispatch?> ISessionQueryStore.GetReservedDispatchAsync(Guid dispatchId, CancellationToken cancellationToken) => throw new NotImplementedException();
    ValueTask<SessionSnapshot?> ISessionQueryStore.GetSessionAsync(Guid sessionId, CancellationToken cancellationToken) => throw new NotImplementedException();
    ValueTask<Page<SessionSnapshot>> ISessionQueryStore.ListSessionsAsync(int pageSize, string? cursor, CancellationToken cancellationToken) => throw new NotImplementedException();
    ValueTask<Page<HistoryEntry>> ISessionQueryStore.ReadHistoryAsync(Guid sessionId, int pageSize, string? cursor, CancellationToken cancellationToken) => throw new NotImplementedException();
    ValueTask<RequestSnapshot?> ISessionQueryStore.GetRequestAsync(Guid requestId, CancellationToken cancellationToken) => throw new NotImplementedException();
    ValueTask<Page<RequestSnapshot>> ISessionQueryStore.ListRequestsAsync(Guid sessionId, int pageSize, string? cursor, CancellationToken cancellationToken) => throw new NotImplementedException();
    ValueTask<RecoveryCaseSnapshot?> ISessionQueryStore.GetRecoveryCaseAsync(RecoveryTarget target, CancellationToken cancellationToken) => throw new NotImplementedException();
    ValueTask<Page<RecoveryCaseSnapshot>> ISessionQueryStore.ListRecoveryCasesAsync(int pageSize, string? cursor, CancellationToken cancellationToken) => throw new NotImplementedException();

    public ValueTask DisposeAsync()
    {
        lock (_disposeGate) { return new(_disposal ??= Task.Run(connection.Dispose)); }
    }
}
