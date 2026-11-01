using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace Monstertacle.Multisession.Delivery.FixtureHarness;

/// <summary>Merges the write-ahead log of a private fixture copy and records every row as the logical oracle.</summary>
internal static class CheckpointSnapshot
{
    private static readonly string[] Tables = ["checkpoint", "preparation_rearms", "delivery_replays", "delivery_rebinds"];

    public static int Write(string databasePath, string outputPath)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadWrite, Pooling = false };
        var dump = new JsonObject();
        using (var connection = new SqliteConnection(builder.ToString()))
        {
            connection.Open();
            using var checkpoint = connection.CreateCommand();
            checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)";
            checkpoint.ExecuteNonQuery();
            foreach (var table in Tables)
            {
                dump[table] = ReadTable(connection, table);
            }
            dump["schema"] = ReadSchema(connection);
        }
        File.WriteAllText(outputPath, dump.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        return 0;
    }

    private static JsonArray? ReadTable(SqliteConnection connection, string table)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name";
        command.Parameters.AddWithValue("$name", table);
        if (Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) == 0)
        {
            return null;
        }
        // Table names come from the fixed list above, never from input.
        command.CommandText = "SELECT * FROM " + table + " ORDER BY rowid";
        using var rows = command.ExecuteReader();
        var result = new JsonArray();
        while (rows.Read())
        {
            var row = new JsonObject();
            for (var column = 0; column < rows.FieldCount; column++)
            {
                row[rows.GetName(column)] = rows.IsDBNull(column) ? null
                    : rows.GetFieldType(column) == typeof(long) ? JsonValue.Create(rows.GetInt64(column)) : JsonValue.Create(rows.GetString(column));
            }
            result.Add(row);
        }
        return result;
    }

    private static JsonArray ReadSchema(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name, sql FROM sqlite_master ORDER BY name";
        using var rows = command.ExecuteReader();
        var result = new JsonArray();
        while (rows.Read())
        {
            result.Add(new JsonObject { ["name"] = rows.GetString(0), ["sql"] = rows.IsDBNull(1) ? null : rows.GetString(1) });
        }
        return result;
    }
}
