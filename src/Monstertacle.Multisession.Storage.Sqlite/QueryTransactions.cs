using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class QueryTransactions
{
    internal static SessionSnapshot? Session(SqliteConnection connection, Guid sessionId, SqliteTransaction? transaction = null)
    {
        using SqliteCommand command = SqliteStatements.Create(connection, transaction,
            "SELECT SnapshotJson FROM Sessions WHERE SessionId=$id", ("$id", sessionId.ToString("D")));
        return command.ExecuteScalar() is string json ? StoreSerialization.Read<SessionSnapshot>(json) : null;
    }

    internal static RequestSnapshot? Request(SqliteConnection connection, Guid requestId, SqliteTransaction? transaction = null)
    {
        using SqliteCommand command = SqliteStatements.Create(connection, transaction,
            "SELECT SnapshotJson FROM Requests WHERE RequestId=$id", ("$id", requestId.ToString("D")));
        return command.ExecuteScalar() is string json ? StoreSerialization.Read<RequestSnapshot>(json) : null;
    }

    // Public reads project acknowledged dispatch evidence from its single persisted envelope; snapshots never store a copy.
    internal static RequestSnapshot? PublicRequest(SqliteConnection connection, Guid requestId)
    {
        using SqliteCommand command = SqliteStatements.Create(connection, null,
            "SELECT SnapshotJson,ReservedDispatchJson FROM Requests WHERE RequestId=$id", ("$id", requestId.ToString("D")));
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? WithEvidence(StoreSerialization.Read<RequestSnapshot>(reader.GetString(0)), reader.IsDBNull(1) ? null : reader.GetString(1)) : null;
    }

    internal static RequestSnapshot WithEvidence(RequestSnapshot snapshot, string? reserved)
    {
        if (reserved is null) { return snapshot with { Evidence = null }; }
        StoredDispatch stored = StoreSerialization.Read<StoredDispatch>(reserved);
        return snapshot with { Evidence = new(stored.InvocationPrepared, stored.OperationReference, stored.NativeSessionReference) };
    }
}
