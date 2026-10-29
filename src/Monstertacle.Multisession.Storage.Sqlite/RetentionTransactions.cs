using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Sessions;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class RetentionTransactions
{
    internal static void Delete(SqliteConnection connection, SqliteStoreState state, Guid sessionId, CancellationToken token)
        => SqliteTransactions.Run(connection, transaction =>
        {
            SessionSnapshot session = QueryTransactions.Session(connection, sessionId, transaction) ?? throw new SessionOperationException(new(ErrorCode.NotFound));
            var (active, uncertain) = SessionCloseProjection.Read(connection, transaction, sessionId);
            if (!SessionRetentionPolicy.CanDelete(session, state.Options.Retention, state.Clock.GetUtcNow(), active, uncertain))
            {
                throw new SessionOperationException(new(ErrorCode.SessionBlocked));
            }
            using SqliteCommand command = SqliteStatements.Create(connection, transaction,
                """
                DELETE FROM RecoveryDecisions WHERE (TargetKind,TargetId) IN (SELECT TargetKind,TargetId FROM RecoveryCases WHERE SessionId=$id);
                DELETE FROM RecoveryCases WHERE SessionId=$id;
                DELETE FROM History WHERE SessionId=$id;
                DELETE FROM Results WHERE RequestId IN (SELECT RequestId FROM Requests WHERE SessionId=$id);
                DELETE FROM Reservations WHERE SessionId=$id;
                DELETE FROM Requests WHERE SessionId=$id;
                DELETE FROM Sessions WHERE SessionId=$id;
                """, ("$id", sessionId.ToString("D")));
            command.ExecuteNonQuery();
            return true;
        }, token);
}
