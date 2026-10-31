using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;
using Monstertacle.Multisession.Sessions;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class SessionCloseTransactions
{
    internal static SessionSnapshot Begin(SqliteConnection connection, SqliteStoreState state, Guid sessionId, CancellationToken token)
        => SqliteTransactions.Run(connection, transaction =>
        {
            SessionSnapshot session = QueryTransactions.Session(connection, sessionId, transaction) ?? throw new SessionOperationException(new(ErrorCode.NotFound));
            if (session.Lifecycle == SessionLifecycle.Closed) { return session; }
            DateTimeOffset now = state.Clock.GetUtcNow();
            if (session.Lifecycle == SessionLifecycle.Opening) { OpeningRecoveryTransactions.OpenCase(connection, transaction, session with { UpdatedUtc = now }); }
            QueuedCancellationTransactions.CancelSession(connection, transaction, sessionId, now, token);
            CancelRunning(connection, transaction, sessionId, now);
            if (session.Lifecycle != SessionLifecycle.Closing)
            {
                SessionTransactions.Update(connection, transaction, session with { Lifecycle = SessionLifecycle.Closing, ClosedUtc = null, UpdatedUtc = now });
            }
            return Refresh(connection, transaction, state, sessionId);
        }, token);

    internal static SessionSnapshot Refresh(SqliteConnection connection, SqliteStoreState state, Guid sessionId, CancellationToken token)
        => SqliteTransactions.Run(connection, transaction => Refresh(connection, transaction, state, sessionId), token);

    internal static SessionSnapshot Refresh(SqliteConnection connection, SqliteTransaction transaction, SqliteStoreState state, Guid sessionId)
    {
        SessionSnapshot session = QueryTransactions.Session(connection, sessionId, transaction) ?? throw new SessionOperationException(new(ErrorCode.NotFound));
        if (session.Lifecycle != SessionLifecycle.Closing) { return session; }
        var (active, uncertain) = SessionCloseProjection.Read(connection, transaction, sessionId);
        SessionSnapshot updated = SessionClosurePolicy.Refresh(session, active, uncertain, state.Clock.GetUtcNow());
        if (updated != session) { SessionTransactions.Update(connection, transaction, updated); }
        return updated;
    }

    private static void CancelRunning(SqliteConnection connection, SqliteTransaction transaction, Guid sessionId, DateTimeOffset now)
    {
        List<RequestSnapshot> running = [];
        using (SqliteCommand command = SqliteStatements.Create(connection, transaction, "SELECT SnapshotJson FROM Requests WHERE SessionId=$id AND State=$running",
            ("$id", sessionId.ToString("D")), ("$running", (int)RequestState.Running)))
        using (SqliteDataReader reader = command.ExecuteReader())
        {
            while (reader.Read()) { running.Add(StoreSerialization.Read<RequestSnapshot>(reader.GetString(0))); }
        }
        foreach (RequestSnapshot request in running)
        {
            RequestSnapshot updated = RequestCancellationPolicy.Apply(request, now);
            if (updated != request) { CancellationTransactions.SaveIntent(connection, transaction, updated); }
        }
    }
}
