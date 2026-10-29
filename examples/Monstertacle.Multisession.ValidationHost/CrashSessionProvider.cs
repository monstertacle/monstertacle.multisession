using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ValidationHost;

// Synthetic external-contract fixture, with one intentionally held accepted execution.
internal sealed class CrashSessionProvider : ISessionProvider
{
    internal TaskCompletionSource Running { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal bool HoldBeforeObservation { get; init; }
    public ProviderCapabilities Capabilities { get; } = new(true, true, false, false, true);
    public ValueTask<ProviderBinding> OpenAsync(ProviderSessionContext context, CancellationToken cancellationToken)
        => ValueTask.FromResult(new ProviderBinding(CrashProtocol.AdapterId, CrashProtocol.AdapterVersion, "native-" + context.SessionId.ToString("N"), "initial"));
    public ValueTask<ProviderBinding> ReopenAsync(ProviderSessionContext context, ProviderBinding binding, CancellationToken cancellationToken) => ValueTask.FromResult(binding);
    public async ValueTask<ProviderReply> SendAsync(ProviderCall request, ProviderBinding binding, Func<ProviderObservation, CancellationToken, ValueTask> observe, CancellationToken cancellationToken)
    {
        if (request.Input == CrashProtocol.RunningInput)
        {
            if (!HoldBeforeObservation) { await observe(new(ProviderObservationKind.OperationAccepted, "operation-" + request.RequestId.ToString("N")), cancellationToken); }
            Running.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        return new(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, "completed:" + request.Input, true,
            binding with { ContinuationState = "head-" + request.RequestId.ToString("N") }, null);
    }
    public ValueTask<ProviderReply> InspectAsync(ProviderSessionContext context, string operationId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public ValueTask<ProviderReply> CancelAsync(ProviderSessionContext context, string operationId, CancellationToken cancellationToken) => throw new NotSupportedException();
}
