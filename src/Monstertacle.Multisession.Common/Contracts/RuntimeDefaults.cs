namespace Monstertacle.Multisession.Contracts;

/// <summary>Documented initial limits; no retention or queue-expiry policy is implied.</summary>
public static class RuntimeDefaults
{
    /// <summary>Default concurrently occupied execution slots.</summary>
    public const int MaxConcurrentRequests = 4;
    /// <summary>Default durable queued requests.</summary>
    public const int MaxQueuedRequests = 100;
    /// <summary>Default inclusive input limit of one mebibyte.</summary>
    public const int MaxInputBytes = 1_048_576;
    /// <summary>Default inclusive output limit of four mebibytes.</summary>
    public const int MaxOutputBytes = 4_194_304;
    /// <summary>Default database work queue capacity.</summary>
    public const int DbWorkQueueCapacity = 256;
    /// <summary>Default local admission deadline.</summary>
    public static readonly TimeSpan AdmissionTimeout = TimeSpan.FromSeconds(5);
    /// <summary>Default execution deadline from dispatch.</summary>
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(600);
    /// <summary>Default controlled shutdown deadline.</summary>
    public static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(10);
    /// <summary>Bounded polling recovers progress after lost coalesced worker wakeups.</summary>
    public static readonly TimeSpan SchedulerPollInterval = TimeSpan.FromSeconds(1);
    /// <summary>Bounded interval for observing durable cancellation of an active dispatch.</summary>
    public static readonly TimeSpan CancellationPollInterval = TimeSpan.FromMilliseconds(100);
    /// <summary>Default database busy timeout.</summary>
    public static readonly TimeSpan DbBusyTimeout = TimeSpan.FromSeconds(1);
    /// <summary>Largest finite delay supported by the .NET timer APIs used by the runtime.</summary>
    public static readonly TimeSpan MaximumTimerTimeout = TimeSpan.FromMilliseconds(uint.MaxValue - 1);
}
