using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;
using Monstertacle.Multisession.Sessions;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class CompletionTransactions
{
    internal static void Commit(SqliteConnection connection, SqliteStoreState state, Guid dispatchId, ProviderReply reply, CancellationToken token)
        => SqliteTransactions.Run(connection, transaction =>
        {
            StoredDispatch stored = StoredDispatchAccess.Read(connection, dispatchId, transaction, true)
                ?? throw new SessionOperationException(new(ErrorCode.RevisionConflict));
            ReservedDispatch snapshot = stored.Snapshot;
            SessionSnapshot session = QueryTransactions.Session(connection, snapshot.Session.SessionId, transaction)!;
            if (session.BindingRevision != snapshot.BindingRevision) { throw new SessionOperationException(new(ErrorCode.RevisionConflict)); }
            ProviderReply normalized = DispatchReplyPolicy.Normalize(reply, snapshot.Session.Binding!, stored.SessionReference ?? snapshot.Session.Binding?.SessionReference, state.Options.MaxOutputBytes);
            if (normalized.Certainty is not (ExecutionCertainty.ConfirmedEnded or ExecutionCertainty.NotStarted)) { throw new SessionOperationException(new(ErrorCode.InvalidInput)); }
            DateTimeOffset completed = state.Clock.GetUtcNow();
            RequestSnapshot request = QueryTransactions.Request(connection, snapshot.Reservation.RequestId, transaction)! with
            {
                State = normalized.Outcome,
                Certainty = normalized.Certainty,
                Result = normalized,
                EndedUtc = completed,
            };
            ResultTransactions.Write(connection, transaction, request, snapshot.Submission.Input, completed);
            SessionSnapshot updated = session with
            {
                Binding = normalized.Binding ?? session.Binding,
                BindingRevision = normalized.Binding is null ? session.BindingRevision : checked(session.BindingRevision + 1),
                UpdatedUtc = completed,
                BlockReason = SessionBindingPolicy.AfterCompletion(session.BlockReason, normalized),
            };
            SessionTransactions.Update(connection, transaction, updated);
            using SqliteCommand release = SqliteStatements.Create(connection, transaction,
                "UPDATE Reservations SET ReleasedUtc=$time,ReleaseReason=$reason WHERE DispatchId=$id AND ReleasedUtc IS NULL",
                ("$time", StoreSerialization.Utc(completed)), ("$reason", normalized.Certainty.ToString()), ("$id", dispatchId.ToString("D")));
            release.ExecuteNonQuery();
            return true;
        }, token);
}
