namespace Monstertacle.Multisession.Delivery;

/// <summary>
/// Records the single host-authorized resend of an Uncertain item together with its Uncertain → Sending transition in one
/// transaction. At most one authorization exists per item, also for a fresh request id or after restart. The resend
/// itself, and accepting a possible duplicate, remain the host's responsibility.
/// </summary>
/// <param name="checkpoint">The open checkpoint.</param>
public sealed class ResendAuthorizations(DeliveryCheckpoint checkpoint)
{
    private readonly ResendJournal _journal = new(checkpoint ?? throw new ArgumentNullException(nameof(checkpoint)));

    /// <summary>Returns the receipt stored for a request, without creating anything.</summary>
    /// <param name="requestId">Idempotency key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The stored receipt text, or null.</returns>
    public Task<string?> FindAsync(Guid requestId, CancellationToken cancellationToken) => _journal.FindAsync(requestId, cancellationToken);

    /// <summary>Consumes the single resend allowance atomically, or returns the receipt already stored for the same request.</summary>
    /// <param name="request">The authorized resend.</param>
    /// <param name="cancellationToken">Cancels before commit.</param>
    /// <returns>The outcome with the stored receipt text.</returns>
    /// <exception cref="DeliveryRefusalException">Not eligible, stale, consumed, conflicting, full or invalid.</exception>
    public Task<GrantOutcome> AuthorizeAsync(ResendAuthorization request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _journal.RecordAsync(request, new GrantKey(request.RequestId, request.Expected, request.ItemId, request.Receipt), cancellationToken);
    }
}
