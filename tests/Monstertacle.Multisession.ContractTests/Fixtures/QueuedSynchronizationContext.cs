using System.Collections.Concurrent;
using System.Diagnostics;

namespace Monstertacle.Multisession.ContractTests.Fixtures;

// A caller-owned synchronization context that queues posted continuations until the test runs them. Awaits in the runtime
// capture the caller's context, so a stop started under this context can be held at any of its awaits. Running an item under
// a different current context keeps later completions from being inlined, so each await hop stays a separate queued item.
internal sealed class QueuedSynchronizationContext : SynchronizationContext
{
    private static readonly TimeSpan _guard = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _poll = TimeSpan.FromMilliseconds(20);
    private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _items = [];

    public override void Post(SendOrPostCallback d, object? state) => _items.Add((d, state));
    public override void Send(SendOrPostCallback d, object? state) => throw new NotSupportedException();

    internal T Start<T>(Func<T> call)
    {
        SynchronizationContext? previous = Current;
        SetSynchronizationContext(this);
        try { return call(); }
        finally { SetSynchronizationContext(previous); }
    }

    // Runs queued items until the condition holds; the guard only bounds a broken or stranded scenario.
    internal void RunUntil(Func<bool> done, SynchronizationContext? runUnder)
    {
        long began = Stopwatch.GetTimestamp();
        while (!done())
        {
            if (Stopwatch.GetElapsedTime(began) > _guard) { throw new TimeoutException("The scheduled condition was not reached."); }
            if (!_items.TryTake(out (SendOrPostCallback Callback, object? State) item, _poll)) { continue; }
            SynchronizationContext? previous = Current;
            SetSynchronizationContext(runUnder);
            try { item.Callback(item.State); }
            finally { SetSynchronizationContext(previous); }
        }
    }
}
