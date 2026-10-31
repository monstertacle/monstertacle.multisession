using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class ObservationTransactions
{
    internal static void Commit(SqliteConnection connection, Guid dispatchId, ProviderObservation observation, CancellationToken token)
        => SqliteTransactions.Run(connection, transaction =>
        {
            StoredDispatch stored = StoredDispatchAccess.Read(connection, dispatchId, transaction, true)
                ?? throw new SessionOperationException(new(ErrorCode.RevisionConflict));
            string? stable = stored.Snapshot.Session.Binding?.SessionReference ?? stored.SessionReference;
            bool reference = !string.IsNullOrWhiteSpace(observation.Reference);
            StoredDispatch updated = observation.Kind switch
            {
                ProviderObservationKind.Progress => stored,
                ProviderObservationKind.OperationAccepted when reference => stored with { OperationReference = observation.Reference },
                ProviderObservationKind.SessionEstablished when reference && (stable is null || stable == observation.Reference)
                    => stored with { SessionReference = observation.Reference },
                _ => throw new SessionOperationException(new(ErrorCode.InvalidInput)),
            };
            using SqliteCommand command = SqliteStatements.Create(connection, transaction,
                "UPDATE Requests SET ReservedDispatchJson=$snapshot WHERE DispatchId=$id", ("$id", dispatchId.ToString("D")), ("$snapshot", StoreSerialization.Write(updated)));
            command.ExecuteNonQuery();
            return true;
        }, token);
}
