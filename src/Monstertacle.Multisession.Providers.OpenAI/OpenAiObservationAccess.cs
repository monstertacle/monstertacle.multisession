using System.Text.Json;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.NativeHttp;

namespace Monstertacle.Multisession.Providers.OpenAI;

internal sealed class OpenAiObservationAccess(NativeHttpTransport transport, TimeProvider clock)
{
    internal async ValueTask<ProviderReply> ReadAsync(ProviderSessionContext context, string operationId, bool cancel, CancellationToken cancellationToken)
    {
        OpenAiValidation.Context(context);
        if (string.IsNullOrWhiteSpace(operationId)) { throw NativeHttpErrors.Create(ErrorCode.InvalidInput, ExecutionCertainty.NotStarted); }
        using CancellationTokenSource deadline = new(RuntimeDefaults.RequestTimeout, clock);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        string path = $"responses/{Uri.EscapeDataString(operationId)}";
        try
        {
            if (cancel)
            {
                using JsonDocument acknowledgement = await transport.SendAsync(HttpMethod.Post, path + "/cancel", null, linked.Token);
                RequireIdentity(acknowledgement.RootElement, operationId);
            }
            while (true)
            {
                if (cancel) { await Task.Delay(NativeHttpDefaults.PollInterval, clock, linked.Token); }
                using JsonDocument response = await transport.SendAsync(HttpMethod.Get, path, null, linked.Token);
                RequireIdentity(response.RootElement, operationId);
                if (cancel && OpenAiResponse.IsPending(response.RootElement)) { continue; }
                return OpenAiResponse.Reply(response.RootElement, null, RuntimeDefaults.MaxOutputBytes);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested) { return OpenAiResponse.Failure(ErrorCode.TimedOut, ExecutionCertainty.Unknown); }
        catch (SessionOperationException failure) { return OpenAiResponse.Failure(failure.Failure.Code, ExecutionCertainty.Unknown); }
        catch (Exception) { return OpenAiResponse.Failure(ErrorCode.ProviderUnavailable, ExecutionCertainty.Unknown); }
    }
    private static void RequireIdentity(JsonElement response, string operationId)
    {
        if (OpenAiResponse.Identity(response) != operationId) { throw NativeHttpErrors.Create(ErrorCode.ProviderUnavailable); }
    }
}
