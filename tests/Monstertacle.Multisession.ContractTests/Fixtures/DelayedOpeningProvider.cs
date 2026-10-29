using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ContractTests.Fixtures;

internal sealed class DelayedOpeningProvider : ISessionProvider
{
    internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource<ProviderBinding> Finish { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public ProviderCapabilities Capabilities { get; } = new(true, true, false, false, false);
    public async ValueTask<ProviderBinding> OpenAsync(ProviderSessionContext context, CancellationToken cancellationToken)
    {
        Entered.TrySetResult();
        return await Finish.Task; // Deliberately ignores caller cancellation; not evidence of native end.
    }
    public ValueTask<ProviderBinding> ReopenAsync(ProviderSessionContext context, ProviderBinding binding, CancellationToken cancellationToken)
        => ValueTask.FromResult(binding);
    public ValueTask<ProviderReply> SendAsync(ProviderCall request, ProviderBinding binding,
        Func<ProviderObservation, CancellationToken, ValueTask> observe, CancellationToken cancellationToken) => ValueTask.FromResult(Unsupported());
    public ValueTask<ProviderReply> InspectAsync(ProviderSessionContext context, string operationId, CancellationToken cancellationToken) => ValueTask.FromResult(Unsupported());
    public ValueTask<ProviderReply> CancelAsync(ProviderSessionContext context, string operationId, CancellationToken cancellationToken) => ValueTask.FromResult(Unsupported());
    private static ProviderReply Unsupported() => new(RequestState.Failed, ExecutionCertainty.Unknown, "", false, null,
        new(ErrorCode.UnsupportedProviderAction, ExecutionCertainty.Unknown));
}
