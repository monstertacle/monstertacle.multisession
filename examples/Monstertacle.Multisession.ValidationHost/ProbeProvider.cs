using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ValidationHost;

// A host-owned synthetic execution contract. It never opens a connection or invokes a tool.
internal sealed class ProbeProvider(string adapterId, ProbeTrace trace,
    Func<ProviderCall, ProviderBinding, CancellationToken, ValueTask<ProviderReply>> sending) : ISessionProvider
{
    public ProviderCapabilities Capabilities { get; } = new(true, true, false, false, false);
    public ValueTask<ProviderBinding> OpenAsync(ProviderSessionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new ProviderBinding(adapterId, 1, "probe-session/" + context.SessionId.ToString("N"), "initial"));
    }
    public ValueTask<ProviderBinding> ReopenAsync(ProviderSessionContext context, ProviderBinding binding, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Validate(context, binding);
        return ValueTask.FromResult(binding);
    }
    public async ValueTask<ProviderReply> SendAsync(ProviderCall request, ProviderBinding binding,
        Func<ProviderObservation, CancellationToken, ValueTask> observe, CancellationToken cancellationToken)
    {
        Validate(request.Session, binding);
        trace.Enter(request, binding);
        try
        {
            await observe(new(ProviderObservationKind.OperationAccepted, "probe-operation/" + request.DispatchId.ToString("N")), cancellationToken);
            return await sending(request, binding, cancellationToken);
        }
        finally { trace.Exit(); }
    }
    public ValueTask<ProviderReply> InspectAsync(ProviderSessionContext context, string operationId, CancellationToken cancellationToken) => ValueTask.FromResult(Unsupported());
    public ValueTask<ProviderReply> CancelAsync(ProviderSessionContext context, string operationId, CancellationToken cancellationToken) => ValueTask.FromResult(Unsupported());
    private void Validate(ProviderSessionContext context, ProviderBinding binding)
    {
        ValidationCheck.Require(binding.AdapterId == adapterId && binding.FormatVersion == 1
            && binding.SessionReference == "probe-session/" + context.SessionId.ToString("N"), "WrongNativeIdentity");
    }
    private static ProviderReply Unsupported() => new(RequestState.Failed, ExecutionCertainty.Unknown, "", false, null, new(ErrorCode.UnsupportedProviderAction));
}
