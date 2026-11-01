using Xunit;

namespace Monstertacle.Multisession.Delivery.Tests;

/// <summary>Grant journals written by the current Listener keep governing idempotency, uniqueness and route custody.</summary>
public sealed class CurrentGrantTests : IDisposable
{
    private static readonly Guid LostReplayRequest = Guid.Parse("c0000000-0000-4000-8000-000000000004");
    private static readonly Guid FirstItem = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid SecondItem = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private readonly TestDirectory _directory = new();

    /// <summary>Listener consumed resend refuses a fresh request without change.</summary>
    [Fact]
    public async Task ListenerConsumedResendRefusesAFreshRequestWithoutChange()
    {
        var fixture = ListenerFixture.Rebuild("uncertain", _directory.StatePath);
        using var checkpoint = await DeliveryCheckpoint.OpenExistingAsync(Payloads.Options(_directory.StatePath, fixture.Channel), CancellationToken.None);
        var expectation = (await checkpoint.ReadAsync(CancellationToken.None)).ToExpectation();

        await Refusals.Expect(DeliveryRefusal.AllowanceConsumed, () => new ResendAuthorizations(checkpoint)
            .AuthorizeAsync(new ResendAuthorization(Guid.NewGuid(), expectation, FirstItem, Payloads.Receipt("fresh")), CancellationToken.None));

        var state = await checkpoint.ReadAsync(CancellationToken.None);
        Assert.Equal((DeliveryPhase?)DeliveryPhase.Uncertain, state.Phase);
        Assert.Equal(2, state.Attempts);
        Assert.Equal(1, RawDatabase.Count(_directory.StatePath, "delivery_replays"));
    }

    /// <summary>Listener resend request repeated returns its stored receipt without transition.</summary>
    [Fact]
    public async Task ListenerResendRequestRepeatedReturnsItsStoredReceiptWithoutTransition()
    {
        var fixture = ListenerFixture.Rebuild("uncertain", _directory.StatePath);
        using var checkpoint = await DeliveryCheckpoint.OpenExistingAsync(Payloads.Options(_directory.StatePath, fixture.Channel), CancellationToken.None);
        var expectation = (await checkpoint.ReadAsync(CancellationToken.None)).ToExpectation();

        var outcome = await new ResendAuthorizations(checkpoint)
            .AuthorizeAsync(new ResendAuthorization(LostReplayRequest, expectation, FirstItem, Payloads.Receipt("new text")), CancellationToken.None);

        Assert.False(outcome.Recorded);
        Assert.Equal(ListenerFixture.Output("uncertainReceipt"), outcome.Receipt);
        Assert.Equal((DeliveryPhase?)DeliveryPhase.Uncertain, (await checkpoint.ReadAsync(CancellationToken.None)).Phase);
    }

    /// <summary>Listener resend request id with another item conflicts.</summary>
    [Fact]
    public async Task ListenerResendRequestIdWithAnotherItemConflicts()
    {
        var fixture = ListenerFixture.Rebuild("uncertain", _directory.StatePath);
        using var checkpoint = await DeliveryCheckpoint.OpenExistingAsync(Payloads.Options(_directory.StatePath, fixture.Channel), CancellationToken.None);
        var expectation = (await checkpoint.ReadAsync(CancellationToken.None)).ToExpectation();

        await Refusals.Expect(DeliveryRefusal.RequestConflict, () => new ResendAuthorizations(checkpoint)
            .AuthorizeAsync(new ResendAuthorization(LostReplayRequest, expectation, Guid.NewGuid(), Payloads.Receipt("x")), CancellationToken.None));
    }

