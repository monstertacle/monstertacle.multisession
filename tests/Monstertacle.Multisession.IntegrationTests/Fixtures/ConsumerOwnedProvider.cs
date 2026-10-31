using System.Collections.Concurrent;
using System.Globalization;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.IntegrationTests.Fixtures;

// Defined by this consuming project, with no library internals or vendor SDK dependence.
internal sealed class ConsumerOwnedProvider : ISessionProvider
{
    private readonly ConcurrentDictionary<Guid, int> _heads = new();
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _firsts;
    internal TaskCompletionSource BothEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource<Guid> DelayedEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal ConcurrentQueue<ConsumerInput> Inputs { get; } = new();
    public ProviderCapabilities Capabilities { get; } = new(true, false, false, false, false);
    internal void ReleaseFirsts() => _release.TrySetResult();
    public ValueTask<ProviderBinding> OpenAsync(ProviderSessionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_heads.TryAdd(context.SessionId, 0)) { throw new InvalidOperationException("Duplicate consumer session."); }
        return ValueTask.FromResult(new ProviderBinding("consumer-owned", 1, "consumer-session/" + context.SessionId.ToString("N"), "0"));
    }
    public ValueTask<ProviderBinding> ReopenAsync(ProviderSessionContext context, ProviderBinding binding, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (binding.SessionReference != "consumer-session/" + context.SessionId.ToString("N")) { throw new InvalidOperationException("Consumer identity mismatch."); }
        return ValueTask.FromResult(binding);
    }
    public async ValueTask<ProviderReply> SendAsync(ProviderCall request, ProviderBinding binding,
        Func<ProviderObservation, CancellationToken, ValueTask> observe, CancellationToken cancellationToken)
    {
        int head = _heads[request.Session.SessionId];
        if (binding.ContinuationState != head.ToString(CultureInfo.InvariantCulture)) { throw new InvalidOperationException("Consumer head mismatch."); }
        Inputs.Enqueue(new(request.Session.SessionId, request.RequestId, request.Input, binding.ContinuationState));
        await observe(new(ProviderObservationKind.OperationAccepted, "consumer-operation/" + request.DispatchId.ToString("N")), cancellationToken);
        if (head == 0)
        {
            if (Interlocked.Increment(ref _firsts) == 2) { BothEntered.TrySetResult(); }
            await _release.Task.WaitAsync(cancellationToken);
        }
        if (request.Input == "delayed")
        {
            DelayedEntered.TrySetResult(request.RequestId);
            try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new(RequestState.Cancelled, ExecutionCertainty.ConfirmedEnded, "", false, binding,
                    new(ErrorCode.Cancelled, ExecutionCertainty.ConfirmedEnded));
            }
        }
        _heads[request.Session.SessionId] = checked(head + 1);
        return new(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, "consumer:" + request.Input, true,
            binding with { ContinuationState = (head + 1).ToString(CultureInfo.InvariantCulture) }, null);
    }
    public ValueTask<ProviderReply> InspectAsync(ProviderSessionContext context, string operationId, CancellationToken cancellationToken) => ValueTask.FromResult(Unsupported());
    public ValueTask<ProviderReply> CancelAsync(ProviderSessionContext context, string operationId, CancellationToken cancellationToken) => ValueTask.FromResult(Unsupported());
    private static ProviderReply Unsupported() => new(RequestState.Failed, ExecutionCertainty.Unknown, "", false, null, new(ErrorCode.UnsupportedProviderAction));
}

internal sealed record ConsumerInput(Guid SessionId, Guid RequestId, string Input, string? PreviousHead);
