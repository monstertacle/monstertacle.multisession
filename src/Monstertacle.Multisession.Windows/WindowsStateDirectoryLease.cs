using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Windows;

/// <summary>Owns a private Windows state directory for the complete store lifetime.</summary>
public sealed class WindowsStateDirectoryLease : IStateDirectoryLease
{
    private WindowsStateDirectoryLease() { }
    /// <inheritdoc />
    public string StateDirectory => throw Unsupported();
    /// <inheritdoc />
    public bool IsHeld => false;
    /// <summary>Creates a new private leaf directory beneath an existing parent.</summary>
    public static WindowsStateDirectoryLease CreateNew(string stateDirectory) => throw Unsupported();
    /// <summary>Acquires an existing private directory without repairing its ACL.</summary>
    public static WindowsStateDirectoryLease OpenExisting(string stateDirectory) => throw Unsupported();
    /// <inheritdoc />
    public bool Matches(string stateDirectory) => false;
    /// <inheritdoc />
    public ValueTask PrepareFileAsync(string fileName, StateFileMode mode, CancellationToken cancellationToken)
        => ValueTask.FromException(Unsupported());
    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    private static SessionOperationException Unsupported() => new(new(ErrorCode.UnsupportedPlatform));
}
