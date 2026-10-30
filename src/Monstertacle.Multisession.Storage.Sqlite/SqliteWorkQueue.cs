using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Storage.Sqlite;

// One logical worker owns every operation on the connection, including final disposal.
internal sealed class SqliteWorkQueue : IAsyncDisposable
{
    private readonly BlockingCollection<ISqliteWork> _work;
    private readonly object _gate = new();
    private readonly Task _worker;
    private int _closed;

    internal SqliteWorkQueue(SqliteConnection connection, int capacity, IStateDirectoryLease lease)
    {
        _work = new(capacity);
        // SQLite is synchronous; its single owner must not occupy or wait for a shared host worker.
        _worker = Task.Factory.StartNew(() =>
        {
            try
            {
                foreach (ISqliteWork item in _work.GetConsumingEnumerable())
                {
                    item.Run(connection, Volatile.Read(ref _closed) == 0 && lease.IsHeld);
                }
            }
            finally
            {
                Close();
                while (_work.TryTake(out ISqliteWork? pending)) { pending.Run(connection, false); }
                try { connection.Dispose(); }
                finally { _work.Dispose(); }
            }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    internal Task<T> ExecuteAsync<T>(Func<SqliteConnection, T> action, CancellationToken cancellationToken, bool admission = false)
    {
        SqliteWork<T> item = new(action, admission, cancellationToken);
        lock (_gate)
        {
            if (_closed != 0) { item.Reject(ErrorCode.StorageUnavailable); }
            else if (!_work.TryAdd(item)) { item.Reject(ErrorCode.LocalCapacityExceeded); }
        }
        return item.Completion;
    }

    internal ValueTask WriteAsync(Action<SqliteConnection> action, CancellationToken cancellationToken)
        => new(ExecuteAsync(connection => { action(connection); return true; }, cancellationToken));

    public ValueTask DisposeAsync()
    {
        Close();
        return new(_worker);
    }

    private void Close()
    {
        lock (_gate)
        {
            if (_closed != 0) { return; }
            Volatile.Write(ref _closed, 1);
            _work.CompleteAdding();
        }
    }
}
