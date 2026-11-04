using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;
using static Monstertacle.Multisession.ContractTests.Fixtures.RuntimeTestComposition;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>
/// A valid stop that has published <c>Stopping</c> requests worker cancellation even when it cannot enter the stop gate, because
/// its deadline has already expired or its caller has already cancelled (spec 008 T006, root contract
/// 008T006-STOP-INTENT-CONTRACT-20261104). The stop still fails as before and keeps ownership until a later stop drains.
/// </summary>
public sealed class StopIntentCancellationTests
{
    /// <summary>The worker token is cancelled and the held callback entered; the failure, ownership and drain stay unchanged.</summary>
    /// <param name="expiredDeadline">True for an already expired 100 ms deadline; false for an already cancelled caller.</param>
    /// <param name="callbackThrows">Whether the provider's cancellation callback throws after it is released.</param>
    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task RequestsWorkerCancellationWhenTheStopCannotEnterTheGate(bool expiredDeadline, bool callbackThrows)
    {
        await using TestStateDirectoryLease lease = new(Options().StateDirectory);
        ScenarioProbe probe = new(lease, Stage.None, null, false);
        await RunScenarioAsync(expiredDeadline, callbackThrows, probe);
        Assert.True(lease.IsDisposed);
        Assert.True(probe.StoreDisposed);
    }

