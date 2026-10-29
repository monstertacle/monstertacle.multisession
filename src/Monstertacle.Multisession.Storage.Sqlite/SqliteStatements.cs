using Microsoft.Data.Sqlite;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class SqliteStatements
{
    internal static SqliteCommand Create(SqliteConnection connection, SqliteTransaction? transaction, string sql, params (string Name, object? Value)[] parameters)
    {
        SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters) { command.Parameters.AddWithValue(name, value ?? DBNull.Value); }
        return command;
    }
}
