using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;

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
            ProviderReply result = new(RequestState.Cancelled, ExecutionCertainty.NotStarted, string.Empty, false, null,
                new(ErrorCode.Cancelled, ExecutionCertainty.NotStarted, snapshot.SessionId, snapshot.RequestId));
            RequestSnapshot cancelled = snapshot with
            {
                State = RequestState.Cancelled,
                Certainty = ExecutionCertainty.NotStarted,
                Result = result,
                EndedUtc = now,
                CancellationRequestedUtc = now,
            };
            ResultTransactions.Write(connection, transaction, cancelled, input.Input, now);
            using SqliteCommand intent = SqliteStatements.Create(connection, transaction, "UPDATE Requests SET CancellationRequestedUtc=$now WHERE RequestId=$id",
                ("$now", StoreSerialization.Utc(now)), ("$id", cancelled.RequestId.ToString("D")));
            intent.ExecuteNonQuery();
        }
    }
}
