using Xunit;

namespace Monstertacle.Multisession.Checkpoints.Delivery.Tests;

/// <summary>
/// The real checkpoint is used through its contracts: the inherited base read, the delivery port and one lease owner. A
/// host-shaped loop drives every ordinary transition through the port against the real store.
/// </summary>
public sealed class DeliveryPortTests : IDisposable
{
    private readonly TestDirectory _directory = new();
    private int _sends;

    /// <inheritdoc />
    public void Dispose() => _directory.Dispose();

    /// <summary>The concrete checkpoint implements the delivery port and, through it, the base checkpoint contract.</summary>
    [Fact]
    public void TheStoreIsTheDeliveryPortAndTheBaseContract()
    {
        Assert.True(typeof(IDeliveryCheckpoint).IsAssignableFrom(typeof(DeliveryCheckpoint)));
        Assert.True(typeof(ICheckpoint<CheckpointState>).IsAssignableFrom(typeof(DeliveryCheckpoint)));
    }

    /// <summary>An acknowledged cycle stages, marks Sending and accepts, each through the port, and the store advances.</summary>
    [Fact]
    public async Task AcceptedCycleRunsEveryStepThroughThePort()
    {
        using var store = await OpenAsync();
        var port = new RecordingPort(store);

        var outcome = await Loop(port, prepared: true, _ => Task.FromResult<string?>("ack-1")).RunOnceAsync(CancellationToken.None);

        Assert.Equal(HostShapedLoop.Accepted, outcome);
        Assert.Equal(["ReadAsync", "StageAsync", "ReadAsync", "MarkSendingAsync", "AcceptAsync"], port.Calls);
        var state = await store.ReadAsync(CancellationToken.None);
        Assert.Equal((1L, (DeliveryPhase?)DeliveryPhase.Accepted, "ack-1", (PendingItem?)null), (state.Position, state.Phase, state.Acknowledgement, state.Pending));
    }

    /// <summary>An unknown outcome is recorded as Uncertain through the port, and the next cycle does not send again.</summary>
    [Fact]
    public async Task UnknownOutcomeIsUncertainAndNeverResentAutomatically()
    {
        using var store = await OpenAsync();
        var port = new RecordingPort(store);
        var loop = Loop(port, prepared: true, _ => throw new IOException("connection reset after the request was written"));

        Assert.Equal(HostShapedLoop.Uncertain, await loop.RunOnceAsync(CancellationToken.None));
        Assert.Equal(HostShapedLoop.UncertainBlocked, await loop.RunOnceAsync(CancellationToken.None));

        Assert.Equal(1, _sends);
        Assert.Contains("MarkUncertainAsync", port.Calls);
        var state = await store.ReadAsync(CancellationToken.None);
        Assert.Equal(((DeliveryPhase?)DeliveryPhase.Uncertain, 1), (state.Phase, state.Attempts));
    }

    /// <summary>A failed preparation counts an attempt through the port and sends nothing.</summary>
    [Fact]
    public async Task FailedPreparationCountsAnAttemptWithoutSending()
    {
        using var store = await OpenAsync();
        var port = new RecordingPort(store);

        var outcome = await Loop(port, prepared: false, _ => Task.FromResult<string?>("never")).RunOnceAsync(CancellationToken.None);

        Assert.Equal(HostShapedLoop.PreparationFailed, outcome);
        Assert.Equal(0, _sends);
        Assert.DoesNotContain("MarkSendingAsync", port.Calls);
        var state = await store.ReadAsync(CancellationToken.None);
        Assert.Equal(((DeliveryPhase?)DeliveryPhase.Pending, 1), (state.Phase, state.Attempts));
    }

    /// <summary>
    /// The inherited base read returns the store's typed state, and disposing through the base contract releases the one lease:
    /// another owner can open the checkpoint afterwards.
    /// </summary>
    [Fact]
    public async Task InheritedReadAndDisposalReleaseTheSameLease()
    {
        var store = await OpenAsync();
        var state = await BaseContractConsumer.ReadAsync(store);
        await Refusals.Expect(DeliveryRefusal.ExclusiveAccessUnavailable, OpenAsync);
        BaseContractConsumer.Release(store);

        Assert.Equal(0, state.Position);
        using var next = await OpenAsync();
        Assert.Equal(0, (await next.ReadAsync(CancellationToken.None)).Position);
    }

    private Task<DeliveryCheckpoint> OpenAsync() => DeliveryCheckpoint.OpenAsync(Payloads.Options(_directory.StatePath), CancellationToken.None);

    private HostShapedLoop Loop(IDeliveryCheckpoint port, bool prepared, Func<ReadOnlyMemory<byte>, Task<string?>> send) =>
        new(port, Payloads.Item, () => prepared, payload =>
        {
            _sends++;
            return send(payload);
        });
}
