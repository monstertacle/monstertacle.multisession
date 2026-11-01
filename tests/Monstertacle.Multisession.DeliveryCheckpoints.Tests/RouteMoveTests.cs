using Xunit;

namespace Monstertacle.Multisession.DeliveryCheckpoints.Tests;

/// <summary>Moving a never-sent item to another route together with its receipt.</summary>
public sealed class RouteMoveTests : IDisposable
{
    private readonly TestDirectory _directory = new();
    private readonly Guid _item = Guid.NewGuid();

    /// <summary>A move keeps custody and only the new identity opens.</summary>
    [Fact]
    public async Task AMoveKeepsCustodyAndOnlyTheNewIdentityOpens()
    {
        var request = Guid.NewGuid();
        using (var checkpoint = await CheckpointScenario.OpenAsync(_directory.StatePath))
        {
            var expectation = await CheckpointScenario.PendingAsync(checkpoint);
            await checkpoint.RecordPreparationFailureAsync(CancellationToken.None);
            expectation = (await checkpoint.ReadAsync(CancellationToken.None)).ToExpectation();
            Assert.True((await new RouteMoves(checkpoint).MoveAsync(new RouteMove(request, expectation, _item, "route-b", "moved"), CancellationToken.None)).Recorded);
            Assert.Equal("route-b", (await checkpoint.ReadAsync(CancellationToken.None)).RouteId);
        }

        await Refusals.Expect(DeliveryRefusal.IdentityMismatch, () => DeliveryCheckpoint.OpenAsync(Payloads.Options(_directory.StatePath), CancellationToken.None));
        using var moved = await DeliveryCheckpoint.OpenAsync(Payloads.Options(_directory.StatePath, Payloads.Channel with { RouteId = "route-b" }), CancellationToken.None);
        var state = await moved.ReadAsync(CancellationToken.None);
        Assert.Equal((0L, 1, (DeliveryPhase?)DeliveryPhase.Pending), (state.Position, state.Attempts, state.Phase));
        Assert.Equal(Payloads.Item(1), state.Pending!.Payload.ToArray());
        Assert.Equal("moved", await new RouteMoves(moved).FindAsync(request, CancellationToken.None));
        Assert.Equal("route-a|route-b|" + _item, RawDatabase.Scalar(_directory.StatePath, "SELECT old_route||'|'||new_route||'|'||pending_id FROM delivery_rebinds"));
    }

    /// <summary>Only a never sent pending item moves.</summary>
    [Fact]
    public async Task OnlyANeverSentPendingItemMoves()
    {
        using var checkpoint = await CheckpointScenario.OpenAsync(_directory.StatePath);
        var moves = new RouteMoves(checkpoint);
        var uncertain = await CheckpointScenario.UncertainAsync(checkpoint);

        await Refusals.Expect(DeliveryRefusal.NotEligible, () => moves.MoveAsync(new RouteMove(Guid.NewGuid(), uncertain, _item, "route-b", "m"), CancellationToken.None));
        await Refusals.Expect(DeliveryRefusal.NotEligible, () => moves.MoveAsync(new RouteMove(Guid.NewGuid(), uncertain with { Phase = DeliveryPhase.Sending }, _item, "route-b", "m"), CancellationToken.None));
        await Refusals.Expect(DeliveryRefusal.StaleExpectation, () => moves.MoveAsync(new RouteMove(Guid.NewGuid(), uncertain with { Phase = DeliveryPhase.Pending }, _item, "route-b", "m"), CancellationToken.None));
        Assert.Equal("route-a", (await checkpoint.ReadAsync(CancellationToken.None)).RouteId);
        Assert.Equal(-1, RawDatabase.Count(_directory.StatePath, "delivery_rebinds"));
    }

    /// <summary>A repeated request with other values conflicts.</summary>
    [Fact]
    public async Task ARepeatedRequestWithOtherValuesConflicts()
    {
        using var checkpoint = await CheckpointScenario.OpenAsync(_directory.StatePath);
        var expectation = await CheckpointScenario.PendingAsync(checkpoint);
        var request = new RouteMove(Guid.NewGuid(), expectation, _item, "route-b", "m");
        var moves = new RouteMoves(checkpoint);
        await moves.MoveAsync(request, CancellationToken.None);

        Assert.Equal(new GrantOutcome(false, "m"), await moves.MoveAsync(request with { Receipt = "other" }, CancellationToken.None));
        await Refusals.Expect(DeliveryRefusal.RequestConflict, () => moves.MoveAsync(request with { NewRouteId = "route-c" }, CancellationToken.None));
        await Refusals.Expect(DeliveryRefusal.RequestConflict, () => moves.MoveAsync(request with { Expected = expectation with { Position = 5 } }, CancellationToken.None));
    }

    /// <summary>A full journal refuses the move.</summary>
    [Fact]
    public async Task AFullJournalRefusesTheMove()
    {
        using var checkpoint = await CheckpointScenario.OpenAsync(_directory.StatePath, maximumRetainedGrants: 1);
        var expectation = await CheckpointScenario.PendingAsync(checkpoint);
        await new RouteMoves(checkpoint).MoveAsync(new RouteMove(Guid.NewGuid(), expectation, _item, "route-b", "m"), CancellationToken.None);
        var moved = (await checkpoint.ReadAsync(CancellationToken.None)).ToExpectation();

        await Refusals.Expect(DeliveryRefusal.GrantCapacity, () => new RouteMoves(checkpoint).MoveAsync(new RouteMove(Guid.NewGuid(), moved, _item, "route-c", "n"), CancellationToken.None));
        Assert.Equal("route-b", (await checkpoint.ReadAsync(CancellationToken.None)).RouteId);
    }

    /// <inheritdoc />
    public void Dispose() => _directory.Dispose();
}
