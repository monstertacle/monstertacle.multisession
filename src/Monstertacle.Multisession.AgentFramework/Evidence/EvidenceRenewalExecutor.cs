using Microsoft.Agents.AI.Workflows;
using Monstertacle.Multisession.Contracts;
namespace Monstertacle.Multisession.AgentFramework.Evidence;

[SendsMessage(typeof(bool))]
[YieldsOutput(typeof(EvidenceWorkflowResult))]
internal sealed class EvidenceRenewalExecutor(EvidenceRoundContext round, EvidenceRoundState state) : Executor<bool>("evidence-renewal")
{
    public override async ValueTask HandleAsync(bool message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (state.Index >= round.Graph.Nodes.Count || state.UncertainRequestId.HasValue)
        {
            await context.YieldOutputAsync(state.Report(), cancellationToken);
            return;
        }
        EvidenceNode node = round.Graph.Nodes[state.Index];
        if (EvidenceGraph.IsInput(node) || state.Accepted.ContainsKey(node.Id)) { Advance(); }
        else if (node.Dependencies.Any(state.Blocked.Contains) || !state.Work.TryGetValue(node.Id, out EvidenceWorkItem? work))
        { Block(node); }
        else { await ObserveAsync(node, work, cancellationToken); }
        await context.SendMessageAsync(true, cancellationToken: cancellationToken);
    }

    private async ValueTask ObserveAsync(EvidenceNode node, EvidenceWorkItem work, CancellationToken cancellationToken)
    {
        RequestSnapshot? request = await round.Requests.GetRequestAsync(work.RequestId, cancellationToken).AsTask().WaitAsync(cancellationToken);
        state.Attempts++;
        if (request?.RequestId != work.RequestId || request.SessionId != work.SessionId) { Block(node); return; }
        if (request.Certainty == ExecutionCertainty.Unknown)
        {
            state.UncertainRequestId = work.RequestId;
            foreach (EvidenceNode remaining in round.Graph.Nodes.Skip(state.Index).Where(item => !EvidenceGraph.IsInput(item) && !state.Accepted.ContainsKey(item.Id)))
            { state.Blocked.Add(remaining.Id); }
            return;
        }
        if (request.State == RequestState.Succeeded && request.Certainty == ExecutionCertainty.ConfirmedEnded
            && request.Result is { Outcome: RequestState.Succeeded, Certainty: ExecutionCertainty.ConfirmedEnded, OutputComplete: true, Error: null } reply)
        {
            EvidenceReceipt receipt = new(node.Id, work.Fingerprint, reply.Output, work.RequestId);
            if (await EvidenceReceiptValidation.InvokeAsync(round.Validate, node, receipt, cancellationToken)) { state.Accepted.Add(node.Id, receipt); Advance(); return; }
        }
        if (state.Attempts >= round.Options.MaximumAttemptsPerNode || request.State is not (RequestState.Queued or RequestState.Running)) { Block(node); }
        else { await Task.Delay(round.Options.ObservationInterval, cancellationToken); }
    }

    private void Block(EvidenceNode node)
    {
        state.Blocked.Add(node.Id);
        Advance();
    }
    private void Advance()
    {
        state.Index++;
        state.Attempts = 0;
    }
}
