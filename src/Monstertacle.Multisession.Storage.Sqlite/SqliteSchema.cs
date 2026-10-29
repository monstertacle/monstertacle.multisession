using System.Globalization;
using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class SqliteSchema
{
    internal const int Version = 1;
    internal const int ApplicationId = 0x4D535331; // MSS1, distinct from SQLite's generic file signature.
    private const int _migrationVersion = 0;
    private const int _bindingEncodingVersion = 1;
    private const string _resourceName = "Monstertacle.Multisession.SchemaV1.sql";

    internal static void Initialize(SqliteConnection connection, Guid trustScopeId, DateTimeOffset createdUtc)
    {
        using SqliteTransaction transaction = connection.BeginTransaction();
        CreateTables(connection, transaction);
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            PRAGMA application_id={ApplicationId};
            PRAGMA user_version={Version};
            INSERT INTO Metadata (Singleton,SchemaVersion,MigrationVersion,BindingEncodingVersion,StoreId,InstanceTrustScopeId,CreatedUtc)
            VALUES (1,$schema,$migration,$binding,$store,$scope,$created);
            """;
        command.Parameters.AddWithValue("$schema", Version);
        command.Parameters.AddWithValue("$migration", _migrationVersion);
        command.Parameters.AddWithValue("$binding", _bindingEncodingVersion);
        command.Parameters.AddWithValue("$store", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("$scope", trustScopeId.ToString("D"));
        command.Parameters.AddWithValue("$created", createdUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    internal static void Validate(SqliteConnection connection, Guid trustScopeId)
    {
        using SqliteConnection expected = SqliteConnectionPolicy.Open(":memory:", SqliteOpenMode.Memory);
        CreateTables(expected, null);
        if (!ReadSchema(connection).SequenceEqual(ReadSchema(expected))) { throw Unsupported(); }
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT SchemaVersion,MigrationVersion,BindingEncodingVersion,StoreId,InstanceTrustScopeId,CreatedUtc FROM Metadata WHERE Singleton=1";
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read() || reader.GetInt32(0) != Version || reader.GetInt32(1) != _migrationVersion
            || reader.GetInt32(2) != _bindingEncodingVersion
            || !Guid.TryParseExact(reader.GetString(3), "D", out Guid storeId) || storeId == Guid.Empty
            || !Guid.TryParseExact(reader.GetString(4), "D", out Guid storedScope) || storedScope != trustScopeId
            || !DateTimeOffset.TryParseExact(reader.GetString(5), "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset created)
            || created.Offset != TimeSpan.Zero || reader.Read())
        {
            throw Unsupported();
        }
        if (!Equals(SqliteConnectionPolicy.Scalar(connection, "PRAGMA integrity_check"), "ok")) { throw Unsupported(); }
    }

    private static void CreateTables(SqliteConnection connection, SqliteTransaction? transaction)
    {
        using Stream stream = typeof(SqliteSchema).Assembly.GetManifestResourceStream(_resourceName)
            ?? throw new InvalidOperationException("Store schema resource is unavailable.");
        using StreamReader reader = new(stream);
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
        command.ExecuteNonQuery();
    }

    private static List<(string Type, string Name, string Table, string? Sql)> ReadSchema(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT type,name,tbl_name,sql FROM sqlite_schema WHERE substr(name,1,7)<>'sqlite_' ORDER BY type COLLATE BINARY,name COLLATE BINARY";
        using SqliteDataReader reader = command.ExecuteReader();
        List<(string, string, string, string?)> entries = [];
        while (reader.Read()) { entries.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3))); }
        return entries;
    }

    internal static SessionOperationException Unsupported() => new(new PublicError(ErrorCode.UnsupportedStoreVersion));
}
