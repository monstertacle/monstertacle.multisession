using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Requests;

// Observes bounded end evidence without retrying the original execution or owning store state.
internal sealed class DispatchTermination(ReservedDispatch dispatch, ProviderRegistration registration,
    DispatchObservationSink observations, int maximumOutputBytes)
{
    internal async ValueTask<ProviderReply?> ConfirmAsync(Task<ProviderReply>? sending, ErrorCode cause)
    {
        using CancellationTokenSource budget = new(RuntimeDefaults.CancellationConfirmationTimeout);
        List<Task<ProviderReply?>> candidates = [];
        if (sending is not null) { candidates.Add(ReadAsync(sending, true, budget.Token)); }
        if (registration.Provider.Capabilities.SupportsCancellation && observations.OperationReference is { } operation)
        {
            candidates.Add(ReadAsync(CancelAsync(operation, budget.Token), false, budget.Token));
        }
        while (candidates.Count != 0)
        {
            Task<ProviderReply?> finished = await Task.WhenAny(candidates);
            candidates.Remove(finished);
            ProviderReply? reply = await finished;
            if (reply is null) { continue; }
            budget.Cancel();
            return reply with
            {
                Outcome = cause == ErrorCode.TimedOut ? RequestState.TimedOut : RequestState.Cancelled,
                Output = string.Empty,
                OutputComplete = false,
                Error = new(cause, reply.Certainty, dispatch.Reservation.SessionId, dispatch.Reservation.RequestId),
            };
        }
        return null;
    }

    private async Task<ProviderReply> CancelAsync(string operation, CancellationToken token)
        => await registration.Provider.CancelAsync(dispatch.Session.Context, operation, token).AsTask().WaitAsync(token);

    private async Task<ProviderReply?> ReadAsync(Task<ProviderReply> operation, bool originalSend, CancellationToken token)
    {
        try
        {
            ProviderReply reply = await operation.WaitAsync(token);
            if (reply.Certainty != ExecutionCertainty.ConfirmedEnded && !(originalSend && reply.Certainty == ExecutionCertainty.NotStarted)) { return null; }
            reply = DispatchReplyPolicy.Normalize(reply, dispatch.Session.Binding!, observations.SessionReference, maximumOutputBytes);
            if (reply.Certainty == ExecutionCertainty.Unknown) { return null; }
            if (reply.Certainty == ExecutionCertainty.ConfirmedEnded && registration.Provider.Capabilities.RequiresOperationAwareRecovery
                && reply.Binding is null && reply.ContinuationAvailable is not false)
            {
                ProviderBinding? binding = await ReopenAsync(token);
                if (binding is null) { return null; }
                reply = DispatchReplyPolicy.Normalize(reply with { Binding = binding }, dispatch.Session.Binding!, observations.SessionReference, maximumOutputBytes);
            }
            return reply.Certainty == ExecutionCertainty.Unknown ? null : reply;
        }
        catch (Exception) { return null; } // Lack of trustworthy end evidence remains Unknown at the caller.
    }

    private async ValueTask<ProviderBinding?> ReopenAsync(CancellationToken token)
    {
        if (registration.Provider is not IExecutionRecoveryProvider recovery) { return null; }
        ProviderBinding known = dispatch.Session.Binding!;
        if (known.SessionReference is null) { known = known with { SessionReference = observations.SessionReference }; }
        if (known.SessionReference is null) { return null; }
        return await recovery.ReopenAfterExecutionAsync(dispatch.Session.Context, known, observations.OperationReference, token).AsTask().WaitAsync(token);
    }
}
