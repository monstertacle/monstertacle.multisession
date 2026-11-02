namespace Monstertacle.Multisession.Checkpoints.Tests;

/// <summary>A test double of the base contract: no storage, no lease, no delivery vocabulary.</summary>
public sealed class InMemoryCheckpoint(CounterSnapshot snapshot) : ICheckpoint<CounterSnapshot>
{
    /// <summary>Number of Dispose calls.</summary>
    public int Disposals { get; private set; }

    /// <summary>Number of completed reads.</summary>
    public int Reads { get; private set; }

    /// <inheritdoc />
    public Task<CounterSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(Disposals > 0, this);
        Reads++;
        return Task.FromResult(snapshot);
    }

    /// <inheritdoc />
    public void Dispose() => Disposals++;
}
