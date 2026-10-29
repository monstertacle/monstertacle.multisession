using Microsoft.Win32.SafeHandles;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Windows;

/// <summary>Owns a private Windows state directory for the complete store lifetime.</summary>
public sealed class WindowsStateDirectoryLease : IStateDirectoryLease
{
    private readonly object _gate = new();
    private readonly List<SafeFileHandle> _directories;
    private readonly SafeFileHandle _ownership;
    private readonly WindowsFileIdentity _identity;
    private WindowsStateDirectoryLease(string path, List<SafeFileHandle> directories, SafeFileHandle ownership)
    {
        StateDirectory = path;
        _directories = directories;
        _ownership = ownership;
        _identity = WindowsFileIdentity.Read(directories[^1]);
    }
    /// <inheritdoc />
    public string StateDirectory { get; }
    /// <inheritdoc />
    public bool IsHeld { get { lock (_gate) { return CheckOwnership(); } } }
    /// <summary>Creates a new private leaf directory beneath an existing parent.</summary>
    public static WindowsStateDirectoryLease CreateNew(string stateDirectory) => Acquire(stateDirectory, true);
    /// <summary>Acquires an existing private directory without repairing its ACL.</summary>
    public static WindowsStateDirectoryLease OpenExisting(string stateDirectory) => Acquire(stateDirectory, false);
    /// <inheritdoc />
    public bool Matches(string stateDirectory) => string.Equals(StateDirectory, stateDirectory, StringComparison.OrdinalIgnoreCase) && IsHeld;
    /// <inheritdoc />
    public ValueTask PrepareFileAsync(string fileName, StateFileMode mode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!CheckOwnership()) { throw WindowsNative.Failure(); }
            if (!WindowsDirectoryPath.ValidFileName(fileName) || string.Equals(fileName, WindowsNative.LockName, StringComparison.OrdinalIgnoreCase)
                || !Enum.IsDefined(mode)) { throw WindowsNative.Failure(ErrorCode.InvalidConfiguration); }
            try
            {
                string path = Path.Combine(StateDirectory, fileName);
                using WindowsPrivateSecurity security = new(false);
                using SafeFileHandle file = WindowsNative.Open(path, WindowsNative.ReadWrite, 0,
                    mode == StateFileMode.CreateNew ? WindowsNative.CreateNew : WindowsNative.OpenExisting,
                    mode == StateFileMode.CreateNew ? security.Pointer : 0);
                WindowsFileIdentity.RequireType(file, false);
                WindowsPrivateSecurity.Require(path, false);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) { throw; }
            catch (SessionOperationException) { throw; }
            catch (Exception) { throw WindowsNative.Failure(); }
        }
        return ValueTask.CompletedTask;
    }
    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            _ownership.Dispose();
            WindowsDirectoryPath.Close(_directories);
        }
        return ValueTask.CompletedTask;
    }
    private bool CheckOwnership()
    {
        if (_ownership.IsClosed) { return false; }
        try
        {
            using SafeFileHandle current = WindowsNative.Open(StateDirectory, WindowsNative.ReadAttributes,
                WindowsNative.ShareReadWrite, WindowsNative.OpenExisting);
            WindowsFileIdentity.RequireType(current, true);
            WindowsFileIdentity.RequireType(_ownership, false);
            WindowsPrivateSecurity.Require(StateDirectory, true);
            WindowsPrivateSecurity.Require(Path.Combine(StateDirectory, WindowsNative.LockName), false);
            return _identity == WindowsFileIdentity.Read(current);
        }
        catch (Exception) { return false; }
    }
    private static WindowsStateDirectoryLease Acquire(string path, bool create)
    {
        List<SafeFileHandle>? directories = null;
        SafeFileHandle? ownership = null;
        try
        {
            WindowsDirectoryPath.Validate(path);
            directories = WindowsDirectoryPath.Open(path, create);
            ownership = WindowsOwnershipLock.Acquire(path);
            return new(path, directories, ownership);
        }
        catch (Exception failure)
        {
            ownership?.Dispose();
            if (directories is not null) { WindowsDirectoryPath.Close(directories); }
            if (failure is SessionOperationException) { throw; }
            throw WindowsNative.Failure();
        }
    }
}
