using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ContractTests.Fixtures;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
using static Monstertacle.Multisession.ContractTests.Fixtures.RuntimeTestComposition;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>
/// A stop that is still unwinding, or still waiting for the stop gate, when a concurrent disposal completes ends with its own
/// typed outcome. A caller-owned synchronization context holds the stop after cleanup has published the stopped state and
/// before the stop releases the gate. Disposal is allowed to complete before such a stop returns.
/// </summary>
public sealed class StopDisposeOverlapTests
{
    private static readonly TimeSpan _stopDeadline = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _waiterDeadline = TimeSpan.FromSeconds(25);
    private static readonly TimeSpan _guard = TimeSpan.FromSeconds(5);
    private const string _callbackMarker = "private callback payload";

    /// <summary>A successful stop keeps its outcome when disposal completes while the stop still holds the gate.</summary>
    [Fact]
    public Task KeepsTheOutcomeOfAStopStillUnwindingWhenDisposalCompletes() => Task.Run(async () =>
    {
        await using Scenario scenario = await Scenario.StartedAsync();
        Task stopping = scenario.HoldStopAfterStoppedIsPublished();
        Assert.Null(await Record.ExceptionAsync(() => scenario.Runtime.DisposeAsync().AsTask().WaitAsync(_guard)));
        scenario.Context.RunUntil(() => stopping.IsCompleted, runUnder: null);
        Assert.Null(await Record.ExceptionAsync(() => stopping));
        scenario.AssertOneCleanupWithStoreBeforeLease();
    });

