using Monstertacle.Multisession.ContractTests.Fixtures;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
using Xunit.Abstractions;
using static Monstertacle.Multisession.ContractTests.Fixtures.RuntimeTestComposition;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Stop deadlines include resource shutdown without releasing ownership before the store closes.</summary>
public sealed class RuntimeCleanupTests(ITestOutputHelper output)
{
    /// <summary>A held store cleanup cannot extend the caller's deadline or release its lease prematurely.</summary>
    [Fact]
    public Task BoundsCleanupAndRetainsLeaseUntilStoreCloses() => new BoundedCleanupWindow().RunAsync(output.WriteLine);

    /// <summary>Concurrent stop callers share one resource shutdown.</summary>
    [Fact]
    public async Task SharesCleanupAcrossConcurrentStops()
    {
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource finish = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        InMemorySessionStore store = new()
        {
            Disposing = async () => { Interlocked.Increment(ref calls); entered.TrySetResult(); await finish.Task; },
        };
        await using TestStateDirectoryLease lease = new(Options().StateDirectory);
        await using SessionRuntime runtime = Runtime(store, new(), lease);
        await runtime.StartAsync(Options(), CancellationToken.None);
        Task first = runtime.StopAsync(TimeSpan.FromSeconds(10), CancellationToken.None).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task second = runtime.StopAsync(TimeSpan.FromSeconds(10), CancellationToken.None).AsTask();
        Assert.True(lease.IsHeld);
        finish.TrySetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, calls);
        Assert.True(lease.IsDisposed);
    }
}
