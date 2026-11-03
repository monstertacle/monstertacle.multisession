using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ContractTests.Fixtures;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
using static Monstertacle.Multisession.ContractTests.Fixtures.RuntimeTestComposition;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>
/// Disposal reports failures of the stop it performs itself, but does not replay an earlier stop failure merely because that
/// stop's cleanup finished first. The runtime clock seam schedules the actual lifecycle: it expires a captured stop deadline on
/// demand and holds the creation of the disposal's own stop deadline, which lies after disposal reads the state and before the
/// stop takes the lifecycle lock.
/// </summary>
public sealed class DisposeAfterStopFailureTests
{
    private static readonly TimeSpan _callerDeadline = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan _guard = TimeSpan.FromSeconds(5);
    private const string _cleanupMarker = "private-cleanup-marker";

    /// <summary>Cleanup completes after disposal has read the stopping state but before its stop takes the lifecycle lock.</summary>
    [Fact]
    public Task IsQuietWhenCleanupCompletesAfterDisposalBegins() => Task.Run(async () =>
    {
        await using Scenario scenario = await Scenario.TimedOutStopAsync();
        scenario.Clock.ArmHold();
        Task disposing = scenario.Runtime.DisposeAsync().AsTask();
        await scenario.Clock.HoldReached.Task.WaitAsync(_guard);
        scenario.CompleteCleanup();
        Assert.True(scenario.Lease.IsDisposed);
        scenario.Clock.HoldRelease.Set();
        Assert.Null(await Record.ExceptionAsync(() => disposing.WaitAsync(_guard)));
    });

    /// <summary>Cleanup completes before disposal; repeated stop still reports the failure and stop after disposal does nothing.</summary>
    [Fact]
    public Task IsQuietWhenCleanupCompletedBeforeDisposal() => Task.Run(async () =>
    {
        await using Scenario scenario = await Scenario.TimedOutStopAsync();
        scenario.CompleteCleanup();
        Assert.True(scenario.Lease.IsDisposed);
        SessionOperationException repeated = await Assert.ThrowsAsync<SessionOperationException>(
            () => scenario.Runtime.StopAsync(TimeSpan.FromSeconds(1), CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.StorageUnavailable, repeated.Failure.Code);
        scenario.Clock.ArmHold();
        Assert.Null(await Record.ExceptionAsync(() => scenario.Runtime.DisposeAsync().AsTask().WaitAsync(_guard)));
        Assert.False(scenario.Clock.HoldReached.Task.IsCompleted);
        Assert.Null(await Record.ExceptionAsync(
            () => scenario.Runtime.StopAsync(TimeSpan.FromSeconds(1), CancellationToken.None).AsTask().WaitAsync(_guard)));
    });

    /// <summary>When disposal performs the stop itself, its own expired deadline is reported and ownership is retained.</summary>
    [Fact]
    public Task ReportsItsOwnDeadlineWhenDisposalPerformsTheStop() => Task.Run(async () =>
    {
        await using Scenario scenario = Scenario.Create(Options().ShutdownTimeout);
        await scenario.Runtime.StartAsync(Options(), CancellationToken.None);
        scenario.Clock.ArmCapture();
        Task disposing = scenario.Runtime.DisposeAsync().AsTask();
        await scenario.Entered.Task.WaitAsync(_guard);
        scenario.Clock.ExpireCaptured();
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => disposing.WaitAsync(_guard));
        Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
        Assert.True(scenario.Lease.IsHeld);
        scenario.CompleteCleanup();
        Assert.True(scenario.Lease.IsDisposed);
        Assert.Null(await Record.ExceptionAsync(() => scenario.Runtime.DisposeAsync().AsTask().WaitAsync(_guard)));
    });

