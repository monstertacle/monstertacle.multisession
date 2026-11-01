using Microsoft.Data.Sqlite;

namespace Monstertacle.Multisession.DeliveryCheckpoints;

/// <summary>The exclusive lease on <c>&lt;state&gt;.lock</c> and the private SQLite connection it protects.</summary>
internal sealed class CheckpointFile : IDisposable
{
    private const string _leaseSuffix = ".lock";
    private readonly FileStream _lease;

    private CheckpointFile(FileStream lease, SqliteConnection connection)
    {
        _lease = lease;
        Connection = connection;
    }

    public SqliteConnection Connection { get; }

    public static CheckpointFile Open(DeliveryCheckpointOptions options, bool owner)
    {
        if (!owner && !File.Exists(options.StatePath))
        {
            throw new DeliveryRefusalException(DeliveryRefusal.CheckpointMissing);
        }
        if (owner)
        {
            PrivateState.PrepareDirectory(Path.GetDirectoryName(options.StatePath)!);
        }
        var lease = PrivateState.OpenLease(options.StatePath + _leaseSuffix);
        SqliteConnection? connection = null;
        try
        {
            if (owner)
            {
                PrivateState.CreateDatabase(options.StatePath);
            }
            connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = options.StatePath,
                Mode = SqliteOpenMode.ReadWrite,
                Pooling = false,
                DefaultTimeout = (int)Math.Ceiling(options.BusyTimeout.TotalSeconds),
            }.ToString());
            connection.Open();
            return new CheckpointFile(lease, connection);
        }
        catch
        {
            // A refused open releases connection and lease at once, so the same process can open another route.
            connection?.Dispose();
            lease.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        Connection.Dispose();
        _lease.Dispose();
    }
}
