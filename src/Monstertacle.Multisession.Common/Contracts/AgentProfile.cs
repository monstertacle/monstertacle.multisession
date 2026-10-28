namespace Monstertacle.Multisession.Contracts;

/// <summary>A host-selected model or external-agent profile, independent of vendor names.</summary>
/// <param name="AgentProfileId">Technical profile identity.</param>
/// <param name="ProviderId">Registered adapter connection.</param>
/// <param name="ModelId">Opaque model identity passed unchanged.</param>
/// <param name="ExecutionKind">Required external execution semantics.</param>
/// <param name="Settings">Non-secret host settings.</param>
public sealed record AgentProfile(
    string AgentProfileId,
    string ProviderId,
    string ModelId,
    ExecutionKind ExecutionKind,
    ProfileSettings Settings);
