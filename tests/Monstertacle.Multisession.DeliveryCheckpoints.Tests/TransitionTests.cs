using Xunit;

namespace Monstertacle.Multisession.DeliveryCheckpoints.Tests;

/// <summary>Compare-and-swap transitions of the single checkpoint row.</summary>
public sealed class TransitionTests : IDisposable
{
    private readonly TestDirectory _directory = new();

    private Task<DeliveryCheckpoint> OpenAsync() => DeliveryCheckpoint.OpenAsync(Payloads.Options(_directory.StatePath), CancellationToken.None);

    /// <summary>Stage requires the next position and an empty slot.</summary>
    [Fact]
    public async Task StageRequiresTheNextPositionAndAnEmptySlot()
    {
        using var checkpoint = await OpenAsync();

        await Refusals.Expect(DeliveryRefusal.TransitionConflict, () => checkpoint.StageAsync(2, Payloads.Item(2), CancellationToken.None));
        await checkpoint.StageAsync(1, Payloads.Item(1), CancellationToken.None);
        await Refusals.Expect(DeliveryRefusal.TransitionConflict, () => checkpoint.StageAsync(1, Payloads.Item(1), CancellationToken.None));

        var state = await checkpoint.ReadAsync(CancellationToken.None);
        Assert.Equal((0L, 1L, (DeliveryPhase?)DeliveryPhase.Pending, 0), (state.Position, state.Pending!.Position, state.Phase, state.Attempts));
    }

    /// <summary>Sending only follows pending and counts the attempt.</summary>
    [Fact]
    public async Task SendingOnlyFollowsPendingAndCountsTheAttempt()
    {
        using var checkpoint = await OpenAsync();
        await Refusals.Expect(DeliveryRefusal.TransitionConflict, () => checkpoint.MarkSendingAsync(CancellationToken.None));
        await checkpoint.StageAsync(1, Payloads.Item(1), CancellationToken.None);

        await checkpoint.MarkSendingAsync(CancellationToken.None);

        await Refusals.Expect(DeliveryRefusal.TransitionConflict, () => checkpoint.MarkSendingAsync(CancellationToken.None));
        var state = await checkpoint.ReadAsync(CancellationToken.None);
        Assert.Equal(((DeliveryPhase?)DeliveryPhase.Sending, 1), (state.Phase, state.Attempts));
    }

    /// <summary>Preparation failures count only while pending.</summary>
    [Fact]
    public async Task PreparationFailuresCountOnlyWhilePending()
    {
        using var checkpoint = await OpenAsync();
        Assert.False(await checkpoint.RecordPreparationFailureAsync(CancellationToken.None));
        await checkpoint.StageAsync(1, Payloads.Item(1), CancellationToken.None);

        Assert.True(await checkpoint.RecordPreparationFailureAsync(CancellationToken.None));
        Assert.True(await checkpoint.RecordPreparationFailureAsync(CancellationToken.None));
        await checkpoint.MarkSendingAsync(CancellationToken.None);

        Assert.False(await checkpoint.RecordPreparationFailureAsync(CancellationToken.None));
        Assert.Equal(3, (await checkpoint.ReadAsync(CancellationToken.None)).Attempts);
    }

    /// <summary>Uncertain only follows sending and stops automatic sending.</summary>
    [Fact]
    public async Task UncertainOnlyFollowsSendingAndStopsAutomaticSending()
    {
        using var checkpoint = await OpenAsync();
        await checkpoint.StageAsync(1, Payloads.Item(1), CancellationToken.None);
        Assert.False(await checkpoint.MarkUncertainAsync(CancellationToken.None));
        await checkpoint.MarkSendingAsync(CancellationToken.None);

        Assert.True(await checkpoint.MarkUncertainAsync(CancellationToken.None));

        await Refusals.Expect(DeliveryRefusal.TransitionConflict, () => checkpoint.MarkSendingAsync(CancellationToken.None));
        await Refusals.Expect(DeliveryRefusal.TransitionConflict, () => checkpoint.AcceptAsync(Payloads.Item(1), "ack", CancellationToken.None));
        Assert.Equal(DeliveryPhase.Uncertain, (await checkpoint.ReadAsync(CancellationToken.None)).Phase);
    }

    /// <summary>Accept requires sending and byte identical payload.</summary>
    [Fact]
    public async Task AcceptRequiresSendingAndByteIdenticalPayload()
    {
        using var checkpoint = await OpenAsync();
        await checkpoint.StageAsync(1, Payloads.Item(1), CancellationToken.None);
        await Refusals.Expect(DeliveryRefusal.TransitionConflict, () => checkpoint.AcceptAsync(Payloads.Item(1), "ack", CancellationToken.None));
        await checkpoint.MarkSendingAsync(CancellationToken.None);
        await Refusals.Expect(DeliveryRefusal.TransitionConflict, () => checkpoint.AcceptAsync(Payloads.Item(9), "ack", CancellationToken.None));

        await checkpoint.AcceptAsync(Payloads.Item(1), "ack-1", CancellationToken.None);

        Assert.Equal(new CheckpointState(1, null, DeliveryPhase.Accepted, 0, "ack-1", "route-a"), await checkpoint.ReadAsync(CancellationToken.None));
    }

    /// <summary>Next stage keeps the last acknowledgement like the current listener.</summary>
    [Fact]
    public async Task NextStageKeepsTheLastAcknowledgementLikeTheCurrentListener()
    {
        using var checkpoint = await OpenAsync();
        await checkpoint.StageAsync(1, Payloads.Item(1), CancellationToken.None);
        await checkpoint.MarkSendingAsync(CancellationToken.None);
        await checkpoint.AcceptAsync(Payloads.Item(1), "ack-1", CancellationToken.None);

        await checkpoint.StageAsync(2, Payloads.Item(2), CancellationToken.None);

        var state = await checkpoint.ReadAsync(CancellationToken.None);
        Assert.Equal(("ack-1", (DeliveryPhase?)DeliveryPhase.Pending, 0, 2L), (state.Acknowledgement, state.Phase, state.Attempts, state.Pending!.Position));
    }

    /// <summary>Attempt counters never overflow.</summary>
    [Fact]
    public async Task AttemptCountersNeverOverflow()
    {
        using (var checkpoint = await OpenAsync())
        {
            await checkpoint.StageAsync(1, Payloads.Item(1), CancellationToken.None);
        }
        RawDatabase.Execute(_directory.StatePath, "UPDATE checkpoint SET attempts=2147483647");
        using var reopened = await OpenAsync();

        Assert.False(await reopened.RecordPreparationFailureAsync(CancellationToken.None));
        await Refusals.Expect(DeliveryRefusal.TransitionConflict, () => reopened.MarkSendingAsync(CancellationToken.None));
        Assert.Equal(int.MaxValue, (await reopened.ReadAsync(CancellationToken.None)).Attempts);
    }

    /// <summary>A no pending state has no expectation.</summary>
    [Fact]
    public async Task ANoPendingStateHasNoExpectation()
    {
        using var checkpoint = await OpenAsync();

        var state = await checkpoint.ReadAsync(CancellationToken.None);

        var refusal = Assert.Throws<DeliveryRefusalException>(state.ToExpectation);
        Assert.Equal(DeliveryRefusal.NotEligible, refusal.Reason);
    }

    /// <inheritdoc />
    public void Dispose() => _directory.Dispose();
}
