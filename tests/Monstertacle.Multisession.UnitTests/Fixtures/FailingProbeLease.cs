using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.UnitTests.Fixtures;

internal sealed class FailingProbeLease(string directory, Exception? failure = null) : IStateDirectoryLease
{
    public string StateDirectory => directory;
    public bool IsHeld => true;
    internal int Disposals { get; private set; }
    public bool Matches(string path) => Path.GetFullPath(path) == Path.GetFullPath(directory);
    public ValueTask PrepareFileAsync(string fileName, StateFileMode mode, CancellationToken token) => ValueTask.CompletedTask;
    public ValueTask DisposeAsync()
    {
        Disposals++;
        return failure is null ? ValueTask.CompletedTask : ValueTask.FromException(failure);
    }
}
