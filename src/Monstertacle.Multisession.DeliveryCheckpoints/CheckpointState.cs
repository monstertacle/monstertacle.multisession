namespace Monstertacle.Multisession.DeliveryCheckpoints;

/// <summary>Durable state of a checkpoint as read in one statement.</summary>
/// <param name="Position">Last accepted source position.</param>
/// <param name="Pending">The staged item, if any.</param>
/// <param name="Phase">Phase, or null before the first item was staged.</param>
/// <param name="Attempts">Attempt counter of the pending item; zero after acceptance.</param>
/// <param name="Acknowledgement">Last positive acknowledgement recorded by <see cref="DeliveryCheckpoint.AcceptAsync"/>, if any.</param>
/// <param name="RouteId">Current route identity.</param>
public sealed record CheckpointState(long Position, PendingItem? Pending, DeliveryPhase? Phase, int Attempts, string? Acknowledgement, string RouteId)
{
    /// <summary>The exact expectation describing this state, for use in a grant the host authorizes.</summary>
    /// <returns>The expectation of the pending item.</returns>
    /// <exception cref="DeliveryRefusalException">Nothing is pending (<see cref="DeliveryRefusal.NotEligible"/>).</exception>
    public CheckpointExpectation ToExpectation() =>
        Pending is { } pending && Phase is { } phase
            ? new CheckpointExpectation(RouteId, Position, pending.Digest, phase, Attempts)
            : throw new DeliveryRefusalException(DeliveryRefusal.NotEligible);
}
