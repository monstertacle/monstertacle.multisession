using Xunit;

namespace Monstertacle.Multisession.DeliveryCheckpoints.Tests;

/// <summary>The single explicit resend of an Uncertain item.</summary>
public sealed class ResendAuthorizationTests : IDisposable
{
    private readonly TestDirectory _directory = new();
    private readonly Guid _item = Guid.NewGuid();

    /// <summary>Authorization records the receipt and moves uncertain to sending together.</summary>
    [Fact]
    public async Task AuthorizationRecordsTheReceiptAndMovesUncertainToSendingTogether()
    {
        using var checkpoint = await CheckpointScenario.OpenAsync(_directory.StatePath);
        var expectation = await CheckpointScenario.UncertainAsync(checkpoint);

        var outcome = await new ResendAuthorizations(checkpoint).AuthorizeAsync(new ResendAuthorization(Guid.NewGuid(), expectation, _item, Payloads.Receipt("one")), CancellationToken.None);

        Assert.Equal(new GrantOutcome(true, Payloads.Receipt("one")), outcome);
        var state = await checkpoint.ReadAsync(CancellationToken.None);
        Assert.Equal(((DeliveryPhase?)DeliveryPhase.Sending, 2), (state.Phase, state.Attempts));
        Assert.Equal(_item.ToString(), RawDatabase.Scalar(_directory.StatePath, "SELECT pending_id FROM delivery_replays"));
    }

    /// <summary>Repeating the request returns the stored receipt without a second send.</summary>
    [Fact]
    public async Task RepeatingTheRequestReturnsTheStoredReceiptWithoutASecondSend()
    {
        using var checkpoint = await CheckpointScenario.OpenAsync(_directory.StatePath);
        var expectation = await CheckpointScenario.UncertainAsync(checkpoint);
        var request = new ResendAuthorization(Guid.NewGuid(), expectation, _item, Payloads.Receipt("one"));
        var resends = new ResendAuthorizations(checkpoint);
        await resends.AuthorizeAsync(request, CancellationToken.None);

        var repeated = await resends.AuthorizeAsync(request with { Receipt = Payloads.Receipt("two") }, CancellationToken.None);

        Assert.Equal(new GrantOutcome(false, Payloads.Receipt("one")), repeated);
        Assert.Equal(2, (await checkpoint.ReadAsync(CancellationToken.None)).Attempts);
        Assert.Equal(Payloads.Receipt("one"), await resends.FindAsync(request.RequestId, CancellationToken.None));
    }

    /// <summary>A fresh request after the resend was lost is refused even after restart.</summary>
    [Fact]
    public async Task AFreshRequestAfterTheResendWasLostIsRefusedEvenAfterRestart()
    {
        CheckpointExpectation lost;
        using (var checkpoint = await CheckpointScenario.OpenAsync(_directory.StatePath))
        {
            var expectation = await CheckpointScenario.UncertainAsync(checkpoint);
            await new ResendAuthorizations(checkpoint).AuthorizeAsync(new ResendAuthorization(Guid.NewGuid(), expectation, _item, "r"), CancellationToken.None);
        }
        using var restarted = await CheckpointScenario.OpenAsync(_directory.StatePath);
        lost = (await restarted.ReadAsync(CancellationToken.None)).ToExpectation();
        Assert.Equal(DeliveryPhase.Uncertain, lost.Phase);

        await Refusals.Expect(DeliveryRefusal.AllowanceConsumed, () => new ResendAuthorizations(restarted)
            .AuthorizeAsync(new ResendAuthorization(Guid.NewGuid(), lost, _item, "again"), CancellationToken.None));
        Assert.Equal(1, RawDatabase.Count(_directory.StatePath, "delivery_replays"));
    }

    /// <summary>Only an uncertain item with an attempt is eligible.</summary>
    [Fact]
    public async Task OnlyAnUncertainItemWithAnAttemptIsEligible()
    {
        using var checkpoint = await CheckpointScenario.OpenAsync(_directory.StatePath);
        var pending = await CheckpointScenario.PendingAsync(checkpoint);
        var resends = new ResendAuthorizations(checkpoint);

        await Refusals.Expect(DeliveryRefusal.NotEligible, () => resends.AuthorizeAsync(new ResendAuthorization(Guid.NewGuid(), pending, _item, "r"), CancellationToken.None));
        await Refusals.Expect(DeliveryRefusal.NotEligible, () => resends.AuthorizeAsync(new ResendAuthorization(Guid.NewGuid(), pending with { Phase = DeliveryPhase.Uncertain }, _item, "r"), CancellationToken.None));
        await Refusals.Expect(DeliveryRefusal.NotEligible, () => resends.AuthorizeAsync(new ResendAuthorization(Guid.NewGuid(), pending with { Phase = DeliveryPhase.Uncertain, Attempts = int.MaxValue }, _item, "r"), CancellationToken.None));
        Assert.Equal(-1, RawDatabase.Count(_directory.StatePath, "delivery_replays"));
    }

    /// <summary>A stale expectation records nothing.</summary>
    [Theory]
    [InlineData("route")]
    [InlineData("position")]
    [InlineData("digest")]
    [InlineData("attempts")]
    public async Task AStaleExpectationRecordsNothing(string field)
    {
        using var checkpoint = await CheckpointScenario.OpenAsync(_directory.StatePath);
        var expectation = await CheckpointScenario.UncertainAsync(checkpoint);
        var stale = field switch
        {
            "route" => expectation with { RouteId = "route-b" },
            "position" => expectation with { Position = 7 },
            "digest" => expectation with { PendingDigest = new string('0', 64) },
            _ => expectation with { Attempts = 3 },
        };

        await Refusals.Expect(DeliveryRefusal.StaleExpectation, () => new ResendAuthorizations(checkpoint)
            .AuthorizeAsync(new ResendAuthorization(Guid.NewGuid(), stale, _item, "r"), CancellationToken.None));

        Assert.Equal(DeliveryPhase.Uncertain, (await checkpoint.ReadAsync(CancellationToken.None)).Phase);
        Assert.Equal(-1, RawDatabase.Count(_directory.StatePath, "delivery_replays"));
    }

    /// <summary>A full journal refuses without eviction at the default capacity.</summary>
    [Fact]
    public async Task AFullJournalRefusesWithoutEvictionAtTheDefaultCapacity()
    {
        using var checkpoint = await CheckpointScenario.OpenAsync(_directory.StatePath);
        var expectation = await CheckpointScenario.UncertainAsync(checkpoint);
        var resends = new ResendAuthorizations(checkpoint);
        await resends.AuthorizeAsync(new ResendAuthorization(Guid.NewGuid(), expectation, Guid.NewGuid(), "first"), CancellationToken.None);
        await checkpoint.MarkUncertainAsync(CancellationToken.None);
        CheckpointScenario.FillJournal(_directory.StatePath, "delivery_replays", DeliveryLimits.RetainedGrants - 1);
        var current = (await checkpoint.ReadAsync(CancellationToken.None)).ToExpectation();

        await Refusals.Expect(DeliveryRefusal.GrantCapacity, () => resends.AuthorizeAsync(new ResendAuthorization(Guid.NewGuid(), current, _item, "late"), CancellationToken.None));

        Assert.Equal(DeliveryLimits.RetainedGrants, RawDatabase.Count(_directory.StatePath, "delivery_replays"));
        Assert.Equal(DeliveryPhase.Uncertain, (await checkpoint.ReadAsync(CancellationToken.None)).Phase);
    }

    /// <inheritdoc />
    public void Dispose() => _directory.Dispose();
}
