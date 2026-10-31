using Monstertacle.Multisession.AgentFramework.Evidence;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
namespace Monstertacle.Multisession.UnitTests;

/// <summary>Rejected or cancelled rounds cannot invoke project validators or accept stale work.</summary>
public sealed class EvidenceContractTests
{
    /// <summary>Cancellation is checked before any validation of persisted receipts.</summary>
    [Fact]
    public async Task CancelledRoundDoesNotRunValidators()
    {
        EvidenceGraph graph = new([new("verdict", "1", EvidenceKind.Verdict, [])]);
        EvidenceReceipt receipt = new("verdict", graph.Fingerprint("verdict"), "approved", Guid.NewGuid());
        int validations = 0;
        using CancellationTokenSource stopped = new();
        stopped.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => EvidenceWorkflow.RunAsync(graph, [receipt], [], new EvidenceRequestReader(),
            (_, _, _) => { validations++; return ValueTask.FromResult(true); }, new(), stopped.Token));
        Assert.Equal(0, validations);
    }

    /// <summary>Duplicate receipts and duplicate work identities are rejected before project callbacks.</summary>
    [Fact]
    public async Task RejectsAmbiguousReceiptOrWorkAssociation()
    {
        EvidenceGraph graph = new([new("verdict", "1", EvidenceKind.Verdict, [])]);
        Guid request = Guid.NewGuid();
        EvidenceReceipt receipt = new("verdict", graph.Fingerprint("verdict"), "approved", request);
        EvidenceWorkItem work = new("verdict", graph.Fingerprint("verdict"), request, Guid.NewGuid());
        int validations = 0;
        await Assert.ThrowsAsync<ArgumentException>(() => EvidenceWorkflow.RunAsync(graph, [receipt, receipt], [], new EvidenceRequestReader(),
            (_, _, _) => { validations++; return ValueTask.FromResult(true); }, new(), default));
        await Assert.ThrowsAsync<ArgumentException>(() => EvidenceWorkflow.RunAsync(graph, [], [work, work], new EvidenceRequestReader(),
            (_, _, _) => { validations++; return ValueTask.FromResult(true); }, new(), default));
        await Assert.ThrowsAsync<ArgumentException>(() => EvidenceWorkflow.RunAsync(graph, [], [work with { Fingerprint = "old" }], new EvidenceRequestReader(),
            (_, _, _) => { validations++; return ValueTask.FromResult(true); }, new(), default));
        Assert.Equal(0, validations);
    }

    /// <summary>Input mutation after graph creation cannot alter the frozen revision or dependencies.</summary>
    [Fact]
    public void FreezesDefinitionAndDependencyArrays()
    {
        string[] dependencies = ["source"];
        EvidenceNode[] nodes = [new("source", "1", EvidenceKind.Source, []), new("verdict", "1", EvidenceKind.Verdict, dependencies)];
        EvidenceGraph graph = new(nodes);
        string original = graph.Fingerprint("verdict");
        dependencies[0] = "other";
        nodes[0] = nodes[0] with { Revision = "2" };
        Assert.Equal(original, graph.Fingerprint("verdict"));
        Assert.Equal("source", Assert.Single(graph.Nodes.Single(node => node.Id == "verdict").Dependencies));
    }
}
