using System.Security.AccessControl;
using System.Security.Principal;
using Xunit;

namespace Monstertacle.Multisession.DeliveryCheckpoints.Tests;

/// <summary>Native private-state and lease behaviour; each branch runs in its native CI.</summary>
public sealed class PrivateAccessTests : IDisposable
{
    private readonly TestDirectory _directory = new();

    /// <summary>Owner open keeps state owner only on the native platform.</summary>
    [Fact]
    public async Task OwnerOpenKeepsStateOwnerOnlyOnTheNativePlatform()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_directory.StatePath)!);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(Path.GetDirectoryName(_directory.StatePath)!, (UnixFileMode)Convert.ToInt32("755", 8));
        }

        using var checkpoint = await DeliveryCheckpoint.OpenAsync(Payloads.Options(_directory.StatePath), CancellationToken.None);

        var directory = Path.GetDirectoryName(_directory.StatePath)!;
        if (OperatingSystem.IsWindows())
        {
            var security = new DirectoryInfo(directory).GetAccessControl();
            var owner = WindowsIdentity.GetCurrent().User!;
            Assert.True(security.AreAccessRulesProtected);
            var rule = Assert.Single(security.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>());
            Assert.Equal(owner, rule.IdentityReference);
            Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
            Assert.Equal(FileSystemRights.FullControl, rule.FileSystemRights & FileSystemRights.FullControl);
        }
        else
        {
            var owner = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            Assert.Equal(owner | UnixFileMode.UserExecute, File.GetUnixFileMode(directory));
            Assert.Equal(owner, File.GetUnixFileMode(_directory.StatePath));
            Assert.Equal(owner, File.GetUnixFileMode(_directory.StatePath + ".lock"));
        }
    }

    /// <summary>A missing state directory is created owner-only on the native platform.</summary>
    [Fact]
    public async Task AMissingStateDirectoryIsCreatedOwnerOnly()
    {
        var directory = Path.GetDirectoryName(_directory.StatePath)!;
        Assert.False(Directory.Exists(directory));

        using var checkpoint = await DeliveryCheckpoint.OpenAsync(Payloads.Options(_directory.StatePath), CancellationToken.None);

        if (OperatingSystem.IsWindows())
        {
            var security = new DirectoryInfo(directory).GetAccessControl();
            Assert.True(security.AreAccessRulesProtected);
            var rule = Assert.Single(security.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>());
            Assert.Equal(WindowsIdentity.GetCurrent().User, rule.IdentityReference);
        }
        else
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(directory));
        }
    }

    /// <summary>The lease is an operating system exclusive lock.</summary>
    [Fact]
    public async Task TheLeaseIsAnOperatingSystemExclusiveLock()
    {
        using var checkpoint = await DeliveryCheckpoint.OpenAsync(Payloads.Options(_directory.StatePath), CancellationToken.None);

        Assert.True(File.Exists(_directory.StatePath + ".lock"));
        Assert.ThrowsAny<IOException>(() => new FileStream(_directory.StatePath + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite).Dispose());
    }

    /// <summary>A writer outside the lease is reported as busy without change.</summary>
    [Fact]
    public async Task AWriterOutsideTheLeaseIsReportedAsBusyWithoutChange()
    {
        using var checkpoint = await DeliveryCheckpoint.OpenAsync(Payloads.Options(_directory.StatePath), CancellationToken.None);
        using var intruder = RawDatabase.Open(_directory.StatePath);
        using var transaction = intruder.BeginTransaction();
        using (var write = intruder.CreateCommand())
        {
            write.Transaction = transaction;
            write.CommandText = "UPDATE checkpoint SET attempts=attempts WHERE id=1";
            write.ExecuteNonQuery();
        }

        await Refusals.Expect(DeliveryRefusal.StorageBusy, () => checkpoint.StageAsync(1, Payloads.Item(1), CancellationToken.None));

        transaction.Rollback();
        Assert.Null((await checkpoint.ReadAsync(CancellationToken.None)).Pending);
    }

    /// <inheritdoc />
    public void Dispose() => _directory.Dispose();
}
