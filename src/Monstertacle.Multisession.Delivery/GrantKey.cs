namespace Monstertacle.Multisession.Delivery;

/// <summary>The fields every grant kind shares: idempotency key, exact expectation, attested item and receipt text.</summary>
internal readonly record struct GrantKey(Guid RequestId, CheckpointExpectation Expected, Guid ItemId, string Receipt)
{
    public string Item => ItemId.ToString();
}
