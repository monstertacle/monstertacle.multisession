namespace Monstertacle.Multisession.Checkpoints.Delivery.Tests;

/// <summary>Forwards every port call to the real checkpoint and records its name, so a loop that bypasses the port is visible.</summary>
/// <param name="inner">The real checkpoint seen through its port.</param>
internal sealed class RecordingPort(IDeliveryCheckpoint inner) : IDeliveryCheckpoint
{
    public List<string> Calls { get; } = [];

    public Task<CheckpointState> ReadAsync(CancellationToken cancellationToken) => Record(nameof(ReadAsync), inner.ReadAsync(cancellationToken));

    public Task StageAsync(long position, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken) =>
        Record(nameof(StageAsync), inner.StageAsync(position, payload, cancellationToken));

    public Task MarkSendingAsync(CancellationToken cancellationToken) => Record(nameof(MarkSendingAsync), inner.MarkSendingAsync(cancellationToken));

    public Task<bool> RecordPreparationFailureAsync(CancellationToken cancellationToken) =>
        Record(nameof(RecordPreparationFailureAsync), inner.RecordPreparationFailureAsync(cancellationToken));

    public Task<bool> MarkUncertainAsync(CancellationToken cancellationToken) => Record(nameof(MarkUncertainAsync), inner.MarkUncertainAsync(cancellationToken));

    public Task AcceptAsync(ReadOnlyMemory<byte> expectedPayload, string acknowledgement, CancellationToken cancellationToken) =>
        Record(nameof(AcceptAsync), inner.AcceptAsync(expectedPayload, acknowledgement, cancellationToken));

    public void Dispose()
    {
        Calls.Add(nameof(Dispose));
        inner.Dispose();
    }

    private T Record<T>(string name, T result)
    {
        Calls.Add(name);
        return result;
    }
}
