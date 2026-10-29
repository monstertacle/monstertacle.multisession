namespace Monstertacle.Multisession.Contracts;

/// <summary>Immutable recovery evidence, containing no prompts or credentials.</summary>
/// <param name="Target">Explicit case identity.</param>
/// <param name="SessionId">Owning local session.</param>
/// <param name="CreatedUtc">First case creation, preserved across reopening.</param>
/// <param name="Revision">Positive monotonically advancing case revision.</param>
public sealed record RecoveryCaseSnapshot(RecoveryTarget Target, Guid SessionId, DateTimeOffset CreatedUtc, long Revision)
{
    /// <summary>Gets an execution request, absent for opening.</summary>
    public Guid? RequestId { get; init; }
    /// <summary>Gets a dispatch, when an execution was reserved.</summary>
    public Guid? DispatchId { get; init; }
    /// <summary>Gets the separately recorded case state.</summary>
    public RecoveryCaseStatus Status { get; init; }
    /// <summary>Gets the current reason for blocked work.</summary>
    public SessionBlockReason BlockReason { get; init; } = SessionBlockReason.RecoveryRequired;
    /// <summary>Gets known external termination evidence.</summary>
    public ExecutionCertainty Certainty { get; init; } = ExecutionCertainty.Unknown;
    /// <summary>Gets the known opaque native binding, when available.</summary>
    public ProviderBinding? Binding { get; init; }
    /// <summary>Gets the dispatch-bound native operation reference, when acknowledged.</summary>
    public string? OperationReference { get; init; }
}
