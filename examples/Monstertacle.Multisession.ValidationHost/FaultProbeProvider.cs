using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ValidationHost;

// Any dispatch during failed admission is a probe failure, never external work.
internal sealed class FaultProbeProvider : ISessionProvider
{
    private int _sends;
    internal int Sends => Volatile.Read(ref _sends);
    public ProviderCapabilities Capabilities { get; } = new(true, true, false, false, false);
    public ValueTask<ProviderBinding> OpenAsync(ProviderSessionContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
    public ValueTask<ProviderBinding> ReopenAsync(ProviderSessionContext context, ProviderBinding binding, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(binding);
    }
    public ValueTask<ProviderReply> SendAsync(ProviderCall request, ProviderBinding binding, Func<ProviderObservation, CancellationToken, ValueTask> observe, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _sends);
        return ValueTask.FromResult(new ProviderReply(RequestState.Failed, ExecutionCertainty.NotStarted, string.Empty, false, null, new(ErrorCode.ExecutionFailed)));
    }
    public ValueTask<ProviderReply> InspectAsync(ProviderSessionContext context, string operationId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public ValueTask<ProviderReply> CancelAsync(ProviderSessionContext context, string operationId, CancellationToken cancellationToken) => throw new NotSupportedException();
}
