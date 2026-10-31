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
}
