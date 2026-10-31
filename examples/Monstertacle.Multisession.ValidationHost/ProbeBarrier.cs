namespace Monstertacle.Multisession.ValidationHost;

internal sealed class ProbeBarrier(int expected)
{
    private int _entered;
    internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal async ValueTask WaitAsync(CancellationToken token)
    {
        if (Interlocked.Increment(ref _entered) >= expected) { Entered.TrySetResult(); }
        await Release.Task.WaitAsync(token);
    }
}
