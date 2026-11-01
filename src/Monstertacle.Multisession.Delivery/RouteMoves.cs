namespace Monstertacle.Multisession.Delivery;

/// <summary>
/// Moves a never-sent pending item to another route together with its receipt in one transaction. Position, payload
/// bytes, attempts and every earlier receipt stay; afterwards only the new channel identity opens the checkpoint.
/// </summary>
/// <param name="checkpoint">The open checkpoint.</param>
public sealed class RouteMoves(DeliveryCheckpoint checkpoint)
{
    private readonly RouteJournal _journal = new(checkpoint ?? throw new ArgumentNullException(nameof(checkpoint)));

    /// <summary>Returns the receipt stored for a request, without creating anything.</summary>
    /// <param name="requestId">Idempotency key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The stored receipt text, or null.</returns>
    public Task<string?> FindAsync(Guid requestId, CancellationToken cancellationToken) => _journal.FindAsync(requestId, cancellationToken);

    /// <summary>Moves the route atomically, or returns the receipt already stored for the same request.</summary>
    /// <param name="request">The authorized move.</param>
    /// <param name="cancellationToken">Cancels before commit.</param>
    /// <returns>The outcome with the stored receipt text.</returns>
    /// <exception cref="DeliveryRefusalException">Not eligible, stale, conflicting, full or invalid.</exception>
    public Task<GrantOutcome> MoveAsync(RouteMove request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _journal.RecordAsync(request, new GrantKey(request.RequestId, request.Expected, request.ItemId, request.Receipt), cancellationToken);
    }
}
