using System.Globalization;
using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Queries;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class SqlitePageQueries
{
    internal static Page<SessionSnapshot> Sessions(SqliteConnection connection, Guid storeId, int size, string? cursor)
    {
        PageCursor.ValidateSize(size);
        PageScope scope = new(storeId, PageListKind.Sessions);
        CursorKey? last = PageCursor.Read(cursor, scope);
        string seek = last is null ? string.Empty : "WHERE (CreatedUtc,SessionId)>($utc,$id) ";
        using SqliteCommand command = SqliteStatements.Create(connection, null,
            "SELECT SnapshotJson,CreatedUtc,SessionId FROM Sessions " + seek + "ORDER BY CreatedUtc COLLATE BINARY,SessionId COLLATE BINARY LIMIT $limit",
            ("$limit", size + 1), ("$utc", last?.Utc is { } utc ? StoreSerialization.Utc(utc) : null), ("$id", last?.Id.ToString("D")));
        return SqlitePages.Read(command, size, scope, reader =>
            (StoreSerialization.Read<SessionSnapshot>(reader.GetString(0)), new CursorKey(Guid.ParseExact(reader.GetString(2), "D"),
                Utc: DateTimeOffset.ParseExact(reader.GetString(1), "O", CultureInfo.InvariantCulture))));
    }

    internal static Page<RequestSnapshot> Requests(SqliteConnection connection, Guid storeId, Guid sessionId, int size, string? cursor)
    {
        PageCursor.ValidateSize(size);
        PageScope scope = new(storeId, PageListKind.Requests, sessionId);
        CursorKey? last = PageCursor.Read(cursor, scope);
        string seek = last is null ? string.Empty : "AND (Sequence,RequestId)>($sequence,$id) ";
        using SqliteCommand command = SqliteStatements.Create(connection, null,
            "SELECT SnapshotJson,Sequence,RequestId,ReservedDispatchJson FROM Requests WHERE SessionId=$session " + seek + "ORDER BY Sequence,RequestId COLLATE BINARY LIMIT $limit",
            ("$session", sessionId.ToString("D")), ("$limit", size + 1), ("$sequence", last?.Ordinal), ("$id", last?.Id.ToString("D")));
        return SqlitePages.Read(command, size, scope, reader =>
            (QueryTransactions.WithEvidence(StoreSerialization.Read<RequestSnapshot>(reader.GetString(0)), reader.IsDBNull(3) ? null : reader.GetString(3)),
                new CursorKey(Guid.ParseExact(reader.GetString(2), "D"), reader.GetInt64(1))));
    }

    internal static Page<HistoryEntry> History(SqliteConnection connection, Guid storeId, Guid sessionId, int size, string? cursor)
    {
        PageCursor.ValidateSize(size);
        PageScope scope = new(storeId, PageListKind.History, sessionId);
        CursorKey? last = PageCursor.Read(cursor, scope);
        string seek = last is null ? string.Empty : "AND (HistoryOrdinal,HistoryId)>($ordinal,$id) ";
        using SqliteCommand command = SqliteStatements.Create(connection, null,
            "SELECT SnapshotJson,HistoryOrdinal,HistoryId FROM History WHERE SessionId=$session " + seek + "ORDER BY HistoryOrdinal,HistoryId COLLATE BINARY LIMIT $limit",
            ("$session", sessionId.ToString("D")), ("$limit", size + 1), ("$ordinal", last?.Ordinal), ("$id", last?.Id.ToString("D")));
        return SqlitePages.Read(command, size, scope, reader =>
            (StoreSerialization.Read<HistoryEntry>(reader.GetString(0)), new CursorKey(Guid.ParseExact(reader.GetString(2), "D"), reader.GetInt64(1))));
    }

    internal static Page<RecoveryCaseSnapshot> Recovery(SqliteConnection connection, Guid storeId, int size, string? cursor)
    {
        PageCursor.ValidateSize(size);
        PageScope scope = new(storeId, PageListKind.Recovery);
        CursorKey? last = PageCursor.Read(cursor, scope);
        string seek = last is null ? string.Empty : "AND (CreatedUtc,TargetKind,TargetId)>($created,$kind,$id) ";
        using SqliteCommand command = SqliteStatements.Create(connection, null,
            "SELECT SnapshotJson,CreatedUtc,TargetKind,TargetId FROM RecoveryCases WHERE Status=$open " + seek + "ORDER BY CreatedUtc COLLATE BINARY,TargetKind,TargetId COLLATE BINARY LIMIT $limit",
            ("$open", (int)RecoveryCaseStatus.Open), ("$limit", size + 1), ("$created", last?.Utc is { } utc ? StoreSerialization.Utc(utc) : null),
            ("$kind", last?.TargetKind is { } kind ? (int)kind : null), ("$id", last?.Id.ToString("D")));
        return SqlitePages.Read(command, size, scope, reader =>
            (StoreSerialization.Read<RecoveryCaseSnapshot>(reader.GetString(0)), new CursorKey(Guid.ParseExact(reader.GetString(3), "D"),
                Utc: DateTimeOffset.ParseExact(reader.GetString(1), "O", CultureInfo.InvariantCulture), TargetKind: (RecoveryTargetKind)reader.GetInt32(2))));
    }
}
