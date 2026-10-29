using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class UnknownTransactions
{
    internal static void Commit(SqliteConnection connection, SqliteStoreState state, Guid dispatchId, PublicError failure, CancellationToken token)
        => SqliteTransactions.Run(connection, transaction => { Apply(connection, transaction, state, dispatchId, failure); return true; }, token);

    internal static void Apply(SqliteConnection connection, SqliteTransaction transaction, SqliteStoreState state, Guid dispatchId, PublicError failure)
    {
        StoredDispatch stored = StoredDispatchAccess.Read(connection, dispatchId, transaction, true)
            ?? throw new SessionOperationException(new(ErrorCode.RevisionConflict));
        ExecutionReservation reservation = stored.Snapshot.Reservation;
        DateTimeOffset now = state.Clock.GetUtcNow();
        PublicError error = failure with { SessionId = reservation.SessionId, RequestId = reservation.RequestId, Certainty = ExecutionCertainty.Unknown };
        RequestSnapshot request = QueryTransactions.Request(connection, reservation.RequestId, transaction)! with
        {
            State = RequestState.Interrupted,
            Certainty = ExecutionCertainty.Unknown,
            EndedUtc = now,
            Result = new(RequestState.Interrupted, ExecutionCertainty.Unknown, string.Empty, false, null, error),
        };
        ResultTransactions.Write(connection, transaction, request, stored.Snapshot.Submission.Input, now);
        SessionSnapshot session = QueryTransactions.Session(connection, reservation.SessionId, transaction)!;
        SessionTransactions.Update(connection, transaction, session with { BlockReason = SessionBlockReason.RecoveryRequired, UpdatedUtc = now });
        ProviderBinding? known = stored.Snapshot.Session.Binding;
        if (known is { SessionReference: null } && stored.SessionReference is not null) { known = known with { SessionReference = stored.SessionReference }; }
        RecoveryCases.Open(connection, transaction, new(new(RecoveryTargetKind.RequestExecution, request.RequestId), session.SessionId, now, 1)
        {
            RequestId = request.RequestId,
            DispatchId = reservation.DispatchId,
            Binding = known,
            OperationReference = stored.OperationReference,
        });
    }
}
