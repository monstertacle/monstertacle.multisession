using System.Collections.Concurrent;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ContractTests.Fixtures;

// Consumer-owned external-run protocol; no endpoint, vendor adapter or core source change.
internal sealed class ThirdContractProvider(Task release) : ISessionProvider
{
    internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal ConcurrentQueue<ProviderCall> Calls { get; } = new();
    public ProviderCapabilities Capabilities { get; } = new(false, true, false, false, true);
    public ValueTask<ProviderBinding> OpenAsync(ProviderSessionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new ProviderBinding("third-contract", 7, $"external-session/{context.SessionId:D}", "initial-resume"));
    }
    public ValueTask<ProviderBinding> ReopenAsync(ProviderSessionContext context, ProviderBinding binding, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(binding);
    }
    public async ValueTask<ProviderReply> SendAsync(ProviderCall request, ProviderBinding binding,
        Func<ProviderObservation, CancellationToken, ValueTask> observe, CancellationToken cancellationToken)
    {
        Calls.Enqueue(request);
        Entered.TrySetResult();
        await observe(new(ProviderObservationKind.OperationAccepted, $"external-run/{request.DispatchId:D}"), cancellationToken);
        await release.WaitAsync(cancellationToken);
        return new(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, request.Input, true,
            binding with { ContinuationState = $"resume/{request.DispatchId:D}" }, null);
    }
    public ValueTask<ProviderReply> InspectAsync(ProviderSessionContext context, string operationId, CancellationToken cancellationToken)
        => ValueTask.FromResult(Unsupported());
    public ValueTask<ProviderReply> CancelAsync(ProviderSessionContext context, string operationId, CancellationToken cancellationToken)
        => ValueTask.FromResult(Unsupported());
    private static ProviderReply Unsupported() => new(RequestState.Failed, ExecutionCertainty.Unknown, string.Empty, false, null,
        new PublicError(ErrorCode.UnsupportedProviderAction, ExecutionCertainty.Unknown));
}
