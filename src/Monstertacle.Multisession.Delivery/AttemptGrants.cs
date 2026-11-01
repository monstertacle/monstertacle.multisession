namespace Monstertacle.Multisession.Delivery;

/// <summary>
/// Records host-authorized further attempts for a pending item. The host decides eligibility and allowance; the library
/// enforces the exact expectation, strictly increasing attempt counts per item, idempotency and capacity.
/// </summary>
/// <param name="checkpoint">The open checkpoint.</param>
public sealed class AttemptGrants(DeliveryCheckpoint checkpoint)
{
    private readonly AttemptJournal _journal = new(checkpoint ?? throw new ArgumentNullException(nameof(checkpoint)));

    /// <summary>Returns the receipt stored for a request, without creating anything.</summary>
    /// <param name="requestId">Idempotency key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The stored receipt text, or null.</returns>
    public Task<string?> FindAsync(Guid requestId, CancellationToken cancellationToken) => _journal.FindAsync(requestId, cancellationToken);

    /// <summary>Lists the grants stored for one item in one context, ordered by attempt count.</summary>
    /// <param name="contextDigest">Host context digest the grants were recorded with.</param>
    /// <param name="expected">Route, position and digest of the item; phase and attempts are ignored.</param>
    /// <param name="itemId">Host-attested item identity.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The stored grants.</returns>
    public Task<IReadOnlyList<StoredAttemptGrant>> ListAsync(string contextDigest, CheckpointExpectation expected, Guid itemId, CancellationToken cancellationToken) =>
        _journal.ListAsync(contextDigest, expected, itemId, cancellationToken);

    /// <summary>Records the grant atomically, or returns the receipt already stored for the same request.</summary>
    /// <param name="request">The authorized grant.</param>
    /// <param name="cancellationToken">Cancels before commit.</param>
    /// <returns>The outcome with the stored receipt text.</returns>
    /// <exception cref="DeliveryRefusalException">Not eligible, stale, conflicting, full or invalid.</exception>
    public Task<GrantOutcome> RecordAsync(AttemptGrant request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _journal.RecordAsync(request, new GrantKey(request.RequestId, request.Expected, request.ItemId, request.Receipt), cancellationToken);
    }
}
