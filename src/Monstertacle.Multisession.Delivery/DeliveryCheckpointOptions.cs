namespace Monstertacle.Multisession.Delivery;

/// <summary>Location, identity and bounds of one checkpoint.</summary>
/// <param name="statePath">Fully qualified path of the SQLite checkpoint; the lease is the sibling file with suffix <c>.lock</c>.</param>
/// <param name="channel">Identity the checkpoint is created with and must match on every open.</param>
public sealed class DeliveryCheckpointOptions(string statePath, DeliveryChannel channel)
{
    /// <summary>Fully qualified path of the SQLite checkpoint.</summary>
    public string StatePath { get; } = statePath;

    /// <summary>Channel identity.</summary>
    public DeliveryChannel Channel { get; } = channel;

    /// <summary>Position recorded when the owner open creates a new checkpoint; ignored for existing files.</summary>
    public long InitialPosition { get; init; }

    /// <summary>Largest accepted payload in bytes (1 to <see cref="DeliveryLimits.PayloadBytesLimit"/>).</summary>
    public int MaximumPayloadBytes { get; init; } = DeliveryLimits.DefaultMaximumPayloadBytes;

    /// <summary>Retained receipts per grant journal (1 to <see cref="DeliveryLimits.RetainedGrants"/>).</summary>
    public int MaximumRetainedGrants { get; init; } = DeliveryLimits.RetainedGrants;

    /// <summary>Time a statement waits for another SQLite writer (1 second to <see cref="DeliveryLimits.BusyTimeoutLimit"/>).</summary>
    public TimeSpan BusyTimeout { get; init; } = DeliveryLimits.DefaultBusyTimeout;
}
