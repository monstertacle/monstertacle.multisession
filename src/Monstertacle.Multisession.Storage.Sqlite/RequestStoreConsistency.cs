using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;

namespace Monstertacle.Multisession.Storage.Sqlite;

// Validate every request before startup recovery can change any row. Reader memory stays bounded.
internal static class RequestStoreConsistency
{
    internal static void Assert(SqliteConnection connection, SqliteTransaction transaction, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using SqliteCommand command = SqliteStatements.Create(connection, transaction, """
            SELECT RequestId,SessionId,Sequence,State,Certainty,AcceptedUtc,StartedUtc,EndedUtc,CancellationRequestedUtc,SnapshotJson
            FROM Requests ORDER BY RequestId COLLATE BINARY
            """);
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            token.ThrowIfCancellationRequested();
            RequestSnapshot snapshot = StoreSerialization.Read<RequestSnapshot>(reader.GetString(9));
            if (!IdentityMatches(reader, snapshot) || !RequestStateMachine.IsValid(snapshot.State, snapshot.Certainty)
                || reader.GetInt32(3) != (int)snapshot.State || reader.GetInt32(4) != (int)snapshot.Certainty
                || !TimesMatch(reader, snapshot) || !FinalizationMatches(snapshot))
            { throw StorageFailurePolicy.Error(ErrorCode.StorageUnavailable, false, false); }
        }
    }

    private static bool IdentityMatches(SqliteDataReader reader, RequestSnapshot snapshot)
        => snapshot.RequestId != Guid.Empty && snapshot.SessionId != Guid.Empty && snapshot.Sequence >= 1
            && reader.GetString(0) == snapshot.RequestId.ToString("D") && reader.GetString(1) == snapshot.SessionId.ToString("D")
            && reader.GetInt64(2) == snapshot.Sequence;

    private static bool TimesMatch(SqliteDataReader reader, RequestSnapshot snapshot)
        => StoredUtcRepresentation.Matches(reader, 5, snapshot.AcceptedUtc) && StoredUtcRepresentation.Matches(reader, 6, snapshot.StartedUtc)
            && StoredUtcRepresentation.Matches(reader, 7, snapshot.EndedUtc) && StoredUtcRepresentation.Matches(reader, 8, snapshot.CancellationRequestedUtc);

    private static bool FinalizationMatches(RequestSnapshot snapshot)
    {
        if (RequestStateMachine.IsTerminal(snapshot.State))
        {
            return snapshot.EndedUtc is not null && snapshot.Result is { } result
                && result.Outcome == snapshot.State && result.Certainty == snapshot.Certainty;
        }
        return snapshot.Result is null && snapshot.EndedUtc is null
            && (snapshot.State == RequestState.Queued ? snapshot.StartedUtc is null : snapshot.StartedUtc is not null);
    }
}
