namespace Monstertacle.Multisession.DeliveryCheckpoints;

/// <summary>Typed reason a checkpoint operation was refused. Refusals never change durable state.</summary>
public enum DeliveryRefusal
{
    /// <summary>An existing checkpoint was required and the file is missing.</summary>
    CheckpointMissing,

    /// <summary>The checkpoint belongs to another consumer, source or route.</summary>
    IdentityMismatch,

    /// <summary>The checkpoint declares a format version this package does not read.</summary>
    UnsupportedFormat,

    /// <summary>The file is not a checkpoint of the supported format or its stored values are inconsistent.</summary>
    CheckpointCorrupt,

    /// <summary>Another owner holds the exclusive checkpoint lease, or the lease file could not be opened; see the inner exception.</summary>
    ExclusiveAccessUnavailable,

    /// <summary>The database stayed locked by another writer beyond the configured busy timeout.</summary>
    StorageBusy,

    /// <summary>The requested transition does not apply to the current phase or position.</summary>
    TransitionConflict,

    /// <summary>The checkpoint no longer matches the expectation the request was authorized for.</summary>
    StaleExpectation,

    /// <summary>The expectation does not describe a state from which this grant may be recorded.</summary>
    NotEligible,

    /// <summary>The single resend allowance of this item was already consumed.</summary>
    AllowanceConsumed,

    /// <summary>A request with the same id was recorded with different values.</summary>
    RequestConflict,

    /// <summary>The grant journal reached its retained capacity; nothing is evicted.</summary>
    GrantCapacity,

    /// <summary>A payload, acknowledgement, receipt or identifier is outside the supported bounds.</summary>
    InvalidInput,
}
