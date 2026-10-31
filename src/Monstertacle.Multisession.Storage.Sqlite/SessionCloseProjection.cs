using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class SessionCloseProjection
{
    internal static (bool Active, bool Uncertain) Read(SqliteConnection connection, SqliteTransaction transaction, Guid sessionId, RecoveryTarget? excluded = null)
    {
        using SqliteCommand command = SqliteStatements.Create(connection, transaction,
            """
            SELECT EXISTS(SELECT 1 FROM Requests WHERE SessionId=$id AND State IN ($queued,$running)),
                EXISTS(SELECT 1 FROM Reservations WHERE SessionId=$id AND ReleasedUtc IS NULL),
                EXISTS(SELECT 1 FROM Requests WHERE SessionId=$id AND Certainty=$unknown),
                EXISTS(SELECT 1 FROM RecoveryCases WHERE SessionId=$id AND Status=$open
                    AND ($excludedId IS NULL OR TargetId<>$excludedId OR TargetKind<>$excludedKind))
            """, ("$id", sessionId.ToString("D")), ("$queued", (int)RequestState.Queued), ("$running", (int)RequestState.Running),
            ("$unknown", (int)ExecutionCertainty.Unknown), ("$open", (int)RecoveryCaseStatus.Open),
            ("$excludedId", excluded?.Id.ToString("D")), ("$excludedKind", excluded is null ? null : (int)excluded.Kind));
        using SqliteDataReader reader = command.ExecuteReader();
        reader.Read();
        return (reader.GetBoolean(0) || reader.GetBoolean(1), reader.GetBoolean(2) || reader.GetBoolean(3));
    }
}
