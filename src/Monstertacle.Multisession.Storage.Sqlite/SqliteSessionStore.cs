using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Storage.Sqlite;

// Atomic domain transactions follow the separate acceptance, dispatch and recovery Red cycles.
internal sealed class SqliteSessionStore(SqliteConnection connection) : SqliteQueryStore(connection), ISessionStore
{
    public ValueTask CreateSessionAsync(SessionSnapshot session, CancellationToken cancellationToken) => throw new NotImplementedException();
    public ValueTask<SessionSnapshot> CommitSessionBindingAsync(Guid sessionId, ProviderBinding binding, CancellationToken cancellationToken) => throw new NotImplementedException();
    public ValueTask MarkSessionBlockedAsync(Guid sessionId, SessionBlockReason reason, CancellationToken cancellationToken) => throw new NotImplementedException();
    public ValueTask<RequestReceipt> CommitAcceptanceAsync(RequestAcceptance acceptance, int maxQueuedRequests, CancellationToken cancellationToken) => throw new NotImplementedException();
    public ValueTask<ExecutionReservation?> TryReserveHeadAsync(int maxConcurrentRequests, CancellationToken cancellationToken) => throw new NotImplementedException();
    public ValueTask CommitObservationAsync(Guid dispatchId, ProviderObservation observation, CancellationToken cancellationToken) => throw new NotImplementedException();
    public ValueTask CommitCompletionAsync(Guid dispatchId, ProviderReply reply, CancellationToken cancellationToken) => throw new NotImplementedException();
    public ValueTask MarkUncertainAsync(Guid dispatchId, PublicError failure, CancellationToken cancellationToken) => throw new NotImplementedException();
    public ValueTask ResolveRecoveryAsync(RecoveryDecision decision, ProviderBinding? confirmedBinding, CancellationToken cancellationToken) => throw new NotImplementedException();
}