    /// <summary>When disposal performs the stop itself, a failed store cleanup is reported sanitized and the lease is kept.</summary>
    [Fact]
    public Task ReportsItsOwnCleanupFailureWhenDisposalPerformsTheStop() => Task.Run(async () =>
    {
        await using Scenario scenario = Scenario.Create(_callerDeadline, () => throw new InvalidOperationException(_cleanupMarker));
        await scenario.Runtime.StartAsync(Options(), CancellationToken.None);
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(
            () => scenario.Runtime.DisposeAsync().AsTask().WaitAsync(_guard));
        Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
        Assert.DoesNotContain(_cleanupMarker, failure.ToString(), StringComparison.Ordinal);
        Assert.True(scenario.Lease.IsHeld);
    });

    /// <summary>Concurrent disposal calls share one disposal and one cleanup, and the store closes before the lease is released.</summary>
    [Fact]
    public Task SharesOneDisposalAndCleanupAcrossConcurrentDisposeCalls() => Task.Run(async () =>
    {
        await using Scenario scenario = Scenario.Create(_callerDeadline);
        await scenario.Runtime.StartAsync(Options(), CancellationToken.None);
        Task first = scenario.Runtime.DisposeAsync().AsTask();
        await scenario.Entered.Task.WaitAsync(_guard);
        Task second = scenario.Runtime.DisposeAsync().AsTask();
        Assert.Same(first, second);
        Assert.False(first.IsCompleted);
        Assert.True(scenario.Lease.IsHeld);
        scenario.CompleteCleanup();
        await Task.WhenAll(first, second).WaitAsync(_guard);
        Assert.Equal(1, scenario.StoreDisposals);
        Assert.True(scenario.LeaseHeldWhileStoreClosed);
        Assert.True(scenario.Lease.IsDisposed);
    });

    private sealed class Scenario : IAsyncDisposable
    {
        // Synchronous continuations: completing it runs the held store disposal and the rest of the cleanup on the caller.
        private readonly TaskCompletionSource _finish = new();
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _storeDisposals;

        private Scenario(TimeSpan captured, Func<ValueTask>? disposing)
        {
            Lease = new(Options().StateDirectory);
            Store = new()
            {
                Disposing = disposing ?? (async () =>
                {
                    Interlocked.Increment(ref _storeDisposals);
                    LeaseHeldWhileStoreClosed = Lease.IsHeld;
                    _entered.TrySetResult();
                    await _finish.Task;
                }),
            };
            Clock = new(captured, Options().ShutdownTimeout);
            Runtime = new(Store, Lease, [new("registered-adapter", "test-adapter", 1, new ControlledProvider())], [], Clock);
        }

        internal TaskCompletionSource Entered => _entered;
        internal InMemorySessionStore Store { get; }
        internal TestStateDirectoryLease Lease { get; }
        internal DeadlineControlClock Clock { get; }
        internal SessionRuntime Runtime { get; }
        internal int StoreDisposals => Volatile.Read(ref _storeDisposals);
        internal bool LeaseHeldWhileStoreClosed { get; private set; }

        internal static Scenario Create(TimeSpan captured, Func<ValueTask>? disposing = null) => new(captured, disposing);

        internal static async Task<Scenario> TimedOutStopAsync()
        {
            Scenario scenario = Create(_callerDeadline);
            await scenario.Runtime.StartAsync(Options(), CancellationToken.None);
            scenario.Clock.ArmCapture();
            Task stopping = scenario.Runtime.StopAsync(_callerDeadline, CancellationToken.None).AsTask();
            await scenario.Entered.Task.WaitAsync(_guard);
            scenario.Clock.ExpireCaptured();
            SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => stopping.WaitAsync(_guard));
            Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
            Assert.True(scenario.Lease.IsHeld);
            return scenario;
        }

        internal void CompleteCleanup() => _finish.TrySetResult();

        public async ValueTask DisposeAsync()
        {
            _finish.TrySetResult();
            Clock.HoldRelease.Set();
            await Record.ExceptionAsync(() => Runtime.DisposeAsync().AsTask().WaitAsync(_guard));
            await Lease.DisposeAsync();
        }
    }
}
