using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Scheduling;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class ReservationTransactions
{
    internal static ExecutionReservation? Reserve(SqliteConnection connection, SqliteStoreState state, int maximumConcurrent, CancellationToken token)
    {
        if (!state.CanDispatch) { return null; }
        return SqliteTransactions.Run<ExecutionReservation?>(connection, transaction =>
        {
            List<ExecutionReservation> active = ReservationProjection.Active(connection, transaction);
            List<QueuedStoreRow> rows = ReservationProjection.Queued(connection, transaction);
            QueuedRequest[] queued = rows.Select(row => new QueuedRequest(row.Request.RequestId, row.Request.SessionId, row.Request.Sequence,
                row.Request.AcceptedUtc, row.Submission.WriteIntent ? row.Submission.WorkspaceKey : null)).ToArray();
            QueuedRequest? head = ReservationPolicy.SelectHead(queued, active, rows.Select(row => row.Session).DistinctBy(session => session.SessionId).ToArray(), maximumConcurrent);
            if (head is null) { return null; }
            QueuedStoreRow selected = rows.Single(row => row.Request.RequestId == head.RequestId);
            return Insert(connection, state, transaction, head, selected);
        }, token);
    }

    private static ExecutionReservation Insert(SqliteConnection connection, SqliteStoreState state, SqliteTransaction transaction, QueuedRequest head, QueuedStoreRow row)
    {
        ExecutionReservation reservation = new(Guid.NewGuid(), head.RequestId, head.SessionId, head.WriteWorkspaceKey, state.Clock.GetUtcNow());
        ReservedDispatch snapshot = new(reservation, row.Session, row.Submission, row.Request.Sequence, row.Session.BindingRevision);
        RequestSnapshot running = row.Request with { State = RequestState.Running, Certainty = ExecutionCertainty.InProgress, StartedUtc = reservation.ReservedUtc };
        using SqliteCommand command = SqliteStatements.Create(connection, transaction,
            """
            INSERT INTO Reservations (DispatchId,RequestId,SessionId,WorkspaceKey,ReservedUtc) VALUES ($dispatch,$request,$session,$workspace,$started);
            UPDATE Requests SET State=$state,Certainty=$certainty,StartedUtc=$started,DispatchId=$dispatch,BindingRevision=$revision,
                SnapshotJson=$requestSnapshot,ReservedDispatchJson=$reserved WHERE RequestId=$request;
            """, ("$dispatch", reservation.DispatchId.ToString("D")), ("$request", reservation.RequestId.ToString("D")),
            ("$session", reservation.SessionId.ToString("D")), ("$workspace", reservation.WorkspaceKey), ("$started", StoreSerialization.Utc(reservation.ReservedUtc)),
            ("$state", (int)running.State), ("$certainty", (int)running.Certainty), ("$revision", snapshot.BindingRevision),
            ("$requestSnapshot", StoreSerialization.Write(running)), ("$reserved", StoreSerialization.Write(new StoredDispatch(snapshot))));
        command.ExecuteNonQuery();
        return reservation;
    }
}
