using System.Text.Json;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Providers.OpenAI;

internal sealed class OpenAiOperation(OpenAiTransport transport, TimeProvider clock)
{
    private static readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(1);

    internal async ValueTask<ProviderReply> SendAsync(ProviderCall request, ProviderBinding binding,
        Func<ProviderObservation, CancellationToken, ValueTask> observe, CancellationToken cancellationToken)
    {
        using CancellationTokenSource deadline = new(request.Timeout, clock);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        CancellationToken execution = linked.Token;
        bool accepted = false;
        try
        {
            using JsonDocument initial = await transport.SendAsync(HttpMethod.Post, "responses", OpenAiProtocol.Input(request, binding), execution);
            string operationId = OpenAiResponse.Identity(initial.RootElement);
            accepted = true;
            try { await observe(new(ProviderObservationKind.OperationAccepted, operationId), execution).AsTask().WaitAsync(execution); }
            catch (OperationCanceledException) when (execution.IsCancellationRequested) { throw; }
            catch (Exception) { return OpenAiResponse.Failure(ErrorCode.StorageUnavailable, ExecutionCertainty.Unknown); }
            JsonElement response = initial.RootElement;
            while (OpenAiResponse.IsPending(response))
            {
                await Task.Delay(_pollInterval, clock, execution);
                using JsonDocument next = await transport.SendAsync(HttpMethod.Get, $"responses/{Uri.EscapeDataString(operationId)}", null, execution);
                if (OpenAiResponse.Identity(next.RootElement) != operationId) { return OpenAiResponse.Failure(ErrorCode.ProviderUnavailable, ExecutionCertainty.Unknown); }
                response = next.RootElement.Clone();
            }
            return OpenAiResponse.Reply(response, binding, request.MaxOutputBytes);
        }
        catch (OperationCanceledException) when (execution.IsCancellationRequested)
        {
            ErrorCode code = deadline.IsCancellationRequested ? ErrorCode.TimedOut : ErrorCode.Cancelled;
            return OpenAiResponse.Failure(code, ExecutionCertainty.Unknown) with { Outcome = deadline.IsCancellationRequested ? RequestState.TimedOut : RequestState.Cancelled };
        }
        catch (SessionOperationException failure)
        {
            return OpenAiResponse.Failure(failure.Failure.Code, accepted ? ExecutionCertainty.Unknown : failure.Failure.Certainty);
        }
        catch (Exception) { return OpenAiResponse.Failure(ErrorCode.ProviderUnavailable, ExecutionCertainty.Unknown); }
    }
}
