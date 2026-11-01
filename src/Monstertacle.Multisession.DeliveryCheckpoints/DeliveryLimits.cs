namespace Monstertacle.Multisession.DeliveryCheckpoints;

/// <summary>Bounds of the current checkpoint format and its inputs.</summary>
public static class DeliveryLimits
{
    /// <summary>Default largest accepted payload in bytes.</summary>
    public const int DefaultMaximumPayloadBytes = 262_144;

    /// <summary>Largest configurable payload bound in bytes.</summary>
    public const int PayloadBytesLimit = 16_777_216;

    /// <summary>Retained receipts per grant journal; also the largest configurable capacity. Full journals never evict.</summary>
    public const int RetainedGrants = 1024;

    /// <summary>Largest acknowledgement in characters.</summary>
    public const int MaximumAcknowledgementCharacters = 1024;

    /// <summary>Largest receipt text in characters.</summary>
    public const int MaximumReceiptCharacters = 65_536;

    /// <summary>Largest channel identifier, context digest or route identifier in characters.</summary>
    public const int MaximumIdentifierCharacters = 256;

    /// <summary>Default time a statement waits for another SQLite writer.</summary>
    public static readonly TimeSpan DefaultBusyTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Longest configurable busy timeout.</summary>
    public static readonly TimeSpan BusyTimeoutLimit = TimeSpan.FromSeconds(60);
}
