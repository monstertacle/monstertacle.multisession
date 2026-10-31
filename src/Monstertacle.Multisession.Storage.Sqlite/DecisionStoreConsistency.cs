using System.Globalization;
using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class DecisionStoreConsistency
{
    internal static void Assert(SqliteConnection connection, SqliteTransaction transaction, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        AssertResolvedHasDecisions(connection, transaction);
        using SqliteCommand command = SqliteStatements.Create(connection, transaction, """
            SELECT d.DecisionId,d.TargetKind,d.TargetId,d.AppliedUtc,d.DecisionJson,c.SnapshotJson
            FROM RecoveryDecisions d JOIN RecoveryCases c ON c.TargetKind=d.TargetKind AND c.TargetId=d.TargetId
            ORDER BY d.TargetKind,d.TargetId COLLATE BINARY
            """);
        using SqliteDataReader reader = command.ExecuteReader();
        RecoveryCaseSnapshot? group = null;
        bool confirmed = false;
        while (reader.Read())
        {
            token.ThrowIfCancellationRequested();
            RecoveryDecision decision = StoreSerialization.Read<RecoveryDecision>(reader.GetString(4));
            RecoveryCaseSnapshot item = StoreSerialization.Read<RecoveryCaseSnapshot>(reader.GetString(5));
            if (group?.Target != item.Target) { AssertConfirmation(group, confirmed); group = item; confirmed = false; }
            if (!Matches(reader, decision, item)) { throw StorageFailurePolicy.Error(ErrorCode.StorageUnavailable, false, false); }
            if (decision.ExpectedRevision == item.Revision - 1 && decision.Kind == RecoveryDecisionKind.ConfirmEnded)
            {
                if (confirmed) { throw StorageFailurePolicy.Error(ErrorCode.StorageUnavailable, false, false); }
                confirmed = true;
            }
        }
        AssertConfirmation(group, confirmed);
    }

    private static bool Matches(SqliteDataReader reader, RecoveryDecision decision, RecoveryCaseSnapshot item)
        => decision.DecisionId != Guid.Empty && reader.GetString(0) == decision.DecisionId.ToString("D")
            && reader.GetInt32(1) == (int)item.Target.Kind && reader.GetString(2) == item.Target.Id.ToString("D") && decision.Target == item.Target
            && decision.ExpectedRevision >= 1 && decision.ExpectedRevision < item.Revision && Enum.IsDefined(decision.Kind)
            && !string.IsNullOrWhiteSpace(decision.Explanation) && !string.IsNullOrWhiteSpace(decision.EvidenceReference)
            && DateTimeOffset.TryParseExact(reader.GetString(3), "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset applied)
            && StoredUtcRepresentation.Matches(reader, 3, applied);

    private static void AssertConfirmation(RecoveryCaseSnapshot? item, bool confirmed)
    {
        if (item is not null && (item.Status == RecoveryCaseStatus.Resolved) != confirmed)
        { throw StorageFailurePolicy.Error(ErrorCode.StorageUnavailable, false, false); }
    }

    private static void AssertResolvedHasDecisions(SqliteConnection connection, SqliteTransaction transaction)
    {
        using SqliteCommand command = SqliteStatements.Create(connection, transaction, """
            WITH targets AS (SELECT TargetKind,TargetId FROM RecoveryDecisions GROUP BY TargetKind,TargetId)
            SELECT count(*) FROM RecoveryCases c LEFT JOIN targets d ON d.TargetKind=c.TargetKind AND d.TargetId=c.TargetId
                WHERE c.Status=$resolved AND d.TargetId IS NULL
            """, ("$resolved", (int)RecoveryCaseStatus.Resolved));
        if (!Equals(command.ExecuteScalar(), 0L)) { throw StorageFailurePolicy.Error(ErrorCode.StorageUnavailable, false, false); }
    }
}
