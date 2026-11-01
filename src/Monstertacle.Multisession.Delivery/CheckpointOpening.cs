using Microsoft.Data.Sqlite;

namespace Monstertacle.Multisession.Delivery;

/// <summary>Owner initialization, identity check and crash conversion performed while the lease is held.</summary>
internal static class CheckpointOpening
{
    public static async Task PrepareAsync(SqliteConnection connection, DeliveryCheckpointOptions options, bool owner, CancellationToken cancellationToken)
    {
        await CheckpointFormat.VerifyAsync(connection, checkpointRequired: !owner, cancellationToken);
        if (owner)
        {
            await ExecuteAsync(connection, "PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; " + CheckpointFormat.CreateIfMissing(CheckpointFormat.CheckpointTable), cancellationToken);
            using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT OR IGNORE INTO checkpoint(id,schema,instance,project,route,cursor) VALUES(1,1,$consumer,$source,$route,$position)";
            AddIdentity(insert, options.Channel);
            insert.Parameters.AddWithValue("$position", options.InitialPosition);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        await VerifyIdentityAsync(connection, options.Channel, cancellationToken);
        if (owner)
        {
            // A Sending phase found at owner open means the previous owner may have caused the effect: never resend automatically.
            await ExecuteAsync(connection, "UPDATE checkpoint SET phase='uncertain' WHERE id=1 AND phase='sending'", cancellationToken);
        }
    }

    private static async Task VerifyIdentityAsync(SqliteConnection connection, DeliveryChannel channel, CancellationToken cancellationToken)
    {
        using var query = connection.CreateCommand();
        query.CommandText = "SELECT schema, instance, project, route FROM checkpoint WHERE id=1";
        using var row = await query.ExecuteReaderAsync(cancellationToken);
        if (!await row.ReadAsync(cancellationToken))
        {
            throw CheckpointFormat.Corrupt();
        }
        if (row.GetFieldType(0) != typeof(long) || row.GetInt64(0) != CheckpointFormat.Version)
        {
            throw new DeliveryRefusalException(DeliveryRefusal.UnsupportedFormat);
        }
        if (row.GetString(1) != channel.ConsumerId || row.GetString(2) != channel.SourceId || row.GetString(3) != channel.RouteId)
        {
            throw new DeliveryRefusalException(DeliveryRefusal.IdentityMismatch);
        }
    }

    private static void AddIdentity(SqliteCommand command, DeliveryChannel channel)
    {
        command.Parameters.AddWithValue("$consumer", channel.ConsumerId);
        command.Parameters.AddWithValue("$source", channel.SourceId);
        command.Parameters.AddWithValue("$route", channel.RouteId);
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
