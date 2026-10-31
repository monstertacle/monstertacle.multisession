using System.Text.Json;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.NativeHttp;

namespace Monstertacle.Multisession.Providers.Interactions;

internal sealed class InteractionObservationAccess(NativeHttpTransport transport, TimeProvider clock)
{
    internal async ValueTask<ProviderReply> ReadAsync(ProviderSessionContext context, string operationId, bool cancel, CancellationToken cancellationToken)
    {
        InteractionBindingPolicy.Context(context);
        if (string.IsNullOrWhiteSpace(operationId)) { throw NativeHttpErrors.Create(ErrorCode.InvalidInput, ExecutionCertainty.NotStarted); }
        using CancellationTokenSource deadline = new(RuntimeDefaults.RequestTimeout, clock);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        string path = $"interactions/{Uri.EscapeDataString(operationId)}";
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
                if (cancel && InteractionResponse.IsPending(response.RootElement)) { continue; }
                return InteractionResponse.Reply(response.RootElement, null, RuntimeDefaults.MaxOutputBytes);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested) { return InteractionResponse.Failure(ErrorCode.TimedOut, ExecutionCertainty.Unknown); }
        catch (SessionOperationException failure) { return InteractionResponse.Failure(failure.Failure.Code, ExecutionCertainty.Unknown); }
        catch (Exception) { return InteractionResponse.Failure(ErrorCode.ProviderUnavailable, ExecutionCertainty.Unknown); }
    }
    private static void RequireIdentity(JsonElement response, string operationId)
    {
        if (InteractionResponse.Identity(response) != operationId) { throw NativeHttpErrors.Create(ErrorCode.ProviderUnavailable); }
    }
}
