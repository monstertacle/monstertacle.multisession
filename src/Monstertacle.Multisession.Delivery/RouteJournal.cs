using Microsoft.Data.Sqlite;

namespace Monstertacle.Multisession.Delivery;

/// <summary>Journal of route moves (current table <c>delivery_rebinds</c>); only the route of a never-sent item changes.</summary>
internal sealed class RouteJournal(DeliveryCheckpoint checkpoint) : GrantJournal<RouteMove>(checkpoint)
{
    protected override string Table => "delivery_rebinds";

    protected override string Definition => CheckpointFormat.RouteJournal;

    protected override void Validate(RouteMove request)
    {
        CheckpointInputs.Identifier(request.NewRouteId);
        if (request.NewRouteId == request.Expected.RouteId)
        {
            throw new DeliveryRefusalException(DeliveryRefusal.InvalidInput);
        }
    }

    protected override bool Eligible(CheckpointExpectation expected) => expected.Phase == DeliveryPhase.Pending;

    protected override bool StoredMatches(SqliteDataReader stored, RouteMove request, GrantKey key) =>
        stored.GetString(1) == key.Expected.RouteId && stored.GetString(2) == request.NewRouteId &&
        stored.GetInt64(3) == key.Expected.Position && (stored.IsDBNull(4) ? null : stored.GetString(4)) == key.Item;

    protected override SqliteCommand InsertCommand(SqliteTransaction transaction, RouteMove request, GrantKey key)
    {
        var insert = Command(transaction, "INSERT INTO delivery_rebinds VALUES($id,$old,$new,$cursor,$item,$receipt)");
        insert.Parameters.AddWithValue("$id", key.RequestId.ToString());
        insert.Parameters.AddWithValue("$old", key.Expected.RouteId);
        insert.Parameters.AddWithValue("$new", request.NewRouteId);
        insert.Parameters.AddWithValue("$cursor", key.Expected.Position);
        insert.Parameters.AddWithValue("$item", key.Item);
        insert.Parameters.AddWithValue("$receipt", key.Receipt);
        return insert;
    }

    protected override async Task ApplyAsync(SqliteTransaction transaction, RouteMove request, GrantKey key, PendingItem pending, CancellationToken cancellationToken)
    {
        using var change = Command(transaction, "UPDATE checkpoint SET route=$new WHERE id=1 AND route=$route AND cursor=$cursor AND attempts=$attempts AND phase='pending' AND pending=$payload");
        change.Parameters.AddWithValue("$new", request.NewRouteId);
        await RequireSingleRowAsync(change, key.Expected, pending, cancellationToken);
    }
}
