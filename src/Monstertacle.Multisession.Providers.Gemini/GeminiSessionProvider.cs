using System.Text.Json;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.NativeHttp;

namespace Monstertacle.Multisession.Providers.Gemini;

/// <summary>Native Interactions adapter with a stable first identity and separately confirmed chain head.</summary>
public sealed class GeminiSessionProvider : ISessionProvider, IExecutionRecoveryProvider, ISessionAvailabilityProvider, IDisposable
{
    private readonly NativeHttpTransport _transport;
    private readonly TimeProvider _clock;
    private readonly GeminiOperation _operation;
    private readonly GeminiRecoveryAccess _recovery;
    private readonly GeminiObservationAccess _observations;
    /// <summary>Composes a host connection, fresh credential resolver and optional host-owned fixture handler.</summary>
    public GeminiSessionProvider(GeminiOptions options, ICredentialResolver credentials, HttpMessageHandler? handler = null, TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
        Uri endpoint = GeminiBindingPolicy.Endpoint(options);
        _transport = new(new(endpoint, options.CredentialReference, "x-goog-api-key", null, options.MaximumResponseBytes),
            credentials, handler, _clock, static _ => ErrorCode.ExecutionFailed);
        _operation = new(_transport, _clock);
        _recovery = new(_transport, _clock);
        _observations = new(_transport, _clock);
    }
    /// <inheritdoc />
    public ProviderCapabilities Capabilities { get; } = new(true, false, true, true, false) { RequiresOperationAwareRecovery = true };
    /// <inheritdoc />
    public ValueTask ValidateAvailabilityAsync(ProviderSessionContext context, CancellationToken cancellationToken)
    {
        GeminiBindingPolicy.Context(context);
        return NativeModelAccess.CheckAsync(_transport, GeminiProtocol.ModelResourceId(context.ModelId), GeminiProtocol.IsAvailableModel, _clock, cancellationToken);
    }
    /// <inheritdoc />
    public ValueTask<ProviderBinding> OpenAsync(ProviderSessionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GeminiBindingPolicy.Context(context);
        return ValueTask.FromResult(new ProviderBinding(GeminiProtocol.AdapterId, GeminiProtocol.BindingVersion, null, null));
    }
    /// <inheritdoc />
    public async ValueTask<ProviderBinding> ReopenAsync(ProviderSessionContext context, ProviderBinding binding, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GeminiBindingPolicy.Context(context);
        GeminiBindingPolicy.Validate(binding);
        if (GeminiBindingPolicy.IsLazy(binding)) { return binding; }
        using CancellationTokenSource deadline = new(RuntimeDefaults.RequestTimeout, _clock);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            using JsonDocument response = await _transport.SendAsync(HttpMethod.Get, $"interactions/{Uri.EscapeDataString(binding.ContinuationState!)}", null, linked.Token);
            if (GeminiResponse.Identity(response.RootElement) != binding.ContinuationState || response.RootElement.GetProperty("status").GetString() != "completed")
            {
                throw NativeHttpErrors.Create(ErrorCode.ProviderUnavailable);
            }
            return binding;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (SessionOperationException) { throw; }
        catch (Exception) { throw NativeHttpErrors.Create(ErrorCode.ProviderUnavailable); }
    }
    /// <inheritdoc />
    public ValueTask<ProviderReply> SendAsync(ProviderCall request, ProviderBinding binding, Func<ProviderObservation, CancellationToken, ValueTask> observe, CancellationToken cancellationToken)
    {
        GeminiBindingPolicy.Call(request);
        GeminiBindingPolicy.Context(request.Session);
        try { GeminiBindingPolicy.Validate(binding); }
        catch (SessionOperationException failure) when (failure.Failure.Code == ErrorCode.ProviderSessionExpired)
        {
            return ValueTask.FromResult(GeminiResponse.Failure(ErrorCode.ProviderSessionExpired, ExecutionCertainty.NotStarted));
        }
        return _operation.SendAsync(request, binding, observe, cancellationToken);
    }
    /// <inheritdoc />
    public ValueTask<ProviderReply> InspectAsync(ProviderSessionContext context, string operationId, CancellationToken cancellationToken)
        => _observations.ReadAsync(context, operationId, false, cancellationToken);
    /// <inheritdoc />
    public ValueTask<ProviderReply> CancelAsync(ProviderSessionContext context, string operationId, CancellationToken cancellationToken)
        => _observations.ReadAsync(context, operationId, true, cancellationToken);
    /// <inheritdoc />
    public ValueTask<ProviderBinding> ReopenAfterExecutionAsync(ProviderSessionContext context, ProviderBinding knownBinding, string? operationReference, CancellationToken cancellationToken)
        => _recovery.ReopenAsync(context, knownBinding, operationReference, cancellationToken);
    /// <inheritdoc />
    public void Dispose() => _transport.Dispose();
}
