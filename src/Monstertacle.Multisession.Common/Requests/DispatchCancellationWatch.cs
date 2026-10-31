using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Requests;

// A dispatch-local wakeup observer; the persisted request remains the cancellation authority.
internal sealed class DispatchCancellationWatch(ISessionQueryStore store, Guid requestId)
{
    internal bool HasFailed { get; private set; }

    internal async ValueTask<bool> IsRequestedBeforeSendAsync(CancellationToken cancellationToken)
    {
        RequestSnapshot? request = await store.GetRequestAsync(requestId, cancellationToken).AsTask().WaitAsync(cancellationToken);
        return request is { State: RequestState.Running, CancellationRequestedUtc: not null };
    }

    internal async Task RunAsync(CancellationTokenSource execution, CancellationToken stopToken)
    {
        using CancellationTokenSource lifetime = CancellationTokenSource.CreateLinkedTokenSource(execution.Token, stopToken);
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                RequestSnapshot request = await store.GetRequestAsync(requestId, lifetime.Token).AsTask().WaitAsync(lifetime.Token)
                    ?? throw new SessionOperationException(new(ErrorCode.StorageUnavailable));
                if (request.CancellationRequestedUtc is not null) { execution.Cancel(); return; }
                if (RequestStateMachine.IsTerminal(request.State)) { return; }
                await Task.Delay(RuntimeDefaults.CancellationPollInterval, lifetime.Token);
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception)
        {
            HasFailed = true;
            execution.Cancel();
        }
    }
}
