using Microsoft.Data.Sqlite;

namespace Monstertacle.Multisession.Delivery;

/// <summary>
/// The current on-disk format, retained canonically: table definitions, phase text and format version exactly as the
/// existing checkpoints store them. Any other structure is refused; there is no converter and no alternate reader.
/// </summary>
internal static class CheckpointFormat
{
    public const long Version = 1;
    public const string CheckpointTable = "checkpoint(id INTEGER PRIMARY KEY CHECK(id=1),schema INTEGER NOT NULL,instance TEXT NOT NULL,project TEXT NOT NULL,route TEXT NOT NULL,cursor INTEGER NOT NULL,pending TEXT,phase TEXT,attempts INTEGER NOT NULL DEFAULT 0,receipt TEXT)";
    public const string AttemptJournal = "preparation_rearms(request_id TEXT PRIMARY KEY,configuration TEXT NOT NULL,route TEXT NOT NULL,cursor INTEGER NOT NULL,pending_id TEXT NOT NULL,pending_digest TEXT NOT NULL,attempts INTEGER NOT NULL,receipt TEXT NOT NULL)";
    public const string ResendJournal = "delivery_replays(request_id TEXT PRIMARY KEY,pending_id TEXT NOT NULL UNIQUE,receipt TEXT NOT NULL)";
    public const string RouteJournal = "delivery_rebinds(request_id TEXT PRIMARY KEY,old_route TEXT NOT NULL,new_route TEXT NOT NULL,cursor INTEGER NOT NULL,pending_id TEXT,receipt TEXT NOT NULL)";
    private const string _tableStatement = "CREATE TABLE ";
    private const string _utf8Encoding = "UTF-8";

    private static readonly Dictionary<string, string> _tables = new(StringComparer.Ordinal)
    {
        ["checkpoint"] = CheckpointTable,
        ["preparation_rearms"] = AttemptJournal,
        ["delivery_replays"] = ResendJournal,
        ["delivery_rebinds"] = RouteJournal,
    };

    private static readonly HashSet<string> _implicitIndexes = new(StringComparer.Ordinal)
    {
        "sqlite_autoindex_preparation_rearms_1",
        "sqlite_autoindex_delivery_replays_1",
        "sqlite_autoindex_delivery_replays_2",
        "sqlite_autoindex_delivery_rebinds_1",
    };

    private static readonly Dictionary<string, DeliveryPhase> _phases = new(StringComparer.Ordinal)
    {
        ["pending"] = DeliveryPhase.Pending,
        ["sending"] = DeliveryPhase.Sending,
        ["uncertain"] = DeliveryPhase.Uncertain,
        ["accepted"] = DeliveryPhase.Accepted,
    };

    /// <summary>The idempotent creation statement used for a table of the current format.</summary>
    public static string CreateIfMissing(string table) => "CREATE TABLE IF NOT EXISTS " + table;

    public static string Text(DeliveryPhase phase) => _phases.Single(entry => entry.Value == phase).Key;

    public static DeliveryPhase? Phase(string? text) =>
        text is null ? null : _phases.TryGetValue(text, out var phase) ? phase : throw Corrupt();

    public static DeliveryRefusalException Corrupt() => new(DeliveryRefusal.CheckpointCorrupt);

    /// <summary>Refuses any schema object or definition that is not part of the current format.</summary>
    public static async Task VerifyAsync(SqliteConnection connection, bool checkpointRequired, CancellationToken cancellationToken)
    {
        using var encoding = connection.CreateCommand();
        encoding.CommandText = "PRAGMA encoding";
        if (await encoding.ExecuteScalarAsync(cancellationToken) as string != _utf8Encoding)
        {
            throw Corrupt();
        }
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT type, name, sql FROM sqlite_master";
        var checkpointFound = false;
        using var rows = await command.ExecuteReaderAsync(cancellationToken);
        while (await rows.ReadAsync(cancellationToken))
        {
            var name = rows.GetString(1);
            var sql = rows.IsDBNull(2) ? null : rows.GetString(2);
            if (rows.GetString(0) == "table" && _tables.TryGetValue(name, out var definition) && sql == _tableStatement + definition)
            {
                checkpointFound |= name == "checkpoint";
            }
            else if (!(rows.GetString(0) == "index" && sql is null && _implicitIndexes.Contains(name)))
            {
                throw Corrupt();
            }
        }
        if (checkpointRequired && !checkpointFound)
        {
            throw Corrupt();
        }
    }
}
