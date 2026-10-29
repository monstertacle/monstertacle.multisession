using System.Security.AccessControl;
using System.Security.Principal;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Windows;
using Xunit;

namespace Monstertacle.Multisession.PlatformTests;

/// <summary>Native Windows ACLs and lifetime ownership, never a cooperative lease double.</summary>
public sealed class WindowsOwnershipTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "windows-ownership-" + Guid.NewGuid().ToString("N"));
    /// <summary>Creates an isolated parent.</summary>
    public WindowsOwnershipTests() => Directory.CreateDirectory(_root);
    /// <summary>Removes only test-owned files.</summary>
    public void Dispose() => Directory.Delete(_root, true);

    /// <summary>Private directory and file creation retains original contents across exclusive lifetime ownership.</summary>
    [Fact]
    public async Task CreatesPrivateDirectoryAndFilesAndReacquires()
    {
        string path = Path.Combine(_root, "state");
        await using (WindowsStateDirectoryLease lease = WindowsStateDirectoryLease.CreateNew(path))
        {
            Assert.True(lease.IsHeld);
            Assert.True(lease.Matches(path));
            AssertPrivateAcl(new DirectoryInfo(path).GetAccessControl());
            await lease.PrepareFileAsync("state.db", StateFileMode.CreateNew, CancellationToken.None);
            string file = Path.Combine(path, "state.db");
            AssertPrivateAcl(new FileInfo(file).GetAccessControl());
            await File.WriteAllTextAsync(file, "preserve");
            Assert.Equal(ErrorCode.StoreAlreadyOwned,
                Assert.Throws<SessionOperationException>(() => WindowsStateDirectoryLease.OpenExisting(path)).Failure.Code);
        }
        await using WindowsStateDirectoryLease reopened = WindowsStateDirectoryLease.OpenExisting(path);
        await reopened.PrepareFileAsync("state.db", StateFileMode.OpenExisting, CancellationToken.None);
        Assert.Equal("preserve", await File.ReadAllTextAsync(Path.Combine(path, "state.db")));
    }

    /// <summary>Broad existing ACLs are rejected without repair.</summary>
    [Fact]
    public void RejectsPublicDirectoryWithoutRepair()
    {
        string path = Path.Combine(_root, "public");
        Directory.CreateDirectory(path);
        DirectorySecurity security = new DirectoryInfo(path).GetAccessControl();
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.ReadAndExecute, AccessControlType.Allow));
        new DirectoryInfo(path).SetAccessControl(security);
        string original = new DirectoryInfo(path).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.All);
        Assert.Equal(ErrorCode.StorageUnavailable,
            Assert.Throws<SessionOperationException>(() => WindowsStateDirectoryLease.OpenExisting(path)).Failure.Code);
        Assert.Equal(original, new DirectoryInfo(path).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.All));
    }

    /// <summary>Existing data cannot be overwritten, and disposed ownership cannot create files.</summary>
    [Fact]
    public async Task PreservesExistingFileAndRefusesDisposedOwnership()
    {
        WindowsStateDirectoryLease lease = WindowsStateDirectoryLease.CreateNew(Path.Combine(_root, "state"));
        await using (lease)
        {
            await lease.PrepareFileAsync("data", StateFileMode.CreateNew, CancellationToken.None);
            await File.WriteAllTextAsync(Path.Combine(lease.StateDirectory, "data"), "preserve");
            await Assert.ThrowsAsync<SessionOperationException>(() => lease.PrepareFileAsync("data", StateFileMode.CreateNew, CancellationToken.None).AsTask());
            Assert.Equal("preserve", await File.ReadAllTextAsync(Path.Combine(lease.StateDirectory, "data")));
        }
        Assert.False(lease.IsHeld);
        Assert.False(lease.Matches(lease.StateDirectory));
        await Assert.ThrowsAsync<SessionOperationException>(() => lease.PrepareFileAsync("data2", StateFileMode.CreateNew, CancellationToken.None).AsTask());
    }

    private static void AssertPrivateAcl(FileSystemSecurity security)
    {
        SecurityIdentifier owner = WindowsIdentity.GetCurrent().User!;
        Assert.Equal(owner, security.GetOwner(typeof(SecurityIdentifier)));
        FileSystemAccessRule rule = Assert.Single(security.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>());
        Assert.Equal(owner, rule.IdentityReference);
        Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
        Assert.Equal(FileSystemRights.FullControl, rule.FileSystemRights);
        Assert.True(security.AreAccessRulesProtected);
    }
}
