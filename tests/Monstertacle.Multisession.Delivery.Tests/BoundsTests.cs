using System.Text;
using Xunit;

namespace Monstertacle.Multisession.Delivery.Tests;

/// <summary>Payload, acknowledgement and receipt bounds, refused before any change.</summary>
public sealed class BoundsTests : IDisposable
{
    private readonly TestDirectory _directory = new();

    /// <summary>Payloads must be non empty strict utf8 without nul and within the bound.</summary>
    [Fact]
    public async Task PayloadsMustBeNonEmptyStrictUtf8WithoutNulAndWithinTheBound()
    {
        var options = new DeliveryCheckpointOptions(_directory.StatePath, Payloads.Channel) { MaximumPayloadBytes = 16 };
        using var checkpoint = await DeliveryCheckpoint.OpenAsync(options, CancellationToken.None);

        foreach (var payload in new[] { Array.Empty<byte>(), new byte[] { 0xC3, 0x28 }, Encoding.UTF8.GetBytes("a\0b"), Encoding.UTF8.GetBytes(new string('a', 17)) })
        {
            await Refusals.Expect(DeliveryRefusal.InvalidInput, () => checkpoint.StageAsync(1, payload, CancellationToken.None));
        }
        Assert.Null((await checkpoint.ReadAsync(CancellationToken.None)).Pending);

        await checkpoint.StageAsync(1, Encoding.UTF8.GetBytes(new string('a', 16)), CancellationToken.None);
        Assert.Equal(16, (await checkpoint.ReadAsync(CancellationToken.None)).Pending!.Payload.Length);
    }

    /// <summary>Acknowledgements must be non blank and bounded.</summary>
    [Fact]
    public async Task AcknowledgementsMustBeNonBlankAndBounded()
    {
        using var checkpoint = await DeliveryCheckpoint.OpenAsync(Payloads.Options(_directory.StatePath), CancellationToken.None);
        await checkpoint.StageAsync(1, Payloads.Item(1), CancellationToken.None);
        await checkpoint.MarkSendingAsync(CancellationToken.None);

        foreach (var acknowledgement in new[] { "", "  ", new string('a', DeliveryLimits.MaximumAcknowledgementCharacters + 1) })
        {
            await Refusals.Expect(DeliveryRefusal.InvalidInput, () => checkpoint.AcceptAsync(Payloads.Item(1), acknowledgement, CancellationToken.None));
        }
        await Assert.ThrowsAsync<ArgumentNullException>(() => checkpoint.AcceptAsync(Payloads.Item(1), null!, CancellationToken.None));
        Assert.Equal(DeliveryPhase.Sending, (await checkpoint.ReadAsync(CancellationToken.None)).Phase);

        await checkpoint.AcceptAsync(Payloads.Item(1), new string('a', DeliveryLimits.MaximumAcknowledgementCharacters), CancellationToken.None);
        Assert.Equal(DeliveryPhase.Accepted, (await checkpoint.ReadAsync(CancellationToken.None)).Phase);
    }

    /// <summary>Grant inputs must be bounded.</summary>
    [Fact]
    public async Task GrantInputsMustBeBounded()
    {
        using var checkpoint = await DeliveryCheckpoint.OpenAsync(Payloads.Options(_directory.StatePath), CancellationToken.None);
        await checkpoint.StageAsync(1, Payloads.Item(1), CancellationToken.None);
        var expectation = (await checkpoint.ReadAsync(CancellationToken.None)).ToExpectation();
        var grants = new AttemptGrants(checkpoint);
        var moves = new RouteMoves(checkpoint);

        await Refusals.Expect(DeliveryRefusal.InvalidInput, () => grants.RecordAsync(new AttemptGrant(Guid.NewGuid(), expectation, Guid.NewGuid(), "ctx", ""), CancellationToken.None));
        await Refusals.Expect(DeliveryRefusal.InvalidInput, () => grants.RecordAsync(new AttemptGrant(Guid.NewGuid(), expectation, Guid.NewGuid(), "ctx", new string('r', DeliveryLimits.MaximumReceiptCharacters + 1)), CancellationToken.None));
        await Refusals.Expect(DeliveryRefusal.InvalidInput, () => grants.RecordAsync(new AttemptGrant(Guid.Empty, expectation, Guid.NewGuid(), "ctx", "r"), CancellationToken.None));
        await Refusals.Expect(DeliveryRefusal.InvalidInput, () => grants.RecordAsync(new AttemptGrant(Guid.NewGuid(), expectation, Guid.Empty, "ctx", "r"), CancellationToken.None));
        await Refusals.Expect(DeliveryRefusal.InvalidInput, () => grants.RecordAsync(new AttemptGrant(Guid.NewGuid(), expectation, Guid.NewGuid(), " ", "r"), CancellationToken.None));
        await Refusals.Expect(DeliveryRefusal.InvalidInput, () => moves.MoveAsync(new RouteMove(Guid.NewGuid(), expectation, Guid.NewGuid(), "route\u0007", "r"), CancellationToken.None));
        await Refusals.Expect(DeliveryRefusal.InvalidInput, () => moves.MoveAsync(new RouteMove(Guid.NewGuid(), expectation, Guid.NewGuid(), expectation.RouteId, "r"), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => moves.MoveAsync(null!, CancellationToken.None));
        Assert.Equal(-1, RawDatabase.Count(_directory.StatePath, "preparation_rearms"));
        Assert.Equal(-1, RawDatabase.Count(_directory.StatePath, "delivery_rebinds"));
    }

    /// <inheritdoc />
    public void Dispose() => _directory.Dispose();
}
