namespace Monstertacle.Multisession.AgentFramework.Evidence;
/// <summary>Project-validated result associated with an existing durable request.</summary>
/// <param name="EvidenceId">Target evidence identity.</param>
/// <param name="Fingerprint">Exact transitive revision fingerprint.</param>
/// <param name="ArtifactReference">Project-owned artifact reference or result.</param>
/// <param name="RequestId">Already durably associated request identity.</param>
public sealed record EvidenceReceipt(string EvidenceId, string Fingerprint, string ArtifactReference, Guid RequestId);
