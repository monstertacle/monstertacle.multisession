namespace Monstertacle.Multisession.Delivery;

/// <summary>
/// The exact checkpoint state a grant was authorized for. Every grant compares it with the stored row inside its transaction.
/// </summary>
/// <param name="RouteId">Route identity of the checkpoint.</param>
/// <param name="Position">Last accepted source position.</param>
/// <param name="PendingDigest">Lowercase hexadecimal SHA-256 of the stored pending bytes, see <see cref="PendingItem.Digest"/>.</param>
/// <param name="Phase">Phase of the pending item.</param>
/// <param name="Attempts">Attempt counter of the pending item.</param>
public sealed record CheckpointExpectation(string RouteId, long Position, string PendingDigest, DeliveryPhase Phase, int Attempts);
