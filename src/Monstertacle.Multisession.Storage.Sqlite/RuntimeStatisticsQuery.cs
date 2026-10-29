using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class RuntimeStatisticsQuery
{
    internal static RuntimeStatistics Read(SqliteConnection connection)
    {
        using SqliteCommand command = SqliteStatements.Create(connection, null, """
            SELECT
                (SELECT count(*) FROM Requests WHERE State = $queued),
                (SELECT count(*) FROM Reservations WHERE ReleasedUtc IS NULL),
                (SELECT count(*) FROM Reservations v JOIN Requests r ON r.RequestId = v.RequestId
                    WHERE v.ReleasedUtc IS NULL AND r.Certainty = $unknown)
            """, ("$queued", (int)RequestState.Queued), ("$unknown", (int)ExecutionCertainty.Unknown));
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read()) { throw StorageFailurePolicy.Error(ErrorCode.StorageUnavailable, true, false); }
        return new(reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2));
    }
}
