using System.Runtime.Versioning;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Linux;
using Xunit;

namespace Monstertacle.Multisession.PlatformTests;

/// <summary>Native permissions and lifetime ownership, never a cooperative lease double.</summary>
[SupportedOSPlatform("linux")]
public sealed class LinuxOwnershipTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "linux-ownership-" + Guid.NewGuid().ToString("N"));
    /// <summary>Creates an isolated parent outside application state.</summary>
    public LinuxOwnershipTests() => Directory.CreateDirectory(_root);
    /// <summary>Removes only this test's directory.</summary>
    public void Dispose() => Directory.Delete(_root, true);

    /// <summary>Creation uses private native modes and preserves existing data across reacquisition.</summary>
    [Fact]
    public async Task CreatesPrivateDirectoryAndFilesAndReacquires()
    {
        string path = Path.Combine(_root, "state");
        await using (LinuxStateDirectoryLease lease = LinuxStateDirectoryLease.CreateNew(path))
        {
            Assert.True(lease.IsHeld);
            Assert.True(lease.Matches(path));
            Assert.Equal((UnixFileMode)448, File.GetUnixFileMode(path));
            await lease.PrepareFileAsync("state.db", StateFileMode.CreateNew, CancellationToken.None);
            Assert.Equal((UnixFileMode)384, File.GetUnixFileMode(Path.Combine(path, "state.db")));
            await File.WriteAllTextAsync(Path.Combine(path, "state.db"), "preserve");
            SessionOperationException failure = Assert.Throws<SessionOperationException>(() => LinuxStateDirectoryLease.OpenExisting(path));
            Assert.Equal(ErrorCode.StoreAlreadyOwned, failure.Failure.Code);
        }
        await using LinuxStateDirectoryLease reopened = LinuxStateDirectoryLease.OpenExisting(path);
        await reopened.PrepareFileAsync("state.db", StateFileMode.OpenExisting, CancellationToken.None);
        Assert.Equal("preserve", await File.ReadAllTextAsync(Path.Combine(path, "state.db")));
    }

    /// <summary>Existing broad permissions are rejected without repair.</summary>
    [Fact]
    public void RejectsPublicDirectoryWithoutChangingIt()
    {
        string path = Path.Combine(_root, "public");
        Directory.CreateDirectory(path);
        File.SetUnixFileMode(path, (UnixFileMode)493);
        SessionOperationException failure = Assert.Throws<SessionOperationException>(() => LinuxStateDirectoryLease.OpenExisting(path));
        Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
        Assert.Equal((UnixFileMode)493, File.GetUnixFileMode(path));
    }

    /// <summary>A symlink at any directory component is not canonical ownership.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectsDirectorySymlinks(bool parent)
    {
        string actual = Path.Combine(_root, "actual");
        Directory.CreateDirectory(actual, (UnixFileMode)448);
        Directory.CreateDirectory(Path.Combine(actual, "state"), (UnixFileMode)448);
        string alias = Path.Combine(_root, "alias");
        Directory.CreateSymbolicLink(alias, actual);
        string path = parent ? Path.Combine(alias, "state") : alias;
        Assert.Equal(ErrorCode.StorageUnavailable,
            Assert.Throws<SessionOperationException>(() => LinuxStateDirectoryLease.OpenExisting(path)).Failure.Code);
    }

    /// <summary>Existing data files must never be overwritten or have permissions repaired.</summary>
    [Fact]
    public async Task RejectsExistingAndPublicFilesWithoutRepair()
    {
        await using LinuxStateDirectoryLease lease = LinuxStateDirectoryLease.CreateNew(Path.Combine(_root, "state"));
        await lease.PrepareFileAsync("data", StateFileMode.CreateNew, CancellationToken.None);
        string file = Path.Combine(lease.StateDirectory, "data");
        await File.WriteAllTextAsync(file, "preserve");
        await Assert.ThrowsAsync<SessionOperationException>(() => lease.PrepareFileAsync("data", StateFileMode.CreateNew, CancellationToken.None).AsTask());
        File.SetUnixFileMode(file, (UnixFileMode)420);
        await Assert.ThrowsAsync<SessionOperationException>(() => lease.PrepareFileAsync("data", StateFileMode.OpenExisting, CancellationToken.None).AsTask());
        Assert.Equal((UnixFileMode)420, File.GetUnixFileMode(file));
        Assert.Equal("preserve", await File.ReadAllTextAsync(file));
    }

    /// <summary>Dispose releases ownership and forbids file creation through the old lease.</summary>
    [Fact]
    public async Task DisposedLeaseCannotPrepareFiles()
    {
        LinuxStateDirectoryLease lease = LinuxStateDirectoryLease.CreateNew(Path.Combine(_root, "state"));
        await lease.DisposeAsync();
        await lease.DisposeAsync();
        Assert.False(lease.IsHeld);
        Assert.False(lease.Matches(lease.StateDirectory));
        await Assert.ThrowsAsync<SessionOperationException>(() => lease.PrepareFileAsync("data", StateFileMode.CreateNew, CancellationToken.None).AsTask());
    }
}
