using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class ResultTransactions
{
    internal static void Write(SqliteConnection connection, SqliteTransaction transaction, RequestSnapshot request, string input, DateTimeOffset completedUtc)
    {
        using SqliteCommand ordinal = SqliteStatements.Create(connection, transaction,
            "SELECT COALESCE(MAX(HistoryOrdinal),0)+1 FROM History WHERE SessionId=$id", ("$id", request.SessionId.ToString("D")));
        HistoryEntry history = new(request.SessionId, request.RequestId, request.Sequence, input, request.Result!)
        {
            HistoryOrdinal = (long)ordinal.ExecuteScalar()!,
            HistoryId = Guid.NewGuid(),
        };
        using SqliteCommand command = SqliteStatements.Create(connection, transaction,
            """
            INSERT INTO Results (RequestId,CompletedUtc,ReplyJson) VALUES ($request,$completed,$reply);
            INSERT INTO History (HistoryId,SessionId,RequestId,Sequence,HistoryOrdinal,SnapshotJson) VALUES ($history,$session,$request,$sequence,$ordinal,$historySnapshot);
            UPDATE Requests SET State=$state,Certainty=$certainty,EndedUtc=$completed,SnapshotJson=$snapshot WHERE RequestId=$request;
            """, ("$request", request.RequestId.ToString("D")), ("$session", request.SessionId.ToString("D")), ("$completed", StoreSerialization.Utc(completedUtc)),
            ("$reply", StoreSerialization.Write(request.Result)), ("$history", history.HistoryId.ToString("D")), ("$sequence", request.Sequence), ("$ordinal", history.HistoryOrdinal),
            ("$historySnapshot", StoreSerialization.Write(history)), ("$state", (int)request.State), ("$certainty", (int)request.Certainty), ("$snapshot", StoreSerialization.Write(request)));
        command.ExecuteNonQuery();
    }
}
