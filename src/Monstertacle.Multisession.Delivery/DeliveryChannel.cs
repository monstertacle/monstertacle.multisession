namespace Monstertacle.Multisession.Delivery;

/// <summary>
/// Persisted identity of one delivery channel. A checkpoint opens only for the identity it was created with.
/// </summary>
/// <param name="ConsumerId">Identity of the consuming host instance that owns the checkpoint.</param>
/// <param name="SourceId">Identity of the ordered source whose positions the checkpoint follows.</param>
/// <param name="RouteId">Host-defined identity (for example a digest) of the current destination route.</param>
public sealed record DeliveryChannel(string ConsumerId, string SourceId, string RouteId);
