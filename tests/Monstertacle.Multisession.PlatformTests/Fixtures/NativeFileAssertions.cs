using System.Security.AccessControl;
using System.Security.Principal;
using Xunit;

namespace Monstertacle.Multisession.PlatformTests.Fixtures;

// Native permissions are independent of request/history recovery assertions.
internal static class NativeFileAssertions
{
    internal static void Private(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            FileSecurity security = new FileInfo(path).GetAccessControl();
            IdentityReference? owner = security.GetOwner(typeof(SecurityIdentifier));
            Assert.True(Equals(identity.User, owner) || Equals(identity.Owner, owner));
            FileSystemAccessRule rule = Assert.Single(security.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>());
            Assert.Equal(identity.User, rule.IdentityReference);
            Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
            Assert.Equal(FileSystemRights.FullControl, rule.FileSystemRights);
        }
        else { Assert.Equal((UnixFileMode)384, File.GetUnixFileMode(path)); }
    }
    internal static void MakePublic(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            FileSecurity security = new FileInfo(path).GetAccessControl();
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.Read, AccessControlType.Allow));
            new FileInfo(path).SetAccessControl(security);
        }
        else { File.SetUnixFileMode(path, (UnixFileMode)420); }
    }
}
