using System.Globalization;
using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class StoredDispatchAccess
{
    internal static StoredDispatch? Read(SqliteConnection connection, Guid dispatchId, SqliteTransaction? transaction = null)
    {
        using SqliteCommand command = SqliteStatements.Create(connection, transaction,
            """
            SELECT v.DispatchId,v.RequestId,v.SessionId,v.WorkspaceKey,v.ReservedUtc,r.Sequence,r.BindingRevision,r.ReservedDispatchJson
            FROM Reservations v JOIN Requests r ON r.DispatchId=v.DispatchId AND r.RequestId=v.RequestId
            WHERE v.DispatchId=$id AND v.ReleasedUtc IS NULL AND r.State=$running AND r.CancellationRequestedUtc IS NULL
            """, ("$id", dispatchId.ToString("D")), ("$running", (int)RequestState.Running));
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read()) { return null; }
        ExecutionReservation reservation = new(Guid.ParseExact(reader.GetString(0), "D"), Guid.ParseExact(reader.GetString(1), "D"),
            Guid.ParseExact(reader.GetString(2), "D"), reader.IsDBNull(3) ? null : reader.GetString(3),
            DateTimeOffset.ParseExact(reader.GetString(4), "O", CultureInfo.InvariantCulture));
        StoredDispatch envelope = StoreSerialization.Read<StoredDispatch>(reader.GetString(7));
        ReservedDispatch snapshot = envelope.Snapshot;
        if (snapshot.Reservation != reservation || snapshot.Session.SessionId != reservation.SessionId
            || snapshot.Session.Context.SessionId != reservation.SessionId || snapshot.Submission.SessionId != reservation.SessionId
            || snapshot.Sequence != reader.GetInt64(5) || snapshot.BindingRevision != reader.GetInt64(6)
            || snapshot.Session.BindingRevision != snapshot.BindingRevision || snapshot.Session.Binding is null)
        {
            throw StorageFailurePolicy.Error(ErrorCode.StorageUnavailable, false, false);
        }
        return envelope;
    }
}
