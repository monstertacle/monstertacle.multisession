namespace Monstertacle.Multisession.ContractTests.Fixtures;

internal sealed class CompletionBarrier(int expected)
{
    private int _count;
    internal TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal void Record()
    {
        if (Interlocked.Increment(ref _count) == expected) { Completed.TrySetResult(); }
    }
}
