using System.Security.AccessControl;
using System.Security.Principal;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.IntegrationTests.Fixtures;

// An isolated private file fixture, not a native lifetime-lock or alias-security implementation.
internal sealed class PrivateStoreDirectory : IStateDirectoryLease
{
    private bool _released;
    public string StateDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "dev", ".agenttemp", "multisession-" + Guid.NewGuid().ToString("N"));
    public string DatabasePath => Path.Combine(StateDirectory, "multisession.db");
    public bool IsHeld => !_released;
    public int PreparedFiles { get; private set; }
    public Guid TrustScopeId { get; } = Guid.NewGuid();
    public RuntimeOptions Options => new() { StateDirectory = StateDirectory, Retention = new(true, null) };

    public PrivateStoreDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            SecurityIdentifier owner = WindowsIdentity.GetCurrent().User!;
            DirectorySecurity security = new();
            security.SetOwner(owner);
            security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new FileSystemAccessRule(owner, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            Directory.CreateDirectory(Path.GetDirectoryName(StateDirectory)!);
            new DirectoryInfo(StateDirectory).Create(security);
        }
        else { Directory.CreateDirectory(StateDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
    }

    public bool Matches(string directory) => string.Equals(Path.GetFullPath(directory), StateDirectory,
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    public ValueTask PrepareFileAsync(string fileName, StateFileMode mode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsHeld || Path.GetFileName(fileName) != fileName || fileName is "." or ".." || string.IsNullOrWhiteSpace(fileName))
        {
            throw new SessionOperationException(new(ErrorCode.InvalidConfiguration));
        }
        PreparedFiles++;
        string path = Path.Combine(StateDirectory, fileName);
        if (mode == StateFileMode.OpenExisting)
        {
            if (!File.Exists(path)) { throw new SessionOperationException(new(ErrorCode.StorageUnavailable)); }
            if (!OperatingSystem.IsWindows() && File.GetUnixFileMode(path) != (UnixFileMode.UserRead | UnixFileMode.UserWrite))
            {
                throw new SessionOperationException(new(ErrorCode.InvalidConfiguration));
            }
        }
        else
        {
            FileStreamOptions options = new() { Mode = FileMode.CreateNew, Access = FileAccess.ReadWrite, Share = FileShare.None };
            if (!OperatingSystem.IsWindows()) { options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite; }
            using FileStream stream = new(path, options);
        }
        return ValueTask.CompletedTask;
    }

    public void Release() => _released = true;
    public ValueTask DisposeAsync()
    {
        _released = true;
        Directory.Delete(StateDirectory, true);
        return ValueTask.CompletedTask;
    }
}
