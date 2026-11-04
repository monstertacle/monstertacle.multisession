using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>The candidate lookup stays finite and observes the fault of every lookup task it stops waiting for.</summary>
public sealed class CandidateLookupTests
{
    private static readonly TimeSpan _bound = TimeSpan.FromMilliseconds(200);

    /// <summary>The outer wait times out and the lookup faults before the timeout is handled: the fault is still observed.</summary>
    [Fact]
    public async Task ObservesALookupThatFaultsAfterTheOuterTimeout()
    {
        TaskCompletionSource<RequestSnapshot?> query = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<Task> observed = [];
        LookupSeams seams = new((_, _) =>
        {
            query.SetException(new ObjectDisposedException("store"));
            return Task.FromException<RequestSnapshot?>(new TimeoutException());
        }, observed.Add);
        string result = await CandidateLookup.ObserveAsync((_, _) => new ValueTask<RequestSnapshot?>(query.Task), Guid.NewGuid(), _bound, seams);
        Assert.StartsWith("timeout lookup-ms=", result, StringComparison.Ordinal);
        Assert.Same(query.Task, Assert.Single(observed));
    }

    /// <summary>A lookup that is still running when the outer wait times out is observed, not joined.</summary>
    [Fact]
    public async Task ObservesALookupThatIsStillRunningAfterTheOuterTimeout()
    {
        TaskCompletionSource<RequestSnapshot?> query = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<Task> observed = [];
        LookupSeams seams = new((_, _) => Task.FromException<RequestSnapshot?>(new TimeoutException()), observed.Add);
        string result = await CandidateLookup.ObserveAsync((_, _) => new ValueTask<RequestSnapshot?>(query.Task), Guid.NewGuid(), _bound, seams);
        Assert.StartsWith("timeout lookup-ms=", result, StringComparison.Ordinal);
        Assert.Same(query.Task, Assert.Single(observed));
        query.SetResult(null);
    }

    /// <summary>A lookup that itself fails with a timeout is reported as its own error, not as the outer timeout.</summary>
    [Fact]
    public async Task ReportsTheLookupsOwnTimeoutAsAnError()
    {
        string result = await CandidateLookup.ObserveAsync(
            (_, _) => ValueTask.FromException<RequestSnapshot?>(new TimeoutException("store")), Guid.NewGuid(), _bound);
        Assert.StartsWith("error:System.TimeoutException lookup-ms=", result, StringComparison.Ordinal);
    }
}
