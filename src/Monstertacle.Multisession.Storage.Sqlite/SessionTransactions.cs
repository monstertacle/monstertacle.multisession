using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class SessionTransactions
{
    internal static void Create(SqliteConnection connection, SqliteStoreState state, SessionSnapshot session, CancellationToken token)
    {
        state.RequireAcceptance();
        SqliteTransactions.Run(connection, transaction =>
        {
            if (session.SessionId == Guid.Empty || session.Context.SessionId != session.SessionId
                || session.Lifecycle != SessionLifecycle.Opening || session.Binding is not null
                || string.IsNullOrWhiteSpace(session.SettingsRevision))
            {
                throw new SessionOperationException(new(ErrorCode.InvalidInput));
            }
            using SqliteCommand command = SqliteStatements.Create(connection, transaction,
                """
                INSERT INTO Sessions (SessionId,ProviderId,AgentProfileId,ModelId,SettingsRevision,CreatedUtc,UpdatedUtc,Lifecycle,BlockReason,BindingRevision,NextSequence,SnapshotJson)
                VALUES ($id,$provider,$profile,$model,$settings,$created,$updated,$lifecycle,$block,$revision,1,$snapshot)
                """, ("$id", session.SessionId.ToString("D")), ("$provider", session.ProviderId), ("$profile", session.AgentProfileId),
                ("$model", session.Context.ModelId), ("$settings", session.SettingsRevision), ("$created", StoreSerialization.Utc(session.CreatedUtc)),
                ("$updated", StoreSerialization.Utc(session.UpdatedUtc)), ("$lifecycle", (int)session.Lifecycle), ("$block", (int)session.BlockReason),
                ("$revision", session.BindingRevision), ("$snapshot", StoreSerialization.Write(session)));
            command.ExecuteNonQuery();
            return true;
        }, token);
    }

    internal static SessionSnapshot Bind(SqliteConnection connection, SqliteStoreState state, Guid id, ProviderBinding binding, CancellationToken token)
        => SqliteTransactions.Run(connection, transaction =>
        {
            SessionSnapshot original = QueryTransactions.Session(connection, id, transaction) ?? throw new SessionOperationException(new(ErrorCode.NotFound));
            SessionSnapshot session = original with
            {
                Binding = binding,
                Lifecycle = SessionLifecycle.Open,
                UpdatedUtc = state.Clock.GetUtcNow(),
                BindingRevision = original.Binding is null ? original.BindingRevision : checked(original.BindingRevision + 1),
            };
            using SqliteCommand command = SqliteStatements.Create(connection, transaction,
                "UPDATE Sessions SET AdapterId=$adapter,ContractVersion=$version,Lifecycle=$lifecycle,UpdatedUtc=$updated,BindingRevision=$revision,SnapshotJson=$snapshot WHERE SessionId=$id",
                ("$adapter", binding.AdapterId), ("$version", binding.FormatVersion), ("$lifecycle", (int)session.Lifecycle),
                ("$updated", StoreSerialization.Utc(session.UpdatedUtc)), ("$revision", session.BindingRevision),
                ("$snapshot", StoreSerialization.Write(session)), ("$id", id.ToString("D")));
            command.ExecuteNonQuery();
            return session;
        }, token);
}
