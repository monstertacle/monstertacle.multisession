namespace Monstertacle.Multisession.ContractTests.Fixtures;

// Controls only stop-deadline timers once armed: captures one caller deadline for explicit expiry and holds the creation of one
// later deadline until released. Every other timer is the system timer.
internal sealed class DeadlineControlClock(TimeSpan captured, TimeSpan held) : TimeProvider
{
    private static readonly TimeSpan _guard = TimeSpan.FromSeconds(10);
    private readonly object _gate = new();
    private (TimerCallback Callback, object? State)? _deadline;
    private bool _capturing;
    private bool _holding;

    internal TaskCompletionSource HoldReached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal ManualResetEventSlim HoldRelease { get; } = new(false);

    internal void ArmCapture() { lock (_gate) { _capturing = true; } }
    internal void ArmHold() { lock (_gate) { _holding = true; } }

    internal void ExpireCaptured()
    {
        (TimerCallback callback, object? state) = _deadline ?? throw new InvalidOperationException("No captured deadline.");
        callback(state);
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        bool hold;
        lock (_gate)
        {
            if (_capturing && dueTime == captured)
            {
                _capturing = false;
                _deadline = (callback, state);
                return new IdleTimer();
            }
            hold = _holding && dueTime == held;
            if (hold) { _holding = false; }
        }
        if (hold)
        {
            HoldReached.TrySetResult();
            if (!HoldRelease.Wait(_guard)) { throw new TimeoutException("Held deadline was not released."); }
        }
        return base.CreateTimer(callback, state, dueTime, period);
    }

    private sealed class IdleTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
