using System.Diagnostics;
using Monstertacle.Multisession.AgentFramework.Evidence;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
namespace Monstertacle.Multisession.UnitTests;

/// <summary>Pending observation allows real time for ordinary admitted work to finish.</summary>
public sealed class EvidenceObservationTests
{
    /// <summary>A pending request completes inside its configured observation window without resubmission.</summary>
    [Fact]
    public async Task ObservesPendingThenCompletedWithinBudget()
    {
        EvidenceGraph graph = new([new("verdict", "1", EvidenceKind.Verdict, [])]);
        Guid request = Guid.NewGuid();
        Guid session = Guid.NewGuid();
        RequestSnapshot completed = new(request, session, 1, RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded,
            new(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, "approved", true, null, null));
        Stopwatch? observation = null;
        EvidenceRequestReader reader = new()
        {
            ReadOverride = _ =>
            {
                observation ??= Stopwatch.StartNew();
                return observation.Elapsed >= TimeSpan.FromMilliseconds(100) ? completed
                    : completed with { State = RequestState.Running, Certainty = ExecutionCertainty.NotStarted, Result = null };
            },
        };
        EvidenceWorkflowResult result = await EvidenceWorkflow.RunAsync(graph, [], [new("verdict", graph.Fingerprint("verdict"), request, session)], reader,
            (_, _, _) => ValueTask.FromResult(true), new() { MaximumAttemptsPerNode = 2, ObservationInterval = TimeSpan.FromMilliseconds(200) }, default);
        Assert.Single(result.Accepted);
        Assert.Empty(result.Blocked);
        Assert.Equal(2, reader.Reads);
        Assert.Equal(0, reader.Submissions);
    }
}
