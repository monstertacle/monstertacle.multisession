using Microsoft.Data.Sqlite;

namespace Monstertacle.Multisession.DeliveryCheckpoints;

/// <summary>Journal of single resend authorizations (current table <c>delivery_replays</c>, one row per item ever).</summary>
internal sealed class ResendJournal(DeliveryCheckpoint checkpoint) : GrantJournal<ResendAuthorization>(checkpoint)
{
    protected override string Table => "delivery_replays";

    protected override string Definition => CheckpointFormat.ResendJournal;

    protected override bool Eligible(CheckpointExpectation expected) =>
        expected.Phase == DeliveryPhase.Uncertain && expected.Attempts is >= 1 and < int.MaxValue;

    protected override bool StoredMatches(SqliteDataReader stored, ResendAuthorization request, GrantKey key) => stored.GetString(1) == key.Item;

    protected override async Task RequireAllowedAsync(SqliteTransaction transaction, ResendAuthorization request, GrantKey key, CancellationToken cancellationToken)
    {
        if (!await ExistsAsync(transaction, cancellationToken))
        {
            return;
        }
        using var query = Command(transaction, "SELECT COUNT(*) FROM delivery_replays WHERE pending_id=$item");
        query.Parameters.AddWithValue("$item", key.Item);
        if (await CountAsync(query, cancellationToken) != 0)
        {
            throw new DeliveryRefusalException(DeliveryRefusal.AllowanceConsumed);
        }
    }

    protected override SqliteCommand InsertCommand(SqliteTransaction transaction, ResendAuthorization request, GrantKey key)
    {
        var insert = Command(transaction, "INSERT INTO delivery_replays VALUES($id,$item,$receipt)");
        insert.Parameters.AddWithValue("$id", key.RequestId.ToString());
        insert.Parameters.AddWithValue("$item", key.Item);
        insert.Parameters.AddWithValue("$receipt", key.Receipt);
        return insert;
    }

    protected override async Task ApplyAsync(SqliteTransaction transaction, ResendAuthorization request, GrantKey key, PendingItem pending, CancellationToken cancellationToken)
    {
        using var change = Command(transaction, "UPDATE checkpoint SET phase='sending',attempts=attempts+1 WHERE id=1 AND phase='uncertain' AND cursor=$cursor AND attempts=$attempts AND route=$route AND pending=$payload");
        await RequireSingleRowAsync(change, key.Expected, pending, cancellationToken);
    }
}
