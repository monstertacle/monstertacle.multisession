using Xunit;

namespace Monstertacle.Multisession.Checkpoints.Tests;

/// <summary>The base contract is usable without any storage: typed reads, cancellation and one ownership release.</summary>
public sealed class CheckpointContractTests
{
    /// <summary>A read returns the implementation's own typed snapshot.</summary>
    [Fact]
    public async Task ReadReturnsTheTypedDomainSnapshot()
    {
        var expected = new CounterSnapshot(7, "ledger");
        using var checkpoint = new InMemoryCheckpoint(expected);

        CounterSnapshot actual = await checkpoint.ReadAsync(CancellationToken.None);

        Assert.Same(expected, actual);
    }

    /// <summary>A contract-only consumer reads once and releases ownership exactly once.</summary>
    [Fact]
    public async Task OwnedReadReleasesTheCheckpointExactlyOnce()
    {
        var checkpoint = new InMemoryCheckpoint(new CounterSnapshot(1, "a"));

        CounterSnapshot snapshot = await OwnedSnapshotReader.ReadAndReleaseAsync(checkpoint, CancellationToken.None);

        Assert.Equal(1, snapshot.Position);
        Assert.Equal(1, checkpoint.Reads);
        Assert.Equal(1, checkpoint.Disposals);
    }

    /// <summary>A cancelled read throws, reads nothing and the consumer still releases ownership.</summary>
    [Fact]
    public async Task CancelledReadThrowsReadsNothingAndStillReleases()
    {
        var checkpoint = new InMemoryCheckpoint(new CounterSnapshot(1, "a"));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => OwnedSnapshotReader.ReadAndReleaseAsync(checkpoint, cancelled.Token));

        Assert.Equal(0, checkpoint.Reads);
        Assert.Equal(1, checkpoint.Disposals);
    }
}
