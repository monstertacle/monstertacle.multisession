using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class QueuedCancellationTransactions
{
    internal static void CancelSession(SqliteConnection connection, SqliteTransaction transaction, Guid sessionId, DateTimeOffset now)
    {
        List<(RequestSnapshot Snapshot, SubmitRequest Input)> queued = [];
        using (SqliteCommand command = SqliteStatements.Create(connection, transaction,
            "SELECT SnapshotJson,SubmissionJson FROM Requests WHERE SessionId=$session AND State=$queued ORDER BY Sequence",
            ("$session", sessionId.ToString("D")), ("$queued", (int)RequestState.Queued)))
        using (SqliteDataReader reader = command.ExecuteReader())
        {
            while (reader.Read()) { queued.Add((StoreSerialization.Read<RequestSnapshot>(reader.GetString(0)), StoreSerialization.Read<SubmitRequest>(reader.GetString(1)))); }
        }
        foreach (var (snapshot, input) in queued)
        {
            RequestSnapshot cancelled = RequestCancellationPolicy.Apply(snapshot, now);
            ResultTransactions.Write(connection, transaction, cancelled, input.Input, now);
            CancellationTransactions.SaveIntent(connection, transaction, cancelled);
        }
    }
}
