using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Storage.Sqlite;

// Atomic domain transactions follow the separate acceptance, dispatch and recovery Red cycles.
internal sealed class SqliteSessionStore(SqliteStoreState state) : SqliteQueryStore(state), ISessionStore
{
    public ValueTask CreateSessionAsync(SessionSnapshot session, CancellationToken cancellationToken)
        => State.Queue.WriteAsync(connection => SessionTransactions.Create(connection, State, session, cancellationToken), cancellationToken);
    public ValueTask<SessionSnapshot> CommitSessionBindingAsync(Guid sessionId, ProviderBinding binding, CancellationToken cancellationToken)
        => new(State.Queue.ExecuteAsync(connection => SessionTransactions.Bind(connection, State, sessionId, binding, cancellationToken), cancellationToken));
    public ValueTask MarkSessionBlockedAsync(Guid sessionId, SessionBlockReason reason, CancellationToken cancellationToken)
        => State.Queue.WriteAsync(connection => OpeningRecoveryTransactions.Block(connection, State, sessionId, reason, cancellationToken), cancellationToken);
    public ValueTask CommitConfigurationAvailabilityAsync(Guid sessionId, bool available, CancellationToken cancellationToken, ErrorCode? failure = null)
        => State.Queue.WriteAsync(connection => SessionTransactions.Availability(connection, State, sessionId, available, failure, cancellationToken), cancellationToken);
    public ValueTask<RequestReceipt> CommitAcceptanceAsync(RequestAcceptance acceptance, int maxQueuedRequests, CancellationToken cancellationToken)
        => AcceptanceTransactions.CommitAsync(State, acceptance, maxQueuedRequests, cancellationToken);
    public ValueTask<ExecutionReservation?> TryReserveHeadAsync(int maxConcurrentRequests, CancellationToken cancellationToken, Guid? expectedRequestId = null)
        => new(State.Queue.ExecuteAsync(connection => ReservationTransactions.Reserve(connection, State, maxConcurrentRequests, expectedRequestId, cancellationToken), cancellationToken));
    public ValueTask CommitObservationAsync(Guid dispatchId, ProviderObservation observation, CancellationToken cancellationToken)
        => State.Queue.WriteAsync(connection => ObservationTransactions.Commit(connection, dispatchId, observation, cancellationToken), cancellationToken);
    public ValueTask CommitCompletionAsync(Guid dispatchId, ProviderReply reply, CancellationToken cancellationToken)
        => State.Queue.WriteAsync(connection => CompletionTransactions.Commit(connection, State, dispatchId, reply, cancellationToken), cancellationToken);
    public ValueTask MarkUncertainAsync(Guid dispatchId, PublicError failure, CancellationToken cancellationToken)
        => State.Queue.WriteAsync(connection => UnknownTransactions.Commit(connection, State, dispatchId, failure, cancellationToken), cancellationToken);
    public ValueTask ResolveRecoveryAsync(RecoveryDecision decision, ProviderBinding? confirmedBinding, CancellationToken cancellationToken)
        => State.Queue.WriteAsync(connection => RecoveryDecisionTransactions.Resolve(connection, State, decision, confirmedBinding, cancellationToken), cancellationToken);
}
