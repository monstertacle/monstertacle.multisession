using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Monstertacle.Multisession.Delivery.Tests;

/// <summary>
/// Rebuilds a checkpoint written by the current Listener release from its captured exact DDL and rows
/// (repository policy forbids committed SQLite files). Values are inserted unchanged as captured.
/// </summary>
internal sealed class ListenerFixture
{
    private static readonly string Directory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "CurrentListener");
    private readonly JsonElement _rows;

    private ListenerFixture(JsonElement rows) => _rows = rows;

    public static JsonElement Oracle { get; } = Load("oracle.json");

    public string Pending => Row("checkpoint", 0).GetProperty("pending").GetString()!;

    public DeliveryChannel Channel
    {
        get
        {
            var row = Row("checkpoint", 0);
            return new DeliveryChannel(row.GetProperty("instance").GetString()!, row.GetProperty("project").GetString()!, row.GetProperty("route").GetString()!);
        }
    }

    public static ListenerFixture Rebuild(string name, string databasePath)
    {
        var fixture = new ListenerFixture(Load(name + ".rows.json"));
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        using var connection = RawDatabase.Open(databasePath);
        Execute(connection, "PRAGMA journal_mode=WAL");
        foreach (var entry in fixture._rows.GetProperty("schema").EnumerateArray())
        {
            if (entry.GetProperty("sql").GetString() is { } ddl)
            {
                Execute(connection, ddl);
            }
        }
        foreach (var table in fixture._rows.EnumerateObject())
        {
            if (table.Name != "schema" && table.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var row in table.Value.EnumerateArray())
                {
                    Insert(connection, table.Name, row);
                }
            }
        }
        return fixture;
    }

    public JsonElement Row(string table, int index) => _rows.GetProperty(table)[index];

    public static string Output(string name) => Oracle.GetProperty("outputs").GetProperty(name).GetString()!;

    public static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    private static JsonElement Load(string file)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Directory, file)));
        return document.RootElement.Clone();
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void Insert(SqliteConnection connection, string table, JsonElement row)
    {
        var names = row.EnumerateObject().Select(column => column.Name).ToArray();
        using var command = connection.CreateCommand();
        // Table and column names come from the captured fixture schema, not from external input.
        command.CommandText = "INSERT INTO " + table + "(" + string.Join(',', names) + ") VALUES(" + string.Join(',', names.Select((_, index) => "$p" + index)) + ")";
        var position = 0;
        foreach (var column in row.EnumerateObject())
        {
            object value = column.Value.ValueKind switch
            {
                JsonValueKind.Null => DBNull.Value,
                JsonValueKind.Number => column.Value.GetInt64(),
                _ => column.Value.GetString()!,
            };
            command.Parameters.AddWithValue("$p" + position++, value);
        }
        command.ExecuteNonQuery();
    }
}
