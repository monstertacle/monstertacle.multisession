using System.Text.Json;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.NativeHttp;

namespace Monstertacle.Multisession.Providers.Conversations;

/// <summary>Native Conversations/Responses adapter; no client-side agent or transcript replay.</summary>
public sealed class ConversationSessionProvider : ISessionProvider, ISessionAvailabilityProvider, IDisposable
{
    private readonly NativeHttpTransport _transport;
    private readonly ConversationOperation _operation;
    private readonly TimeProvider _clock;
    private readonly ConversationObservationAccess _observations;
    /// <summary>Composes host connection, fresh credentials and optional host-owned fixture transport.</summary>
    public ConversationSessionProvider(ConversationOptions options, ICredentialResolver credentials, HttpMessageHandler? handler = null, TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
        _transport = new(new(ConversationValidation.Endpoint(options), options.CredentialReference, "Authorization", "Bearer", options.MaximumResponseBytes),
            credentials, handler, _clock, ConversationResponse.NativeError);
        _operation = new(_transport, _clock);
        _observations = new(_transport, _clock);
    }
    /// <inheritdoc />
    public ProviderCapabilities Capabilities { get; } = new(true, false, true, true, false);
    /// <inheritdoc />
    public ValueTask ValidateAvailabilityAsync(ProviderSessionContext context, CancellationToken cancellationToken)
    {
        ConversationValidation.Context(context);
        return NativeModelAccess.CheckAsync(_transport, context.ModelId, ConversationProtocol.IsAvailableModel, _clock, cancellationToken);
    }
    /// <inheritdoc />
    public async ValueTask<ProviderBinding> OpenAsync(ProviderSessionContext context, CancellationToken cancellationToken)
    {
        using CancellationTokenSource deadline = new(RuntimeDefaults.RequestTimeout, _clock);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        ConversationValidation.Context(context);
        using JsonDocument document = await _transport.SendAsync(HttpMethod.Post, "conversations", "{}", linked.Token);
        return ConversationProtocol.Conversation(document.RootElement);
    }
    /// <inheritdoc />
    public async ValueTask<ProviderBinding> ReopenAsync(ProviderSessionContext context, ProviderBinding binding, CancellationToken cancellationToken)
    {
        using CancellationTokenSource deadline = new(RuntimeDefaults.RequestTimeout, _clock);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        ConversationValidation.Context(context);
        ConversationValidation.Binding(binding);
        using JsonDocument document = await _transport.SendAsync(HttpMethod.Get, $"conversations/{Uri.EscapeDataString(binding.SessionReference!)}", null, linked.Token);
        if (ConversationProtocol.Conversation(document.RootElement).SessionReference != binding.SessionReference)
        {
            throw NativeHttpErrors.Create(ErrorCode.ProviderSessionExpired);
        }
        return binding;
    }
    /// <inheritdoc />
    public ValueTask<ProviderReply> SendAsync(ProviderCall request, ProviderBinding binding, Func<ProviderObservation, CancellationToken, ValueTask> observe, CancellationToken cancellationToken)
    {
        ConversationValidation.Call(request);
        ConversationValidation.Context(request.Session);
        ConversationValidation.Binding(binding);
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
