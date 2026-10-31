using System.Collections.Concurrent;
using Monstertacle.Multisession.UnitTests.Fixtures;

namespace Monstertacle.Multisession.ContractTests.Fixtures;

internal sealed class HttpTestClock : TimeProvider, IDisposable
{
    private readonly ConcurrentDictionary<TimeSpan, ConcurrentQueue<DispatchTimer>> _timers = new();
    private readonly ConcurrentDictionary<TimeSpan, TaskCompletionSource> _created = new();
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
    internal Task WaitForTimerAsync(TimeSpan dueTime) => _created.GetOrAdd(dueTime, _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        DispatchTimer timer = new(callback, state);
        _timers.GetOrAdd(dueTime, _ => new()).Enqueue(timer);
        _created.GetOrAdd(dueTime, _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
        return timer;
    }
    internal void Fire(TimeSpan dueTime)
    {
        if (_timers.TryGetValue(dueTime, out ConcurrentQueue<DispatchTimer>? timers))
        {
            foreach (DispatchTimer timer in timers) { timer.Fire(); }
        }
    }
    public void Dispose()
    {
        foreach (DispatchTimer timer in _timers.Values.SelectMany(value => value)) { timer.Dispose(); }
    }
}
