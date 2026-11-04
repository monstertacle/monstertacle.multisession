using System.Diagnostics;
using System.Globalization;
using Monstertacle.Multisession.Contracts;
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
    public async Task BoundsCleanupAndRetainsLeaseUntilStoreCloses()
    {
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource finish = new(TaskCreationOptions.RunContinuationsAsynchronously);
        InMemorySessionStore store = new() { Disposing = async () => { entered.TrySetResult(); await finish.Task; } };
        await using TestStateDirectoryLease lease = new(Options().StateDirectory);
        SessionRuntime runtime = Runtime(store, new(), lease);
        await runtime.StartAsync(Options(), CancellationToken.None);
        long began = Stopwatch.GetTimestamp();
        Task stopping = runtime.StopAsync(TimeSpan.FromMilliseconds(200), CancellationToken.None).AsTask();
        long returned = 0;
        _ = stopping.ContinueWith(_ => returned = Stopwatch.GetTimestamp(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        long cleanupEntered = Stopwatch.GetTimestamp();
        try
        {
            SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => stopping.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
            // Diagnostic stage timing (T020), written before the unchanged bound.
            output.WriteLine(string.Format(CultureInfo.InvariantCulture, "stage-timing T020 cleanup-entered-ms={0:F1} stop-returned-ms={1:F1} observed-ms={2:F1} bound-ms=1000",
                Stopwatch.GetElapsedTime(began, cleanupEntered).TotalMilliseconds,
                returned == 0 ? -1 : Stopwatch.GetElapsedTime(began, returned).TotalMilliseconds, Stopwatch.GetElapsedTime(began).TotalMilliseconds));
            Assert.True(Stopwatch.GetElapsedTime(began) < TimeSpan.FromSeconds(1));
            Assert.True(lease.IsHeld);
        }
        finally
        {
            finish.TrySetResult();
            await Record.ExceptionAsync(() => stopping);
            await runtime.DisposeAsync();
        }
        Assert.True(lease.IsDisposed);
    }

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
