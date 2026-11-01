using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Monstertacle.Multisession.Delivery;

/// <summary>
/// One receipt journal of the current format. Recording a grant is one library-owned SQLite transaction: idempotent
/// lookup, eligibility, exact expectation, journal-specific uniqueness, capacity, receipt insert and the state change.
/// No connection, transaction or callback leaves this type.
/// </summary>
internal abstract class GrantJournal<TRequest>(DeliveryCheckpoint checkpoint)
{
    protected abstract string Table { get; }

    protected abstract string Definition { get; }

    protected SqliteConnection Connection => checkpoint.Connection;

    public Task<string?> FindAsync(Guid requestId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return StorageFailures.RunAsync(async () =>
        {
            if (!await ExistsAsync(null, cancellationToken))
            {
                return null;
            }
            using var query = Command(null, "SELECT receipt FROM " + Table + " WHERE request_id=$id");
            query.Parameters.AddWithValue("$id", requestId.ToString());
            return await query.ExecuteScalarAsync(cancellationToken) as string;
        });
    }

    public Task<GrantOutcome> RecordAsync(TRequest request, GrantKey key, CancellationToken cancellationToken)
    {
        CheckpointInputs.Grant(key.RequestId, key.Expected, key.ItemId, key.Receipt);
        Validate(request);
        cancellationToken.ThrowIfCancellationRequested();
        return StorageFailures.RunAsync(async () =>
        {
            using var transaction = Connection.BeginTransaction(deferred: false);
            if (await StoredAsync(transaction, request, key, cancellationToken) is { } stored)
            {
                return stored;
            }
            if (!Eligible(key.Expected))
            {
                throw new DeliveryRefusalException(DeliveryRefusal.NotEligible);
            }
            var state = await CheckpointRow.ReadAsync(Connection, transaction, cancellationToken);
            if (!Matches(state, key.Expected))
            {
                throw new DeliveryRefusalException(DeliveryRefusal.StaleExpectation);
            }
            await RequireAllowedAsync(transaction, request, key, cancellationToken);
            await InsertAsync(transaction, request, key, cancellationToken);
            await ApplyAsync(transaction, request, key, state.Pending!, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            return new GrantOutcome(true, key.Receipt);
        });
    }

    protected virtual void Validate(TRequest request)
    {
    }

    protected abstract bool Eligible(CheckpointExpectation expected);

    protected abstract bool StoredMatches(SqliteDataReader stored, TRequest request, GrantKey key);

    protected abstract SqliteCommand InsertCommand(SqliteTransaction transaction, TRequest request, GrantKey key);

    protected virtual Task RequireAllowedAsync(SqliteTransaction transaction, TRequest request, GrantKey key, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    protected virtual Task ApplyAsync(SqliteTransaction transaction, TRequest request, GrantKey key, PendingItem pending, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    protected SqliteCommand Command(SqliteTransaction? transaction, string sql)
    {
        var command = Connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return command;
    }

    protected static async Task<long> CountAsync(SqliteCommand query, CancellationToken cancellationToken) =>
        Convert.ToInt64(await query.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);

    protected async Task<bool> ExistsAsync(SqliteTransaction? transaction, CancellationToken cancellationToken)
    {
        using var query = Command(transaction, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name");
        query.Parameters.AddWithValue("$name", Table);
        return await CountAsync(query, cancellationToken) == 1;
    }

    /// <summary>Compare-and-swap on everything the expectation froze; the stored bytes stay exactly as they are.</summary>
    protected static async Task RequireSingleRowAsync(SqliteCommand change, CheckpointExpectation expected, PendingItem pending, CancellationToken cancellationToken)
    {
        change.Parameters.AddWithValue("$route", expected.RouteId);
        change.Parameters.AddWithValue("$cursor", expected.Position);
        change.Parameters.AddWithValue("$attempts", expected.Attempts);
        change.Parameters.AddWithValue("$payload", CheckpointInputs.Decode(pending.Payload.Span));
        if (await change.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new DeliveryRefusalException(DeliveryRefusal.StaleExpectation);
        }
    }

    private static bool Matches(CheckpointState state, CheckpointExpectation expected) =>
        state.Pending is { } pending && state.Phase == expected.Phase && state.RouteId == expected.RouteId &&
        state.Position == expected.Position && state.Attempts == expected.Attempts && pending.Digest == expected.PendingDigest;

    private async Task<GrantOutcome?> StoredAsync(SqliteTransaction transaction, TRequest request, GrantKey key, CancellationToken cancellationToken)
    {
        if (!await ExistsAsync(transaction, cancellationToken))
        {
            return null;
        }
        using var query = Command(transaction, "SELECT * FROM " + Table + " WHERE request_id=$id");
        query.Parameters.AddWithValue("$id", key.RequestId.ToString());
        using var stored = await query.ExecuteReaderAsync(cancellationToken);
        if (!await stored.ReadAsync(cancellationToken))
        {
            return null;
        }
        return StoredMatches(stored, request, key)
            ? new GrantOutcome(false, stored.GetString(stored.GetOrdinal("receipt")))
            : throw new DeliveryRefusalException(DeliveryRefusal.RequestConflict);
    }

    private async Task InsertAsync(SqliteTransaction transaction, TRequest request, GrantKey key, CancellationToken cancellationToken)
    {
        using (var create = Command(transaction, CheckpointFormat.CreateIfMissing(Definition)))
        {
            await create.ExecuteNonQueryAsync(cancellationToken);
        }
        using (var count = Command(transaction, "SELECT COUNT(*) FROM " + Table))
        {
            if (await CountAsync(count, cancellationToken) >= checkpoint.Options.MaximumRetainedGrants)
            {
                throw new DeliveryRefusalException(DeliveryRefusal.GrantCapacity);
            }
        }
        using var insert = InsertCommand(transaction, request, key);
        await insert.ExecuteNonQueryAsync(cancellationToken);
    }
}
