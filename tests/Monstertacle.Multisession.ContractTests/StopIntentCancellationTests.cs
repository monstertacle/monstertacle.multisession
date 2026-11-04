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
        InMemorySessionStore store = new();
        SessionSnapshot session = SchedulingData.Session();
        await store.CreateSessionAsync(session, CancellationToken.None);
        await using TestStateDirectoryLease lease = new(Options().StateDirectory);
        StopDeadlineClock clock = new();
        SessionRuntime runtime = new(store, lease, [new("registered-adapter", "test-adapter", 1, provider)], [], clock);
        await runtime.StartAsync(Options(), CancellationToken.None);
        await runtime.Requests.SubmitAsync(Guid.NewGuid(), new(session.SessionId, "input", TimeSpan.FromSeconds(30), null, false), CancellationToken.None);
        await registered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task stopping = Task.Run(async () =>
        {
            clock.ExpireOnThisFlow(expiredDeadline);
            await runtime.StopAsync(TimeSpan.FromMilliseconds(100), new CancellationToken(canceled: !expiredDeadline));
        });
        try
        {
            SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => stopping.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
            Assert.Equal(ExecutionCertainty.Unknown, failure.Failure.Certainty);
            Assert.True(await Entered(entered.Task), "the stop did not request worker cancellation");
            Assert.True(workerToken.IsCancellationRequested);
            Assert.True(lease.IsHeld);
        }
        finally { release.TrySetResult(); }
        Exception? drained = await Record.ExceptionAsync(() => runtime.StopAsync(TimeSpan.FromSeconds(5), CancellationToken.None).AsTask());
        if (callbackThrows) { Assert.Equal(ErrorCode.StorageUnavailable, Assert.IsType<SessionOperationException>(drained).Failure.Code); }
        else { Assert.Null(drained); }
        await runtime.DisposeAsync();
        Assert.True(lease.IsDisposed);
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
