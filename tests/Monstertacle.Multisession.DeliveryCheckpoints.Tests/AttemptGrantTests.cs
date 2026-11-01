using Xunit;

namespace Monstertacle.Multisession.DeliveryCheckpoints.Tests;

/// <summary>Host-authorized further attempts for a pending item; the checkpoint row stays unchanged.</summary>
public sealed class AttemptGrantTests : IDisposable
{
    private const string _context = "context-digest";
    private readonly TestDirectory _directory = new();
    private readonly Guid _item = Guid.NewGuid();

    private static async Task FailPreparationAsync(DeliveryCheckpoint checkpoint, int times)
    {
        for (var attempt = 0; attempt < times; attempt++)
        {
            await checkpoint.RecordPreparationFailureAsync(CancellationToken.None);
        }
    }

    /// <summary>A grant is recorded without changing the checkpoint row.</summary>
    [Fact]
    public async Task AGrantIsRecordedWithoutChangingTheCheckpointRow()
    {
        using var checkpoint = await CheckpointScenario.OpenAsync(_directory.StatePath);
        await CheckpointScenario.PendingAsync(checkpoint);
        await FailPreparationAsync(checkpoint, 3);
        var before = await checkpoint.ReadAsync(CancellationToken.None);

        var outcome = await new AttemptGrants(checkpoint).RecordAsync(new AttemptGrant(Guid.NewGuid(), before.ToExpectation(), _item, _context, Payloads.Receipt("g")), CancellationToken.None);

        Assert.True(outcome.Recorded);
        var after = await checkpoint.ReadAsync(CancellationToken.None);
        Assert.Equal((before.Position, before.Attempts, before.Phase, before.Pending!.Digest), (after.Position, after.Attempts, after.Phase, after.Pending!.Digest));
        Assert.Equal("3", RawDatabase.Scalar(_directory.StatePath, "SELECT attempts FROM preparation_rearms"));
    }

    /// <summary>Grants for one item need strictly increasing attempts and are listed in order.</summary>
    [Fact]
    public async Task GrantsForOneItemNeedStrictlyIncreasingAttemptsAndAreListedInOrder()
    {
        using var checkpoint = await CheckpointScenario.OpenAsync(_directory.StatePath);
        await CheckpointScenario.PendingAsync(checkpoint);
        await FailPreparationAsync(checkpoint, 3);
        var grants = new AttemptGrants(checkpoint);
        var first = (await checkpoint.ReadAsync(CancellationToken.None)).ToExpectation();
        await grants.RecordAsync(new AttemptGrant(Guid.NewGuid(), first, _item, _context, "first"), CancellationToken.None);

        await Refusals.Expect(DeliveryRefusal.NotEligible, () => grants.RecordAsync(new AttemptGrant(Guid.NewGuid(), first, _item, _context, "same"), CancellationToken.None));
        await FailPreparationAsync(checkpoint, 3);
        var second = (await checkpoint.ReadAsync(CancellationToken.None)).ToExpectation();
        await grants.RecordAsync(new AttemptGrant(Guid.NewGuid(), second, _item, _context, "second"), CancellationToken.None);

        var listed = await grants.ListAsync(_context, second, _item, CancellationToken.None);
        Assert.Equal(["first", "second"], listed.Select(grant => grant.Receipt));
        Assert.Equal([3, 6], listed.Select(grant => grant.Attempts));
        Assert.Empty(await grants.ListAsync("other-context", second, _item, CancellationToken.None));
    }

    /// <summary>Only a pending item is eligible.</summary>
    [Fact]
    public async Task OnlyAPendingItemIsEligible()
    {
        using var checkpoint = await CheckpointScenario.OpenAsync(_directory.StatePath);
        var uncertain = await CheckpointScenario.UncertainAsync(checkpoint);

        await Refusals.Expect(DeliveryRefusal.NotEligible, () => new AttemptGrants(checkpoint).RecordAsync(new AttemptGrant(Guid.NewGuid(), uncertain, _item, _context, "g"), CancellationToken.None));
        await Refusals.Expect(DeliveryRefusal.StaleExpectation, () => new AttemptGrants(checkpoint).RecordAsync(new AttemptGrant(Guid.NewGuid(), uncertain with { Phase = DeliveryPhase.Pending }, _item, _context, "g"), CancellationToken.None));
        Assert.Equal(-1, RawDatabase.Count(_directory.StatePath, "preparation_rearms"));
    }

    /// <summary>A full journal refuses at the configured capacity.</summary>
    [Fact]
    public async Task AFullJournalRefusesAtTheConfiguredCapacity()
    {
        using var checkpoint = await CheckpointScenario.OpenAsync(_directory.StatePath, maximumRetainedGrants: 2);
        var expectation = await CheckpointScenario.PendingAsync(checkpoint);
        var grants = new AttemptGrants(checkpoint);
        await grants.RecordAsync(new AttemptGrant(Guid.NewGuid(), expectation, _item, _context, "g"), CancellationToken.None);
        CheckpointScenario.FillJournal(_directory.StatePath, "preparation_rearms", 1);
        await FailPreparationAsync(checkpoint, 1);
        var next = (await checkpoint.ReadAsync(CancellationToken.None)).ToExpectation();

        await Refusals.Expect(DeliveryRefusal.GrantCapacity, () => grants.RecordAsync(new AttemptGrant(Guid.NewGuid(), next, _item, _context, "h"), CancellationToken.None));
        Assert.Equal(2, RawDatabase.Count(_directory.StatePath, "preparation_rearms"));
    }

    /// <inheritdoc />
    public void Dispose() => _directory.Dispose();
}
