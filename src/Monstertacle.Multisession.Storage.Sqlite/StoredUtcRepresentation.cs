using Microsoft.Data.Sqlite;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class StoredUtcRepresentation
{
    internal static bool Matches(SqliteDataReader reader, int ordinal, DateTimeOffset? timestamp)
        => timestamp is null ? reader.IsDBNull(ordinal)
            : timestamp.Value.Offset == TimeSpan.Zero && !reader.IsDBNull(ordinal) && reader.GetString(ordinal) == StoreSerialization.Utc(timestamp.Value);
}
