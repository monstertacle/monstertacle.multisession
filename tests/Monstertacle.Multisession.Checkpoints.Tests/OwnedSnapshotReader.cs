namespace Monstertacle.Multisession.Checkpoints.Tests;

/// <summary>A consumer written only against the base contract: it takes ownership, reads once and releases.</summary>
public static class OwnedSnapshotReader
{
    /// <summary>Reads the snapshot and disposes the checkpoint, also when the read fails or is cancelled.</summary>
    /// <typeparam name="TSnapshot">The domain's snapshot type.</typeparam>
    /// <param name="checkpoint">The checkpoint whose ownership is transferred.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The snapshot.</returns>
    public static async Task<TSnapshot> ReadAndReleaseAsync<TSnapshot>(ICheckpoint<TSnapshot> checkpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        using (checkpoint)
        {
            return await checkpoint.ReadAsync(cancellationToken);
        }
    }
}
