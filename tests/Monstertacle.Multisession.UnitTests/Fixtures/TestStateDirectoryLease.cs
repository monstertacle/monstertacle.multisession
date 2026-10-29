using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.UnitTests.Fixtures;

// A controlled lease double, deliberately not a native ownership or permission implementation.
internal sealed class TestStateDirectoryLease(string stateDirectory) : IStateDirectoryLease
{
    public string StateDirectory { get; } = stateDirectory;
    public bool IsHeld => !IsDisposed;
    public bool Matches(string directory) => Path.GetFullPath(directory) == Path.GetFullPath(StateDirectory);
    public bool IsDisposed { get; private set; }
    public ValueTask PrepareFileAsync(string fileName, StateFileMode mode, CancellationToken cancellationToken)
        => ValueTask.FromException(new SessionOperationException(new PublicError(ErrorCode.UnsupportedPlatform)));

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
