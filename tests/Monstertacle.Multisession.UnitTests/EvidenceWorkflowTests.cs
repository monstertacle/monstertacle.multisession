using Monstertacle.Multisession.AgentFramework.Evidence;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
namespace Monstertacle.Multisession.UnitTests;

/// <summary>Revision validity and bounded coordination, independent of logs or status file activity.</summary>
public sealed class EvidenceWorkflowTests
{
    private static readonly string[] _renewalIds = ["binding", "verdict", "decision", "output"];
    private static EvidenceGraph Graph(string sourceRevision = "source-1", string ruleRevision = "rule-1") => new([
        new("source", sourceRevision, EvidenceKind.Source, []), new("rule", ruleRevision, EvidenceKind.Rule, []),
        new("binding", "policy-1", EvidenceKind.Binding, ["source"]),
        new("verdict", "policy-1", EvidenceKind.Verdict, ["source", "rule"]),
        new("decision", "policy-1", EvidenceKind.RuleDecision, ["rule"]),
        new("output", "policy-1", EvidenceKind.Artifact, ["binding", "verdict", "decision"])]);
    private static Task<EvidenceWorkflowResult> Run(EvidenceGraph graph, EvidenceRequestReader reader, EvidenceWorkItem[]? work = null, EvidenceReceipt[]? accepted = null,
        Func<EvidenceNode, EvidenceReceipt, bool>? validate = null)
        => EvidenceWorkflow.RunAsync(graph, accepted ?? [], work ?? [], reader, (node, receipt, _) => ValueTask.FromResult(validate is null ? receipt.ArtifactReference == "approved" : validate(node, receipt)), new() { MaximumAttemptsPerNode = 2 }, default);
    private static EvidenceWorkItem Complete(EvidenceGraph graph, string id, EvidenceRequestReader reader, ExecutionCertainty certainty = ExecutionCertainty.ConfirmedEnded)
    {
        Guid request = Guid.NewGuid();
        Guid session = Guid.NewGuid();
        reader.Items[request] = new(request, session, 1, RequestState.Succeeded, certainty, new(RequestState.Succeeded, certainty, "approved", true, null, null));
        return new(id, graph.Fingerprint(id), request, session);
    }

    /// <summary>Transitive invalidation affects exactly the declared dependencies.</summary>
    [Fact]
    public void SourceAndRuleChangesInvalidateDifferentDependents()
    {
        EvidenceGraph original = Graph();
        EvidenceGraph sourceChanged = Graph(sourceRevision: "source-2");
        EvidenceGraph ruleChanged = Graph(ruleRevision: "rule-2");
        Assert.NotEqual(original.Fingerprint("binding"), sourceChanged.Fingerprint("binding"));
        Assert.Equal(original.Fingerprint("decision"), sourceChanged.Fingerprint("decision"));
        Assert.Equal(original.Fingerprint("binding"), ruleChanged.Fingerprint("binding"));
        Assert.NotEqual(original.Fingerprint("verdict"), ruleChanged.Fingerprint("verdict"));
        Assert.NotEqual(original.Fingerprint("output"), ruleChanged.Fingerprint("output"));
    }

    /// <summary>Missing, duplicate and cyclic definitions fail before any user work.</summary>
    [Fact]
    public void RejectsInvalidGraphs()
    {
        Assert.Throws<ArgumentException>(() => new EvidenceGraph([new("a", "1", EvidenceKind.Verdict, ["missing"])]));
        Assert.Throws<ArgumentException>(() => new EvidenceGraph([new("a", "1", EvidenceKind.Verdict, ["b"]), new("b", "1", EvidenceKind.Verdict, ["a"])]));
        Assert.Throws<ArgumentException>(() => new EvidenceGraph([new("a", "1", EvidenceKind.Source, []), new("a", "2", EvidenceKind.Source, [])]));
    }

