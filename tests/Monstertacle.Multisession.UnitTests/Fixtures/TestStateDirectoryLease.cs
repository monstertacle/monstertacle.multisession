using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.UnitTests.Fixtures;

// A controlled lease double, deliberately not a native ownership or permission implementation.
internal sealed class TestStateDirectoryLease(string stateDirectory) : IStateDirectoryLease
{
    public string StateDirectory { get; } = stateDirectory;
    public bool IsDisposed { get; private set; }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
