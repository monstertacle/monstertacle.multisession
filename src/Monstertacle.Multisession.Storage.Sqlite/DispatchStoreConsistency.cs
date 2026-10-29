using System.Globalization;
using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class DispatchStoreConsistency
{
    internal static void Assert(SqliteConnection connection, SqliteTransaction transaction)
    {
        AssertTopology(connection, transaction);
        using SqliteCommand command = SqliteStatements.Create(connection, transaction, """
            SELECT v.DispatchId,v.RequestId,v.SessionId,v.WorkspaceKey,v.ReservedUtc,v.ReleasedUtc,v.ReleaseReason,
                r.BindingRevision,r.ReservedDispatchJson,r.SubmissionJson,r.StartedUtc,s.SnapshotJson,r.Sequence
            FROM Reservations v JOIN Requests r ON r.RequestId=v.RequestId JOIN Sessions s ON s.SessionId=r.SessionId
            ORDER BY v.DispatchId COLLATE BINARY
            """);
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            ExecutionReservation reservation = new(Guid.ParseExact(reader.GetString(0), "D"), Guid.ParseExact(reader.GetString(1), "D"),
                Guid.ParseExact(reader.GetString(2), "D"), reader.IsDBNull(3) ? null : reader.GetString(3),
                DateTimeOffset.ParseExact(reader.GetString(4), "O", CultureInfo.InvariantCulture));
            StoredDispatch stored = StoreSerialization.Read<StoredDispatch>(reader.GetString(8));
            SubmitRequest submission = StoreSerialization.Read<SubmitRequest>(reader.GetString(9));
            SessionSnapshot session = StoreSerialization.Read<SessionSnapshot>(reader.GetString(11));
            if (!FrozenDispatchConsistency.Matches(stored, reservation, submission, session, reader.GetInt64(12), reader.GetInt64(7))
                || reservation.DispatchId == Guid.Empty || reservation.SessionId != session.SessionId
                || reservation.WorkspaceKey != (submission.WriteIntent ? submission.WorkspaceKey : null)
                || !StoredUtcRepresentation.Matches(reader, 4, reservation.ReservedUtc)
                || !StoredUtcRepresentation.Matches(reader, 10, reservation.ReservedUtc) || !ReleaseMatches(reader))
            { throw StorageFailurePolicy.Error(ErrorCode.StorageUnavailable, false, false); }
        }
    }

    private static void AssertTopology(SqliteConnection connection, SqliteTransaction transaction)
    {
        using SqliteCommand command = SqliteStatements.Create(connection, transaction, """
            SELECT count(*) FROM Requests r LEFT JOIN Reservations v ON v.DispatchId=r.DispatchId WHERE
                (r.DispatchId IS NULL AND (r.BindingRevision IS NOT NULL OR r.ReservedDispatchJson IS NOT NULL
                    OR EXISTS (SELECT 1 FROM Reservations owned WHERE owned.RequestId=r.RequestId)
                    OR (r.State<>$queued AND NOT(r.State=$cancelled AND r.Certainty=$notStarted))))
                OR (r.DispatchId IS NOT NULL AND (r.BindingRevision IS NULL OR r.ReservedDispatchJson IS NULL
                    OR v.DispatchId IS NULL OR v.RequestId<>r.RequestId OR v.SessionId<>r.SessionId))
                OR (r.Certainty=$unknown AND (v.DispatchId IS NULL OR v.ReleasedUtc IS NOT NULL))
            """, ("$queued", (int)RequestState.Queued), ("$cancelled", (int)RequestState.Cancelled),
            ("$notStarted", (int)ExecutionCertainty.NotStarted), ("$unknown", (int)ExecutionCertainty.Unknown));
        if (!Equals(command.ExecuteScalar(), 0L)) { throw StorageFailurePolicy.Error(ErrorCode.StorageUnavailable, false, false); }
    }

    private static bool ReleaseMatches(SqliteDataReader reader)
    {
        if (reader.IsDBNull(5)) { return reader.IsDBNull(6); }
        return !reader.IsDBNull(6) && DateTimeOffset.TryParseExact(reader.GetString(5), "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset released)
            && StoredUtcRepresentation.Matches(reader, 5, released)
            && reader.GetString(6) is nameof(ExecutionCertainty.NotStarted) or nameof(ExecutionCertainty.ConfirmedEnded) or nameof(RecoveryDecisionKind.ConfirmEnded);
    }
}
