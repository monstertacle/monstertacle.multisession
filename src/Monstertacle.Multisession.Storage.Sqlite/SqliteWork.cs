using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal interface ISqliteWork
{
    void Run(SqliteConnection connection, bool available);
}

internal sealed class SqliteWork<T> : ISqliteWork
{
    private readonly Func<SqliteConnection, T> _action;
    private readonly CancellationToken _token;
    private readonly bool _admission;
    private readonly TaskCompletionSource<T> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenRegistration _registration;
    private const int _waiting = 0;
    private const int _running = 1;
    private const int _finished = 2;
    private int _state;

    internal SqliteWork(Func<SqliteConnection, T> action, bool admission, CancellationToken token)
    {
        _action = action;
        _token = token;
        _admission = admission;
        _registration = token.Register(static item => ((SqliteWork<T>)item!).CancelWaiting(), this);
        if (_completion.Task.IsCompleted) { _registration.Unregister(); }
    }

    internal Task<T> Completion => _completion.Task;

    internal void Reject(ErrorCode code)
    {
        if (Interlocked.CompareExchange(ref _state, _finished, _waiting) == _waiting)
        {
            _completion.TrySetException(StorageFailurePolicy.Error(code, true, _admission));
        }
        _registration.Unregister();
    }

    private void CancelWaiting()
    {
        if (Interlocked.CompareExchange(ref _state, _finished, _waiting) == _waiting)
        {
            _completion.TrySetException(StorageFailurePolicy.Normalize(new OperationCanceledException(_token), true, _admission, _token));
        }
    }

    public void Run(SqliteConnection connection, bool available)
    {
        if (Interlocked.CompareExchange(ref _state, _running, _waiting) != _waiting) { _registration.Unregister(); return; }
        try
        {
            if (!available) { throw StorageFailurePolicy.Error(ErrorCode.StorageUnavailable, true, _admission); }
            if (_token.IsCancellationRequested) { throw StorageFailurePolicy.Normalize(new OperationCanceledException(_token), true, _admission, _token); }
            _completion.TrySetResult(_action(connection));
        }
        catch (Exception failure)
        {
            // Domain transaction code carries any proven rollback/commit certainty.
            _completion.TrySetException(failure is SessionOperationException or OperationCanceledException
                ? failure : StorageFailurePolicy.Normalize(failure, false, _admission, _token));
        }
        finally { Volatile.Write(ref _state, _finished); _registration.Unregister(); }
    }
}
