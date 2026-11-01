using System.Security.AccessControl;
using System.Security.Principal;

namespace Monstertacle.Multisession.Delivery;

/// <summary>
/// Owner-only state on both platforms. Unix: directory 0700, database and lease 0600. Windows: a protected access rule
/// for the current user only on the state directory, inherited by the files created in it.
/// </summary>
internal static class PrivateState
{
    private const UnixFileMode OwnerFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode OwnerDirectory = OwnerFile | UnixFileMode.UserExecute;

    public static void PrepareDirectory(string directory)
    {
        if (OperatingSystem.IsWindows())
        {
            var owner = WindowsIdentity.GetCurrent().User ?? throw new UnauthorizedAccessException("No current user identity.");
            var security = new DirectorySecurity();
            security.SetOwner(owner);
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(owner, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            var info = new DirectoryInfo(directory);
            if (info.Parent is { Exists: false } parent)
            {
                parent.Create();
            }
            if (info.Exists)
            {
                info.SetAccessControl(security);
            }
            else
            {
                info.Create(security);
            }
            return;
        }
        Directory.CreateDirectory(directory, OwnerDirectory);
        File.SetUnixFileMode(directory, OwnerDirectory);
    }

    /// <summary>Creates a missing database file owner-only before SQLite opens it; an empty file is a valid new database.</summary>
    public static void CreateDatabase(string path)
    {
        if (!File.Exists(path))
        {
            using var file = new FileStream(path, Options(FileMode.CreateNew, FileShare.ReadWrite));
        }
    }

    /// <summary>Takes the exclusive operating-system lease on the sibling lock file.</summary>
    public static FileStream OpenLease(string path)
    {
        try
        {
            return new FileStream(path, Options(FileMode.OpenOrCreate, FileShare.None));
        }
        catch (IOException contention)
        {
            throw new DeliveryRefusalException(DeliveryRefusal.ExclusiveAccessUnavailable, contention);
        }
    }

    private static FileStreamOptions Options(FileMode mode, FileShare share)
    {
        var options = new FileStreamOptions { Mode = mode, Access = FileAccess.ReadWrite, Share = share };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = OwnerFile;
        }
        return options;
    }
}
