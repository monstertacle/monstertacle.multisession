using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class RecoveryCases
{
    internal static RecoveryCaseSnapshot? Read(SqliteConnection connection, RecoveryTarget target, SqliteTransaction? transaction = null)
    {
        if (target.Id == Guid.Empty || !Enum.IsDefined(target.Kind)) { throw new SessionOperationException(new(ErrorCode.InvalidInput)); }
        using SqliteCommand command = SqliteStatements.Create(connection, transaction,
            "SELECT SnapshotJson,Revision,Status,SessionId,RequestId,DispatchId FROM RecoveryCases WHERE TargetKind=$kind AND TargetId=$id",
            ("$kind", (int)target.Kind), ("$id", target.Id.ToString("D")));
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read()) { return null; }
        RecoveryCaseSnapshot item = StoreSerialization.Read<RecoveryCaseSnapshot>(reader.GetString(0));
        Guid? requestId = reader.IsDBNull(4) ? null : Guid.ParseExact(reader.GetString(4), "D");
        Guid? dispatchId = reader.IsDBNull(5) ? null : Guid.ParseExact(reader.GetString(5), "D");
        bool validTarget = target.Kind == RecoveryTargetKind.RequestExecution
            ? item.RequestId == target.Id && item.DispatchId is not null : item.RequestId is null && item.DispatchId is null;
        if (item.Target != target || item.Revision != reader.GetInt64(1) || (int)item.Status != reader.GetInt32(2)
            || item.SessionId != Guid.ParseExact(reader.GetString(3), "D") || item.RequestId != requestId || item.DispatchId != dispatchId
            || !validTarget) { throw StorageFailurePolicy.Error(ErrorCode.StorageUnavailable, false, false); }
        return item;
    }

    internal static void Save(SqliteConnection connection, SqliteTransaction transaction, RecoveryCaseSnapshot item)
    {
        using SqliteCommand command = SqliteStatements.Create(connection, transaction,
            """
            INSERT INTO RecoveryCases (TargetKind,TargetId,SessionId,RequestId,DispatchId,Revision,CreatedUtc,Status,SnapshotJson)
            VALUES ($kind,$target,$session,$request,$dispatch,$revision,$created,$status,$snapshot)
            ON CONFLICT (TargetKind,TargetId) DO UPDATE SET Revision=$revision,Status=$status,SnapshotJson=$snapshot;
            """, ("$kind", (int)item.Target.Kind), ("$target", item.Target.Id.ToString("D")), ("$session", item.SessionId.ToString("D")),
            ("$request", item.RequestId?.ToString("D")), ("$dispatch", item.DispatchId?.ToString("D")), ("$revision", item.Revision),
            ("$created", StoreSerialization.Utc(item.CreatedUtc)), ("$status", (int)item.Status), ("$snapshot", StoreSerialization.Write(item)));
        command.ExecuteNonQuery();
    }

    internal static void Open(SqliteConnection connection, SqliteTransaction transaction, RecoveryCaseSnapshot proposed)
    {
        RecoveryCaseSnapshot? old = Read(connection, proposed.Target, transaction);
        if (old is { Status: RecoveryCaseStatus.Open } && old.Binding == proposed.Binding && old.OperationReference == proposed.OperationReference) { return; }
        RecoveryCaseSnapshot item = proposed with { CreatedUtc = old?.CreatedUtc ?? proposed.CreatedUtc, Revision = old is null ? 1 : checked(old.Revision + 1) };
        Save(connection, transaction, item);
    }

}
