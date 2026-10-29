using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Storage.Sqlite;

/// <summary>Explicit initialization or reopening of one supported, privately owned SQLite store.</summary>
/// <remarks>The host retains ownership of the lease and must keep it held until the returned store is disposed.</remarks>
public static class SqliteStoreFactory
{
    /// <summary>Initializes an explicitly new store; existing files must never be overwritten.</summary>
    public static ValueTask<ISessionStore> CreateNewAsync(IStateDirectoryLease lease, Guid trustScopeId, RuntimeOptions options, CancellationToken cancellationToken)
        => OpenAsync(lease, trustScopeId, options, StateFileMode.CreateNew, cancellationToken);

    /// <summary>Opens an existing supported store without creating a missing replacement or migrating its format.</summary>
    public static ValueTask<ISessionStore> OpenExistingAsync(IStateDirectoryLease lease, Guid trustScopeId, RuntimeOptions options, CancellationToken cancellationToken)
        => OpenAsync(lease, trustScopeId, options, StateFileMode.OpenExisting, cancellationToken);

    private static async ValueTask<ISessionStore> OpenAsync(IStateDirectoryLease lease, Guid trustScopeId, RuntimeOptions options, StateFileMode mode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        if (options.Validate() is not null || trustScopeId == Guid.Empty || !lease.IsHeld || !lease.Matches(options.StateDirectory))
        {
            throw new SessionOperationException(new(ErrorCode.InvalidConfiguration));
        }
        try
        {
            await lease.PrepareFileAsync(SqliteConnectionPolicy.DatabaseFileName, mode, cancellationToken);
            string path = Path.Combine(lease.StateDirectory, SqliteConnectionPolicy.DatabaseFileName);
            if (mode == StateFileMode.OpenExisting) { await SqliteStorePreflight.ValidateAsync(path, lease, trustScopeId, cancellationToken); }
            return await Task.Run(() => OpenValidated(path, lease, trustScopeId, mode, cancellationToken), cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (SessionOperationException failure) { throw new SessionOperationException(failure.Failure); }
        catch (Exception) { throw new SessionOperationException(new(ErrorCode.StorageUnavailable)); }
    }

    private static SqliteSessionStore OpenValidated(string path, IStateDirectoryLease lease, Guid trustScopeId, StateFileMode mode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!lease.IsHeld) { throw new SessionOperationException(new(ErrorCode.InvalidConfiguration)); }
        SqliteConnection connection = SqliteConnectionPolicy.Open(path, SqliteOpenMode.ReadWrite);
        try
        {
            SqliteConnectionPolicy.Configure(connection);
            if (mode == StateFileMode.CreateNew) { SqliteSchema.Initialize(connection, trustScopeId, DateTimeOffset.UtcNow); }
            return new SqliteSessionStore(connection);
        }
        catch { connection.Dispose(); throw; }
    }
}
