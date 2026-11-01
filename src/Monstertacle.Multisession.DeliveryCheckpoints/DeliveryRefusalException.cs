namespace Monstertacle.Multisession.DeliveryCheckpoints;

/// <summary>Raised when a checkpoint operation is refused; <see cref="Reason"/> carries the typed cause.</summary>
public sealed class DeliveryRefusalException : Exception
{
    /// <summary>Creates a refusal for the given reason.</summary>
    /// <param name="reason">Typed refusal reason.</param>
    public DeliveryRefusalException(DeliveryRefusal reason)
        : base("Delivery checkpoint operation refused: " + reason)
    {
        Reason = reason;
    }

    /// <summary>Creates a refusal for the given reason caused by a storage failure.</summary>
    /// <param name="reason">Typed refusal reason.</param>
    /// <param name="innerException">The underlying storage failure.</param>
    public DeliveryRefusalException(DeliveryRefusal reason, Exception innerException)
        : base("Delivery checkpoint operation refused: " + reason, innerException)
    {
        Reason = reason;
    }

    /// <summary>Typed refusal reason.</summary>
    public DeliveryRefusal Reason { get; }
}
