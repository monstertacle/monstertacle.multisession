using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ValidationHost;

// Cooperative fixture ownership only. Parent prepared and marked this private test root.
internal sealed class CrashStateDirectoryLease(string directory) : IStateDirectoryLease
{
    private bool _released;
    public bool IsHeld => !_released;
    public string StateDirectory { get; } = directory;
    public bool Matches(string stateDirectory) => string.Equals(Path.GetFullPath(stateDirectory), StateDirectory,
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    public ValueTask PrepareFileAsync(string fileName, StateFileMode mode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsHeld || mode != StateFileMode.OpenExisting || Path.GetFileName(fileName) != fileName
            || !File.Exists(Path.Combine(StateDirectory, fileName)))
        {
            throw new SessionOperationException(new(ErrorCode.InvalidConfiguration));
        }
        return ValueTask.CompletedTask;
    }
    public ValueTask DisposeAsync() { _released = true; return ValueTask.CompletedTask; }
}
