namespace Monstertacle.Multisession.Checkpoints;

/// <summary>
/// An open checkpoint: a typed read of its current domain snapshot and the ownership lifetime that disposing ends. The
/// implementing domain defines what the snapshot means and how it is validated; this contract makes no claim about
/// persistence, leases, atomicity or delivery.
/// </summary>
/// <typeparam name="TSnapshot">The domain's own snapshot type.</typeparam>
public interface ICheckpoint<TSnapshot> : IDisposable
{
    /// <summary>Reads the current snapshot.</summary>
    /// <param name="cancellationToken">Cancels the read; a cancelled read changes nothing.</param>
    /// <returns>The current snapshot.</returns>
    Task<TSnapshot> ReadAsync(CancellationToken cancellationToken);
}
