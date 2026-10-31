namespace Monstertacle.Multisession.AgentFramework.Evidence;

internal sealed class EvidenceRoundState
{
    internal Dictionary<string, EvidenceReceipt> Accepted { get; } = [];
    internal HashSet<string> Previous { get; } = [];
    internal Dictionary<string, EvidenceWorkItem> Work { get; } = [];
    internal List<string> Blocked { get; } = [];
    internal int Index { get; set; }
    internal int Attempts { get; set; }
    internal Guid? UncertainRequestId { get; set; }

    internal EvidenceWorkflowResult Report() => new(Array.AsReadOnly(Accepted.Values.ToArray()), Accepted.Keys.Count(id => !Previous.Contains(id)), Blocked.AsReadOnly(), UncertainRequestId);
}
