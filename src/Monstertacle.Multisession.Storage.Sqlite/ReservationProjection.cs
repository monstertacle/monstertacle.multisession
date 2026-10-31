using System.Globalization;
using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal sealed record QueuedStoreRow(RequestSnapshot Request, SubmitRequest Submission, SessionSnapshot Session);

internal static class ReservationProjection
{
    internal static List<ExecutionReservation> Active(SqliteConnection connection, SqliteTransaction transaction)
    {
        using SqliteCommand command = SqliteStatements.Create(connection, transaction,
            "SELECT DispatchId,RequestId,SessionId,WorkspaceKey,ReservedUtc FROM Reservations WHERE ReleasedUtc IS NULL");
        using SqliteDataReader reader = command.ExecuteReader();
        List<ExecutionReservation> reservations = [];
        while (reader.Read())
        {
            reservations.Add(new(Guid.ParseExact(reader.GetString(0), "D"), Guid.ParseExact(reader.GetString(1), "D"), Guid.ParseExact(reader.GetString(2), "D"),
                reader.IsDBNull(3) ? null : reader.GetString(3), DateTimeOffset.ParseExact(reader.GetString(4), "O", CultureInfo.InvariantCulture)));
        }
        return reservations;
    }

    internal static List<QueuedStoreRow> Queued(SqliteConnection connection, SqliteTransaction transaction)
    {
        using SqliteCommand command = SqliteStatements.Create(connection, transaction,
            "SELECT r.SnapshotJson,r.SubmissionJson,s.SnapshotJson FROM Requests r JOIN Sessions s ON s.SessionId=r.SessionId WHERE r.State=$queued",
            ("$queued", (int)RequestState.Queued));
        using SqliteDataReader reader = command.ExecuteReader();
        List<QueuedStoreRow> queued = [];
        while (reader.Read())
        {
            queued.Add(new(StoreSerialization.Read<RequestSnapshot>(reader.GetString(0)), StoreSerialization.Read<SubmitRequest>(reader.GetString(1)),
                StoreSerialization.Read<SessionSnapshot>(reader.GetString(2))));
        }
        return queued;
    }
}
