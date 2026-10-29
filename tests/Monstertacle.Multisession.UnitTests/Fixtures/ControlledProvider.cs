using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.UnitTests.Fixtures;

internal sealed class ControlledProvider : ISessionProvider
{
    public TaskCompletionSource<ProviderCall> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<ProviderReply> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public ProviderCapabilities Capabilities { get; init; } = new(true, true, false, false, false);
    public Func<ProviderCall, ProviderBinding, Func<ProviderObservation, CancellationToken, ValueTask>, CancellationToken, ValueTask<ProviderReply>>? Sending { get; init; }
    public Func<ProviderSessionContext, ProviderBinding, CancellationToken, ValueTask<ProviderBinding>>? Reopening { get; init; }
    public Func<ProviderSessionContext, string, CancellationToken, ValueTask<ProviderReply>>? Inspecting { get; init; }
    public Func<ProviderSessionContext, ProviderBinding>? BindingForContext { get; init; }
    public Action<ProviderSessionContext>? Opening { get; init; }
    public ProviderBinding OpeningBinding { get; init; } = new("controlled", 1, null, null);

    public ValueTask<ProviderBinding> OpenAsync(ProviderSessionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Opening?.Invoke(context);
        return ValueTask.FromResult(BindingForContext?.Invoke(context) ?? OpeningBinding);
    }

    public ValueTask<ProviderBinding> ReopenAsync(ProviderSessionContext context, ProviderBinding binding, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Reopening is null ? ValueTask.FromResult(binding) : Reopening(context, binding, cancellationToken);
    }

    public async ValueTask<ProviderReply> SendAsync(
        ProviderCall request,
        ProviderBinding binding,
        Func<ProviderObservation, CancellationToken, ValueTask> observe,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Started.TrySetResult(request);
        if (Sending is not null)
        {
            return await Sending(request, binding, observe, cancellationToken);
        }
        return await Completion.Task.WaitAsync(cancellationToken);
    }

    public ValueTask<ProviderReply> InspectAsync(ProviderSessionContext context, string operationId, CancellationToken cancellationToken)
        => Inspecting is null ? ValueTask.FromResult(Unsupported()) : Inspecting(context, operationId, cancellationToken);

    public ValueTask<ProviderReply> CancelAsync(ProviderSessionContext context, string operationId, CancellationToken cancellationToken)
        => ValueTask.FromResult(Unsupported());

    private static ProviderReply Unsupported() => new(
        RequestState.Failed,
        ExecutionCertainty.Unknown,
        string.Empty,
        false,
        null,
        new PublicError(ErrorCode.UnsupportedProviderAction, ExecutionCertainty.Unknown));
}
