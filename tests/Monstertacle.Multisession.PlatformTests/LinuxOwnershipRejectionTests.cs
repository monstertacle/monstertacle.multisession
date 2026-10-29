using System.Runtime.Versioning;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Linux;
using Xunit;

namespace Monstertacle.Multisession.PlatformTests;

/// <summary>Unsafe names and changed filesystem objects cannot confer ownership.</summary>
[SupportedOSPlatform("linux")]
public sealed class LinuxOwnershipRejectionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "linux-rejection-" + Guid.NewGuid().ToString("N"));
    /// <summary>Creates an isolated test parent.</summary>
    public LinuxOwnershipRejectionTests() => Directory.CreateDirectory(_root);
    /// <summary>Removes only test-owned files.</summary>
    public void Dispose() => Directory.Delete(_root, true);

    /// <summary>Aliases and relative paths are refused rather than silently normalized.</summary>
    [Theory]
    [InlineData("relative")]
    [InlineData("/")]
    [InlineData("//home/state")]
    [InlineData("/home/../state")]
    [InlineData("/home/./state")]
    [InlineData("/home/state/")]
    public void RejectsNoncanonicalDirectoryNames(string path)
    {
        Assert.Equal(ErrorCode.InvalidConfiguration,
            Assert.Throws<SessionOperationException>(() => LinuxStateDirectoryLease.CreateNew(path)).Failure.Code);
    }

    /// <summary>File preparation has no path traversal or lock-file replacement operation.</summary>
    [Theory]
    [InlineData("../outside")]
    [InlineData("/absolute")]
    [InlineData("nested/file")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData(".multisession.lock")]
    [InlineData("")]
    public async Task RejectsUnsafeFileNames(string name)
    {
        await using LinuxStateDirectoryLease lease = LinuxStateDirectoryLease.CreateNew(Path.Combine(_root, "state"));
        Assert.Equal(ErrorCode.InvalidConfiguration,
            (await Assert.ThrowsAsync<SessionOperationException>(() => lease.PrepareFileAsync(name, StateFileMode.CreateNew, CancellationToken.None).AsTask())).Failure.Code);
    }

    /// <summary>Existing file symlinks are not followed even when their target is private.</summary>
    [Fact]
    public async Task RejectsFileSymlinkAndRetainsTarget()
    {
        await using LinuxStateDirectoryLease lease = LinuxStateDirectoryLease.CreateNew(Path.Combine(_root, "state"));
        string target = Path.Combine(_root, "target");
        await File.WriteAllTextAsync(target, "outside");
        File.SetUnixFileMode(target, (UnixFileMode)384);
        File.CreateSymbolicLink(Path.Combine(lease.StateDirectory, "link"), target);
        await Assert.ThrowsAsync<SessionOperationException>(() => lease.PrepareFileAsync("link", StateFileMode.OpenExisting, CancellationToken.None).AsTask());
        Assert.Equal("outside", await File.ReadAllTextAsync(target));
    }

    /// <summary>A renamed directory or replaced lock inode invalidates the held path immediately.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectsReplacedOwnershipObjects(bool directory)
    {
        string path = Path.Combine(_root, "state");
        await using LinuxStateDirectoryLease lease = LinuxStateDirectoryLease.CreateNew(path);
        if (directory)
        {
            Directory.Move(path, Path.Combine(_root, "moved"));
            Directory.CreateDirectory(path, (UnixFileMode)448);
        }
        else
        {
            string file = Path.Combine(path, ".multisession.lock");
            File.Move(file, Path.Combine(path, "old-lock"));
            await File.WriteAllTextAsync(file, "");
            File.SetUnixFileMode(file, (UnixFileMode)384);
        }
        Assert.False(lease.IsHeld);
        Assert.False(lease.Matches(path));
        await Assert.ThrowsAsync<SessionOperationException>(() => lease.PrepareFileAsync("data", StateFileMode.CreateNew, CancellationToken.None).AsTask());
    }

    /// <summary>Pre-cancelled work creates no file and leaves ownership intact.</summary>
    [Fact]
    public async Task CancellationBeforePrepareDoesNotCreateFile()
    {
        await using LinuxStateDirectoryLease lease = LinuxStateDirectoryLease.CreateNew(Path.Combine(_root, "state"));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => lease.PrepareFileAsync("data", StateFileMode.CreateNew, cancellation.Token).AsTask());
        Assert.False(File.Exists(Path.Combine(lease.StateDirectory, "data")));
        Assert.True(lease.IsHeld);
    }
}
