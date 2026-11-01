using System.Security.Cryptography;

namespace Monstertacle.Multisession.DeliveryCheckpoints;

/// <summary>The staged item: opaque canonical bytes supplied by the host, returned exactly as stored.</summary>
public sealed class PendingItem
{
    private readonly byte[] _payload;

    internal PendingItem(long position, byte[] payload)
    {
        Position = position;
        _payload = payload;
        Digest = Convert.ToHexStringLower(SHA256.HashData(payload));
    }

    /// <summary>Source position of the item; always the checkpoint position plus one.</summary>
    public long Position { get; }

    /// <summary>The stored canonical bytes.</summary>
    public ReadOnlyMemory<byte> Payload => _payload;

    /// <summary>Lowercase hexadecimal SHA-256 of <see cref="Payload"/>.</summary>
    public string Digest { get; }
}
