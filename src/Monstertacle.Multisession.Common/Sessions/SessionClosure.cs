using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Sessions;

/// <summary>Closes local acceptance and observes bounded cancellation without deleting provider state.</summary>
public sealed class SessionClosure(ISessionClosureStore store, TimeProvider? clock = null)
{
    /// <summary>Returns when closed or explicitly recovery-blocked; committed close intent survives caller cancellation.</summary>
    public async ValueTask CloseAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        TimeProvider time = clock ?? TimeProvider.System;
        using CancellationTokenSource deadline = new(RuntimeDefaults.ShutdownTimeout, time);
        using CancellationTokenSource execution = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            SessionSnapshot snapshot = await store.BeginSessionCloseAsync(sessionId, execution.Token).AsTask().WaitAsync(execution.Token);
            while (snapshot.Lifecycle == SessionLifecycle.Closing && snapshot.BlockReason != SessionBlockReason.RecoveryRequired)
            {
                await Task.Delay(RuntimeDefaults.CancellationPollInterval, time, execution.Token);
                snapshot = await store.RefreshSessionCloseAsync(sessionId, execution.Token).AsTask().WaitAsync(execution.Token);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { throw new SessionOperationException(new(ErrorCode.TimedOut, ExecutionCertainty.Unknown, sessionId)); }
        catch (SessionOperationException failure) { throw new SessionOperationException(failure.Failure with { SessionId = sessionId }); }
        catch (Exception) { throw new SessionOperationException(new(ErrorCode.StorageUnavailable, ExecutionCertainty.Unknown, sessionId)); }
    }
}
