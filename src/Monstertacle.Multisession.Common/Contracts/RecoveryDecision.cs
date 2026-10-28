namespace Monstertacle.Multisession.Contracts;

/// <summary>A revision-bound operator decision concerning uncertain execution.</summary>
/// <param name="DecisionId">Unique decision identity.</param>
/// <param name="RequestId">Affected request.</param>
/// <param name="ExpectedRevision">Optimistic concurrency guard.</param>
/// <param name="Kind">Explicit requested resolution.</param>
/// <param name="Explanation">Required non-secret rationale.</param>
/// <param name="EvidenceReference">Required non-secret investigation reference.</param>
public sealed record RecoveryDecision(
    Guid DecisionId,
    Guid RequestId,
    long ExpectedRevision,
    RecoveryDecisionKind Kind,
    string Explanation,
    string EvidenceReference);
