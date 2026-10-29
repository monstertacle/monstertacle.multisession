using System.Collections.Concurrent;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ValidationHost;

internal sealed class ProbeTrace
{
    private int _active;
    private int _peak;
    internal ConcurrentQueue<ProbeInvocation> Calls { get; } = new();
    internal int Peak => Volatile.Read(ref _peak);
    internal void Enter(ProviderCall call, ProviderBinding binding)
    {
        Calls.Enqueue(new(call, binding));
        int active = Interlocked.Increment(ref _active);
        int peak;
        do { peak = Volatile.Read(ref _peak); } while (active > peak && Interlocked.CompareExchange(ref _peak, active, peak) != peak);
    }
    internal void Exit() => Interlocked.Decrement(ref _active);
}

internal sealed record ProbeInvocation(ProviderCall Call, ProviderBinding Binding);
