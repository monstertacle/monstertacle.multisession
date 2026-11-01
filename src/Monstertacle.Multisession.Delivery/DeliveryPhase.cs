namespace Monstertacle.Multisession.Delivery;

/// <summary>Durable phase of the pending item of a checkpoint.</summary>
public enum DeliveryPhase
{
    /// <summary>An item is staged and has not been handed to the destination by an automatic attempt in this phase.</summary>
    Pending,

    /// <summary>The host is about to cause, or has caused, the external effect; the outcome is not yet recorded.</summary>
    Sending,

    /// <summary>The outcome of a send is unknown. Only an explicit resend authorization leaves this phase.</summary>
    Uncertain,

    /// <summary>The destination positively acknowledged the last item; nothing is pending.</summary>
    Accepted,
}
