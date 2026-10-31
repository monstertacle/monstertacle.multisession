using System.Text.Json;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.NativeHttp;

namespace Monstertacle.Multisession.Providers.Interactions;

internal sealed class InteractionOperation(NativeHttpTransport transport, TimeProvider clock)
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
            using JsonDocument initial = await transport.SendAsync(HttpMethod.Post, "interactions", InteractionProtocol.Input(request, binding), execution);
            string operationId = InteractionResponse.Identity(initial.RootElement);
            accepted = true;
            if (operationId == binding.ContinuationState || operationId == binding.SessionReference || !InteractionResponse.MatchesLineage(initial.RootElement, binding))
            {
                return InteractionResponse.Failure(ErrorCode.ProviderUnavailable, ExecutionCertainty.Unknown);
            }
            try
            {
                if (binding.SessionReference is null)
                {
                    await observe(new(ProviderObservationKind.SessionEstablished, operationId), execution).AsTask().WaitAsync(execution);
                }
                await observe(new(ProviderObservationKind.OperationAccepted, operationId), execution).AsTask().WaitAsync(execution);
            }
            catch (OperationCanceledException) when (execution.IsCancellationRequested) { throw; }
            catch (Exception) { return InteractionResponse.Failure(ErrorCode.StorageUnavailable, ExecutionCertainty.Unknown); }
            JsonElement response = initial.RootElement;
            while (InteractionResponse.IsPending(response))
            {
                await Task.Delay(NativeHttpDefaults.PollInterval, clock, execution);
                using JsonDocument next = await transport.SendAsync(HttpMethod.Get, $"interactions/{Uri.EscapeDataString(operationId)}", null, execution);
                if (InteractionResponse.Identity(next.RootElement) != operationId) { return InteractionResponse.Failure(ErrorCode.ProviderUnavailable, ExecutionCertainty.Unknown); }
                response = next.RootElement.Clone();
            }
            return InteractionResponse.Reply(response, binding, request.MaxOutputBytes);
        }
        catch (OperationCanceledException) when (execution.IsCancellationRequested)
        {
            ErrorCode code = deadline.IsCancellationRequested ? ErrorCode.TimedOut : ErrorCode.Cancelled;
            return InteractionResponse.Failure(code, ExecutionCertainty.Unknown) with { Outcome = deadline.IsCancellationRequested ? RequestState.TimedOut : RequestState.Cancelled };
        }
        catch (SessionOperationException failure) { return InteractionResponse.Failure(failure.Failure.Code, accepted ? ExecutionCertainty.Unknown : failure.Failure.Certainty); }
        catch (Exception) { return InteractionResponse.Failure(ErrorCode.ProviderUnavailable, ExecutionCertainty.Unknown); }
    }
}
