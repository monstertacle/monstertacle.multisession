using System.Text.Json;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.NativeHttp;

namespace Monstertacle.Multisession.Providers.Interactions;

/// <summary>Native Interactions adapter with a stable first identity and separately confirmed chain head.</summary>
public sealed class InteractionSessionProvider : ISessionProvider, IExecutionRecoveryProvider, ISessionAvailabilityProvider, IDisposable
{
    private readonly NativeHttpTransport _transport;
    private readonly TimeProvider _clock;
    private readonly InteractionOperation _operation;
    private readonly InteractionRecoveryAccess _recovery;
    private readonly InteractionObservationAccess _observations;
    /// <summary>Composes a host connection, fresh credential resolver and optional host-owned fixture handler.</summary>
    public InteractionSessionProvider(InteractionOptions options, ICredentialResolver credentials, HttpMessageHandler? handler = null, TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
        Uri endpoint = InteractionBindingPolicy.Endpoint(options);
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
        InteractionBindingPolicy.Context(context);
        return NativeModelAccess.CheckAsync(_transport, InteractionProtocol.ModelResourceId(context.ModelId), InteractionProtocol.IsAvailableModel, _clock, cancellationToken);
    }
    /// <inheritdoc />
    public ValueTask<ProviderBinding> OpenAsync(ProviderSessionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        InteractionBindingPolicy.Context(context);
        return ValueTask.FromResult(new ProviderBinding(InteractionProtocol.AdapterId, InteractionProtocol.BindingVersion, null, null));
    }
    /// <inheritdoc />
    public async ValueTask<ProviderBinding> ReopenAsync(ProviderSessionContext context, ProviderBinding binding, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        InteractionBindingPolicy.Context(context);
        InteractionBindingPolicy.Validate(binding);
        if (InteractionBindingPolicy.IsLazy(binding)) { return binding; }
        using CancellationTokenSource deadline = new(RuntimeDefaults.RequestTimeout, _clock);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            using JsonDocument response = await _transport.SendAsync(HttpMethod.Get, $"interactions/{Uri.EscapeDataString(binding.ContinuationState!)}", null, linked.Token);
            if (InteractionResponse.Identity(response.RootElement) != binding.ContinuationState || response.RootElement.GetProperty("status").GetString() != "completed")
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
        InteractionBindingPolicy.Call(request);
        InteractionBindingPolicy.Context(request.Session);
        try { InteractionBindingPolicy.Validate(binding); }
        catch (SessionOperationException failure) when (failure.Failure.Code == ErrorCode.ProviderSessionExpired)
        {
            return ValueTask.FromResult(InteractionResponse.Failure(ErrorCode.ProviderSessionExpired, ExecutionCertainty.NotStarted));
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
