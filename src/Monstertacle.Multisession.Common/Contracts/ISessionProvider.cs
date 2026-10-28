namespace Monstertacle.Multisession.Contracts;

/// <summary>Transport-neutral session execution; implementations own their external runtime.</summary>
public interface ISessionProvider
{
    /// <summary>Gets explicit adapter capabilities.</summary>
    ProviderCapabilities Capabilities { get; }

    /// <summary>Opens a native session, or returns an explicitly lazy unbound session.</summary>
    ValueTask<ProviderBinding> OpenAsync(ProviderSessionContext context, CancellationToken cancellationToken);

    /// <summary>Reopens the same native identity without replay or replacement.</summary>
    ValueTask<ProviderBinding> ReopenAsync(ProviderSessionContext context, ProviderBinding binding, CancellationToken cancellationToken);

    /// <summary>Executes one new input, awaiting durable observation acknowledgements.</summary>
    ValueTask<ProviderReply> SendAsync(
        ProviderCall request,
        ProviderBinding binding,
        Func<ProviderObservation, CancellationToken, ValueTask> observe,
        CancellationToken cancellationToken);

    /// <summary>Reads native state without dispatching or releasing reservations.</summary>
    ValueTask<ProviderReply> InspectAsync(ProviderSessionContext context, string operationId, CancellationToken cancellationToken);

    /// <summary>Requests cancellation; the reply distinguishes acknowledgement from confirmed end.</summary>
    ValueTask<ProviderReply> CancelAsync(ProviderSessionContext context, string operationId, CancellationToken cancellationToken);
}
