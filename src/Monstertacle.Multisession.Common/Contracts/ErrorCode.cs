namespace Monstertacle.Multisession.Contracts;

/// <summary>Stable public error categories without provider response bodies.</summary>
public enum ErrorCode
{
    /// <summary>Invalid host or runtime configuration.</summary>
    InvalidConfiguration,
    /// <summary>The requested local identity does not exist.</summary>
    NotFound,
    /// <summary>The session does not accept new requests.</summary>
    SessionClosed,
    /// <summary>The session is blocked pending explicit recovery.</summary>
    SessionBlocked,
    /// <summary>The durable request queue is full.</summary>
    QueueFull,
    /// <summary>The local infrastructure work queue is full.</summary>
    LocalCapacityExceeded,
    /// <summary>Another host owns the state directory.</summary>
    StoreAlreadyOwned,
    /// <summary>Durable state is unavailable.</summary>
    StorageUnavailable,
    /// <summary>The persistent format is not supported.</summary>
    UnsupportedStoreVersion,
    /// <summary>The requested native platform is not supported.</summary>
    UnsupportedPlatform,
    /// <summary>External credentials were rejected.</summary>
    AuthenticationFailed,
    /// <summary>The external service rejected rate or quota limits.</summary>
    ProviderRateLimited,
    /// <summary>The configured execution adapter is unavailable.</summary>
    ProviderUnavailable,
    /// <summary>The native session no longer exists.</summary>
    ProviderSessionExpired,
    /// <summary>The native context limit was reached.</summary>
    ProviderContextLimit,
    /// <summary>The requested operation is not supported by the adapter.</summary>
    UnsupportedProviderAction,
    /// <summary>Execution failed.</summary>
    ExecutionFailed,
    /// <summary>Execution was cancelled locally.</summary>
    Cancelled,
    /// <summary>The dispatch deadline expired.</summary>
    TimedOut,
    /// <summary>The host stopped without a confirmed final result.</summary>
    Interrupted,
    /// <summary>A recovery decision targets an outdated revision.</summary>
    RevisionConflict,
    /// <summary>The input is invalid.</summary>
    InvalidInput,
    /// <summary>The inclusive input byte limit was exceeded.</summary>
    InputLimitExceeded,
    /// <summary>The inclusive output byte limit was exceeded.</summary>
    OutputLimitExceeded,
    /// <summary>The local acceptance deadline expired.</summary>
    AdmissionTimedOut,
}
