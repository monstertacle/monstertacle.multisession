namespace Monstertacle.Multisession.Contracts;

/// <summary>A local session snapshot; full lifecycle fields are added with their behavior tests.</summary>
/// <param name="SessionId">Immutable local identity.</param>
/// <param name="ProviderId">Registered execution adapter.</param>
/// <param name="AgentProfileId">Frozen host profile.</param>
/// <param name="Context">Frozen model and workspace context.</param>
/// <param name="Binding">Confirmed native identity and continuation, if established.</param>
public sealed record SessionSnapshot(
    Guid SessionId,
    string ProviderId,
    string AgentProfileId,
    ProviderSessionContext Context,
    ProviderBinding? Binding)
{
    /// <summary>Gets the original UTC creation time used for stable session ordering.</summary>
    public DateTimeOffset CreatedUtc { get; init; }
    /// <summary>Gets the immutable host settings revision captured when this session was created.</summary>
    public string SettingsRevision { get; init; } = string.Empty;
    /// <summary>Gets the UTC timestamp of the last committed local session mutation.</summary>
    public DateTimeOffset UpdatedUtc { get; init; }
    /// <summary>Gets the actual UTC closure time, absent while opening, open or closing.</summary>
    public DateTimeOffset? ClosedUtc { get; init; }
    /// <summary>Gets the monotonically advancing confirmed binding revision, initially zero.</summary>
    public long BindingRevision { get; init; }
    /// <summary>Gets the local lifecycle, separate from execution evidence and block reason.</summary>
    public SessionLifecycle Lifecycle { get; init; } = SessionLifecycle.Opening;
    /// <summary>Gets the explicit reason for refusing further execution.</summary>
    public SessionBlockReason BlockReason { get; init; }
    /// <summary>Gets whether this block was caused by missing local adapter configuration rather than native continuation evidence.</summary>
    public bool ConfigurationUnavailable { get; init; }
}
