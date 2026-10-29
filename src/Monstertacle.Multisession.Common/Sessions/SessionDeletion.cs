using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Sessions;

/// <summary>Explicit bounded local deletion, without claiming physical or provider erasure.</summary>
public sealed class SessionDeletion(ISessionRetentionStore store, TimeProvider? clock = null)
{
    /// <summary>Deletes only when the authoritative store confirms closure and the selected retention rule.</summary>
    public async ValueTask DeleteAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        using CancellationTokenSource deadline = new(RuntimeDefaults.ShutdownTimeout, clock ?? TimeProvider.System);
        using CancellationTokenSource execution = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try { await store.DeleteClosedSessionAsync(sessionId, execution.Token).AsTask().WaitAsync(execution.Token); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { throw new SessionOperationException(new(ErrorCode.TimedOut, ExecutionCertainty.Unknown, sessionId)); }
        catch (SessionOperationException failure) { throw new SessionOperationException(failure.Failure with { SessionId = sessionId }); }
        catch (Exception) { throw new SessionOperationException(new(ErrorCode.StorageUnavailable, ExecutionCertainty.Unknown, sessionId)); }
    }
}