    /// <summary>The real workflow accepts complete durable results only after project validation.</summary>
    [Fact]
    public async Task AcceptsAssociatedResultsInDependencyOrder()
    {
        EvidenceGraph graph = Graph();
        EvidenceRequestReader reader = new();
        EvidenceWorkItem[] work = [.. _renewalIds.Select(id => Complete(graph, id, reader))];
        EvidenceWorkflowResult result = await Run(graph, reader, work);
        Assert.Equal(4, result.Accepted.Count);
        Assert.Equal(4, result.NewlyAccepted);
        Assert.Empty(result.Blocked);
        Assert.Equal(0, reader.Submissions);
        EvidenceWorkflowResult resumed = await Run(graph, reader, work, [.. result.Accepted]);
        Assert.Equal(0, resumed.NewlyAccepted);
        Assert.Equal(4, resumed.Accepted.Count);
    }

    /// <summary>Missing admission association after a crash is coordination, never a new submission.</summary>
    [Fact]
    public async Task MissingAssociationNeverSubmitsReplacement()
    {
        EvidenceRequestReader reader = new();
        EvidenceWorkflowResult first = await Run(Graph(), reader);
        EvidenceWorkflowResult restarted = await Run(Graph(), reader);
        Assert.Equal(4, first.Blocked.Count);
        Assert.Equal(first.Blocked, restarted.Blocked);
        Assert.Empty(restarted.Accepted);
        Assert.Equal(0, reader.Submissions);
        Assert.Equal(0, reader.Reads);
    }

    /// <summary>Unchanged running state exhausts a finite observation budget without counting activity.</summary>
    [Fact]
    public async Task PendingRequestCoordinatesWithoutProgressOrResubmission()
    {
        EvidenceGraph graph = Graph();
        EvidenceRequestReader reader = new();
        EvidenceWorkItem work = Complete(graph, "binding", reader);
        reader.Items[work.RequestId] = reader.Items[work.RequestId] with { State = RequestState.Running, Result = null, Certainty = ExecutionCertainty.NotStarted };
        EvidenceWorkflowResult result = await Run(graph, reader, [work]);
        Assert.Equal(0, result.NewlyAccepted);
        Assert.Equal(2, reader.Reads);
        Assert.Contains("binding", result.Blocked);
        Assert.Equal(0, reader.Submissions);
    }

    /// <summary>Unknown work halts immediately and survives restart under the same durable identity.</summary>
    [Fact]
    public async Task UnknownRetainsRequestAndDoesNotObserveLaterWork()
    {
        EvidenceGraph graph = Graph();
        EvidenceRequestReader reader = new();
        EvidenceWorkItem work = Complete(graph, "binding", reader, ExecutionCertainty.Unknown);
        EvidenceWorkItem later = Complete(graph, "verdict", reader);
        EvidenceWorkflowResult result = await Run(graph, reader, [work, later]);
        Assert.Equal(work.RequestId, result.UncertainRequestId);
        Assert.Equal(1, reader.Reads);
        Assert.Empty(result.Accepted);
        Assert.Equal(work.RequestId, (await Run(graph, reader, [work, later])).UncertainRequestId);
        Assert.Equal(0, reader.Submissions);
    }

    /// <summary>Stale results and failed validators do not approve evidence or reset budgets.</summary>
    [Fact]
    public async Task RejectsStaleAndValidatorFailedReceipts()
    {
        EvidenceGraph original = Graph();
        EvidenceRequestReader reader = new();
        EvidenceWorkItem work = Complete(original, "binding", reader);
        EvidenceReceipt stale = new("binding", original.Fingerprint("binding"), "approved", work.RequestId);
        EvidenceWorkflowResult changed = await Run(Graph(sourceRevision: "changed"), reader, accepted: [stale]);
        Assert.Empty(changed.Accepted);
        EvidenceWorkflowResult rejected = await Run(original, reader, [work], validate: (_, _) => false);
        Assert.Empty(rejected.Accepted);
        Assert.Equal(0, rejected.NewlyAccepted);
    }
}
