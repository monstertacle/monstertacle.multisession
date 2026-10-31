using System.Text.Json;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.NativeHttp;

namespace Monstertacle.Multisession.Providers.Interactions;

internal sealed class InteractionRecoveryAccess(NativeHttpTransport transport, TimeProvider clock)
{
    internal async ValueTask<ProviderBinding> ReopenAsync(ProviderSessionContext context, ProviderBinding known,
        string? operationReference, CancellationToken cancellationToken)
    {
        InteractionBindingPolicy.Context(context);
        InteractionBindingPolicy.Validate(known, allowUnconfirmedHead: true);
        if (string.IsNullOrWhiteSpace(known.SessionReference)) { throw NativeHttpErrors.Create(ErrorCode.UnsupportedProviderAction); }
        string? operationId = operationReference ?? (known.ContinuationState is null ? known.SessionReference : null);
        if (string.IsNullOrWhiteSpace(operationId)) { throw NativeHttpErrors.Create(ErrorCode.UnsupportedProviderAction); }
        using CancellationTokenSource deadline = new(RuntimeDefaults.RequestTimeout, clock);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            using JsonDocument response = await ReadAsync(operationId, linked.Token);
            if (!InteractionResponse.MatchesLineage(response.RootElement, known)) { throw NativeHttpErrors.Create(ErrorCode.ProviderUnavailable); }
            string? status = response.RootElement.GetProperty("status").GetString();
            if (status == "completed") { return known with { ContinuationState = operationId }; }
            if (status is not ("failed" or "incomplete" or "cancelled") || known.ContinuationState is null)
            {
                throw NativeHttpErrors.Create(ErrorCode.UnsupportedProviderAction);
            }
            using JsonDocument previous = await ReadAsync(known.ContinuationState, linked.Token);
            if (previous.RootElement.GetProperty("status").GetString() != "completed") { throw NativeHttpErrors.Create(ErrorCode.ProviderUnavailable); }
            return known;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (SessionOperationException failure) { throw NativeHttpErrors.Create(failure.Failure.Code); }
        catch (Exception) { throw NativeHttpErrors.Create(ErrorCode.ProviderUnavailable); }
    }
    private async ValueTask<JsonDocument> ReadAsync(string operationId, CancellationToken cancellationToken)
    {
        JsonDocument response = await transport.SendAsync(HttpMethod.Get, $"interactions/{Uri.EscapeDataString(operationId)}", null, cancellationToken);
        try
        {
            if (InteractionResponse.Identity(response.RootElement) != operationId) { throw NativeHttpErrors.Create(ErrorCode.ProviderUnavailable); }
            return response;
        }
        catch { response.Dispose(); throw; }
    }
}
