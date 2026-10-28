namespace Monstertacle.Multisession.Contracts;

/// <summary>Atomic domain writes; provider I/O never occurs inside these transactions.</summary>
public interface ISessionStore : ISessionQueryStore
{
    /// <summary>Persists local session identity before an external open attempt.</summary>
    ValueTask CreateSessionAsync(SessionSnapshot session, CancellationToken cancellationToken);
    /// <summary>Persists established native identity without replacing the local session.</summary>
    ValueTask CommitSessionBindingAsync(Guid sessionId, ProviderBinding binding, CancellationToken cancellationToken);
    /// <summary>Blocks further execution after an uncertain native open without deleting local identity.</summary>
    ValueTask MarkSessionBlockedAsync(Guid sessionId, SessionBlockReason reason, CancellationToken cancellationToken);
    /// <summary>Commits input, sequence and queue capacity atomically.</summary>
    ValueTask<RequestReceipt> CommitAcceptanceAsync(SubmitRequest request, int maxQueuedRequests, CancellationToken cancellationToken);
    /// <summary>Reserves an eligible FIFO head, respecting session, workspace and slot occupancy.</summary>
    ValueTask<ExecutionReservation?> TryReserveHeadAsync(int maxConcurrentRequests, CancellationToken cancellationToken);
    /// <summary>Persists acknowledged native identity before the adapter can continue.</summary>
    ValueTask CommitObservationAsync(Guid dispatchId, ProviderObservation observation, CancellationToken cancellationToken);
    /// <summary>Commits result, history, confirmed binding and eligible reservation release together.</summary>
    ValueTask CommitCompletionAsync(Guid dispatchId, ProviderReply reply, CancellationToken cancellationToken);
    /// <summary>Records an uncertain result without releasing its reservation.</summary>
    ValueTask MarkUncertainAsync(Guid dispatchId, PublicError failure, CancellationToken cancellationToken);
    /// <summary>Applies a revision-bound resolution without changing failure into success.</summary>
    ValueTask ResolveReservationAsync(RecoveryDecision decision, CancellationToken cancellationToken);
}
