using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class CancellationTransactions
{
    internal static void CancelQueued(SqliteConnection connection, SqliteStoreState state, Guid sessionId, CancellationToken token)
        => SqliteTransactions.Run(connection, transaction =>
        {
            QueuedCancellationTransactions.CancelSession(connection, transaction, sessionId, state.Clock.GetUtcNow(), token);
            return true;
        }, token);

    internal static RequestSnapshot Request(SqliteConnection connection, SqliteStoreState state, Guid requestId, CancellationToken token)
        => SqliteTransactions.Run(connection, transaction =>
        {
            RequestSnapshot before = QueryTransactions.Request(connection, requestId, transaction)
                ?? throw new SessionOperationException(new(ErrorCode.NotFound, RequestId: requestId));
            RequestSnapshot updated = RequestCancellationPolicy.Apply(before, state.Clock.GetUtcNow());
            if (updated == before) { return before; }
            if (before.State == RequestState.Queued)
            {
                using SqliteCommand input = SqliteStatements.Create(connection, transaction, "SELECT SubmissionJson FROM Requests WHERE RequestId=$id", ("$id", requestId.ToString("D")));
                SubmitRequest submission = StoreSerialization.Read<SubmitRequest>((string)input.ExecuteScalar()!);
                ResultTransactions.Write(connection, transaction, updated, submission.Input, updated.EndedUtc!.Value);
            }
            SaveIntent(connection, transaction, updated);
            return updated;
        }, token);

    internal static void SaveIntent(SqliteConnection connection, SqliteTransaction transaction, RequestSnapshot request)
    {
        using SqliteCommand command = SqliteStatements.Create(connection, transaction,
            "UPDATE Requests SET CancellationRequestedUtc=$now,SnapshotJson=$snapshot WHERE RequestId=$id",
            ("$now", StoreSerialization.Utc(request.CancellationRequestedUtc!.Value)), ("$snapshot", StoreSerialization.Write(request)), ("$id", request.RequestId.ToString("D")));
        command.ExecuteNonQuery();
    }
}
