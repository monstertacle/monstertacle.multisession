using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Scheduling;

/// <summary>Requests one atomic store reservation without keeping a second in-memory slot count.</summary>
public sealed class RequestScheduler
{
    private readonly ISessionStore _store;
    private readonly int _maxConcurrentRequests;

    /// <summary>Composes the authoritative store and runtime limits.</summary>
    public RequestScheduler(ISessionStore store, RuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(options);
        PublicError? failure = options.Validate();
        if (failure is not null)
        {
            throw new SessionOperationException(failure);
        }

        _store = store;
        _maxConcurrentRequests = options.MaxConcurrentRequests;
    }

    /// <summary>Returns only after the store has reserved a head, or reports that none is eligible.</summary>
    public ValueTask<ExecutionReservation?> TryReserveAsync(CancellationToken cancellationToken)
        => _store.TryReserveHeadAsync(_maxConcurrentRequests, cancellationToken);
}
