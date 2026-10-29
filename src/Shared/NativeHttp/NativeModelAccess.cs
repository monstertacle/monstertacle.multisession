using System.Text.Json;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.NativeHttp;

// Reads only the explicitly selected model metadata through the existing credential/transport boundary.
internal static class NativeModelAccess
{
    internal static async ValueTask CheckAsync(NativeHttpTransport transport, string modelId,
        Func<JsonElement, bool> validMetadata, TimeProvider clock, CancellationToken caller)
    {
        caller.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(modelId) || modelId is "." or "..")
        { throw NativeHttpErrors.Create(ErrorCode.InvalidConfiguration, ExecutionCertainty.NotStarted); }
        using CancellationTokenSource deadline = new(RuntimeDefaults.RequestTimeout, clock);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(caller, deadline.Token);
        try
        {
            using JsonDocument metadata = await transport.SendAsync(HttpMethod.Get, $"models/{Uri.EscapeDataString(modelId)}", null, linked.Token);
            if (!validMetadata(metadata.RootElement)) { throw NativeHttpErrors.Create(ErrorCode.ProviderUnavailable, ExecutionCertainty.NotStarted); }
        }
        catch (SessionOperationException failure) when (failure.Failure.Code == ErrorCode.ProviderSessionExpired)
        { throw NativeHttpErrors.Create(ErrorCode.ProviderUnavailable, ExecutionCertainty.NotStarted); }
        catch (OperationCanceledException) when (!caller.IsCancellationRequested)
        { throw NativeHttpErrors.Create(ErrorCode.TimedOut, ExecutionCertainty.NotStarted); }
    }
}
