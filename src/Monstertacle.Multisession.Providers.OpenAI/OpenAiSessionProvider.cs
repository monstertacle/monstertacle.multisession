using System.Text.Json;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.NativeHttp;

namespace Monstertacle.Multisession.Providers.OpenAI;

/// <summary>Native Conversations/Responses adapter; no client-side agent or transcript replay.</summary>
public sealed class OpenAiSessionProvider : ISessionProvider, ISessionAvailabilityProvider, IDisposable
{
    private readonly NativeHttpTransport _transport;
    private readonly OpenAiOperation _operation;
    private readonly TimeProvider _clock;
    private readonly OpenAiObservationAccess _observations;
    /// <summary>Composes host connection, fresh credentials and optional host-owned fixture transport.</summary>
    public OpenAiSessionProvider(OpenAiOptions options, ICredentialResolver credentials, HttpMessageHandler? handler = null, TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
        _transport = new(new(OpenAiValidation.Endpoint(options), options.CredentialReference, "Authorization", "Bearer", options.MaximumResponseBytes),
            credentials, handler, _clock, OpenAiResponse.NativeError);
        _operation = new(_transport, _clock);
        _observations = new(_transport, _clock);
    }
    /// <inheritdoc />
    public ProviderCapabilities Capabilities { get; } = new(true, false, true, true, false);
    /// <inheritdoc />
    public ValueTask ValidateAvailabilityAsync(ProviderSessionContext context, CancellationToken cancellationToken)
    {
        OpenAiValidation.Context(context);
        return NativeModelAccess.CheckAsync(_transport, context.ModelId, OpenAiProtocol.IsAvailableModel, _clock, cancellationToken);
    }
    /// <inheritdoc />
    public async ValueTask<ProviderBinding> OpenAsync(ProviderSessionContext context, CancellationToken cancellationToken)
    {
        using CancellationTokenSource deadline = new(RuntimeDefaults.RequestTimeout, _clock);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        OpenAiValidation.Context(context);
        using JsonDocument document = await _transport.SendAsync(HttpMethod.Post, "conversations", "{}", linked.Token);
        return OpenAiProtocol.Conversation(document.RootElement);
    }
    /// <inheritdoc />
    public async ValueTask<ProviderBinding> ReopenAsync(ProviderSessionContext context, ProviderBinding binding, CancellationToken cancellationToken)
    {
        using CancellationTokenSource deadline = new(RuntimeDefaults.RequestTimeout, _clock);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        OpenAiValidation.Context(context);
        OpenAiValidation.Binding(binding);
        using JsonDocument document = await _transport.SendAsync(HttpMethod.Get, $"conversations/{Uri.EscapeDataString(binding.SessionReference!)}", null, linked.Token);
        if (OpenAiProtocol.Conversation(document.RootElement).SessionReference != binding.SessionReference)
        {
            throw NativeHttpErrors.Create(ErrorCode.ProviderSessionExpired);
        }
        return binding;
    }
    /// <inheritdoc />
    public ValueTask<ProviderReply> SendAsync(ProviderCall request, ProviderBinding binding, Func<ProviderObservation, CancellationToken, ValueTask> observe, CancellationToken cancellationToken)
    {
        OpenAiValidation.Call(request);
        OpenAiValidation.Context(request.Session);
        OpenAiValidation.Binding(binding);
        return _operation.SendAsync(request, binding, observe, cancellationToken);
    }
    /// <inheritdoc />
    public ValueTask<ProviderReply> InspectAsync(ProviderSessionContext context, string operationId, CancellationToken cancellationToken)
        => _observations.ReadAsync(context, operationId, false, cancellationToken);
    /// <inheritdoc />
    public ValueTask<ProviderReply> CancelAsync(ProviderSessionContext context, string operationId, CancellationToken cancellationToken)
        => _observations.ReadAsync(context, operationId, true, cancellationToken);
    /// <inheritdoc />
    public void Dispose() => _transport.Dispose();
}
