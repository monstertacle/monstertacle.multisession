using Microsoft.Data.Sqlite;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class SqliteConnectionPolicy
{
    internal const int BusySeconds = 1;
    private const int _busyMilliseconds = 1000;
    internal const string DatabaseFileName = "multisession.db";

    internal static SqliteConnection Open(string path, SqliteOpenMode mode)
    {
        SqliteConnection connection = new(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = mode,
            Pooling = false,
            DefaultTimeout = BusySeconds,
        }.ToString());
        try { connection.Open(); return connection; }
        catch { connection.Dispose(); throw; }
    }

    internal static void Configure(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"PRAGMA journal_mode=DELETE; PRAGMA synchronous=EXTRA; PRAGMA foreign_keys=ON; PRAGMA busy_timeout={_busyMilliseconds};";
        command.ExecuteNonQuery();
        if (!Equals(Scalar(connection, "PRAGMA journal_mode"), "delete")
            || !Equals(Scalar(connection, "PRAGMA synchronous"), 3L)
            || !Equals(Scalar(connection, "PRAGMA foreign_keys"), 1L)
            || !Equals(Scalar(connection, "PRAGMA busy_timeout"), (long)_busyMilliseconds))
        {
            throw new InvalidOperationException("Required SQLite durability settings are unavailable.");
        }
    }

    internal static object? Scalar(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }
}
