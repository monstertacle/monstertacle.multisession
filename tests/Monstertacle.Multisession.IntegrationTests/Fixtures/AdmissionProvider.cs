using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.IntegrationTests.Fixtures;

internal sealed class AdmissionProvider : ISessionProvider
{
    public Action<ProviderSessionContext>? Opening { get; set; }
    public Func<ProviderCall, ProviderBinding, Func<ProviderObservation, CancellationToken, ValueTask>, CancellationToken, ValueTask<ProviderReply>>? Sending { get; set; }
    public Func<ProviderSessionContext, string, CancellationToken, ValueTask<ProviderReply>>? Cancelling { get; init; }
    public ProviderCapabilities Capabilities { get; init; } = new(true, true, false, false, false);
    public ValueTask<ProviderBinding> OpenAsync(ProviderSessionContext context, CancellationToken cancellationToken)
    {
        Opening?.Invoke(context);
        return ValueTask.FromResult(new ProviderBinding("fixture-adapter", 7, "native-" + context.SessionId.ToString("N"), "confirmed"));
    }
    public ValueTask<ProviderBinding> ReopenAsync(ProviderSessionContext context, ProviderBinding binding, CancellationToken cancellationToken) => throw new NotImplementedException();
    public ValueTask<ProviderReply> SendAsync(ProviderCall request, ProviderBinding binding, Func<ProviderObservation, CancellationToken, ValueTask> observe, CancellationToken cancellationToken)
        => Sending is null ? throw new NotImplementedException() : Sending(request, binding, observe, cancellationToken);
    public ValueTask<ProviderReply> InspectAsync(ProviderSessionContext context, string operationId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public ValueTask<ProviderReply> CancelAsync(ProviderSessionContext context, string operationId, CancellationToken cancellationToken)
        => Cancelling is null ? throw new NotImplementedException() : Cancelling(context, operationId, cancellationToken);
}
