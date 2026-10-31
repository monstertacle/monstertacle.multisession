using Monstertacle.Multisession.AgentFramework.Evidence;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
namespace Monstertacle.Multisession.UnitTests;

/// <summary>Caller-visible deadlines also cover noncooperative project validation.</summary>
public sealed class EvidenceDeadlineTests
{
    /// <summary>Neither previous nor newly observed receipts can bypass the round deadline.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NoncooperativeValidationCannotExceedRoundDeadline(bool previous)
    {
        EvidenceGraph graph = new([new("verdict", "1", EvidenceKind.Verdict, [])]);
        Guid request = Guid.NewGuid();
        Guid session = Guid.NewGuid();
        EvidenceReceipt receipt = new("verdict", graph.Fingerprint("verdict"), "approved", request);
        EvidenceRequestReader reader = new();
        reader.Items[request] = new(request, session, 1, RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded,
            new(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, "approved", true, null, null));
        using ManualResetEventSlim release = new();
        Task<EvidenceWorkflowResult> running = Task.Run(() => EvidenceWorkflow.RunAsync(graph, previous ? [receipt] : [],
            previous ? [] : [new("verdict", graph.Fingerprint("verdict"), request, session)], reader,
            (_, _, _) => { release.Wait(CancellationToken.None); return ValueTask.FromResult(true); }, new() { Timeout = TimeSpan.FromMilliseconds(50) }, default));
        try { await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running.WaitAsync(TimeSpan.FromSeconds(1))); }
        finally { release.Set(); }
    }
}
