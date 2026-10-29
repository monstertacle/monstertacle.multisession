using System.Threading.Channels;
using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Storage.Sqlite;

// One logical worker owns every operation on the connection, including final disposal.
internal sealed class SqliteWorkQueue : IAsyncDisposable
{
    private readonly Channel<ISqliteWork> _work;
    private readonly Task _worker;
    private int _closed;

    internal SqliteWorkQueue(SqliteConnection connection, int capacity, IStateDirectoryLease lease)
    {
        _work = Channel.CreateBounded<ISqliteWork>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false,
        });
        _worker = Task.Run(async () =>
        {
            try
            {
                await foreach (ISqliteWork item in _work.Reader.ReadAllAsync())
                {
                    item.Run(connection, Volatile.Read(ref _closed) == 0 && lease.IsHeld);
                }
            }
            finally
            {
                Volatile.Write(ref _closed, 1);
                _work.Writer.TryComplete();
                while (_work.Reader.TryRead(out ISqliteWork? pending)) { pending.Run(connection, false); }
                connection.Dispose();
            }
        });
    }

    internal Task<T> ExecuteAsync<T>(Func<SqliteConnection, T> action, CancellationToken cancellationToken, bool admission = false)
    {
        SqliteWork<T> item = new(action, admission, cancellationToken);
        if (Volatile.Read(ref _closed) != 0) { item.Reject(ErrorCode.StorageUnavailable); }
        else if (!_work.Writer.TryWrite(item)) { item.Reject(ErrorCode.LocalCapacityExceeded); }
        return item.Completion;
    }

    internal ValueTask WriteAsync(Action<SqliteConnection> action, CancellationToken cancellationToken)
        => new(ExecuteAsync(connection => { action(connection); return true; }, cancellationToken));

    public ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref _closed, 1);
        _work.Writer.TryComplete();
        return new(_worker);
    }
}
