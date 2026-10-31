using Microsoft.Agents.AI;
using Monstertacle.Multisession.Contracts;
namespace Monstertacle.Multisession.AgentFramework;

internal sealed class FrameworkExecution(FrameworkBindingCodec codec, FrameworkAgentOptions options)
{
    internal async ValueTask<ProviderReply> SendAsync(AIAgent agent, ProviderCall request, ProviderBinding binding,
        Func<ProviderObservation, CancellationToken, ValueTask> observe, CancellationToken cancellationToken)
    {
        using CancellationTokenSource deadline = new(request.Timeout);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        FrameworkInvocation? invocation = null;
        try
        {
            AgentSession session = await codec.RestoreAsync(agent, request.Session, binding, linked.Token);
            // Durable intent precedes the call; it is not evidence that the external operation was accepted.
            await observe(new(ProviderObservationKind.InvocationPrepared, request.DispatchId.ToString("N")), linked.Token).AsTask().WaitAsync(linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            invocation = new(request, observe, options.MaximumObservations, linked.Token);
            Task<AgentResponse> running = agent.RunAsync(request.Input, session, invocation.CreateOptions(), linked.Token);
            // Timeout returns Unknown while a noncooperative external invocation may still run.
            // Observe late faults without publishing raw exception text or advancing continuation.
            _ = running.ContinueWith(static completed => { _ = completed.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            AgentResponse response = await running.WaitAsync(linked.Token);
            invocation.Close();
            return invocation.Trustworthy
                ? await CompleteAsync(agent, session, request, response, invocation)
                : FrameworkResponsePolicy.Failure(ErrorCode.ExecutionFailed, ExecutionCertainty.Unknown);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            invocation?.Close();
            return FrameworkResponsePolicy.Failure(deadline.IsCancellationRequested ? ErrorCode.TimedOut : ErrorCode.Cancelled,
                invocation is null ? ExecutionCertainty.NotStarted : ExecutionCertainty.Unknown) with
            { ContinuationAvailable = invocation is null ? null : false };
        }
        catch (Exception)
        {
            invocation?.Close();
            return FrameworkResponsePolicy.Failure(invocation is null ? ErrorCode.ProviderSessionExpired : ErrorCode.ExecutionFailed,
                invocation is null ? ExecutionCertainty.NotStarted : ExecutionCertainty.Unknown);
        }
    }

    private async ValueTask<ProviderReply> CompleteAsync(AIAgent agent, AgentSession session, ProviderCall request, AgentResponse response, FrameworkInvocation invocation)
    {
        if (response.RawRepresentation is FrameworkExecutionOutcome typed)
        {
            // Acknowledged external evidence contradicts a not-started fact; the honest answer is Unknown.
            if (!options.QualifiedOutcomes || !FrameworkOutcomeRules.IsAdmissible(typed, response)
                || (typed.Certainty == ExecutionCertainty.NotStarted && invocation.HasExternalEvidence))
            {
                return FrameworkResponsePolicy.Failure(ErrorCode.ExecutionFailed, ExecutionCertainty.Unknown);
            }
            bool continues = typed.Certainty == ExecutionCertainty.ConfirmedEnded && typed.ContinuationAvailable;
            return FrameworkOutcomeRules.ToReply(typed, continues ? await TryEncodeAsync(agent, session, request) : null, request.MaxOutputBytes);
        }
        if (!FrameworkResponsePolicy.IsTerminal(response)) { return FrameworkResponsePolicy.Failure(ErrorCode.ExecutionFailed, ExecutionCertainty.Unknown); }
        return FrameworkResponsePolicy.Complete(response, await TryEncodeAsync(agent, session, request), request.MaxOutputBytes);
    }

    // Confirmed end and captured output survive serialization failure; only continuation becomes unavailable.
    private async ValueTask<ProviderBinding?> TryEncodeAsync(AIAgent agent, AgentSession session, ProviderCall request)
    {
        try { return await codec.EncodeAsync(agent, session, request.Session, CancellationToken.None); }
        catch (Exception) { return null; }
    }
}
