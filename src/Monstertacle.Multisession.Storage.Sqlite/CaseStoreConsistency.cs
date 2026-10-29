using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class CaseStoreConsistency
{
    internal static void Assert(SqliteConnection connection, SqliteTransaction transaction)
    {
        using (SqliteCommand command = SqliteStatements.Create(connection, transaction, """
            SELECT c.TargetKind,c.TargetId,c.SnapshotJson,c.CreatedUtc,c.Revision,c.Status,c.SessionId,c.RequestId,c.DispatchId,
                r.SessionId,r.DispatchId,r.Certainty,v.ReleasedUtc,r.ReservedDispatchJson,s.SnapshotJson,r.State
            FROM RecoveryCases c JOIN Sessions s ON s.SessionId=c.SessionId
                LEFT JOIN Requests r ON r.RequestId=c.RequestId LEFT JOIN Reservations v ON v.DispatchId=c.DispatchId
            ORDER BY c.TargetKind,c.TargetId COLLATE BINARY
            """))
        using (SqliteDataReader reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                RecoveryCaseSnapshot item = StoreSerialization.Read<RecoveryCaseSnapshot>(reader.GetString(2));
                SessionSnapshot session = StoreSerialization.Read<SessionSnapshot>(reader.GetString(14));
                if (!HeaderMatches(reader, item) || (item.Status == RecoveryCaseStatus.Open && session.BlockReason != SessionBlockReason.RecoveryRequired)
                    || !(item.Target.Kind == RecoveryTargetKind.SessionOpening ? OpeningMatches(item, session) : RequestMatches(reader, item)))
                { throw StorageFailurePolicy.Error(ErrorCode.StorageUnavailable, false, false); }
            }
        }
        AssertLinks(connection, transaction);
    }

    private static bool HeaderMatches(SqliteDataReader reader, RecoveryCaseSnapshot item)
    {
        if (item.Target is not { } target || target.Id == Guid.Empty || !Enum.IsDefined(target.Kind) || item.SessionId == Guid.Empty) { return false; }
        bool state = item.Status switch
        {
            RecoveryCaseStatus.Open => item.Certainty == ExecutionCertainty.Unknown && item.BlockReason == SessionBlockReason.RecoveryRequired,
            RecoveryCaseStatus.Resolved => item.Certainty == ExecutionCertainty.ConfirmedEnded && item.BlockReason == SessionBlockReason.None,
            _ => false,
        };
        return state && reader.GetInt32(0) == (int)target.Kind && reader.GetString(1) == target.Id.ToString("D")
            && StoredUtcRepresentation.Matches(reader, 3, item.CreatedUtc) && item.Revision >= 1 && reader.GetInt64(4) == item.Revision
            && reader.GetInt32(5) == (int)item.Status && reader.GetString(6) == item.SessionId.ToString("D")
            && GuidMatches(reader, 7, item.RequestId) && GuidMatches(reader, 8, item.DispatchId);
    }

    private static bool GuidMatches(SqliteDataReader reader, int ordinal, Guid? value)
        => value is null ? reader.IsDBNull(ordinal) : !reader.IsDBNull(ordinal) && reader.GetString(ordinal) == value.Value.ToString("D");

    private static bool OpeningMatches(RecoveryCaseSnapshot item, SessionSnapshot session)
    {
        if (item.Target.Id != item.SessionId || item.RequestId is not null || item.DispatchId is not null || item.OperationReference is not null) { return false; }
        if (item.Status == RecoveryCaseStatus.Open) { return item.Binding == session.Binding; }
        return item.Binding is null || (session.Binding is { } current && item.Binding.AdapterId == current.AdapterId
            && item.Binding.FormatVersion == current.FormatVersion && (item.Binding.SessionReference is null || item.Binding.SessionReference == current.SessionReference));
    }

    private static bool RequestMatches(SqliteDataReader reader, RecoveryCaseSnapshot item)
    {
        if (item.RequestId != item.Target.Id || item.DispatchId is null || reader.IsDBNull(9) || reader.IsDBNull(10)
            || reader.GetString(9) != item.SessionId.ToString("D") || reader.GetString(10) != item.DispatchId.Value.ToString("D")) { return false; }
        ExecutionCertainty expected = item.Status == RecoveryCaseStatus.Open ? ExecutionCertainty.Unknown : ExecutionCertainty.ConfirmedEnded;
        if (reader.GetInt32(11) != (int)expected || reader.IsDBNull(12) != (item.Status == RecoveryCaseStatus.Open)
            || (RequestState)reader.GetInt32(15) is not (RequestState.Failed or RequestState.Cancelled or RequestState.TimedOut or RequestState.Interrupted)) { return false; }
        StoredDispatch stored = StoreSerialization.Read<StoredDispatch>(reader.GetString(13));
        ProviderBinding? binding = stored.Snapshot.Session.Binding;
        if (binding is { SessionReference: null } && stored.SessionReference is not null) { binding = binding with { SessionReference = stored.SessionReference }; }
        return binding is not null && item.Binding == binding && item.OperationReference == stored.OperationReference;
    }

    private static void AssertLinks(SqliteConnection connection, SqliteTransaction transaction)
    {
        using SqliteCommand unknown = SqliteStatements.Create(connection, transaction, """
            SELECT count(*) FROM Requests r WHERE r.Certainty=$unknown AND NOT EXISTS
                (SELECT 1 FROM RecoveryCases c WHERE c.TargetKind=$request AND c.TargetId=r.RequestId AND c.Status=$open)
            """, ("$unknown", (int)ExecutionCertainty.Unknown), ("$request", (int)RecoveryTargetKind.RequestExecution), ("$open", (int)RecoveryCaseStatus.Open));
        if (!Equals(unknown.ExecuteScalar(), 0L)) { throw StorageFailurePolicy.Error(ErrorCode.StorageUnavailable, false, false); }
        using SqliteCommand blocked = SqliteStatements.Create(connection, transaction, """
            WITH owners AS (SELECT DISTINCT SessionId FROM RecoveryCases WHERE Status=$open)
            SELECT count(*) FROM Sessions s LEFT JOIN owners c ON c.SessionId=s.SessionId
                WHERE s.BlockReason=$recovery AND s.Lifecycle<>$opening AND c.SessionId IS NULL
            """, ("$open", (int)RecoveryCaseStatus.Open), ("$recovery", (int)SessionBlockReason.RecoveryRequired), ("$opening", (int)SessionLifecycle.Opening));
        if (!Equals(blocked.ExecuteScalar(), 0L)) { throw StorageFailurePolicy.Error(ErrorCode.StorageUnavailable, false, false); }
    }
}