    /// <summary>A stop whose own worker cancellation failed keeps that sanitized failure when disposal overlaps it.</summary>
    [Fact]
    public Task KeepsTheOwnFailureOfAStopStillUnwindingWhenDisposalCompletes() => Task.Run(async () =>
    {
        await using Scenario scenario = await Scenario.StartedWithThrowingCancellationAsync();
        Task stopping = scenario.HoldStopAfterStoppedIsPublished();
        Assert.Null(await Record.ExceptionAsync(() => scenario.Runtime.DisposeAsync().AsTask().WaitAsync(_guard)));
        scenario.Context.RunUntil(() => stopping.IsCompleted, runUnder: null);
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => stopping);
        Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
        Assert.Equal(ExecutionCertainty.Unknown, failure.Failure.Certainty);
        Assert.DoesNotContain(_callbackMarker, failure.ToString(), StringComparison.Ordinal);
        scenario.AssertOneCleanupWithStoreBeforeLease();
    });

    /// <summary>A stop that started waiting for the gate before the stopped state was published finishes after disposal.</summary>
    [Fact]
    public Task LetsAStopQueuedBeforeStoppedFinishWhenDisposalCompletes() => Task.Run(async () =>
    {
        await using Scenario scenario = await Scenario.StartedAsync();
        Task stopping = scenario.StartHeldStop();
        Task waiting = scenario.StartQueuedStop(_stopDeadline);
        Assert.False(waiting.IsCompleted);
        scenario.CompleteCleanupAndPublishStopped();
        Assert.Null(await Record.ExceptionAsync(() => scenario.Runtime.DisposeAsync().AsTask().WaitAsync(_guard)));
        scenario.Context.RunUntil(() => stopping.IsCompleted && waiting.IsCompleted, runUnder: null);
        Assert.Null(await Record.ExceptionAsync(() => stopping));
        Assert.Null(await Record.ExceptionAsync(() => waiting));
        scenario.AssertOneCleanupWithStoreBeforeLease();
    });

    /// <summary>A queued stop whose own deadline expires while it waits for the gate reports that bounded failure.</summary>
    [Fact]
    public Task ReportsTheOwnDeadlineOfAQueuedStop() => Task.Run(async () =>
    {
        await using Scenario scenario = await Scenario.StartedAsync();
        Task stopping = scenario.StartHeldStop();
        scenario.Clock.ArmCapture();
        Task waiting = scenario.StartQueuedStop(_waiterDeadline);
        scenario.Clock.ExpireCaptured();
        scenario.Context.RunUntil(() => waiting.IsCompleted, runUnder: scenario.Context);
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => waiting);
        Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
        scenario.CompleteCleanupAndPublishStopped();
        scenario.Context.RunUntil(() => stopping.IsCompleted, runUnder: null);
        Assert.Null(await Record.ExceptionAsync(() => stopping));
        Assert.Null(await Record.ExceptionAsync(() => scenario.Runtime.DisposeAsync().AsTask().WaitAsync(_guard)));
        scenario.AssertOneCleanupWithStoreBeforeLease();
    });

    private sealed class Scenario : IAsyncDisposable
    {
        // Completing it resumes the held store disposal; the continuation is queued on the caller-owned context.
        private readonly TaskCompletionSource _finish = new();
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _storeDisposals;
        private bool _leaseHeldWhileStoreClosed;

        private Scenario(ControlledProvider provider)
        {
            Lease = new(Options().StateDirectory);
            Store = new()
            {
                Disposing = async () =>
                {
                    Interlocked.Increment(ref _storeDisposals);
                    _leaseHeldWhileStoreClosed = Lease.IsHeld;
                    _entered.TrySetResult();
                    await _finish.Task;
                },
            };
            Clock = new(_waiterDeadline, Timeout.InfiniteTimeSpan);
            Runtime = new(Store, Lease, [new("registered-adapter", "test-adapter", 1, provider)], [], Clock);
        }

        internal QueuedSynchronizationContext Context { get; } = new();
        internal InMemorySessionStore Store { get; }
        internal TestStateDirectoryLease Lease { get; }
        internal DeadlineControlClock Clock { get; }
        internal SessionRuntime Runtime { get; }

        internal static async Task<Scenario> StartedAsync()
        {
            Scenario scenario = new(new ControlledProvider());
            await scenario.Runtime.StartAsync(Options(), CancellationToken.None);
            return scenario;
        }

        // A running request whose provider cancellation callback throws makes this stop's own worker cancellation fail.
        internal static async Task<Scenario> StartedWithThrowingCancellationAsync()
        {
            TaskCompletionSource registered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            ControlledProvider provider = new()
            {
                Sending = async (_, binding, _, token) =>
                {
                    using CancellationTokenRegistration registration = token.Register(() => throw new InvalidOperationException(_callbackMarker));
                    registered.TrySetResult();
                    try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
                    catch (OperationCanceledException) { }
                    return new(RequestState.Cancelled, ExecutionCertainty.ConfirmedEnded, string.Empty, false, binding, null);
                },
            };
            Scenario scenario = new(provider);
            SessionSnapshot session = SchedulingData.Session();
            await scenario.Store.CreateSessionAsync(session, CancellationToken.None);
            await scenario.Runtime.StartAsync(Options(), CancellationToken.None);
            await scenario.Runtime.Requests.SubmitAsync(Guid.NewGuid(), new(session.SessionId, "input", TimeSpan.FromSeconds(30), null, false), CancellationToken.None);
            await registered.Task.WaitAsync(_guard);
            return scenario;
        }

        // The stop holds the gate and waits for the held store disposal.
        internal Task StartHeldStop()
        {
            Task stopping = Context.Start(() => Runtime.StopAsync(_stopDeadline, CancellationToken.None).AsTask());
            Context.RunUntil(() => _entered.Task.IsCompleted, runUnder: Context);
            return stopping;
        }

        // Runs synchronously up to the gate wait, so the stop is admitted while the state is still stopping.
        internal Task StartQueuedStop(TimeSpan deadline) => Context.Start(() => Runtime.StopAsync(deadline, CancellationToken.None).AsTask());

        // Cleanup publishes the stopped state; the held stop's continuation stays queued and the stop still holds the gate.
        internal void CompleteCleanupAndPublishStopped()
        {
            _finish.TrySetResult();
            Context.RunUntil(() => Lease.IsDisposed, runUnder: null);
        }

        internal Task HoldStopAfterStoppedIsPublished()
        {
            Task stopping = StartHeldStop();
            CompleteCleanupAndPublishStopped();
            Assert.False(stopping.IsCompleted);
            return stopping;
        }

        internal void AssertOneCleanupWithStoreBeforeLease()
        {
            Assert.Equal(1, Volatile.Read(ref _storeDisposals));
            Assert.True(_leaseHeldWhileStoreClosed);
            Assert.True(Lease.IsDisposed);
        }

        public async ValueTask DisposeAsync()
        {
            _finish.TrySetResult();
            await Record.ExceptionAsync(() => Runtime.DisposeAsync().AsTask().WaitAsync(_guard));
            await Lease.DisposeAsync();
        }
    }
}
