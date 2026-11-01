using Xunit;

namespace Monstertacle.Multisession.DeliveryCheckpoints.Tests;

/// <summary>Cancellation before commit is never success and leaves durable state unchanged.</summary>
public sealed class CancellationTests : IDisposable
{
    private readonly TestDirectory _directory = new();

    /// <summary>Canceled open creates nothing.</summary>
    [Fact]
    public async Task CanceledOpenCreatesNothing()
    {
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DeliveryCheckpoint.OpenAsync(Payloads.Options(_directory.StatePath), canceled.Token));

        Assert.False(File.Exists(_directory.StatePath));
        using var checkpoint = await DeliveryCheckpoint.OpenAsync(Payloads.Options(_directory.StatePath), CancellationToken.None);
    }

    /// <summary>Canceled transitions leave the checkpoint unchanged.</summary>
    [Fact]
    public async Task CanceledTransitionsLeaveTheCheckpointUnchanged()
    {
        using var checkpoint = await DeliveryCheckpoint.OpenAsync(Payloads.Options(_directory.StatePath), CancellationToken.None);
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => checkpoint.StageAsync(1, Payloads.Item(1), canceled.Token));
        await checkpoint.StageAsync(1, Payloads.Item(1), CancellationToken.None);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => checkpoint.MarkSendingAsync(canceled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => checkpoint.RecordPreparationFailureAsync(canceled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => checkpoint.ReadAsync(canceled.Token));

        var state = await checkpoint.ReadAsync(CancellationToken.None);
        Assert.Equal(((DeliveryPhase?)DeliveryPhase.Pending, 0), (state.Phase, state.Attempts));
    }

    /// <summary>Canceled grants record nothing.</summary>
    [Fact]
    public async Task CanceledGrantsRecordNothing()
    {
        using var checkpoint = await DeliveryCheckpoint.OpenAsync(Payloads.Options(_directory.StatePath), CancellationToken.None);
        await checkpoint.StageAsync(1, Payloads.Item(1), CancellationToken.None);
        await checkpoint.MarkSendingAsync(CancellationToken.None);
        await checkpoint.MarkUncertainAsync(CancellationToken.None);
        var expectation = (await checkpoint.ReadAsync(CancellationToken.None)).ToExpectation();
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ResendAuthorizations(checkpoint)
            .AuthorizeAsync(new ResendAuthorization(Guid.NewGuid(), expectation, Guid.NewGuid(), "r"), canceled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ResendAuthorizations(checkpoint).FindAsync(Guid.NewGuid(), canceled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => checkpoint.MarkUncertainAsync(canceled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => checkpoint.AcceptAsync(Payloads.Item(1), "a", canceled.Token));

        Assert.Equal(DeliveryPhase.Uncertain, (await checkpoint.ReadAsync(CancellationToken.None)).Phase);
        Assert.Equal(-1, RawDatabase.Count(_directory.StatePath, "delivery_replays"));
    }

    /// <inheritdoc />
    public void Dispose() => _directory.Dispose();
}
