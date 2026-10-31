using Monstertacle.Multisession.Contracts;
namespace Monstertacle.Multisession.AgentFramework.Evidence;

internal sealed record EvidenceRoundContext(EvidenceGraph Graph, IRequests Requests, Func<EvidenceNode, EvidenceReceipt, CancellationToken, ValueTask<bool>> Validate, EvidenceRenewalOptions Options);
