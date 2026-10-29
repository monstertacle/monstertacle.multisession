using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.IntegrationTests.Fixtures;

// Existing lease dependency cancels after the queue's check, inside the already-running startup action.
internal sealed class CancellingStartupLease(PrivateStoreDirectory owner, CancellationTokenSource cancellation) : IStateDirectoryLease
{
    private int _checksUntilCancellation;
    private bool _released;
    public string StateDirectory => owner.StateDirectory;
    public bool IsHeld
    {
        get
        {
            if (_released) { return false; }
            if (Volatile.Read(ref _checksUntilCancellation) > 0 && Interlocked.Decrement(ref _checksUntilCancellation) == 0) { cancellation.Cancel(); }
            return owner.IsHeld;
        }
    }
    internal void ArmAfterQueueCheck() => Volatile.Write(ref _checksUntilCancellation, 2);
    public bool Matches(string directory) => owner.Matches(directory);
    public ValueTask PrepareFileAsync(string fileName, StateFileMode mode, CancellationToken cancellationToken)
        => owner.PrepareFileAsync(fileName, mode, cancellationToken);
    public ValueTask DisposeAsync() { _released = true; return ValueTask.CompletedTask; }
}
