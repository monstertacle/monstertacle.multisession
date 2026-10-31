namespace Monstertacle.Multisession.AgentFramework.Evidence;
/// <summary>Current acceptance and explicit coordination outcome, never inferred from file activity.</summary>
/// <param name="Accepted">Validated receipts for this frozen graph only.</param>
/// <param name="NewlyAccepted">Newly accepted identities, excluding previously valid receipts.</param>
/// <param name="Blocked">Evidence identities requiring coordination.</param>
/// <param name="UncertainRequestId">Request retaining execution custody, when unknown.</param>
public sealed record EvidenceWorkflowResult(IReadOnlyList<EvidenceReceipt> Accepted, int NewlyAccepted, IReadOnlyList<string> Blocked, Guid? UncertainRequestId);
