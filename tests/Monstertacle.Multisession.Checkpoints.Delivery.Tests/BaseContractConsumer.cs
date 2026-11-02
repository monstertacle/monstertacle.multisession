namespace Monstertacle.Multisession.Checkpoints.Delivery.Tests;

/// <summary>A consumer that knows only the base checkpoint contract, never the delivery types it is used with.</summary>
public static class BaseContractConsumer
{
    /// <summary>Reads the snapshot through the base contract.</summary>
    /// <typeparam name="TSnapshot">The domain's snapshot type.</typeparam>
    /// <param name="checkpoint">Any checkpoint.</param>
    /// <returns>The snapshot.</returns>
    public static Task<TSnapshot> ReadAsync<TSnapshot>(ICheckpoint<TSnapshot> checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        return checkpoint.ReadAsync(CancellationToken.None);
    }

    /// <summary>Ends the checkpoint's ownership through the base contract.</summary>
    /// <typeparam name="TSnapshot">The domain's snapshot type.</typeparam>
    /// <param name="checkpoint">Any checkpoint.</param>
    public static void Release<TSnapshot>(ICheckpoint<TSnapshot> checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        checkpoint.Dispose();
    }
}
