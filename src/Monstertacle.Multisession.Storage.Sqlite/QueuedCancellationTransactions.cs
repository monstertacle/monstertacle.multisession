using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class QueuedCancellationTransactions
{
    internal static void CancelSession(SqliteConnection connection, SqliteTransaction transaction, Guid sessionId, DateTimeOffset now, CancellationToken token = default)
    {
        long after = 0;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var queued = ReadPage(connection, transaction, sessionId, after);
            if (queued.Count == 0) { return; }
            foreach (var (snapshot, input) in queued)
            {
                token.ThrowIfCancellationRequested();
                RequestSnapshot cancelled = RequestCancellationPolicy.Apply(snapshot, now);
                ResultTransactions.Write(connection, transaction, cancelled, input.Input, now);
                CancellationTransactions.SaveIntent(connection, transaction, cancelled);
                after = snapshot.Sequence;
            }
        }
    }

    private static List<(RequestSnapshot Snapshot, SubmitRequest Input)> ReadPage(SqliteConnection connection, SqliteTransaction transaction, Guid sessionId, long after)
    {
        List<(RequestSnapshot, SubmitRequest)> queued = [];
        using SqliteCommand command = SqliteStatements.Create(connection, transaction,
            "SELECT SnapshotJson,SubmissionJson FROM Requests WHERE SessionId=$session AND State=$queued AND Sequence>$after ORDER BY Sequence LIMIT $size",
            ("$session", sessionId.ToString("D")), ("$queued", (int)RequestState.Queued), ("$after", after), ("$size", PageLimits.MaximumSize));
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read()) { queued.Add((StoreSerialization.Read<RequestSnapshot>(reader.GetString(0)), StoreSerialization.Read<SubmitRequest>(reader.GetString(1)))); }
        return queued;
    }
}
