using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class ResultStoreConsistency
{
    internal static void Assert(SqliteConnection connection, SqliteTransaction transaction, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        AssertRelations(connection, transaction);
        using SqliteCommand command = SqliteStatements.Create(connection, transaction, """
            SELECT r.SnapshotJson,r.SubmissionJson,t.CompletedUtc,t.ReplyJson,
                h.HistoryId,h.SessionId,h.RequestId,h.Sequence,h.HistoryOrdinal,h.SnapshotJson
            FROM Requests r JOIN Results t ON t.RequestId=r.RequestId JOIN History h ON h.RequestId=r.RequestId
            ORDER BY r.RequestId COLLATE BINARY
            """);
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            token.ThrowIfCancellationRequested();
            RequestSnapshot request = StoreSerialization.Read<RequestSnapshot>(reader.GetString(0));
            SubmitRequest submission = StoreSerialization.Read<SubmitRequest>(reader.GetString(1));
            ProviderReply result = StoreSerialization.Read<ProviderReply>(reader.GetString(3));
            HistoryEntry history = StoreSerialization.Read<HistoryEntry>(reader.GetString(9));
            if (request.Result != result || submission.SessionId != request.SessionId || submission.Input is null || history.Input != submission.Input
                || !StoredUtcRepresentation.Matches(reader, 2, request.EndedUtc) || !HistoryMatches(reader, history, request)
                || !HistoricalResultMatches(history.Result, result))
            { throw StorageFailurePolicy.Error(ErrorCode.StorageUnavailable, false, false); }
        }
    }

    private static void AssertRelations(SqliteConnection connection, SqliteTransaction transaction)
    {
        using SqliteCommand relations = SqliteStatements.Create(connection, transaction, """
            WITH history_counts AS (SELECT RequestId,count(*) AS Entries FROM History GROUP BY RequestId)
            SELECT count(*) FROM Requests r LEFT JOIN Results t ON t.RequestId=r.RequestId
                LEFT JOIN history_counts h ON h.RequestId=r.RequestId WHERE
                (r.State IN ($queued,$running) AND (t.RequestId IS NOT NULL OR h.Entries IS NOT NULL))
                OR (r.State NOT IN ($queued,$running) AND (t.RequestId IS NULL OR COALESCE(h.Entries,0)<>1))
            """, ("$queued", (int)RequestState.Queued), ("$running", (int)RequestState.Running));
        if (!Equals(relations.ExecuteScalar(), 0L)) { throw StorageFailurePolicy.Error(ErrorCode.StorageUnavailable, false, false); }
        using SqliteCommand ordinals = SqliteStatements.Create(connection, transaction, """
            SELECT count(*) FROM (SELECT SessionId FROM History GROUP BY SessionId HAVING max(HistoryOrdinal)<>count(*))
            """);
        if (!Equals(ordinals.ExecuteScalar(), 0L)) { throw StorageFailurePolicy.Error(ErrorCode.StorageUnavailable, false, false); }
    }

    private static bool HistoryMatches(SqliteDataReader reader, HistoryEntry history, RequestSnapshot request)
        => history.HistoryId != Guid.Empty && reader.GetString(4) == history.HistoryId.ToString("D")
            && history.SessionId == request.SessionId && reader.GetString(5) == history.SessionId.ToString("D")
            && history.RequestId == request.RequestId && reader.GetString(6) == history.RequestId.ToString("D")
            && history.Sequence == request.Sequence && reader.GetInt64(7) == history.Sequence
            && history.HistoryOrdinal >= 1 && reader.GetInt64(8) == history.HistoryOrdinal;

    private static bool HistoricalResultMatches(ProviderReply? historical, ProviderReply current)
    {
        if (historical is null || !RequestStateMachine.IsValid(historical.Outcome, historical.Certainty)) { return false; }
        if (historical == current) { return true; }
        return historical.Certainty == ExecutionCertainty.Unknown && current.Certainty == ExecutionCertainty.ConfirmedEnded
            && historical with
            {
                Certainty = ExecutionCertainty.ConfirmedEnded,
                Error = historical.Error is null ? null : historical.Error with { Certainty = ExecutionCertainty.ConfirmedEnded },
            } == current;
    }
}
