namespace Monstertacle.Multisession.Checkpoints.Delivery;

/// <summary>
/// The delivery operations of an open checkpoint, for ordinary host transition code. Composition keeps the concrete
/// <see cref="DeliveryCheckpoint"/> it opened, constructs grant helpers from it and passes this port, a view of the same
/// object, to the delivery loop; disposing either reference ends the same lease once.
/// </summary>
public interface IDeliveryCheckpoint : ICheckpoint<CheckpointState>
{
    /// <summary>Stages the item at the next position when nothing is pending; attempts restart at zero.</summary>
    /// <param name="position">Source position of the item; must be the stored position plus one and at least 1.</param>
    /// <param name="payload">Canonical UTF-8 bytes produced by the host; never interpreted.</param>
    /// <param name="cancellationToken">Cancels before the change.</param>
    /// <returns>A task completing once the item is durably staged.</returns>
    /// <exception cref="DeliveryRefusalException"><see cref="DeliveryRefusal.TransitionConflict"/> or <see cref="DeliveryRefusal.InvalidInput"/>.</exception>
    Task StageAsync(long position, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);

    /// <summary>Marks the pending item Sending and counts the attempt; call immediately before the external effect.</summary>
    /// <param name="cancellationToken">Cancels before the change.</param>
    /// <returns>A task completing once Sending is durable.</returns>
    /// <exception cref="DeliveryRefusalException"><see cref="DeliveryRefusal.TransitionConflict"/> unless Pending.</exception>
    Task MarkSendingAsync(CancellationToken cancellationToken);

    /// <summary>Counts a failed preparation that caused no external effect, only while Pending.</summary>
    /// <param name="cancellationToken">Cancels before the change.</param>
    /// <returns>True when the counter changed.</returns>
    Task<bool> RecordPreparationFailureAsync(CancellationToken cancellationToken);

    /// <summary>Records that the outcome of the current send is unknown, only from Sending.</summary>
    /// <param name="cancellationToken">Cancels before the change.</param>
    /// <returns>True when the phase changed.</returns>
    Task<bool> MarkUncertainAsync(CancellationToken cancellationToken);

    /// <summary>Records a positive acknowledgement for exactly the stored bytes; the position advances and the item clears.</summary>
    /// <param name="expectedPayload">The bytes that were sent; must equal the stored bytes.</param>
    /// <param name="acknowledgement">Non-blank acknowledgement of at most 1024 characters.</param>
    /// <param name="cancellationToken">Cancels before the change.</param>
    /// <returns>A task completing once Accepted is durable.</returns>
    /// <exception cref="DeliveryRefusalException"><see cref="DeliveryRefusal.TransitionConflict"/> or <see cref="DeliveryRefusal.InvalidInput"/>.</exception>
    Task AcceptAsync(ReadOnlyMemory<byte> expectedPayload, string acknowledgement, CancellationToken cancellationToken);
}
