using System.Runtime.Versioning;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Linux;
using Xunit;

namespace Monstertacle.Multisession.PlatformTests;

/// <summary>Explicit lease disposal releases ownership even while a fork-equivalent descriptor remains open.</summary>
[SupportedOSPlatform("linux")]
public sealed class LinuxOwnershipReleaseTests
{
    /// <summary>Reacquisition needs no retry and disposing the old lease twice cannot unlock its successor.</summary>
    [Fact]
    public async Task ReleasesSharedDescriptionWithoutUnlockingSuccessor()
    {
        string path = Path.Combine(Path.GetTempPath(), "linux-release-" + Guid.NewGuid().ToString("N"));
        LinuxStateDirectoryLease lease = LinuxStateDirectoryLease.CreateNew(path);
        int duplicate = -1;
        try
        {
            duplicate = LinuxDescriptorProbe.DuplicateLock(path);
            await lease.DisposeAsync();
            await using LinuxStateDirectoryLease successor = LinuxStateDirectoryLease.OpenExisting(path);
            Assert.True(successor.IsHeld);
            await lease.DisposeAsync();
            SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(async () =>
            {
                await using LinuxStateDirectoryLease refused = LinuxStateDirectoryLease.OpenExisting(path);
            });
            Assert.Equal(ErrorCode.StoreAlreadyOwned, failure.Failure.Code);
            Assert.True(successor.IsHeld);
        }
        finally
        {
            if (duplicate >= 0) { Assert.Equal(0, LinuxDescriptorProbe.Close(duplicate)); }
            await lease.DisposeAsync();
            Directory.Delete(path, recursive: true);
        }
    }
}
