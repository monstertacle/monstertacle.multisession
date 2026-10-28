namespace Monstertacle.Multisession.Contracts;

/// <summary>Host runtime options. Validation is introduced by the corresponding behavior tests.</summary>
public sealed record RuntimeOptions
{
    /// <summary>Gets the explicit absolute private state directory.</summary>
    public required string StateDirectory { get; init; }

    /// <summary>Gets the concurrent dispatch capacity, including uncertain reservations.</summary>
    public int MaxConcurrentRequests { get; init; } = RuntimeDefaults.MaxConcurrentRequests;
    /// <summary>Gets the durable queued-request capacity.</summary>
    public int MaxQueuedRequests { get; init; } = RuntimeDefaults.MaxQueuedRequests;
    /// <summary>Gets the inclusive UTF-8 input byte limit.</summary>
    public int MaxInputBytes { get; init; } = RuntimeDefaults.MaxInputBytes;
    /// <summary>Gets the inclusive UTF-8 output byte limit.</summary>
    public int MaxOutputBytes { get; init; } = RuntimeDefaults.MaxOutputBytes;
    /// <summary>Gets the bounded infrastructure database queue capacity.</summary>
    public int DbWorkQueueCapacity { get; init; } = RuntimeDefaults.DbWorkQueueCapacity;
    /// <summary>Gets the local acceptance timeout measured from submission.</summary>
    public TimeSpan AdmissionTimeout { get; init; } = RuntimeDefaults.AdmissionTimeout;
    /// <summary>Gets the execution timeout measured from dispatch, not queue admission.</summary>
    public TimeSpan RequestTimeout { get; init; } = RuntimeDefaults.RequestTimeout;
    /// <summary>Gets the controlled shutdown deadline.</summary>
    public TimeSpan ShutdownTimeout { get; init; } = RuntimeDefaults.ShutdownTimeout;
    /// <summary>Gets the database busy timeout, shorter than shutdown.</summary>
    public TimeSpan DbBusyTimeout { get; init; } = RuntimeDefaults.DbBusyTimeout;
    /// <summary>Gets explicit retention; null does not authorize implicit deletion.</summary>
    public RetentionPolicy? Retention { get; init; }

    /// <summary>Validates options without performing I/O or starting external execution.</summary>
    public PublicError? Validate()
    {
        if (!LocalDirectoryPath.IsValid(StateDirectory))
        {
            return new PublicError(ErrorCode.InvalidConfiguration);
        }

        if (MaxConcurrentRequests <= 0 || MaxQueuedRequests <= 0
            || MaxInputBytes <= 0 || MaxOutputBytes <= 0 || DbWorkQueueCapacity <= 0
            || !ExecutionDeadline.IsValid(AdmissionTimeout) || !ExecutionDeadline.IsValid(RequestTimeout)
            || !ExecutionDeadline.IsValid(ShutdownTimeout) || !ExecutionDeadline.IsValid(DbBusyTimeout)
            || DbBusyTimeout >= ShutdownTimeout)
        {
            return new PublicError(ErrorCode.InvalidConfiguration);
        }

        if (!HasExplicitRetention())
        {
            return new PublicError(ErrorCode.InvalidConfiguration);
        }

        return null;
    }

    private bool HasExplicitRetention()
        => Retention is { RetainUntilExplicitDeletion: true, Duration: null }
            || (Retention is { RetainUntilExplicitDeletion: false, Duration: { } duration }
                && duration > TimeSpan.Zero && duration < TimeSpan.MaxValue);
}
