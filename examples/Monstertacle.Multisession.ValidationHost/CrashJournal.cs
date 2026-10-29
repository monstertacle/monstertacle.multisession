using Microsoft.Data.Sqlite;

namespace Monstertacle.Multisession.ValidationHost;

// Shared, bounded test-only transaction: produces a real spilled rollback journal without committing.
internal sealed class CrashJournal(SqliteConnection connection, SqliteTransaction transaction) : IDisposable
{
    private const int _spillBytes = 1024 * 1024;
    internal static CrashJournal Begin(string path)
    {
        SqliteConnection connection = new(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false,
            DefaultTimeout = 1,
        }.ToString());
        SqliteTransaction? transaction = null;
        try
        {
            connection.Open();
            using SqliteCommand settings = connection.CreateCommand();
            settings.CommandText = "PRAGMA journal_mode=DELETE; PRAGMA synchronous=EXTRA; PRAGMA cache_size=1; PRAGMA cache_spill=ON;";
            settings.ExecuteNonQuery();
            transaction = connection.BeginTransaction();
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "CREATE TABLE UncommittedProbe (Value BLOB); INSERT INTO UncommittedProbe VALUES (zeroblob($bytes));";
            command.Parameters.AddWithValue("$bytes", _spillBytes);
            command.ExecuteNonQuery();
            return new(connection, transaction);
        }
        catch
        {
            transaction?.Dispose();
            connection.Dispose();
            throw;
        }
    }
    public void Dispose()
    {
        try { transaction.Dispose(); }
        finally { connection.Dispose(); }
    }
}
