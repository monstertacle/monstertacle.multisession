using Microsoft.Data.Sqlite;

namespace Monstertacle.Multisession.Delivery;

/// <summary>Journal of attempt grants (current table <c>preparation_rearms</c>); the checkpoint row does not change.</summary>
internal sealed class AttemptJournal(DeliveryCheckpoint checkpoint) : GrantJournal<AttemptGrant>(checkpoint)
{
    private const string _scope = "configuration=$context AND route=$route AND cursor=$cursor AND pending_id=$item AND pending_digest=$digest";

    protected override string Table => "preparation_rearms";

    protected override string Definition => CheckpointFormat.AttemptJournal;

    public Task<IReadOnlyList<StoredAttemptGrant>> ListAsync(string contextDigest, CheckpointExpectation expected, Guid itemId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expected);
        CheckpointInputs.Identifier(contextDigest);
        cancellationToken.ThrowIfCancellationRequested();
        return StorageFailures.RunAsync<IReadOnlyList<StoredAttemptGrant>>(async () =>
        {
            var grants = new List<StoredAttemptGrant>();
            if (!await ExistsAsync(null, cancellationToken))
            {
                return grants;
            }
            using var query = ScopeQuery(null, "SELECT request_id, attempts, receipt FROM preparation_rearms WHERE " + _scope + " ORDER BY attempts", contextDigest, expected, itemId);
            using var rows = await query.ExecuteReaderAsync(cancellationToken);
            while (await rows.ReadAsync(cancellationToken))
            {
                var attempts = rows.GetInt64(1);
                grants.Add(Guid.TryParse(rows.GetString(0), out var request) && attempts is >= 0 and <= int.MaxValue
                    ? new StoredAttemptGrant(request, (int)attempts, rows.GetString(2))
                    : throw CheckpointFormat.Corrupt());
            }
            return grants;
        });
    }

    protected override void Validate(AttemptGrant request) => CheckpointInputs.Identifier(request.ContextDigest);

    protected override bool Eligible(CheckpointExpectation expected) => expected.Phase == DeliveryPhase.Pending;

    protected override bool StoredMatches(SqliteDataReader stored, AttemptGrant request, GrantKey key) =>
        stored.GetString(1) == request.ContextDigest && stored.GetString(2) == key.Expected.RouteId && stored.GetInt64(3) == key.Expected.Position &&
        stored.GetString(4) == key.Item && stored.GetString(5) == key.Expected.PendingDigest && stored.GetInt64(6) == key.Expected.Attempts;

    /// <summary>Grants for one item must be recorded at strictly increasing attempt counts.</summary>
    protected override async Task RequireAllowedAsync(SqliteTransaction transaction, AttemptGrant request, GrantKey key, CancellationToken cancellationToken)
    {
        if (!await ExistsAsync(transaction, cancellationToken))
        {
            return;
        }
        using var query = ScopeQuery(transaction, "SELECT COUNT(*) FROM preparation_rearms WHERE " + _scope + " AND attempts>=$attempts", request.ContextDigest, key.Expected, key.ItemId);
        query.Parameters.AddWithValue("$attempts", key.Expected.Attempts);
        if (await CountAsync(query, cancellationToken) != 0)
        {
            throw new DeliveryRefusalException(DeliveryRefusal.NotEligible);
        }
    }

    protected override SqliteCommand InsertCommand(SqliteTransaction transaction, AttemptGrant request, GrantKey key)
    {
        var insert = ScopeQuery(transaction, "INSERT INTO preparation_rearms VALUES($id,$context,$route,$cursor,$item,$digest,$attempts,$receipt)", request.ContextDigest, key.Expected, key.ItemId);
        insert.Parameters.AddWithValue("$id", key.RequestId.ToString());
        insert.Parameters.AddWithValue("$attempts", key.Expected.Attempts);
        insert.Parameters.AddWithValue("$receipt", key.Receipt);
        return insert;
    }

    private SqliteCommand ScopeQuery(SqliteTransaction? transaction, string sql, string context, CheckpointExpectation expected, Guid itemId)
    {
        var command = Command(transaction, sql);
        command.Parameters.AddWithValue("$context", context);
        command.Parameters.AddWithValue("$route", expected.RouteId);
        command.Parameters.AddWithValue("$cursor", expected.Position);
        command.Parameters.AddWithValue("$item", itemId.ToString());
        command.Parameters.AddWithValue("$digest", expected.PendingDigest);
        return command;
    }
}
