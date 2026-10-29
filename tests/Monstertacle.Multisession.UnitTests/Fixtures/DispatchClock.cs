namespace Monstertacle.Multisession.UnitTests.Fixtures;

// A manually fired deadline, used only after the provider's explicit start barrier.
internal sealed class DispatchClock : TimeProvider, IDisposable
{
    private DispatchTimer? _timer;
    public TimeSpan? DueTime { get; private set; }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        DueTime = dueTime;
        _timer = new DispatchTimer(callback, state);
        return _timer;
    }

    public void Fire() => _timer?.Fire();
    public void Dispose() => _timer?.Dispose();
}

internal sealed class DispatchTimer(TimerCallback callback, object? state) : ITimer
{
    private int _disposed;

    public bool Change(TimeSpan dueTime, TimeSpan period) => Volatile.Read(ref _disposed) == 0;
    public void Fire()
    {
        if (Volatile.Read(ref _disposed) == 0) { callback(state); }
    }
    public void Dispose() => Interlocked.Exchange(ref _disposed, 1);
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
