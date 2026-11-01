using Xunit;

namespace Monstertacle.Multisession.DeliveryCheckpoints.Tests;

/// <summary>Checkpoints written by the current Listener release open unchanged and keep their custody.</summary>
public sealed class CurrentFormatTests : IDisposable
{
    private readonly TestDirectory _directory = new();

    /// <summary>Owner open returns listener pending bytes and counters unchanged.</summary>
    [Fact]
    public async Task OwnerOpenReturnsListenerPendingBytesAndCountersUnchanged()
    {
        var fixture = ListenerFixture.Rebuild("custody", _directory.StatePath);
        using var checkpoint = await DeliveryCheckpoint.OpenAsync(Payloads.Options(_directory.StatePath, fixture.Channel), CancellationToken.None);

        var state = await checkpoint.ReadAsync(CancellationToken.None);

        Assert.Equal(1, state.Position);
        Assert.Equal(2, state.Pending!.Position);
        Assert.Equal(ListenerFixture.Utf8(fixture.Pending), state.Pending.Payload.ToArray());
        Assert.Equal(DeliveryPhase.Pending, state.Phase);
        Assert.Equal(1, state.Attempts);
        Assert.Equal(fixture.Row("checkpoint", 0).GetProperty("receipt").GetString(), state.Acknowledgement);
        Assert.Equal(fixture.Channel.RouteId, state.RouteId);
    }

    /// <summary>Pending digest equals the digest the listener recorded.</summary>
    [Fact]
    public async Task PendingDigestEqualsTheDigestTheListenerRecorded()
    {
        var fixture = ListenerFixture.Rebuild("custody", _directory.StatePath);
        using var checkpoint = await DeliveryCheckpoint.OpenExistingAsync(Payloads.Options(_directory.StatePath, fixture.Channel), CancellationToken.None);

        var state = await checkpoint.ReadAsync(CancellationToken.None);

        var rebindKey = System.Text.Json.JsonDocument.Parse(ListenerFixture.Output("rebindKey")).RootElement;
        Assert.Equal(rebindKey.GetProperty("pendingDigest").GetString(), state.Pending!.Digest);
    }

    /// <summary>Uncertain listener checkpoint stays uncertain with its attempts.</summary>
    [Fact]
    public async Task UncertainListenerCheckpointStaysUncertainWithItsAttempts()
    {
        var fixture = ListenerFixture.Rebuild("uncertain", _directory.StatePath);
        using var checkpoint = await DeliveryCheckpoint.OpenAsync(Payloads.Options(_directory.StatePath, fixture.Channel), CancellationToken.None);

        var state = await checkpoint.ReadAsync(CancellationToken.None);

        Assert.Equal(DeliveryPhase.Uncertain, state.Phase);
        Assert.Equal(2, state.Attempts);
        Assert.Equal(0, state.Position);
    }

    /// <summary>Stored listener receipts are returned byte for byte.</summary>
    [Fact]
    public async Task StoredListenerReceiptsAreReturnedByteForByte()
    {
        var fixture = ListenerFixture.Rebuild("custody", _directory.StatePath);
        using var checkpoint = await DeliveryCheckpoint.OpenExistingAsync(Payloads.Options(_directory.StatePath, fixture.Channel), CancellationToken.None);

        var rearm = await new AttemptGrants(checkpoint).FindAsync(Guid.Parse("c0000000-0000-4000-8000-000000000001"), CancellationToken.None);
        var replay = await new ResendAuthorizations(checkpoint).FindAsync(Guid.Parse("c0000000-0000-4000-8000-000000000002"), CancellationToken.None);
        var rebind = await new RouteMoves(checkpoint).FindAsync(Guid.Parse("c0000000-0000-4000-8000-000000000003"), CancellationToken.None);

        Assert.Equal(ListenerFixture.Output("preparationReceipt"), rearm);
        Assert.Equal(ListenerFixture.Output("deliveryReceipt"), replay);
        Assert.Equal(ListenerFixture.Output("rebindReceipt"), rebind);
        Assert.Null(await new ResendAuthorizations(checkpoint).FindAsync(Guid.NewGuid(), CancellationToken.None));
    }

