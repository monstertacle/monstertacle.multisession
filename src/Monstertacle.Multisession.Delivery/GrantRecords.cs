namespace Monstertacle.Multisession.Delivery;

/// <summary>Host-authorized grant of further attempts for a pending item. The checkpoint row itself does not change.</summary>
/// <param name="RequestId">Idempotency key of the authorization.</param>
/// <param name="Expected">Exact expected checkpoint state; phase must be <see cref="DeliveryPhase.Pending"/>.</param>
/// <param name="ItemId">Host-attested identity of the pending item.</param>
/// <param name="ContextDigest">Host-defined digest of the authorizing context, for example its configuration.</param>
/// <param name="Receipt">Canonical receipt text produced by the host; stored and returned unchanged.</param>
public sealed record AttemptGrant(Guid RequestId, CheckpointExpectation Expected, Guid ItemId, string ContextDigest, string Receipt);

/// <summary>Host-authorized single resend of an item whose earlier send outcome is unknown.</summary>
/// <param name="RequestId">Idempotency key of the authorization.</param>
/// <param name="Expected">Exact expected checkpoint state; phase must be <see cref="DeliveryPhase.Uncertain"/>.</param>
/// <param name="ItemId">Host-attested identity of the pending item; at most one authorization per item ever. Derive it
/// deterministically from the canonical item, never per request.</param>
/// <param name="Receipt">Canonical receipt text produced by the host; stored and returned unchanged.</param>
public sealed record ResendAuthorization(Guid RequestId, CheckpointExpectation Expected, Guid ItemId, string Receipt);

/// <summary>Host-authorized move of a never-sent pending item to another route.</summary>
/// <param name="RequestId">Idempotency key of the authorization.</param>
/// <param name="Expected">Exact expected checkpoint state; phase must be <see cref="DeliveryPhase.Pending"/>.</param>
/// <param name="ItemId">Host-attested identity of the pending item.</param>
/// <param name="NewRouteId">Route identity after the move; must differ from the expected route.</param>
/// <param name="Receipt">Canonical receipt text produced by the host; stored and returned unchanged.</param>
public sealed record RouteMove(Guid RequestId, CheckpointExpectation Expected, Guid ItemId, string NewRouteId, string Receipt);

/// <summary>Result of a grant: either newly recorded with the supplied receipt, or the receipt stored earlier for the same request.</summary>
/// <param name="Recorded">True when this call recorded the grant and its transition.</param>
/// <param name="Receipt">Receipt text stored for the request.</param>
public sealed record GrantOutcome(bool Recorded, string Receipt);

/// <summary>An attempt grant stored for one pending item.</summary>
/// <param name="RequestId">Idempotency key of the grant.</param>
/// <param name="Attempts">Attempt counter the grant was recorded at.</param>
/// <param name="Receipt">Stored receipt text.</param>
public sealed record StoredAttemptGrant(Guid RequestId, int Attempts, string Receipt);
