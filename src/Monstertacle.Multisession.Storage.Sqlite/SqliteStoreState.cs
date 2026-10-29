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

    internal void Start(SqliteConnection connection, IStateDirectoryLease lease, CancellationToken token)
    {
        if (!ReferenceEquals(lease, _lease) || !lease.IsHeld) { throw StorageFailurePolicy.Error(ErrorCode.InvalidConfiguration, true, false); }
        if (_started) { throw StorageFailurePolicy.Error(ErrorCode.StoreAlreadyOwned, true, false); }
        RecoveryTransactions.Prepare(connection, this, token);
        _started = true;
        _accepting = true;
    }

    internal void ValidateRuntimeOptions(RuntimeOptions options)
    {
        if (_started) { throw StorageFailurePolicy.Error(ErrorCode.StoreAlreadyOwned, true, false); }
        if (!_lease.IsHeld || !_lease.Matches(options.StateDirectory) || (options with { StateDirectory = Options.StateDirectory }) != Options)
        {
            throw StorageFailurePolicy.Error(ErrorCode.InvalidConfiguration, true, false);
        }
    }

    internal void StopAcceptance() => _accepting = false;
    internal void RequireAcceptance()
    {
        if (!_started) { throw StorageFailurePolicy.Error(ErrorCode.InvalidConfiguration, true, true); }
        if (!_accepting) { throw StorageFailurePolicy.Error(ErrorCode.SessionClosed, true, true); }
    }
}
