using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class SessionStoreConsistency
{
    internal static void Assert(SqliteConnection connection, SqliteTransaction transaction)
    {
        using (SqliteCommand command = SqliteStatements.Create(connection, transaction, """
            SELECT SessionId,ProviderId,AgentProfileId,ModelId,SettingsRevision,AdapterId,ContractVersion,
                CreatedUtc,UpdatedUtc,ClosedUtc,Lifecycle,BlockReason,BindingRevision,SnapshotJson
            FROM Sessions ORDER BY SessionId COLLATE BINARY
            """))
        using (SqliteDataReader reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                SessionSnapshot snapshot = StoreSerialization.Read<SessionSnapshot>(reader.GetString(13));
                if (!IdentityMatches(reader, snapshot) || !StateMatches(reader, snapshot) || !BindingMatches(reader, snapshot.Binding)
                    || !StoredUtcRepresentation.Matches(reader, 7, snapshot.CreatedUtc)
                    || !StoredUtcRepresentation.Matches(reader, 8, snapshot.UpdatedUtc)
                    || !StoredUtcRepresentation.Matches(reader, 9, snapshot.ClosedUtc))
                { throw StorageFailurePolicy.Error(ErrorCode.StorageUnavailable, false, false); }
            }
        }
        using SqliteCommand sequence = SqliteStatements.Create(connection, transaction, """
            SELECT count(*) FROM Sessions s WHERE s.NextSequence <> COALESCE(
                (SELECT max(r.Sequence)+1 FROM Requests r WHERE r.SessionId=s.SessionId),1)
            """);
        if (!Equals(sequence.ExecuteScalar(), 0L)) { throw StorageFailurePolicy.Error(ErrorCode.StorageUnavailable, false, false); }
    }

    private static bool IdentityMatches(SqliteDataReader reader, SessionSnapshot snapshot)
        => snapshot.SessionId != Guid.Empty && snapshot.Context is { } context && context.SessionId == snapshot.SessionId
            && context.ExecutionContext is not null && Enum.IsDefined(context.ExecutionKind)
            && !string.IsNullOrWhiteSpace(snapshot.ProviderId) && !string.IsNullOrWhiteSpace(snapshot.AgentProfileId)
            && !string.IsNullOrWhiteSpace(snapshot.SettingsRevision) && !string.IsNullOrWhiteSpace(context.ModelId)
            && reader.GetString(0) == snapshot.SessionId.ToString("D") && reader.GetString(1) == snapshot.ProviderId
            && reader.GetString(2) == snapshot.AgentProfileId && reader.GetString(3) == context.ModelId
            && reader.GetString(4) == snapshot.SettingsRevision;

    private static bool StateMatches(SqliteDataReader reader, SessionSnapshot snapshot)
        => Enum.IsDefined(snapshot.Lifecycle) && Enum.IsDefined(snapshot.BlockReason)
            && reader.GetInt32(10) == (int)snapshot.Lifecycle && reader.GetInt32(11) == (int)snapshot.BlockReason
            && snapshot.BindingRevision >= 0 && reader.GetInt64(12) == snapshot.BindingRevision
            && (snapshot.Lifecycle == SessionLifecycle.Closed) == (snapshot.ClosedUtc is not null)
            && (snapshot.Lifecycle != SessionLifecycle.Open || snapshot.Binding is not null);

    private static bool BindingMatches(SqliteDataReader reader, ProviderBinding? binding)
        => binding is null ? reader.IsDBNull(5) && reader.IsDBNull(6)
            : !string.IsNullOrWhiteSpace(binding.AdapterId) && binding.FormatVersion >= 1 && !reader.IsDBNull(5) && !reader.IsDBNull(6)
                && reader.GetString(5) == binding.AdapterId && reader.GetInt32(6) == binding.FormatVersion;
}