    /// <summary>Attempt grant history of the listener is listed.</summary>
    [Fact]
    public async Task AttemptGrantHistoryOfTheListenerIsListed()
    {
        var fixture = ListenerFixture.Rebuild("custody", _directory.StatePath);
        using var checkpoint = await DeliveryCheckpoint.OpenExistingAsync(Payloads.Options(_directory.StatePath, fixture.Channel), CancellationToken.None);
        var row = fixture.Row("preparation_rearms", 0);
        var expected = new CheckpointExpectation(row.GetProperty("route").GetString()!, 0, row.GetProperty("pending_digest").GetString()!, DeliveryPhase.Pending, 3);

        var grants = await new AttemptGrants(checkpoint).ListAsync(row.GetProperty("configuration").GetString()!, expected,
            Guid.Parse(row.GetProperty("pending_id").GetString()!), CancellationToken.None);

        var grant = Assert.Single(grants);
        Assert.Equal(3, grant.Attempts);
        Assert.Equal(ListenerFixture.Output("preparationReceipt"), grant.Receipt);
    }

    /// <summary>Identity mismatch on listener checkpoint is refused and releases the lease.</summary>
    [Fact]
    public async Task IdentityMismatchOnListenerCheckpointIsRefusedAndReleasesTheLease()
    {
        var fixture = ListenerFixture.Rebuild("custody", _directory.StatePath);
        var stale = fixture.Channel with { RouteId = fixture.Row("delivery_rebinds", 0).GetProperty("old_route").GetString()! };

        await Refusals.Expect(DeliveryRefusal.IdentityMismatch, () => DeliveryCheckpoint.OpenAsync(Payloads.Options(_directory.StatePath, stale), CancellationToken.None));

        using var checkpoint = await DeliveryCheckpoint.OpenAsync(Payloads.Options(_directory.StatePath, fixture.Channel), CancellationToken.None);
        Assert.Equal(DeliveryPhase.Pending, (await checkpoint.ReadAsync(CancellationToken.None)).Phase);
    }

    /// <summary>Listener pending item continues through sending to accepted.</summary>
    [Fact]
    public async Task ListenerPendingItemContinuesThroughSendingToAccepted()
    {
        var fixture = ListenerFixture.Rebuild("custody", _directory.StatePath);
        using var checkpoint = await DeliveryCheckpoint.OpenAsync(Payloads.Options(_directory.StatePath, fixture.Channel), CancellationToken.None);

        await checkpoint.MarkSendingAsync(CancellationToken.None);
        await checkpoint.AcceptAsync(ListenerFixture.Utf8(fixture.Pending), "accepted-after-extraction", CancellationToken.None);

        var state = await checkpoint.ReadAsync(CancellationToken.None);
        Assert.Equal((2L, (PendingItem?)null, (DeliveryPhase?)DeliveryPhase.Accepted, 0, "accepted-after-extraction"),
            (state.Position, state.Pending, state.Phase, state.Attempts, state.Acknowledgement));
    }

    /// <summary>New checkpoint uses the listener schema text.</summary>
    [Fact]
    public async Task NewCheckpointUsesTheListenerSchemaText()
    {
        var fixture = ListenerFixture.Rebuild("custody", Path.Combine(_directory.Root, "reference", "checkpoint.db"));
        using (var checkpoint = await DeliveryCheckpoint.OpenAsync(Payloads.Options(_directory.StatePath), CancellationToken.None))
        {
            await checkpoint.StageAsync(1, Payloads.Item(1), CancellationToken.None);
            await checkpoint.MarkSendingAsync(CancellationToken.None);
            await checkpoint.MarkUncertainAsync(CancellationToken.None);
            var expectation = (await checkpoint.ReadAsync(CancellationToken.None)).ToExpectation();
            await new ResendAuthorizations(checkpoint).AuthorizeAsync(new ResendAuthorization(Guid.NewGuid(), expectation, Guid.NewGuid(), Payloads.Receipt("r")), CancellationToken.None);
        }

        foreach (var table in new[] { "checkpoint", "delivery_replays" })
        {
            var expected = RawDatabase.Scalar(Path.Combine(_directory.Root, "reference", "checkpoint.db"), "SELECT sql FROM sqlite_master WHERE name='" + table + "'");
            Assert.Equal(expected, RawDatabase.Scalar(_directory.StatePath, "SELECT sql FROM sqlite_master WHERE name='" + table + "'"));
        }
        Assert.Equal("wal", RawDatabase.Scalar(_directory.StatePath, "PRAGMA journal_mode"));
        Assert.NotNull(fixture);
    }

    /// <inheritdoc />
    public void Dispose() => _directory.Dispose();
}
