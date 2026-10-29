using Microsoft.Data.Sqlite;

namespace Monstertacle.Multisession.IntegrationTests.Fixtures;

// Direct inspection/tampering is confined to our isolated fixture, outside the product API.
internal static class StoreProbe
{
    public static object? Scalar(string path, string sql)
    {
        using SqliteConnection connection = Open(path, SqliteOpenMode.ReadOnly);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    public static void Execute(string path, string sql, params (string Name, object Value)[] parameters)
    {
        using SqliteConnection connection = Open(path, SqliteOpenMode.ReadWrite);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters) { command.Parameters.AddWithValue(name, value); }
        command.ExecuteNonQuery();
    }

    private static SqliteConnection Open(string path, SqliteOpenMode mode)
    {
        SqliteConnection connection = new(new SqliteConnectionStringBuilder { DataSource = path, Mode = mode, Pooling = false }.ToString());
        connection.Open();
        return connection;
    }
}
