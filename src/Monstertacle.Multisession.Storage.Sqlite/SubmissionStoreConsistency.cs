using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class SubmissionStoreConsistency
{
    internal static void Assert(SqliteConnection connection, SqliteTransaction transaction, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using SqliteCommand command = SqliteStatements.Create(connection, transaction, """
            SELECT r.SubmissionJson,s.SnapshotJson FROM Requests r JOIN Sessions s ON s.SessionId=r.SessionId
            ORDER BY r.RequestId COLLATE BINARY
            """);
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            token.ThrowIfCancellationRequested();
            SubmitRequest submission = StoreSerialization.Read<SubmitRequest>(reader.GetString(0));
            SessionSnapshot session = StoreSerialization.Read<SessionSnapshot>(reader.GetString(1));
            // Historical acceptance is not re-evaluated against a later host's smaller byte limit or closed lifecycle.
            if (RequestSubmissionRules.ValidateFrozen(submission, session.Context, int.MaxValue) is not null)
            { throw StorageFailurePolicy.Error(ErrorCode.StorageUnavailable, false, false); }
        }
    }
}
