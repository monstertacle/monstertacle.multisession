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
            Assert.Equal(identity.User, security.GetOwner(typeof(SecurityIdentifier)));
            FileSystemAccessRule rule = Assert.Single(security.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>());
            Assert.Equal(identity.User, rule.IdentityReference);
            Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
            Assert.Equal(FileSystemRights.FullControl, rule.FileSystemRights);
        }
        else { Assert.Equal((UnixFileMode)384, File.GetUnixFileMode(path)); }
    }
}
