namespace Monstertacle.Multisession.Contracts;

/// <summary>A stable public failure, intentionally without raw exception or provider text.</summary>
/// <param name="Code">Stable error category.</param>
/// <param name="Certainty">Known external execution status.</param>
/// <param name="SessionId">Related local session, when known.</param>
/// <param name="RequestId">Related local request, when known.</param>
/// <param name="AdmissionStatus">Optional local acceptance certainty; null for unrelated operations.</param>
public sealed record PublicError(
    ErrorCode Code,
    ExecutionCertainty Certainty = ExecutionCertainty.NotStarted,
    Guid? SessionId = null,
    Guid? RequestId = null,
    AdmissionStatus? AdmissionStatus = null);
