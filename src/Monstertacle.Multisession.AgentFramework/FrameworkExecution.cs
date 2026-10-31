using Microsoft.Agents.AI;
using Monstertacle.Multisession.Contracts;
namespace Monstertacle.Multisession.AgentFramework;

internal sealed class FrameworkExecution(FrameworkBindingCodec codec)
{
    internal async ValueTask<ProviderReply> SendAsync(AIAgent agent, ProviderCall request, ProviderBinding binding,
        Func<ProviderObservation, CancellationToken, ValueTask> observe, CancellationToken cancellationToken)
    {
        using CancellationTokenSource deadline = new(request.Timeout);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        bool invoked = false;
        try
        {
            AgentSession session = await codec.RestoreAsync(agent, request.Session, binding, linked.Token);
            await observe(new(ProviderObservationKind.OperationAccepted, request.DispatchId.ToString("N")), linked.Token).AsTask().WaitAsync(linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            invoked = true;
            Task<AgentResponse> invocation = agent.RunAsync(request.Input, session, cancellationToken: linked.Token);
            // Timeout returns Unknown while a noncooperative external invocation may still run.
            // Observe late faults without publishing raw exception text or advancing continuation.
            _ = invocation.ContinueWith(static completed => { _ = completed.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            AgentResponse response = await invocation.WaitAsync(linked.Token);
            if (!FrameworkResponsePolicy.IsTerminal(response)) { return FrameworkResponsePolicy.Failure(ErrorCode.ExecutionFailed, ExecutionCertainty.Unknown); }
            ProviderBinding continuation;
            try { continuation = await codec.EncodeAsync(agent, session, request.Session, CancellationToken.None); }
            catch (Exception) { return FrameworkResponsePolicy.Failure(ErrorCode.ExecutionFailed, ExecutionCertainty.ConfirmedEnded, binding); }
            return FrameworkResponsePolicy.Complete(response, continuation, request.MaxOutputBytes);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            return FrameworkResponsePolicy.Failure(deadline.IsCancellationRequested ? ErrorCode.TimedOut : ErrorCode.Cancelled,
                invoked ? ExecutionCertainty.Unknown : ExecutionCertainty.NotStarted) with
            { ContinuationAvailable = invoked ? false : null };
        }
        catch (Exception)
        {
            return FrameworkResponsePolicy.Failure(invoked ? ErrorCode.ExecutionFailed : ErrorCode.ProviderSessionExpired,
                invoked ? ExecutionCertainty.Unknown : ExecutionCertainty.NotStarted);
        }
    }
}
