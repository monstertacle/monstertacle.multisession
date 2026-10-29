using System.Security.AccessControl;
using System.Security.Principal;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Windows;
using Xunit;

namespace Monstertacle.Multisession.PlatformTests;

/// <summary>Actual Windows aliases and security changes never confer or repair store ownership.</summary>
public sealed class WindowsOwnershipRejectionTests : IDisposable
{
    private readonly string _root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "dev", ".agenttemp", "windows-rejection-" + Guid.NewGuid().ToString("N"));
    /// <summary>Creates only a test-owned parent.</summary>
    public WindowsOwnershipRejectionTests() => Directory.CreateDirectory(_root);
    /// <summary>Removes only the fixture's synthetic data.</summary>
    public void Dispose() => Directory.Delete(_root, true);

    /// <summary>UNC, extended aliases, device names and ambiguous lexical paths fail before creating data.</summary>
    [Theory]
    [InlineData("relative")]
    [InlineData(@"C:\")]
    [InlineData(@"\\127.0.0.1\share\state")]
    [InlineData(@"\\?\C:\state")]
    [InlineData("C:relative")]
    [InlineData("C:/state")]
    [InlineData(@"C:\a\..\state")]
    [InlineData(@"C:\a\.\state")]
    [InlineData(@"C:\state\\child")]
    [InlineData(@"C:\state.")]
    [InlineData(@"C:\state ")]
    [InlineData(@"C:\state:stream")]
    [InlineData(@"C:\NUL")]
    public void RejectsNoncanonicalDirectoryNames(string path)
        => Assert.Equal(ErrorCode.InvalidConfiguration, Assert.Throws<SessionOperationException>(() => WindowsStateDirectoryLease.CreateNew(path)).Failure.Code);

    /// <summary>File preparation cannot traverse paths, use devices/streams or replace the lock name through case folding.</summary>
    [Theory]
    [InlineData("../outside")]
    [InlineData(@"..\outside")]
    [InlineData(@"folder\file")]
    [InlineData("name:stream")]
    [InlineData("NUL.txt")]
    [InlineData("COM1")]
    [InlineData("LPT9")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("trailing.")]
    [InlineData("trailing ")]
    [InlineData(".MULTISESSION.LOCK")]
    public async Task RejectsUnsafeFileNames(string name)
    {
        await using WindowsStateDirectoryLease lease = WindowsStateDirectoryLease.CreateNew(Path.Combine(_root, "state"));
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => lease.PrepareFileAsync(name, StateFileMode.CreateNew, CancellationToken.None).AsTask());
        Assert.Equal(ErrorCode.InvalidConfiguration, failure.Failure.Code);
        Assert.Single(Directory.GetFiles(lease.StateDirectory));
        Assert.True(lease.IsHeld);
    }

    /// <summary>A junction ancestor is refused before a new child can appear beneath its target.</summary>
    [Fact]
    public async Task RejectsJunctionAncestorWithoutCreatingTargetChild()
    {
        await using WindowsStateDirectoryLease target = WindowsStateDirectoryLease.CreateNew(Path.Combine(_root, "target"));
        string link = Path.Combine(_root, "junction");
        await WindowsLinkProbe.CreateAsync("Junction", link, target.StateDirectory);
        try
        {
            Assert.Throws<SessionOperationException>(() => WindowsStateDirectoryLease.CreateNew(Path.Combine(link, "child")));
            Assert.False(Directory.Exists(Path.Combine(target.StateDirectory, "child")));
            Assert.True(target.IsHeld);
        }
        finally { Directory.Delete(link, recursive: false); }
    }

    /// <summary>A private file's hardlink is rejected without changing the original bytes.</summary>
    [Fact]
    public async Task RejectsHardlinkedFileWithoutChangingTarget()
    {
        await using WindowsStateDirectoryLease lease = WindowsStateDirectoryLease.CreateNew(Path.Combine(_root, "state"));
        await lease.PrepareFileAsync("original", StateFileMode.CreateNew, CancellationToken.None);
        string original = Path.Combine(lease.StateDirectory, "original");
        await File.WriteAllTextAsync(original, "retained");
        await WindowsLinkProbe.CreateAsync("HardLink", Path.Combine(lease.StateDirectory, "alias"), original);
        await Assert.ThrowsAsync<SessionOperationException>(() => lease.PrepareFileAsync("alias", StateFileMode.OpenExisting, CancellationToken.None).AsTask());
        Assert.Equal("retained", await File.ReadAllTextAsync(original));
    }

    /// <summary>Broad file rights are rejected and are not silently repaired.</summary>
    [Fact]
    public async Task RejectsBroadFileAclWithoutRepair()
    {
        await using WindowsStateDirectoryLease lease = WindowsStateDirectoryLease.CreateNew(Path.Combine(_root, "state"));
        await lease.PrepareFileAsync("data", StateFileMode.CreateNew, CancellationToken.None);
        FileInfo file = new(Path.Combine(lease.StateDirectory, "data"));
        FileSecurity security = file.GetAccessControl();
        security.AddAccessRule(PublicRead());
        file.SetAccessControl(security);
        string original = file.GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.Access | AccessControlSections.Owner);
        await Assert.ThrowsAsync<SessionOperationException>(() => lease.PrepareFileAsync("data", StateFileMode.OpenExisting, CancellationToken.None).AsTask());
        Assert.Equal(original, file.GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.Access | AccessControlSections.Owner));
    }

    /// <summary>A changed root ACL immediately withdraws ownership and blocks further file creation.</summary>
    [Fact]
    public async Task RejectsRootAclChangeDuringOwnership()
    {
        await using WindowsStateDirectoryLease lease = WindowsStateDirectoryLease.CreateNew(Path.Combine(_root, "state"));
        DirectoryInfo directory = new(lease.StateDirectory);
        DirectorySecurity security = directory.GetAccessControl();
        security.AddAccessRule(PublicRead());
        directory.SetAccessControl(security);
        Assert.False(lease.IsHeld);
        await Assert.ThrowsAsync<SessionOperationException>(() => lease.PrepareFileAsync("data", StateFileMode.CreateNew, CancellationToken.None).AsTask());
        Assert.False(File.Exists(Path.Combine(lease.StateDirectory, "data")));
    }

    /// <summary>Held ancestor handles prevent renaming the owned root; release permits an explicit later move.</summary>
    [Fact]
    public async Task HoldsDirectoryIdentityUntilRelease()
    {
        string path = Path.Combine(_root, "state");
        string moved = Path.Combine(_root, "moved");
        await using (WindowsStateDirectoryLease lease = WindowsStateDirectoryLease.CreateNew(path))
        {
            Assert.Throws<IOException>(() => Directory.Move(path, moved));
            Assert.True(lease.IsHeld);
        }
        Directory.Move(path, moved);
        await using WindowsStateDirectoryLease reopened = WindowsStateDirectoryLease.OpenExisting(moved);
        Assert.True(reopened.IsHeld);
    }

    private static FileSystemAccessRule PublicRead()
        => new(new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.ReadAndExecute, AccessControlType.Allow);
}