    /// <summary>Listener checkpoint moves route and keeps bytes counters and every receipt.</summary>
    [Fact]
    public async Task ListenerCheckpointMovesRouteAndKeepsBytesCountersAndEveryReceipt()
    {
        var fixture = ListenerFixture.Rebuild("custody", _directory.StatePath);
        var request = Guid.NewGuid();
        CheckpointExpectation expectation;
        using (var checkpoint = await DeliveryCheckpoint.OpenExistingAsync(Payloads.Options(_directory.StatePath, fixture.Channel), CancellationToken.None))
        {
            expectation = (await checkpoint.ReadAsync(CancellationToken.None)).ToExpectation();
            var outcome = await new RouteMoves(checkpoint).MoveAsync(new RouteMove(request, expectation, SecondItem, "route-next", Payloads.Receipt("move")), CancellationToken.None);
            Assert.True(outcome.Recorded);
        }

        await Refusals.Expect(DeliveryRefusal.IdentityMismatch, () => DeliveryCheckpoint.OpenExistingAsync(Payloads.Options(_directory.StatePath, fixture.Channel), CancellationToken.None));
        using var moved = await DeliveryCheckpoint.OpenAsync(Payloads.Options(_directory.StatePath, fixture.Channel with { RouteId = "route-next" }), CancellationToken.None);
        var state = await moved.ReadAsync(CancellationToken.None);
        Assert.Equal(ListenerFixture.Utf8(fixture.Pending), state.Pending!.Payload.ToArray());
        Assert.Equal((expectation.Position, expectation.Attempts, (DeliveryPhase?)expectation.Phase), (state.Position, state.Attempts, state.Phase));
        Assert.Equal(ListenerFixture.Output("rebindReceipt"), await new RouteMoves(moved).FindAsync(Guid.Parse("c0000000-0000-4000-8000-000000000003"), CancellationToken.None));
        Assert.Equal(Payloads.Receipt("move"), await new RouteMoves(moved).FindAsync(request, CancellationToken.None));
        Assert.Equal(ListenerFixture.Output("deliveryReceipt"), await new ResendAuthorizations(moved).FindAsync(Guid.Parse("c0000000-0000-4000-8000-000000000002"), CancellationToken.None));
        Assert.Equal(1, RawDatabase.Count(_directory.StatePath, "preparation_rearms"));
    }

    /// <summary>Listener route move repeated after the move answers from its receipt.</summary>
    [Fact]
    public async Task ListenerRouteMoveRepeatedAfterTheMoveAnswersFromItsReceipt()
    {
        var fixture = ListenerFixture.Rebuild("custody", _directory.StatePath);
        using var checkpoint = await DeliveryCheckpoint.OpenExistingAsync(Payloads.Options(_directory.StatePath, fixture.Channel), CancellationToken.None);
        var row = fixture.Row("delivery_rebinds", 0);
        var expected = new CheckpointExpectation(row.GetProperty("old_route").GetString()!, 1, "ignored-for-stored-request", DeliveryPhase.Pending, 1);

        var outcome = await new RouteMoves(checkpoint).MoveAsync(new RouteMove(Guid.Parse("c0000000-0000-4000-8000-000000000003"), expected, SecondItem,
            row.GetProperty("new_route").GetString()!, Payloads.Receipt("other")), CancellationToken.None);

        Assert.False(outcome.Recorded);
        Assert.Equal(ListenerFixture.Output("rebindReceipt"), outcome.Receipt);
    }

    /// <summary>Listener attempt grant request repeated returns its receipt and another attempt count conflicts.</summary>
    [Fact]
    public async Task ListenerAttemptGrantRequestRepeatedReturnsItsReceiptAndAnotherAttemptCountConflicts()
    {
        var fixture = ListenerFixture.Rebuild("custody", _directory.StatePath);
        using var checkpoint = await DeliveryCheckpoint.OpenExistingAsync(Payloads.Options(_directory.StatePath, fixture.Channel), CancellationToken.None);
        var row = fixture.Row("preparation_rearms", 0);
        var expected = new CheckpointExpectation(row.GetProperty("route").GetString()!, 0, row.GetProperty("pending_digest").GetString()!, DeliveryPhase.Pending, 3);
        var id = Guid.Parse("c0000000-0000-4000-8000-000000000001");
        var context = row.GetProperty("configuration").GetString()!;
        var grants = new AttemptGrants(checkpoint);

        var outcome = await grants.RecordAsync(new AttemptGrant(id, expected, FirstItem, context, Payloads.Receipt("other")), CancellationToken.None);

        Assert.False(outcome.Recorded);
        Assert.Equal(ListenerFixture.Output("preparationReceipt"), outcome.Receipt);
        await Refusals.Expect(DeliveryRefusal.RequestConflict, () => grants.RecordAsync(new AttemptGrant(id, expected with { Attempts = 4 }, FirstItem, context, "x"), CancellationToken.None));
    }

    /// <inheritdoc />
    public void Dispose() => _directory.Dispose();
}
