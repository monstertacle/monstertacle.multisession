using Microsoft.Data.Sqlite;

namespace Monstertacle.Multisession.IntegrationTests.Fixtures;

// An intentional competing write transaction in our controlled store, not a production owner.
internal sealed class DatabaseWriteBlock : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction _transaction;
    public DatabaseWriteBlock(string path)
    {
        _connection = new(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWrite, Pooling = false, DefaultTimeout = 1 }.ToString());
        _connection.Open();
        _transaction = _connection.BeginTransaction();
    }
    public void Dispose()
    {
        _transaction.Dispose();
        _connection.Dispose();
    }
}
