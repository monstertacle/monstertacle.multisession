using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal sealed class SqliteStoreState
{
    private readonly IStateDirectoryLease _lease;
    private bool _started;
    private bool _accepting;
    internal SqliteWorkQueue Queue { get; }
    internal RuntimeOptions Options { get; }
    internal Guid StoreId { get; }
    internal TimeProvider Clock { get; } = TimeProvider.System;
    internal bool IsStarted => _started;
    internal bool CanDispatch
    {
        get
        {
            if (!_started) { throw StorageFailurePolicy.Error(ErrorCode.InvalidConfiguration, true, false); }
            return _accepting;
        }
    }

    internal SqliteStoreState(SqliteConnection connection, IStateDirectoryLease lease, RuntimeOptions options)
    {
        _lease = lease;
        Options = options;
        StoreId = Guid.ParseExact((string)SqliteConnectionPolicy.Scalar(connection, "SELECT StoreId FROM Metadata")!, "D");
        Queue = new(connection, options.DbWorkQueueCapacity, lease);
    }

    internal void Start(SqliteConnection connection, IStateDirectoryLease lease)
    {
        if (!ReferenceEquals(lease, _lease) || !lease.IsHeld) { throw StorageFailurePolicy.Error(ErrorCode.InvalidConfiguration, true, false); }
        if (_started) { throw StorageFailurePolicy.Error(ErrorCode.StoreAlreadyOwned, true, false); }
        // Until the explicit recovery transaction is implemented, uncertain old work cannot start.
        using SqliteCommand uncertain = SqliteStatements.Create(connection, null,
            "SELECT (SELECT count(*) FROM Sessions WHERE Lifecycle=$opening)+(SELECT count(*) FROM Requests WHERE State=$running)",
            ("$opening", (int)SessionLifecycle.Opening), ("$running", (int)RequestState.Running));
        if (!Equals(uncertain.ExecuteScalar(), 0L))
        {
            throw StorageFailurePolicy.Error(ErrorCode.StorageUnavailable, true, false);
        }
        _started = true;
        _accepting = true;
    }

    internal void StopAcceptance() => _accepting = false;
    internal void RequireAcceptance()
    {
        if (!_started) { throw StorageFailurePolicy.Error(ErrorCode.InvalidConfiguration, true, true); }
        if (!_accepting) { throw StorageFailurePolicy.Error(ErrorCode.SessionClosed, true, true); }
    }
}