    /// <summary>
    /// An assertion exit after runtime start (before the stop, after the stop failure, or after the drain) still releases the
    /// callback, observes the first stop, drains and disposes the runtime before the test lease, and the forced failure stays the
    /// primary one. A failing store disposal keeps the lease and is reported after the primary failure.
    /// </summary>
    /// <param name="stage">The stage of the forced exit: 1 after start, 2 after the stop failure, 3 after the drain.</param>
    /// <param name="storeDisposalThrows">Whether the store disposal fails during cleanup.</param>
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(2, true)]
    public async Task CleansUpTheRuntimeAndKeepsThePrimaryOnEveryAssertionExit(int stage, bool storeDisposalThrows)
    {
        await using TestStateDirectoryLease lease = new(Options().StateDirectory);
        InvalidOperationException exit = new("forced assertion exit");
        ScenarioProbe probe = new(lease, (Stage)stage, exit, storeDisposalThrows);
        Exception? thrown = await Record.ExceptionAsync(() => RunScenarioAsync(true, true, probe));
        Assert.True(probe.StoreDisposed, "runtime cleanup did not dispose the store");
        Assert.True(probe.LeaseHeldAtStoreDisposal);
        Assert.True(probe.Stopping is null || probe.Stopping.IsCompleted, "the first stop was not observed");
        if (storeDisposalThrows)
        {
            AggregateException both = Assert.IsType<AggregateException>(thrown);
            Assert.Same(exit, both.InnerExceptions[0]);
            Assert.IsType<SessionOperationException>(both.InnerExceptions[1]);
            Assert.True(lease.IsHeld);
            return;
        }
        Assert.Same(exit, thrown);
        Assert.True(lease.IsDisposed, "runtime cleanup did not release the lease");
        Assert.Null(await Record.ExceptionAsync(() => probe.Runtime!.StopAsync(TimeSpan.FromSeconds(5), CancellationToken.None).AsTask()));
    }

    // The stop-intent scenario. A probe may force one assertion exit at a chosen stage and records what cleanup did.
    private static async Task RunScenarioAsync(bool expiredDeadline, bool callbackThrows, ScenarioProbe probe)
    {
        TaskCompletionSource registered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken workerToken = default;
        ControlledProvider provider = new()
        {
            Sending = async (_, binding, _, token) =>
            {
                workerToken = token;
                using CancellationTokenRegistration registration = token.Register(() =>
                {
                    entered.TrySetResult();
                    release.Task.GetAwaiter().GetResult();
                    if (callbackThrows) { throw new InvalidOperationException("private callback payload"); }
                });
                registered.TrySetResult();
                try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
                catch (OperationCanceledException) { }
                return new(RequestState.Cancelled, ExecutionCertainty.ConfirmedEnded, string.Empty, false, binding, null);
            },
        };
        TestStateDirectoryLease lease = probe.Lease;
        InMemorySessionStore store = new() { Disposing = probe.StoreDisposedAsync };
        SessionSnapshot session = SchedulingData.Session();
        await store.CreateSessionAsync(session, CancellationToken.None);
        StopDeadlineClock clock = new();
        SessionRuntime runtime = new(store, lease, [new("registered-adapter", "test-adapter", 1, provider)], [], clock);
        probe.Runtime = runtime;
        await runtime.StartAsync(Options(), CancellationToken.None);
        Task? stopping = null;
        try
        {
            probe.ExitAt(Stage.AfterStart);
            await runtime.Requests.SubmitAsync(Guid.NewGuid(), new(session.SessionId, "input", TimeSpan.FromSeconds(30), null, false), CancellationToken.None);
            await registered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            stopping = Task.Run(async () =>
            {
                clock.ExpireOnThisFlow(expiredDeadline);
                await runtime.StopAsync(TimeSpan.FromMilliseconds(100), new CancellationToken(canceled: !expiredDeadline));
            });
            probe.Stopping = stopping;
            SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => stopping.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
            Assert.Equal(ExecutionCertainty.Unknown, failure.Failure.Certainty);
            Assert.True(await Entered(entered.Task), "the stop did not request worker cancellation");
            Assert.True(workerToken.IsCancellationRequested);
            Assert.True(lease.IsHeld);
            probe.ExitAt(Stage.AfterStopFailure);
            release.TrySetResult();
            Exception? drained = await Record.ExceptionAsync(() => runtime.StopAsync(TimeSpan.FromSeconds(5), CancellationToken.None).AsTask());
            if (callbackThrows) { Assert.Equal(ErrorCode.StorageUnavailable, Assert.IsType<SessionOperationException>(drained).Failure.Code); }
            else { Assert.Null(drained); }
            probe.ExitAt(Stage.AfterDrain);
        }
        catch (Exception primary)
        {
            // The original failure stays first; a cleanup failure is reported with it, never instead of it.
            List<Exception> unexpected = await CleanUpAsync(runtime, release, stopping);
            if (unexpected.Count > 0) { throw new AggregateException("Cleaning up the runtime failed after the test failed.", [primary, .. unexpected]); }
            throw;
        }
        List<Exception> failures = await CleanUpAsync(runtime, release, stopping);
        if (failures.Count > 0) { throw new AggregateException("Cleaning up the runtime failed.", failures); }
    }

    // The original callback test's cleanup order: release the callback, observe the first stop within its bound, drain with a
    // bounded stop, then dispose the runtime, all before the test lease is disposed. An expected stop failure (the typed result
    // of these scenarios) is not a cleanup failure; anything else is returned to be reported.
    private static async Task<List<Exception>> CleanUpAsync(SessionRuntime runtime, TaskCompletionSource release, Task? stopping)
    {
        List<Exception> unexpected = [];
        release.TrySetResult();
        if (stopping is not null) { KeepUnexpected(await Record.ExceptionAsync(() => stopping.WaitAsync(TimeSpan.FromSeconds(5))), unexpected); }
        KeepUnexpected(await Record.ExceptionAsync(() => runtime.StopAsync(TimeSpan.FromSeconds(5), CancellationToken.None).AsTask()), unexpected);
        if (await Record.ExceptionAsync(() => runtime.DisposeAsync().AsTask()) is { } disposal) { unexpected.Add(disposal); }
        return unexpected;
    }

    private static void KeepUnexpected(Exception? failure, List<Exception> unexpected)
    {
        if (failure is not null and not SessionOperationException) { unexpected.Add(failure); }
    }

    private enum Stage { None, AfterStart, AfterStopFailure, AfterDrain }

    // Forces one assertion exit at a chosen stage and records whether runtime cleanup disposed the store, and in which order.
    private sealed class ScenarioProbe(TestStateDirectoryLease lease, Stage exitAt, Exception? exit, bool storeDisposalThrows)
    {
        public TestStateDirectoryLease Lease { get; } = lease;
        public Task? Stopping { get; set; }
        public SessionRuntime? Runtime { get; set; }
        public bool StoreDisposed { get; private set; }
        public bool LeaseHeldAtStoreDisposal { get; private set; }

        public void ExitAt(Stage stage)
        {
            if (stage == exitAt && exit is not null) { throw exit; }
        }

        public ValueTask StoreDisposedAsync()
        {
            StoreDisposed = true;
            LeaseHeldAtStoreDisposal = Lease.IsHeld;
            return storeDisposalThrows ? ValueTask.FromException(new IOException("store disposal failed")) : ValueTask.CompletedTask;
        }
    }

    // Waits the original test's 5 s for callback entry and reports whether it happened.
    private static async Task<bool> Entered(Task entered)
        => await Task.WhenAny(entered, Task.Delay(TimeSpan.FromSeconds(5))) == entered;

    // Fires at once only the first 100 ms timer created on an armed stop flow (the stop deadline); all other timers are the
    // system clock, so the runtime's own 100 ms polling timers are not affected.
    private sealed class StopDeadlineClock : TimeProvider
    {
        private static readonly TimeSpan _deadline = TimeSpan.FromMilliseconds(100);
        private readonly AsyncLocal<bool> _armed = new();
        private int _fired;

        public void ExpireOnThisFlow(bool armed) => _armed.Value = armed;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            if (!_armed.Value || dueTime != _deadline || Interlocked.CompareExchange(ref _fired, 1, 0) != 0)
            {
                return TimeProvider.System.CreateTimer(callback, state, dueTime, period);
            }
            callback(state);
            return TimeProvider.System.CreateTimer(static _ => { }, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }
}
