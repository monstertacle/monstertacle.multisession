using Microsoft.Agents.AI;
using Monstertacle.Multisession.Contracts;
namespace Monstertacle.Multisession.AgentFramework;

/// <summary>Adapts host-owned framework agents while preserving durable dispatch and recovery ownership.</summary>
public sealed class FrameworkSessionProvider : ISessionProvider
{
    private readonly FrameworkAgentOptions _options;
    private readonly Func<ProviderSessionContext, AIAgent> _factory;
    private readonly FrameworkBindingCodec _codec;

    /// <summary>Registers an explicitly qualified, stable-ID agent factory; the host owns agent resource lifetime.</summary>
    public FrameworkSessionProvider(FrameworkAgentOptions options, Func<ProviderSessionContext, AIAgent> factory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.AdapterId);
        if (!Enum.IsDefined(options.ExecutionKind) || options.MaximumStateBytes <= 0 || options.MaximumObservations < 0
            || options.SessionOperationTimeout <= TimeSpan.Zero || options.SessionOperationTimeout > TimeSpan.FromMilliseconds(uint.MaxValue - 1))
        {
            throw new ArgumentException("A valid execution kind, finite deadline, positive state limit and nonnegative observation bound are required.", nameof(options));
        }
        _options = options;
        _factory = factory;
        _codec = new(options);
        Capabilities = new(options.ExecutionKind == ExecutionKind.ModelResponse, options.ExecutionKind == ExecutionKind.AgentTask, false, false, options.RequiresWorkspace)
        { RequiresOperationAwareRecovery = true };
    }
    /// <inheritdoc />
    public ProviderCapabilities Capabilities { get; }
    /// <inheritdoc />
    public async ValueTask<ProviderBinding> OpenAsync(ProviderSessionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AIAgent agent = GetAgent(context);
        using CancellationTokenSource deadline = new(_options.SessionOperationTimeout);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        AgentSession session = await agent.CreateSessionAsync(linked.Token).AsTask().WaitAsync(linked.Token);
        return await _codec.EncodeAsync(agent, session, context, linked.Token);
    }
    /// <inheritdoc />
    public async ValueTask<ProviderBinding> ReopenAsync(ProviderSessionContext context, ProviderBinding binding, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = await _codec.RestoreAsync(GetAgent(context), context, binding, cancellationToken);
        return binding;
    }
    /// <inheritdoc />
    public ValueTask<ProviderReply> SendAsync(ProviderCall request, ProviderBinding binding, Func<ProviderObservation, CancellationToken, ValueTask> observe, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(observe);
        if (request.Timeout <= TimeSpan.Zero || request.Timeout > TimeSpan.FromMilliseconds(uint.MaxValue - 1) || request.MaxOutputBytes <= 0)
        {
            return ValueTask.FromResult(FrameworkResponsePolicy.Failure(ErrorCode.InvalidConfiguration, ExecutionCertainty.NotStarted));
        }
        return new FrameworkExecution(_codec, _options).SendAsync(GetAgent(request.Session), request, binding, observe, cancellationToken);
    }
    /// <inheritdoc />
    public ValueTask<ProviderReply> InspectAsync(ProviderSessionContext context, string operationId, CancellationToken cancellationToken)
        => ValueTask.FromResult(FrameworkResponsePolicy.Failure(ErrorCode.UnsupportedProviderAction, ExecutionCertainty.Unknown));
    /// <inheritdoc />
    public ValueTask<ProviderReply> CancelAsync(ProviderSessionContext context, string operationId, CancellationToken cancellationToken)
        => ValueTask.FromResult(FrameworkResponsePolicy.Failure(ErrorCode.UnsupportedProviderAction, ExecutionCertainty.Unknown));

    private AIAgent GetAgent(ProviderSessionContext context)
    {
        if (context.ExecutionKind != _options.ExecutionKind) { throw new SessionOperationException(new(ErrorCode.UnsupportedProviderAction)); }
        return _factory(context) ?? throw new SessionOperationException(new(ErrorCode.InvalidConfiguration));
    }
}
