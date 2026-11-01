using System.Text;

namespace Monstertacle.Multisession.Delivery;

/// <summary>Validation of options and host inputs at the trust boundary; refusals happen before any change.</summary>
internal static class CheckpointInputs
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static void Validate(DeliveryCheckpointOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Channel);
        if (string.IsNullOrEmpty(options.StatePath) || !Path.IsPathFullyQualified(options.StatePath) || Path.GetDirectoryName(options.StatePath) is null)
        {
            throw new ArgumentException("The checkpoint path must be fully qualified.", nameof(options));
        }
        if (!IsIdentifier(options.Channel.ConsumerId) || !IsIdentifier(options.Channel.SourceId) || !IsIdentifier(options.Channel.RouteId))
        {
            throw new ArgumentException("Channel identifiers must be non-blank, bounded and free of control characters.", nameof(options));
        }
        if (options.InitialPosition < 0 || options.MaximumPayloadBytes is < 1 or > DeliveryLimits.PayloadBytesLimit ||
            options.MaximumRetainedGrants is < 1 or > DeliveryLimits.RetainedGrants ||
            options.BusyTimeout < TimeSpan.FromSeconds(1) || options.BusyTimeout > DeliveryLimits.BusyTimeoutLimit)
        {
            throw new ArgumentException("A checkpoint bound is outside its supported range.", nameof(options));
        }
    }

    public static bool IsIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= DeliveryLimits.MaximumIdentifierCharacters && !value.Any(char.IsControl);

    /// <summary>Returns the payload as text for storage, refusing anything that would not round-trip byte for byte.</summary>
    public static string Payload(ReadOnlyMemory<byte> payload, int maximumBytes)
    {
        if (payload.IsEmpty || payload.Length > maximumBytes || payload.Span.Contains((byte)0))
        {
            throw new DeliveryRefusalException(DeliveryRefusal.InvalidInput);
        }
        return Decode(payload.Span) ?? throw new DeliveryRefusalException(DeliveryRefusal.InvalidInput);
    }

    /// <summary>Strict UTF-8 decoding; null when the bytes are not valid UTF-8.</summary>
    public static string? Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    public static void Acknowledgement(string acknowledgement)
    {
        ArgumentNullException.ThrowIfNull(acknowledgement);
        if (string.IsNullOrWhiteSpace(acknowledgement) || acknowledgement.Length > DeliveryLimits.MaximumAcknowledgementCharacters)
        {
            throw new DeliveryRefusalException(DeliveryRefusal.InvalidInput);
        }
    }

    public static void Grant(Guid requestId, CheckpointExpectation expected, Guid itemId, string receipt)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(receipt);
        if (requestId == Guid.Empty || itemId == Guid.Empty || receipt.Length is 0 or > DeliveryLimits.MaximumReceiptCharacters ||
            !IsIdentifier(expected.RouteId) || string.IsNullOrEmpty(expected.PendingDigest))
        {
            throw new DeliveryRefusalException(DeliveryRefusal.InvalidInput);
        }
    }

    public static void Identifier(string? value)
    {
        if (!IsIdentifier(value))
        {
            throw new DeliveryRefusalException(DeliveryRefusal.InvalidInput);
        }
    }
}
