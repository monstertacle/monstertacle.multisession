using Microsoft.Data.Sqlite;

namespace Monstertacle.Multisession.Delivery;

/// <summary>Reads and validates the single checkpoint row, returning stored bytes exactly.</summary>
internal static class CheckpointRow
{
    private const string _query = "SELECT cursor, typeof(cursor), CAST(pending AS BLOB), typeof(pending), phase, typeof(phase), attempts, typeof(attempts), receipt, typeof(receipt), route FROM checkpoint WHERE id=1";

    public static async Task<CheckpointState> ReadAsync(SqliteConnection connection, SqliteTransaction? transaction, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = _query;
        using var row = await command.ExecuteReaderAsync(cancellationToken);
        if (!await row.ReadAsync(cancellationToken) || row.GetString(1) != "integer" || row.GetString(7) != "integer" ||
            !IsTextOrNull(row, 3) || !IsTextOrNull(row, 5) || !IsTextOrNull(row, 9))
        {
            throw CheckpointFormat.Corrupt();
        }
        var position = row.GetInt64(0);
        var attempts = row.GetInt64(6);
        var phase = CheckpointFormat.Phase(row.IsDBNull(4) ? null : row.GetString(4));
        var pending = row.IsDBNull(2) ? null : (byte[])row.GetValue(2);
        if (position < 0 || attempts is < 0 or > int.MaxValue || !Consistent(phase, pending) ||
            pending is not null && (pending.Length == 0 || CheckpointInputs.Decode(pending) is null))
        {
            throw CheckpointFormat.Corrupt();
        }
        var item = pending is null ? null : new PendingItem(position + 1, pending);
        return new CheckpointState(position, item, phase, (int)attempts, row.IsDBNull(8) ? null : row.GetString(8), row.GetString(10));
    }

    private static bool IsTextOrNull(SqliteDataReader row, int typeColumn) => row.GetString(typeColumn) is "text" or "null";

    private static bool Consistent(DeliveryPhase? phase, byte[]? pending) =>
        phase is null or DeliveryPhase.Accepted ? pending is null : pending is not null;
}
