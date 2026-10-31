using System.Text.Json;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.NativeHttp;

namespace Monstertacle.Multisession.Providers.Conversations;

internal sealed class ConversationOperation(NativeHttpTransport transport, TimeProvider clock)
{

    internal async ValueTask<ProviderReply> SendAsync(ProviderCall request, ProviderBinding binding,
        Func<ProviderObservation, CancellationToken, ValueTask> observe, CancellationToken cancellationToken)
    {
        using CancellationTokenSource deadline = new(request.Timeout, clock);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        CancellationToken execution = linked.Token;
        bool accepted = false;
        try
        {
            using JsonDocument initial = await transport.SendAsync(HttpMethod.Post, "responses", ConversationProtocol.Input(request, binding), execution);
            string operationId = ConversationResponse.Identity(initial.RootElement);
            accepted = true;
            try { await observe(new(ProviderObservationKind.OperationAccepted, operationId), execution).AsTask().WaitAsync(execution); }
            catch (OperationCanceledException) when (execution.IsCancellationRequested) { throw; }
            catch (Exception) { return ConversationResponse.Failure(ErrorCode.StorageUnavailable, ExecutionCertainty.Unknown); }
            JsonElement response = initial.RootElement;
            while (ConversationResponse.IsPending(response))
            {
                await Task.Delay(NativeHttpDefaults.PollInterval, clock, execution);
                using JsonDocument next = await transport.SendAsync(HttpMethod.Get, $"responses/{Uri.EscapeDataString(operationId)}", null, execution);
                if (ConversationResponse.Identity(next.RootElement) != operationId) { return ConversationResponse.Failure(ErrorCode.ProviderUnavailable, ExecutionCertainty.Unknown); }
                response = next.RootElement.Clone();
            }
            return ConversationResponse.Reply(response, binding, request.MaxOutputBytes);
        }
        catch (OperationCanceledException) when (execution.IsCancellationRequested)
        {
            ErrorCode code = deadline.IsCancellationRequested ? ErrorCode.TimedOut : ErrorCode.Cancelled;
            return ConversationResponse.Failure(code, ExecutionCertainty.Unknown) with { Outcome = deadline.IsCancellationRequested ? RequestState.TimedOut : RequestState.Cancelled };
        }
        catch (SessionOperationException failure)
        {
            return ConversationResponse.Failure(failure.Failure.Code, accepted ? ExecutionCertainty.Unknown : failure.Failure.Certainty);
        }
        catch (Exception) { return ConversationResponse.Failure(ErrorCode.ProviderUnavailable, ExecutionCertainty.Unknown); }
    }
}
