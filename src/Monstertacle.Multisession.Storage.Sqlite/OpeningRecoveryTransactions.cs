using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class OpeningRecoveryTransactions
{
    internal static void Block(SqliteConnection connection, SqliteStoreState state, Guid sessionId, SessionBlockReason reason, CancellationToken token)
        => SqliteTransactions.Run(connection, transaction =>
        {
            SessionSnapshot session = QueryTransactions.Session(connection, sessionId, transaction) ?? throw new SessionOperationException(new(ErrorCode.NotFound));
            SessionSnapshot updated = session with
            {
                BlockReason = session.BlockReason == SessionBlockReason.RecoveryRequired ? session.BlockReason : reason,
                UpdatedUtc = state.Clock.GetUtcNow(),
            };
            SessionTransactions.Update(connection, transaction, updated);
            if (reason == SessionBlockReason.RecoveryRequired) { OpenCase(connection, transaction, updated); }
            return true;
        }, token);

    internal static void OpenCase(SqliteConnection connection, SqliteTransaction transaction, SessionSnapshot session)
        => RecoveryCases.Open(connection, transaction, new(new(RecoveryTargetKind.SessionOpening, session.SessionId), session.SessionId, session.UpdatedUtc, 1)
        { Binding = session.Binding });
}
