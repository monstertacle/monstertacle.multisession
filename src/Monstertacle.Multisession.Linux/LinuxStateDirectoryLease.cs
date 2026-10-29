
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Linux;

/// <summary>Owns a private Linux state directory for the complete store lifetime.</summary>
public sealed class LinuxStateDirectoryLease : IStateDirectoryLease
{
    private readonly object _gate = new();
    private readonly LinuxFileHandle _directory;
    private readonly LinuxFileHandle _ownership;
    private readonly LinuxFileIdentity _directoryIdentity;
    private readonly LinuxFileIdentity _lockIdentity;

    private LinuxStateDirectoryLease(string path, LinuxFileHandle directory, LinuxFileHandle ownership)
    {
        StateDirectory = path;
        _directory = directory;
        _ownership = ownership;
        _directoryIdentity = LinuxFileIdentity.Read(directory);
        _lockIdentity = LinuxFileIdentity.Read(ownership);
    }
    /// <inheritdoc />
    public string StateDirectory { get; }
    /// <inheritdoc />
    public bool IsHeld { get { lock (_gate) { return CheckOwnership(); } } }
    /// <summary>Creates a new private leaf directory beneath an existing parent.</summary>
    public static LinuxStateDirectoryLease CreateNew(string stateDirectory) => Acquire(stateDirectory, true);
    /// <summary>Acquires an existing private directory without repairing its permissions.</summary>
    public static LinuxStateDirectoryLease OpenExisting(string stateDirectory) => Acquire(stateDirectory, false);
    /// <inheritdoc />
    public bool Matches(string stateDirectory) => string.Equals(StateDirectory, stateDirectory, StringComparison.Ordinal) && IsHeld;
    /// <inheritdoc />
    public ValueTask PrepareFileAsync(string fileName, StateFileMode mode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!CheckOwnership()) { throw LinuxNative.Failure(); }
            if (string.IsNullOrWhiteSpace(fileName) || fileName is "." or ".." or LinuxNative.LockName
                || fileName.Contains('/') || fileName.Contains('\0') || !Enum.IsDefined(mode))
            {
                throw LinuxNative.Failure(ErrorCode.InvalidConfiguration);
            }
            int flags = LinuxNative.FileFlags | (mode == StateFileMode.CreateNew ? LinuxNative.ExclusiveCreate : 0);
            using LinuxFileHandle file = LinuxNative.Open(LinuxNative.Descriptor(_directory), fileName, flags, LinuxNative.FileMode);
            LinuxFileIdentity.Read(file).RequirePrivate(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        return ValueTask.CompletedTask;
    }
    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            try { LinuxOwnershipLock.Release(_ownership); }
            finally { _directory.Dispose(); }
        }
        return ValueTask.CompletedTask;
    }

    private bool CheckOwnership()
    {
        if (_directory.IsClosed || _ownership.IsClosed) { return false; }
        try
        {
            using LinuxFileHandle current = LinuxDirectoryPath.Open(StateDirectory, false);
            using LinuxFileHandle currentLock = LinuxNative.Open(LinuxNative.Descriptor(current), LinuxNative.LockName, LinuxNative.FileFlags);
            LinuxFileIdentity identity = LinuxFileIdentity.Read(currentLock);
            identity.RequirePrivate(false);
            return _directoryIdentity.SameObject(LinuxFileIdentity.Read(current)) && _lockIdentity.SameObject(identity);
        }
        catch (SessionOperationException) { return false; }
    }

    private static LinuxStateDirectoryLease Acquire(string path, bool create)
    {
        LinuxDirectoryPath.Validate(path);
        LinuxFileHandle directory = LinuxDirectoryPath.Open(path, create);
        LinuxFileHandle? ownership = null;
        try
        {
            ownership = LinuxOwnershipLock.Acquire(directory);
            return new(path, directory, ownership);
        }
        catch
        {
            ownership?.Dispose();
            directory.Dispose();
            throw;
        }
    }
}
