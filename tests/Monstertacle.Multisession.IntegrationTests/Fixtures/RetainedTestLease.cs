using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.IntegrationTests.Fixtures;

// Runtime may release ownership without deleting the outer fixture's persisted data.
internal sealed class RetainedTestLease(PrivateStoreDirectory owner) : IStateDirectoryLease
{
    private bool _released;
    public bool IsHeld => !_released && owner.IsHeld;
    public string StateDirectory => owner.StateDirectory;
    public bool Matches(string directory) => owner.Matches(directory);
    public ValueTask PrepareFileAsync(string fileName, StateFileMode mode, CancellationToken cancellationToken)
        => owner.PrepareFileAsync(fileName, mode, cancellationToken);
    public ValueTask DisposeAsync() { _released = true; return ValueTask.CompletedTask; }
}
