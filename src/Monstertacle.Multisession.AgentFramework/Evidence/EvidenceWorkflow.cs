using Microsoft.Agents.AI.Workflows;
using Monstertacle.Multisession.Contracts;
namespace Monstertacle.Multisession.AgentFramework.Evidence;

/// <summary>Observes durable renewal work through a typed framework workflow without submitting or replaying.</summary>
public static class EvidenceWorkflow
{
    /// <summary>Validates revision-bound receipts and reads only requests already associated by the host.</summary>
    public static async Task<EvidenceWorkflowResult> RunAsync(EvidenceGraph graph, IReadOnlyList<EvidenceReceipt> accepted,
        IReadOnlyList<EvidenceWorkItem> work, IRequests requests, Func<EvidenceNode, EvidenceReceipt, CancellationToken, ValueTask<bool>> validate,
        EvidenceRenewalOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(accepted);
        ArgumentNullException.ThrowIfNull(work);
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(validate);
        ArgumentNullException.ThrowIfNull(options);
        if (options.ObservationInterval <= TimeSpan.Zero || options.ObservationInterval > TimeSpan.FromMilliseconds(uint.MaxValue - 1)
            || options.MaximumAttemptsPerNode <= 0 || options.Timeout <= TimeSpan.Zero || options.Timeout > TimeSpan.FromMilliseconds(uint.MaxValue - 1))
        { throw new ArgumentException("A positive attempt budget and finite positive timeout are required.", nameof(options)); }
        cancellationToken.ThrowIfCancellationRequested();
        using CancellationTokenSource deadline = new(options.Timeout);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        EvidenceRoundState state = await PrepareAsync(graph, accepted, work, validate, linked.Token);
        EvidenceRenewalExecutor executor = new(new(graph, requests, validate, options), state);
        Workflow workflow = new WorkflowBuilder(executor).AddEdge(executor, executor).WithOutputFrom(executor).Build();
        await using StreamingRun running = await InProcessExecution.RunStreamingAsync(workflow, true, cancellationToken: linked.Token);
        EvidenceWorkflowResult? result = null;
        await foreach (WorkflowEvent item in running.WatchStreamAsync(linked.Token))
        {
            linked.Token.ThrowIfCancellationRequested();
            if (item is WorkflowErrorEvent) { throw new InvalidOperationException("Evidence workflow failed."); }
            if (item is ExecutorFailedEvent) { throw new InvalidOperationException("Evidence observation or validation failed."); }
            if (item is WorkflowOutputEvent { Data: EvidenceWorkflowResult output }) { result = output; }
        }
        linked.Token.ThrowIfCancellationRequested();
        return result ?? throw new InvalidOperationException("Evidence workflow did not produce an acceptance result.");
    }

    private static async ValueTask<EvidenceRoundState> PrepareAsync(EvidenceGraph graph, IReadOnlyList<EvidenceReceipt> accepted,
        IReadOnlyList<EvidenceWorkItem> work, Func<EvidenceNode, EvidenceReceipt, CancellationToken, ValueTask<bool>> validate, CancellationToken cancellationToken)
    {
        Dictionary<string, EvidenceNode> nodes = graph.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        Dictionary<string, EvidenceReceipt> receipts = accepted.ToDictionary(item => item.EvidenceId, StringComparer.Ordinal);
        EvidenceRoundState state = new();
        HashSet<Guid> requests = [];
        foreach (EvidenceWorkItem item in work)
        {
            if (!nodes.TryGetValue(item.EvidenceId, out EvidenceNode? node) || EvidenceGraph.IsInput(node)
                || item.Fingerprint != graph.Fingerprint(node.Id) || item.RequestId == Guid.Empty || item.SessionId == Guid.Empty
                || !requests.Add(item.RequestId) || !state.Work.TryAdd(item.EvidenceId, item))
            { throw new ArgumentException("Work must uniquely identify admitted requests for the frozen graph.", nameof(work)); }
        }
        foreach (EvidenceNode node in graph.Nodes.Where(node => !EvidenceGraph.IsInput(node)))
        {
            if (node.Dependencies.Any(id => !EvidenceGraph.IsInput(nodes[id]) && !state.Accepted.ContainsKey(id))) { continue; }
            if (receipts.TryGetValue(node.Id, out EvidenceReceipt? receipt) && receipt.RequestId != Guid.Empty
                && receipt.Fingerprint == graph.Fingerprint(node.Id) && await EvidenceReceiptValidation.InvokeAsync(validate, node, receipt, cancellationToken))
            {
                state.Accepted.Add(node.Id, receipt);
                state.Previous.Add(node.Id);
            }
        }
        return state;
    }
}
