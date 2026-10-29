using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class RecoveryTransactions
{
    internal static void Prepare(SqliteConnection connection, SqliteStoreState state)
        => SqliteTransactions.Run(connection, transaction =>
        {
            AssertConsistent(connection, transaction);
            List<Guid> running = [];
            using (SqliteCommand command = SqliteStatements.Create(connection, transaction, "SELECT DispatchId FROM Requests WHERE State=$running", ("$running", (int)RequestState.Running)))
            using (SqliteDataReader reader = command.ExecuteReader())
            {
                while (reader.Read()) { running.Add(Guid.ParseExact(reader.GetString(0), "D")); }
            }
            foreach (Guid id in running) { UnknownTransactions.Apply(connection, transaction, state, id, new(ErrorCode.Interrupted)); }
            List<SessionSnapshot> openings = [];
            using (SqliteCommand command = SqliteStatements.Create(connection, transaction, "SELECT SnapshotJson FROM Sessions WHERE Lifecycle=$opening", ("$opening", (int)SessionLifecycle.Opening)))
            using (SqliteDataReader reader = command.ExecuteReader())
            {
                while (reader.Read()) { openings.Add(StoreSerialization.Read<SessionSnapshot>(reader.GetString(0))); }
            }
            foreach (SessionSnapshot session in openings)
            {
                RecoveryTarget target = new(RecoveryTargetKind.SessionOpening, session.SessionId);
                if (session.BlockReason == SessionBlockReason.RecoveryRequired && RecoveryCases.Read(connection, target, transaction)?.Status == RecoveryCaseStatus.Open) { continue; }
                SessionSnapshot blocked = session with { BlockReason = SessionBlockReason.RecoveryRequired, UpdatedUtc = state.Clock.GetUtcNow() };
                SessionTransactions.Update(connection, transaction, blocked);
                OpeningRecoveryTransactions.OpenCase(connection, transaction, blocked);
            }
            return true;
        }, CancellationToken.None);

    private static void AssertConsistent(SqliteConnection connection, SqliteTransaction transaction)
    {
        using (SqliteCommand foreignKeys = SqliteStatements.Create(connection, transaction, "PRAGMA foreign_key_check"))
        using (SqliteDataReader reader = foreignKeys.ExecuteReader())
        {
            if (reader.Read()) { throw StorageFailurePolicy.Error(ErrorCode.StorageUnavailable, false, false); }
        }
        RequestStoreConsistency.Assert(connection, transaction);
        SessionStoreConsistency.Assert(connection, transaction);
        using SqliteCommand missing = SqliteStatements.Create(connection, transaction,
            """
            SELECT count(*) FROM Requests r WHERE r.State=$running AND NOT EXISTS
                (SELECT 1 FROM Reservations v WHERE v.DispatchId=r.DispatchId AND v.RequestId=r.RequestId AND v.SessionId=r.SessionId AND v.ReleasedUtc IS NULL)
            """, ("$running", (int)RequestState.Running));
        if (!Equals(missing.ExecuteScalar(), 0L)) { throw StorageFailurePolicy.Error(ErrorCode.StorageUnavailable, false, false); }
        using SqliteCommand inconsistent = SqliteStatements.Create(connection, transaction,
            """
            SELECT count(*) FROM Reservations v JOIN Requests r ON r.RequestId=v.RequestId WHERE v.ReleasedUtc IS NULL AND
                (v.DispatchId<>r.DispatchId OR v.SessionId<>r.SessionId OR (r.State<>$running AND r.Certainty<>$unknown))
            """, ("$running", (int)RequestState.Running), ("$unknown", (int)ExecutionCertainty.Unknown));
        if (!Equals(inconsistent.ExecuteScalar(), 0L)) { throw StorageFailurePolicy.Error(ErrorCode.StorageUnavailable, false, false); }
    }
}
